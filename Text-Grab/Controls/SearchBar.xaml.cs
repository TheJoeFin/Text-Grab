using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Text_Grab.Models;

namespace Text_Grab.Controls;

/// <summary>
/// A shared search input used by Quick Simple Lookup, Find &amp; Replace, and Grab Frame. It bundles
/// the free-text box, a regex icon toggle (and optional exact-match toggle), a removable
/// smart-pattern "chip", and the unified <see cref="PatternItem"/> picker so all three search
/// surfaces look and behave the same. Each host keeps its own search/filter engine and debounce —
/// this control only owns the inputs and raises <see cref="SearchChanged"/> when any of them change.
/// </summary>
public partial class SearchBar : UserControl
{
    private const string RegexToolTip = "Search using Regular Expression syntax";

    /// <summary>Suppresses <see cref="SearchChanged"/> while several inputs are updated as one action.</summary>
    private bool suppressSearchChanged;

    public SearchBar()
    {
        InitializeComponent();
        UpdateAdornments();
    }

    /// <summary>Raised whenever the search text, regex/exact toggles, or selected pattern change.</summary>
    public event EventHandler? SearchChanged;

    /// <summary>Raised only when the exact-match toggle changes (hosts that adjust case handling subscribe to this).</summary>
    public event EventHandler? ExactMatchChanged;

