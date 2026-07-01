using Humanizer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for FindAndReplaceWindow.xaml
/// </summary>
public partial class FindAndReplaceWindow : FluentWindow
{
    #region Fields

    public static RoutedCommand CopyMatchesCmd = new();
    public static RoutedCommand DeleteAllCmd = new();
    public static RoutedCommand ExtractPatternCmd = new();
    public static RoutedCommand ReplaceAllCmd = new();
    public static RoutedCommand ReplaceOneCmd = new();
    public static RoutedCommand TextSearchCmd = new();
    private readonly DispatcherTimer ChangeFindTextTimer = new();
    private readonly DispatcherTimer PrecisionSliderTimer = new();
    private MatchCollection? Matches;
    private string stringFromWindow = "";
    private EditTextWindow? textEditWindow;
    private ExtractedPattern? extractedPattern = null;

    #endregion Fields

    #region Constructors

    public FindAndReplaceWindow()
    {
        InitializeComponent();

        ChangeFindTextTimer.Interval = TimeSpan.FromMilliseconds(400);
        ChangeFindTextTimer.Tick -= ChangeFindText_Tick;
        ChangeFindTextTimer.Tick += ChangeFindText_Tick;

        PrecisionSliderTimer.Interval = TimeSpan.FromMilliseconds(300);
        PrecisionSliderTimer.Tick -= PrecisionSlider_Tick;
        PrecisionSliderTimer.Tick += PrecisionSlider_Tick;
    }

    #endregion Constructors

    #region Properties

    private bool IsSpreadsheetSearch => textEditWindow?.IsSpreadsheetMode is true;

    public List<FindResult> FindResults { get; set; } = [];

    public string StringFromWindow
    {
        get => stringFromWindow;
        set => stringFromWindow = value;
    }

    public EditTextWindow? TextEditWindow
    {
        get => textEditWindow;
        set
        {
            textEditWindow?.PassedTextControl.TextChanged -= EditTextBoxChanged;

            textEditWindow = value;

            textEditWindow?.PassedTextControl.TextChanged += EditTextBoxChanged;
        }
    }
    private string? Pattern { get; set; }

    #endregion Properties

    #region Methods

