using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for SplitColumnWindow.xaml.
/// Lets the user break a spreadsheet cell into multiple columns by delimiter,
/// regex pattern, or fixed length, with a live preview of the result.
/// </summary>
public partial class SplitColumnWindow : Wpf.Ui.Controls.FluentWindow
{
    private const int PreviewDebounceDelayMs = 200;
    private const int PreviewMaxColumnWidth = 24;

    private static readonly Regex PatternTokenRegex = new(@"^\{([pr]):(.+)\}$", RegexOptions.Compiled);

    private readonly DispatcherTimer previewDebounceTimer = new();
    private string lastSourceSelectedText = string.Empty;
    private IReadOnlyList<PatternItem> allPatternItems = [];

    public static RoutedCommand SplitCmd = new();
    public static RoutedCommand ApplyCmd = new();

    /// <summary>
    /// The contents of the sample cell shown in the read-only source box.
    /// Set by the owner before the window is shown.
    /// </summary>
    public string SampleText { get; set; } = string.Empty;

    public SplitColumnWindow()
    {
        InitializeComponent();

        previewDebounceTimer.Interval = TimeSpan.FromMilliseconds(PreviewDebounceDelayMs);
        previewDebounceTimer.Tick += PreviewDebounceTimer_Tick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SourceTextBox.Text = SampleText;
        LoadPatternPicker();
        UpdatePreview();
        DelimiterTextBox.Focus();
    }

    private void LoadPatternPicker()
    {
        // Feed the inline picker the same unified catalog the Grab Template editor uses:
        // saved regexes (inserted as {p:Name}) and built-in smart patterns ({r:Name}).
        allPatternItems = PatternItem.GetAll();
        PatternPickerBox.ItemsSource =
        [
            .. allPatternItems.Select(p => new InlinePickerItem(p.Name, TokenFor(p), p.GroupLabel)
            {
                Kind = p.Kind,
            }),
        ];
    }

    private static string TokenFor(PatternItem pattern)
        => pattern.Kind == PatternKind.SavedRegex ? $"{{p:{pattern.Name}}}" : $"{{r:{pattern.Name}}}";

    private void Window_Closed(object? sender, EventArgs e)
    {
        previewDebounceTimer.Stop();
        PreviewTextBox.Clear();
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

    private void Split_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = Owner is EditTextWindow;
    }

    private void Split_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ApplySplit();
        Close();
    }

    private void Apply_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ApplySplit();
    }

    private void ApplySplit()
    {
        if (Owner is EditTextWindow etwOwner)
            etwOwner.SplitSelectedSpreadsheetCells(BuildOptions());
    }

    private SplitColumnOptions BuildOptions()
    {
        if (RegexRadioButton.IsChecked is true)
        {
            (PatternItem? chosenPattern, string rawRegex) = ResolvePatternInput();
            return new SplitColumnOptions
            {
                Mode = SplitMode.Regex,
                PatternItem = chosenPattern,
                Pattern = rawRegex,
                IgnoreCase = IgnoreCaseToggle.IsChecked is true,
                SplitterHandling = GetSplitterHandling(),
            };
        }

        if (FixedLengthRadioButton.IsChecked is true)
        {
            _ = int.TryParse(LengthTextBox.Text, out int length);
            return new SplitColumnOptions
            {
                Mode = SplitMode.FixedLength,
                Length = Math.Max(0, length),
                SplitFromEnd = FromEndToggle.IsChecked is true,
            };
        }

        return new SplitColumnOptions
        {
            Mode = SplitMode.Delimiter,
            DelimiterText = DelimiterTextBox.Text,
            SplitterHandling = GetSplitterHandling(),
        };
    }

    private SplitterHandling GetSplitterHandling()
    {
        if (SplitterLeftRadio.IsChecked is true)
            return SplitterHandling.KeepLeft;
        if (SplitterRightRadio.IsChecked is true)
            return SplitterHandling.KeepRight;
        return SplitterHandling.Remove;
    }

    private void SplitModeRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        DelimiterPanel.Visibility = DelimiterRadioButton.IsChecked is true ? Visibility.Visible : Visibility.Collapsed;
        RegexPanel.Visibility = RegexRadioButton.IsChecked is true ? Visibility.Visible : Visibility.Collapsed;
        FixedLengthPanel.Visibility = FixedLengthRadioButton.IsChecked is true ? Visibility.Visible : Visibility.Collapsed;

        // The splitter is removed/kept only when splitting on a delimiter or pattern; fixed-length keeps all text.
        SplitterHandlingPanel.Visibility = FixedLengthRadioButton.IsChecked is true ? Visibility.Collapsed : Visibility.Visible;

        UpdatePreview();
    }

    private void SplitInputChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        previewDebounceTimer.Stop();
        previewDebounceTimer.Start();
    }

    private void SourceTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (SourceTextBox.SelectionLength > 0)
            lastSourceSelectedText = SourceTextBox.SelectedText;
    }

    private void PatternInputChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        previewDebounceTimer.Stop();
        previewDebounceTimer.Start();
    }

    /// <summary>
    /// Interprets the pattern picker's content: a lone {p:Name}/{r:Name} token resolves to that
    /// saved/smart <see cref="PatternItem"/>; anything else is treated as a raw regex.
    /// </summary>
    private (PatternItem? Item, string RawRegex) ResolvePatternInput()
    {
        string serialized = PatternPickerBox.GetSerializedText().Trim();

        Match tokenMatch = PatternTokenRegex.Match(serialized);
        if (tokenMatch.Success)
        {
            PatternKind kind = tokenMatch.Groups[1].Value == "p" ? PatternKind.SavedRegex : PatternKind.Recognizer;
            string name = tokenMatch.Groups[2].Value;

            PatternItem? item = allPatternItems.FirstOrDefault(
                p => p.Kind == kind && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (item is not null)
                return (item, string.Empty);
        }

        return (null, serialized);
    }

    private void ExtractButton_Click(object sender, RoutedEventArgs e)
    {
        string selection = SourceTextBox.SelectionLength > 0 ? SourceTextBox.SelectedText : lastSourceSelectedText;
        if (string.IsNullOrEmpty(selection))
            return;

        bool ignoreCase = IgnoreCaseToggle.IsChecked is true;
        ExtractedPattern extractedPattern = new(selection, ignoreCase);
        int level = ExtractedPattern.DetermineStartingLevel(selection);

        // Extraction produces a raw regex; drop it into the picker as plain text.
        PatternPickerBox.SetSerializedText(extractedPattern.GetPattern(level), []);

        previewDebounceTimer.Stop();
        previewDebounceTimer.Start();
    }

    private void PreviewDebounceTimer_Tick(object? sender, EventArgs e)
    {
        previewDebounceTimer.Stop();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell(SampleText, BuildOptions());

        // Render the resulting parts as side-by-side columns: a header row of
        // column labels above a row of the values, aligned in a monospace grid.
        StringBuilder headerRow = new();
        StringBuilder valueRow = new();

        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                headerRow.Append(" │ ");
                valueRow.Append(" │ ");
            }

            string header = $"Col {i + 1}";
            string value = parts[i].ReplaceLineEndings(" ");
            if (value.Length > PreviewMaxColumnWidth)
                value = string.Concat(value.AsSpan(0, PreviewMaxColumnWidth - 1), "…");

            int columnWidth = Math.Max(header.Length, value.Length);
            headerRow.Append(header.PadRight(columnWidth));
            valueRow.Append(value.PadRight(columnWidth));
        }

        PreviewTextBox.Text = string.Concat(headerRow.ToString(), Environment.NewLine, valueRow.ToString());
    }
}