    #region Dependency properties

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(SearchBar),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));

    public bool UseRegex
    {
        get => (bool)GetValue(UseRegexProperty);
        set => SetValue(UseRegexProperty, value);
    }

    public static readonly DependencyProperty UseRegexProperty =
        DependencyProperty.Register(nameof(UseRegex), typeof(bool), typeof(SearchBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnUseRegexChanged));

    public bool ExactMatch
    {
        get => (bool)GetValue(ExactMatchProperty);
        set => SetValue(ExactMatchProperty, value);
    }

    public static readonly DependencyProperty ExactMatchProperty =
        DependencyProperty.Register(nameof(ExactMatch), typeof(bool), typeof(SearchBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExactMatchChanged));

    /// <summary>When true the exact-match toggle is visible (used by Grab Frame). Hidden by default.</summary>
    public bool ShowExactMatchToggle
    {
        get => (bool)GetValue(ShowExactMatchToggleProperty);
        set => SetValue(ShowExactMatchToggleProperty, value);
    }

    public static readonly DependencyProperty ShowExactMatchToggleProperty =
        DependencyProperty.Register(nameof(ShowExactMatchToggle), typeof(bool), typeof(SearchBar),
            new PropertyMetadata(false));

    /// <summary>The active recognizer shown as a chip, or null. Saved regexes do not set this (they load into the text box).</summary>
    public PatternItem? SelectedPattern
    {
        get => (PatternItem?)GetValue(SelectedPatternProperty);
        set => SetValue(SelectedPatternProperty, value);
    }

    public static readonly DependencyProperty SelectedPatternProperty =
        DependencyProperty.Register(nameof(SelectedPattern), typeof(PatternItem), typeof(SearchBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedPatternChanged));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(SearchBar),
            new PropertyMetadata("Type to search..."));

    public bool AcceptsReturn
    {
        get => (bool)GetValue(AcceptsReturnProperty);
        set => SetValue(AcceptsReturnProperty, value);
    }

    public static readonly DependencyProperty AcceptsReturnProperty =
        DependencyProperty.Register(nameof(AcceptsReturn), typeof(bool), typeof(SearchBar),
            new PropertyMetadata(false));

    public bool AcceptsTab
    {
        get => (bool)GetValue(AcceptsTabProperty);
        set => SetValue(AcceptsTabProperty, value);
    }

    public static readonly DependencyProperty AcceptsTabProperty =
        DependencyProperty.Register(nameof(AcceptsTab), typeof(bool), typeof(SearchBar),
            new PropertyMetadata(false));

    #endregion Dependency properties

    #region Public API

    /// <summary>Colors the split-button border red on an invalid pattern and sets a matching tooltip.</summary>
    public void SetRegexValidity(bool isValid, string? toolTip = null)
    {
        if (isValid)
            RegexSplitContainer.ClearValue(Border.BorderBrushProperty); // let the style/checked trigger drive the border
        else
            RegexSplitContainer.BorderBrush = Brushes.Red;

        RegExToggleButton.ToolTip = toolTip ?? (isValid ? RegexToolTip : "Invalid Regular Expression");
    }

    /// <summary>The underlying text box, for hosts that need the control directly (focus, OCR target, caret, etc.).</summary>
    public TextBox TextBox => InnerTextBox;

    /// <summary>Focuses the text box and places the caret at the end.</summary>
    public void FocusInput()
    {
        InnerTextBox.Focus();
        InnerTextBox.CaretIndex = InnerTextBox.Text.Length;
    }

    #endregion Public API

    #region Change handlers

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SearchBar bar = (SearchBar)d;
        bar.UpdateAdornments();
        bar.RaiseSearchChanged();
    }

    private static void OnUseRegexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SearchBar bar = (SearchBar)d;
        bar.SetRegexValidity(true);
        bar.RaiseSearchChanged();
    }

    private static void OnExactMatchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SearchBar bar = (SearchBar)d;
        bar.ExactMatchChanged?.Invoke(bar, EventArgs.Empty);
        bar.RaiseSearchChanged();
    }

    private static void OnSelectedPatternChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SearchBar bar = (SearchBar)d;
        bar.UpdateChip();
        bar.UpdateAdornments();
        bar.RaiseSearchChanged();
    }

    private void RaiseSearchChanged()
    {
        if (!suppressSearchChanged)
            SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateChip()
    {
        if (PatternChip is null)
            return;

        if (SelectedPattern is not null)
        {
            PatternChipText.Text = SelectedPattern.Name;
            PatternChip.Visibility = Visibility.Visible;
        }
        else
        {
            PatternChip.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateAdornments()
    {
        if (ClearButton is null)
            return;

        ClearButton.Visibility = string.IsNullOrEmpty(SearchText) ? Visibility.Collapsed : Visibility.Visible;
        PlaceholderTextBlock.Visibility = string.IsNullOrEmpty(SearchText) && SelectedPattern is null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void PatternDropDownButton_Click(object sender, RoutedEventArgs e)
    {
        PatternMenu.PlacementTarget = PatternDropDownButton;
        PatternMenu.Placement = PlacementMode.Bottom;
        PatternMenu.IsOpen = true;
    }

    // Rebuild on each open so newly saved regexes appear. Headers are non-selectable.
    private void PatternMenu_Opened(object sender, RoutedEventArgs e)
    {
        PatternMenu.Items.Clear();

        string? currentGroup = null;
        foreach (PatternItem pattern in PatternItemCatalog.GetAll())
        {
            if (pattern.GroupLabel != currentGroup)
            {
                currentGroup = pattern.GroupLabel;
                if (PatternMenu.Items.Count > 0)
                    PatternMenu.Items.Add(new Separator());
                PatternMenu.Items.Add(new MenuItem { Header = currentGroup, IsEnabled = false });
            }

            MenuItem item = new()
            {
                Header = pattern.Name,
                ToolTip = string.IsNullOrWhiteSpace(pattern.Description) ? null : pattern.Description,
                Tag = pattern,
            };
            item.Click += PatternMenuItem_Click;
            PatternMenu.Items.Add(item);
        }
    }

    private void PatternMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: PatternItem pattern })
            ApplyPickedPattern(pattern);
    }

    /// <summary>
    /// Applies a pattern chosen from the dropdown: a saved regex loads into the text box and turns
    /// on the regex toggle; a smart pattern (recognizer) becomes a chip with typing allowed to narrow.
    /// </summary>
    private void ApplyPickedPattern(PatternItem pattern)
    {
        suppressSearchChanged = true;

        if (pattern is { Kind: PatternKind.SavedRegex, SavedRegex: { } savedRegex })
        {
            SelectedPattern = null;
            SearchText = savedRegex.Pattern;
            UseRegex = true;
        }
        else
        {
            SearchText = string.Empty;
            SelectedPattern = pattern;
        }

        suppressSearchChanged = false;

        RaiseSearchChanged();
        FocusInput();
    }

    private void ChipClearButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedPattern = null;
        FocusInput();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SearchText = string.Empty;
        FocusInput();
    }

    #endregion Change handlers
}