    public void SearchForText()
    {
        if (IsSpreadsheetSearch) { SearchSpreadsheetCells(); return; }

        FindResults.Clear();
        ResultsListView.ItemsSource = null;

        if (!TextSearchUtilities.HasSearchText(FindTextBox.Text))
        {
            Matches = null;
            MatchesText.Text = "0 Matches";
            return;
        }

        Pattern = FindTextBox.Text;

        // Auto-detect regex pattern: if starts with ^ and ends with $, enable regex mode and strip anchors
        if (Pattern.StartsWith('^') && Pattern.EndsWith('$') && Pattern.Length > 2)
        {
            UsePatternCheckBox.IsChecked = true;
            Pattern = Pattern[1..^1]; // Strip ^ from start and $ from end
        }

        if (UsePatternCheckBox.IsChecked is false && ExactMatchCheckBox.IsChecked is bool matchExactly)
            Pattern = Pattern.EscapeSpecialRegexChars(matchExactly);

        if (string.IsNullOrEmpty(StringFromWindow) && TextEditWindow is not null)
            StringFromWindow = TextEditWindow.GetSelectedTextOrAllText();

        try
        {
            // When using pattern mode with inline flags, rely on the inline flags for case sensitivity
            // Otherwise, use RegexOptions for backward compatibility
            bool usingPatternMode = UsePatternCheckBox.IsChecked is true;
            bool exactMatch = ExactMatchCheckBox.IsChecked is true;
            Regex regex = TextSearchUtilities.CreateFindAndReplaceSearchRegex(Pattern, usingPatternMode, exactMatch);
            Matches = regex.Matches(StringFromWindow);
        }
        catch (RegexMatchTimeoutException)
        {
            MatchesText.Text = "Regex timeout - pattern too complex";
            Wpf.Ui.Controls.MessageBox messageBox = new()
            {
                Title = "Regex Timeout",
                Content = "The regular expression took too long to execute (>5 seconds). Please simplify your pattern or reduce the amount of text being searched.",
                CloseButtonText = "OK"
            };
            _ = messageBox.ShowDialogAsync();
            return;
        }
        catch (Exception ex)
        {
            MatchesText.Text = "Error searching: " + ex.GetType().ToString();
            return;
        }

        if (Matches.Count < 1)
        {
            MatchesText.Text = "0 Matches";
            return;
        }

        if (Matches.Count == 1)
            MatchesText.Text = $"{Matches.Count} Match";
        else
            MatchesText.Text = $"{Matches.Count} Matches";

        ResultsListView.IsEnabled = true;
        int count = 1;
        foreach (Match m in Matches)
        {
            FindResult fr = new()
            {
                Index = m.Index,
                Text = TextSearchUtilities.FormatMatchTextForDisplay(m.Value),
                PreviewLeft = StringMethods.GetCharactersToLeftOfNewLine(ref stringFromWindow, m.Index, 12).MakeStringSingleLine(),
                PreviewRight = StringMethods.GetCharactersToRightOfNewLine(ref stringFromWindow, m.Index + m.Length, 12).MakeStringSingleLine(),
                Length = m.Length,
                Count = count
            };
            FindResults.Add(fr);

            count++;
        }

        ResultsListView.ItemsSource = FindResults;

        Match? firstMatch = Matches[0];

        if (textEditWindow is not null
            && firstMatch is not null
            && this.IsFocused)
        {
            textEditWindow.PassedTextControl.Select(firstMatch.Index, firstMatch.Value.Length);
            textEditWindow.PassedTextControl.Focus();
            this.Focus();
        }
    }

    private Regex? BuildCurrentRegex()
    {
        string rawPattern = FindTextBox.Text;
        if (!TextSearchUtilities.HasSearchText(rawPattern)) return null;

        if (rawPattern.StartsWith('^') && rawPattern.EndsWith('$') && rawPattern.Length > 2)
            rawPattern = rawPattern[1..^1];

        if (UsePatternCheckBox.IsChecked is false && ExactMatchCheckBox.IsChecked is bool matchExactly)
            rawPattern = rawPattern.EscapeSpecialRegexChars(matchExactly);

        try { return TextSearchUtilities.CreateReplacementRegex(rawPattern, ExactMatchCheckBox.IsChecked is true); }
        catch { return null; }
    }

    private void SearchSpreadsheetCells()
    {
        FindResults.Clear();
        ResultsListView.ItemsSource = null;
        Matches = null;

        if (textEditWindow is null || !TextSearchUtilities.HasSearchText(FindTextBox.Text))
        {
            MatchesText.Text = "0 Matches";
            return;
        }

        Regex? regex = BuildCurrentRegex();
        if (regex is null) { MatchesText.Text = "0 Matches"; return; }

        textEditWindow.CommitSpreadsheetAndSync();

        List<FindResult> results;
        try { results = textEditWindow.SearchSpreadsheetCells(regex); }
        catch (RegexMatchTimeoutException) { MatchesText.Text = "Regex timeout"; return; }

        FindResults.AddRange(results);
        if (FindResults.Count == 0) { MatchesText.Text = "0 Matches"; return; }

        MatchesText.Text = FindResults.Count == 1 ? "1 Match" : $"{FindResults.Count} Matches";
        ResultsListView.IsEnabled = true;
        ResultsListView.ItemsSource = FindResults;

        FindResult first = FindResults[0];
        if (this.IsFocused && first.RowIndex.HasValue && first.ColumnIndex.HasValue)
        {
            textEditWindow.NavigateToSpreadsheetCell(first.RowIndex.Value, first.ColumnIndex.Value);
            this.Focus();
        }
    }

    public void ShouldCloseWithThisETW(EditTextWindow etw)
    {
        if (textEditWindow is not null && etw == textEditWindow)
            Close();
    }

    private void ChangeFindText_Tick(object? sender, EventArgs? e)
    {
        ChangeFindTextTimer.Stop();
        SearchForText();
    }

    private void PrecisionSlider_Tick(object? sender, EventArgs? e)
    {
        PrecisionSliderTimer.Stop();
        SearchForText();
    }

    private void CopyMatchesCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (IsSpreadsheetSearch)
        {
            e.CanExecute = FindResults.Count > 0 && !string.IsNullOrEmpty(FindTextBox.Text);
            return;
        }

        if (Matches is null || Matches.Count < 1 || string.IsNullOrEmpty(FindTextBox.Text))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void CopyMatchesCmd_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (textEditWindow is null) return;

        if (!IsSpreadsheetSearch && (Matches is null || Matches.Count < 1))
            return;

        StringBuilder stringBuilder = new();

        IList selection = ResultsListView.SelectedItems;
        if (selection.Count < 2)
            selection = ResultsListView.Items;

        foreach (object? item in selection)
            if (item is FindResult findResult)
                stringBuilder.AppendLine(findResult.Text);

        EditTextWindow etw = new();
        etw.AddThisText(stringBuilder.ToString());
        etw.Show();
    }

    private void DeleteAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (IsSpreadsheetSearch)
        {
            e.CanExecute = FindResults.Count > 0 && !string.IsNullOrEmpty(FindTextBox.Text);
            return;
        }

        if (Matches is not null && Matches.Count > 1 && !string.IsNullOrEmpty(FindTextBox.Text))
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private async void DeleteAll_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (textEditWindow is null) return;

        if (IsSpreadsheetSearch)
        {
            if (FindResults.Count == 0) return;
            SetWindowToLoading();
            Regex? regex = BuildCurrentRegex();
            if (regex is null) { ResetWindowLoading(); return; }
            IList selection = ResultsListView.SelectedItems;
            List<FindResult> targets = selection.Count >= 2
                ? [.. selection.Cast<FindResult>()]
                : [.. ResultsListView.Items.Cast<FindResult>()];
            await Task.Run(() => Dispatcher.Invoke(() =>
                textEditWindow.ReplaceInSpreadsheetCells(targets, string.Empty, regex)));
            SearchForText();
            ResetWindowLoading();
            return;
        }

        if (Matches is null || Matches.Count < 1)
            return;

        SetWindowToLoading();

        IList selection2 = ResultsListView.SelectedItems;
        StringBuilder stringBuilderOfText = new(textEditWindow.PassedTextControl.Text);

        await Task.Run(() =>
        {
            if (selection2.Count < 2)
                selection2 = ResultsListView.Items;

            for (int j = selection2.Count - 1; j >= 0; j--)
            {
                if (selection2[j] is not FindResult selectedResult)
                    continue;

                stringBuilderOfText.Remove(selectedResult.Index, selectedResult.Length);
            }
        });

        textEditWindow.PassedTextControl.Text = stringBuilderOfText.ToString();

        SearchForText();
        ResetWindowLoading();
    }

    private void EditTextBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (IsSpreadsheetSearch) return;

        ChangeFindTextTimer.Stop();
        if (textEditWindow is not null)
            StringFromWindow = textEditWindow.PassedTextControl.Text;

        ChangeFindTextTimer.Start();
    }

    private void ExtractPattern_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (IsSpreadsheetSearch) { e.CanExecute = false; return; }

        if (textEditWindow is not null
            && textEditWindow.PassedTextControl.SelectedText.Length > 0)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void ExtractPattern_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (textEditWindow is null)
            return;

        string? selection = textEditWindow.PassedTextControl.SelectedText;

        // Generate all precision levels from the selected text
        // Use inverse of ExactMatchCheckBox: when exact match is OFF, ignore case
        bool ignoreCase = ExactMatchCheckBox.IsChecked is not true;
        extractedPattern = new ExtractedPattern(selection, ignoreCase);

        int precisionLevel = (int)PrecisionSlider.Value;
        string simplePattern = extractedPattern.GetPattern(precisionLevel);

        UsePatternCheckBox.IsChecked = true;
        FindTextBox.Text = simplePattern;

        // Show the slider now that we have an extracted pattern
        PrecisionSliderPanel.Visibility = Visibility.Visible;

        SearchForText();
    }

    private void FindAndReplacedLoaded(object sender, RoutedEventArgs e)
    {
        if (TextSearchUtilities.HasSearchText(FindTextBox.Text))
            SearchForText();

        // Update save button visibility on load
        UpdateSaveButtonVisibility();

        FindTextBox.Focus();
    }

    private void FindTextBox_KeyUp(object sender, KeyEventArgs e)
    {
        ChangeFindTextTimer.Stop();

        // Clear extracted pattern when user manually edits the find text
        // This prevents the slider from trying to generate patterns from regex
        if (extractedPattern is not null)
        {
            extractedPattern = null;
            // Hide slider when there's no extracted pattern
            PrecisionSliderPanel.Visibility = Visibility.Collapsed;
        }

        // Update save button visibility when text changes
        UpdateSaveButtonVisibility();

        if (e.Key == Key.Enter)
        {
            ChangeFindTextTimer.Stop();
            SearchForText();
            e.Handled = true;
        }
        else
        {
            ChangeFindTextTimer.Start();
        }
    }

    private void MoreOptionsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        Visibility optionsVisibility = Visibility.Collapsed;
        if (MoreOptionsToggleButton.IsChecked is true)
            optionsVisibility = Visibility.Visible;

        SetExtraOptionsVisibility(optionsVisibility);
    }

    private void OptionsChangedRefresh(object sender, RoutedEventArgs e)
    {
        bool ignoreCase = ExactMatchCheckBox.IsChecked is not true;

        // If we have an extracted pattern and the case sensitivity changed, update it
        if (extractedPattern is not null)
        {
            if (extractedPattern.IgnoreCase != ignoreCase)
            {
                extractedPattern.IgnoreCase = ignoreCase;

                // Update the FindTextBox with the regenerated pattern
                int precisionLevel = (int)PrecisionSlider.Value;
                FindTextBox.Text = extractedPattern.GetPattern(precisionLevel);
            }
        }
        else if (UsePatternCheckBox.IsChecked is true && TextSearchUtilities.HasSearchText(FindTextBox.Text))
        {
            // No extracted pattern, but we're in pattern mode - manually toggle (?i) flag
            string currentPattern = FindTextBox.Text;
            bool hasIgnoreCaseFlag = currentPattern.StartsWith("(?i)");
            bool hasCaseSensitiveFlag = currentPattern.StartsWith("(?-i)");

            if (ignoreCase && !hasIgnoreCaseFlag)
            {
                // Need case-insensitive: add (?i) flag
                if (hasCaseSensitiveFlag)
                {
                    // Replace (?-i) with (?i)
                    FindTextBox.Text = "(?i)" + currentPattern[5..];
                }
                else
                {
                    // Add (?i) at the beginning
                    FindTextBox.Text = $"(?i){currentPattern}";
                }
            }
            else if (!ignoreCase && hasIgnoreCaseFlag)
            {
                // Need case-sensitive: remove (?i) flag
                FindTextBox.Text = currentPattern[4..];
            }
        }

        // Update save button visibility
        UpdateSaveButtonVisibility();

        SearchForText();
    }

    private void Replace_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (IsSpreadsheetSearch)
        {
            e.CanExecute = FindResults.Count > 0 && !string.IsNullOrEmpty(ReplaceTextBox.Text);
            return;
        }

        if (string.IsNullOrEmpty(ReplaceTextBox.Text)
            || Matches is null
            || Matches.Count < 1)
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void Replace_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (textEditWindow is null || ResultsListView.Items.Count is 0)
            return;

        if (IsSpreadsheetSearch)
        {
            if (ResultsListView.SelectedIndex == -1) ResultsListView.SelectedIndex = 0;
            if (ResultsListView.SelectedItem is not FindResult fr) return;
            Regex? regex = BuildCurrentRegex();
            if (regex is null) return;
            textEditWindow.ReplaceInSpreadsheetCells([fr], ReplaceTextBox.Text, regex);
            SearchForText();
            return;
        }

        if (Matches is null) return;

        if (ResultsListView.SelectedIndex == -1)
            ResultsListView.SelectedIndex = 0;

        if (ResultsListView.SelectedItem is not FindResult selectedResult)
            return;

        textEditWindow.PassedTextControl.Select(selectedResult.Index, selectedResult.Length);
        textEditWindow.PassedTextControl.SelectedText = ReplaceTextBox.Text;

        SearchForText();
    }

    private async void ReplaceAll_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (textEditWindow is null) return;

        if (IsSpreadsheetSearch)
        {
            if (FindResults.Count == 0) return;
            SetWindowToLoading();
            Regex? regex = BuildCurrentRegex();
            if (regex is null) { ResetWindowLoading(); return; }
            IList selection = ResultsListView.SelectedItems;
            List<FindResult> targets = selection.Count >= 2
                ? [.. selection.Cast<FindResult>()]
                : [.. ResultsListView.Items.Cast<FindResult>()];
            string replaceWith = ReplaceTextBox.Text;
            await Task.Run(() => Dispatcher.Invoke(() =>
                textEditWindow.ReplaceInSpreadsheetCells(targets, replaceWith, regex)));
            SearchForText();
            ResetWindowLoading();
            return;
        }

        if (Matches is null || Matches.Count < 1)
            return;

        SetWindowToLoading();

        StringBuilder stringBuilder = new(textEditWindow.PassedTextControl.Text);

        IList selection2 = ResultsListView.SelectedItems;
        string newText = ReplaceTextBox.Text;

        await Task.Run(() =>
        {
            if (selection2.Count < 2)
                selection2 = ResultsListView.Items;

            for (int j = selection2.Count - 1; j >= 0; j--)
            {
                if (selection2[j] is not FindResult selectedResult)
                    continue;

                stringBuilder.Remove(selectedResult.Index, selectedResult.Length);
                stringBuilder.Insert(selectedResult.Index, newText);
            }
        });

        textEditWindow.PassedTextControl.Text = stringBuilder.ToString();

        SearchForText();
        ResetWindowLoading();
    }

    private void ApplyTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Controls.ContextMenu menu = new()
        {
            PlacementTarget = ApplyTemplateButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
        };

        if (IsSpreadsheetSearch)
        {
            menu.Items.Add(DisabledMenuItem("Not available in spreadsheet mode"));
            menu.IsOpen = true;
            return;
        }

        // Load text-only templates fresh each time, mirroring the Edit Text Window menu.
        List<GrabTemplate> textOnlyTemplates = GrabTemplateManager.GetAllTemplates()
            .Where(template => template.IsTextOnly && template.IsValid)
            .ToList();

        if (textOnlyTemplates.Count == 0)
        {
            menu.Items.Add(DisabledMenuItem("No text-only templates found"));
            menu.IsOpen = true;
            return;
        }

        bool hasMatches = Matches is not null && Matches.Count > 0;

        foreach (GrabTemplate template in textOnlyTemplates)
        {
            System.Windows.Controls.MenuItem item = new()
            {
                Header = template.Name,
                ToolTip = string.IsNullOrWhiteSpace(template.Description) ? null : template.Description,
                Tag = template,
                IsEnabled = hasMatches,
            };
            item.Click += TemplateMenuItem_Click;
            menu.Items.Add(item);
        }

        if (!hasMatches)
            menu.Items.Add(DisabledMenuItem("Run a search to find matches first"));

        menu.IsOpen = true;
    }

    private static System.Windows.Controls.MenuItem DisabledMenuItem(string text) =>
        new() { Header = text, IsEnabled = false };

    private void TemplateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem { Tag: GrabTemplate template })
            _ = ApplyTemplateToMatchesAsync(template);
    }

    /// <summary>
    /// Applies a text-only Grab Template to every matched result, replacing each match
    /// with the template output evaluated against that match's own text. When two or
    /// more results are selected, only those are affected; otherwise all matches are.
    /// </summary>
    private async Task ApplyTemplateToMatchesAsync(GrabTemplate template)
    {
        if (textEditWindow is null || IsSpreadsheetSearch)
            return;

        if (Matches is null || Matches.Count < 1)
            return;

        SetWindowToLoading();

        string originalText = textEditWindow.PassedTextControl.Text;
        StringBuilder stringBuilder = new(originalText);

        IList selection = ResultsListView.SelectedItems;
        List<FindResult> targets = selection.Count >= 2
            ? [.. selection.Cast<FindResult>()]
            : [.. ResultsListView.Items.Cast<FindResult>()];

        await Task.Run(() =>
        {
            // Apply from the end backwards so earlier indices stay valid as we edit.
            foreach (FindResult result in targets.OrderByDescending(r => r.Index))
            {
                if (result.Index < 0 || result.Index + result.Length > originalText.Length)
                    continue;

                string matchText = originalText.Substring(result.Index, result.Length);
                string replacement = GrabTemplateExecutor.ApplyTextOnlyTemplate(template, matchText);

                stringBuilder.Remove(result.Index, result.Length);
                stringBuilder.Insert(result.Index, replacement);
            }
        });

        textEditWindow.PassedTextControl.Text = stringBuilder.ToString();
        GrabTemplateManager.RecordUsage(template.Id);

        SearchForText();
        ResetWindowLoading();
    }

    private void ResetWindowLoading()
    {
        MainContentGrid.IsEnabled = true;
        LoadingSpinner.Visibility = Visibility.Collapsed;
    }

    private void SetWindowToLoading()
    {
        MainContentGrid.IsEnabled = false;
        LoadingSpinner.Visibility = Visibility.Visible;
    }

    private void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsListView.SelectedItem is not FindResult selectedResult || textEditWindow is null)
            return;

        if (IsSpreadsheetSearch)
        {
            if (selectedResult.RowIndex.HasValue && selectedResult.ColumnIndex.HasValue)
                textEditWindow.NavigateToSpreadsheetCell(
                    selectedResult.RowIndex.Value, selectedResult.ColumnIndex.Value);
            this.Focus();
            return;
        }

        textEditWindow.PassedTextControl.Focus();
        textEditWindow.PassedTextControl.Select(selectedResult.Index, selectedResult.Length);
        this.Focus();
    }

    private void SetExtraOptionsVisibility(Visibility optionsVisibility)
    {
        ReplaceTextBox.Visibility = optionsVisibility;
        ReplaceButton.Visibility = optionsVisibility;
        ReplaceAllButton.Visibility = optionsVisibility;
        MoreOptionsHozStack.Visibility = optionsVisibility;
        EvenMoreOptionsHozStack.Visibility = optionsVisibility;
        PatternButtonsStack.Visibility = optionsVisibility;
    }

    private void TextSearch_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (!TextSearchUtilities.HasSearchText(FindTextBox.Text))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void TextSearch_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SearchForText();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        ChangeFindTextTimer.Tick -= ChangeFindText_Tick;
        PrecisionSliderTimer.Tick -= PrecisionSlider_Tick;
        textEditWindow?.PassedTextControl.TextChanged -= EditTextBoxChanged;
    }
    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (TextSearchUtilities.HasSearchText(FindTextBox.Text))
                FindTextBox.Clear();
            else
                this.Close();
        }
    }

    private void PrecisionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Prevent event from firing during window initialization
        if (!IsLoaded)
            return;

        // Only update if we have a previously extracted pattern
        if (extractedPattern is null)
            return;

        // Only update if regex mode is enabled
        if (UsePatternCheckBox?.IsChecked is not true)
            return;

        int precisionLevel = (int)e.NewValue;

        // Get the pre-generated pattern at this precision level (instant, no recalculation!)
        string pattern = extractedPattern.GetPattern(precisionLevel);

        FindTextBox.Text = pattern;

        // Use debounced search instead of immediate search
        PrecisionSliderTimer.Stop();
        PrecisionSliderTimer.Start();
    }

    private void ManageRegexButton_Click(object sender, RoutedEventArgs e)
    {
        RegexManager regexManager = WindowUtilities.OpenOrActivateWindow<RegexManager>();
        regexManager.Owner = this;
        regexManager.Show();
    }

    private void SavePatternButton_Click(object sender, RoutedEventArgs e)
    {
        // Get the current pattern from the FindTextBox
        string pattern = FindTextBox.Text;

        if (string.IsNullOrWhiteSpace(pattern))
            return;

        // Get a short description from the source text if available
        string sourceText = string.Empty;
        if (textEditWindow is not null)
        {
            sourceText = !string.IsNullOrWhiteSpace(textEditWindow.PassedTextControl.SelectedText)
                ? textEditWindow.PassedTextControl.SelectedText.MakeStringSingleLine().Truncate(30)
                : textEditWindow.PassedTextControl.Text.MakeStringSingleLine().Truncate(30);
        }

        // Open the RegexManager and start adding the pattern
        RegexManager regexManager = WindowUtilities.OpenOrActivateWindow<RegexManager>();
        regexManager.Owner = this;
        regexManager.SourceEditTextWindow = textEditWindow;
        regexManager.Show();
        regexManager.AddPatternFromText(pattern, sourceText, textEditWindow);
    }

    private void UsePatternCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        // Update save button visibility when regex mode is toggled
        UpdateSaveButtonVisibility();
    }

    private void UpdateSaveButtonVisibility()
    {
        // Show save button only when:
        // 1. Using regex mode
        // 2. Find text is not empty
        // 3. Pattern doesn't already exist in saved patterns
        SavePatternButton.Visibility =
            (UsePatternCheckBox.IsChecked is true &&
             !string.IsNullOrWhiteSpace(FindTextBox.Text) &&
             !IsPatternAlreadySaved(FindTextBox.Text))
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private bool IsPatternAlreadySaved(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        StoredRegex[] savedPatterns = AppUtilities.TextGrabSettingsService.LoadStoredRegexes();
        if (savedPatterns.Length == 0)
            return false;

        // Check if any saved pattern matches the current pattern exactly
        return savedPatterns.Any(p => p.Pattern == pattern);
    }

    internal void FindByPattern(ExtractedPattern pattern, int? precisionLevel = null)
    {
        // Store the ExtractedPattern so the slider can use it
        extractedPattern = pattern;

        // Ensure the pattern's case sensitivity matches the current checkbox state
        bool ignoreCase = ExactMatchCheckBox.IsChecked is not true;
        extractedPattern.IgnoreCase = ignoreCase;

        // If a precision level was provided, use it; otherwise use the current slider value
        int levelToUse = precisionLevel ?? (int)PrecisionSlider.Value;

        // Update the slider to reflect the precision level being used
        PrecisionSlider.Value = levelToUse;

        FindTextBox.Text = pattern.GetPattern(levelToUse);

        UsePatternCheckBox.IsChecked = true;

        // Show the slider now that we have an extracted pattern
        PrecisionSliderPanel.Visibility = Visibility.Visible;

        // Update save button visibility
        UpdateSaveButtonVisibility();

        SearchForText();
    }

    #endregion Methods
}
