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

    private bool IsSmartPatternSearch =>
        SearchBar.SelectedPattern is { Kind: PatternKind.Recognizer, Recognizer: not null };

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
            if (textEditWindow is not null)
            {
                textEditWindow.PassedTextControl.TextChanged -= EditTextBoxChanged;
                textEditWindow.EditorModeChanged -= EditTextWindow_EditorModeChanged;
            }

            textEditWindow = value;

            if (textEditWindow is not null)
            {
                textEditWindow.PassedTextControl.TextChanged += EditTextBoxChanged;
                textEditWindow.EditorModeChanged += EditTextWindow_EditorModeChanged;
            }
        }
    }
    private string? Pattern { get; set; }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Loads text into the shared search bar (optionally enabling regex) and places the caret at
    /// the end. Used by other windows that open Find &amp; Replace pre-filled with a pattern.
    /// </summary>
    public void SetFindText(string text, bool useRegex = false)
    {
        SearchBar.SearchText = text;
        if (useRegex)
            SearchBar.UseRegex = true;
        SearchBar.FocusInput();
    }

    public void SearchForText()
    {
        if (IsSpreadsheetSearch) { SearchSpreadsheetCells(); return; }

        RefreshSourceTextFromEditor();
        FindResults.Clear();
        ResultsListView.ItemsSource = null;

        // Recognizers are find-only (no regex replace). A saved regex, by contrast, has already
        // been loaded into the find box, so it flows through the normal regex search below.
        // When a recognizer chip is active, any typed text narrows its matches.
        if (SearchBar.SelectedPattern is { Kind: PatternKind.Recognizer, Recognizer: { } selectedRecognizer })
        {
            SearchByRecognizer(selectedRecognizer, SearchBar.SearchText);
            return;
        }

        if (!TextSearchUtilities.HasSearchText(SearchBar.SearchText))
        {
            Matches = null;
            MatchesText.Text = "0 Matches";
            return;
        }

        Pattern = SearchBar.SearchText;

        // Auto-detect regex pattern: if starts with ^ and ends with $, enable regex mode and strip anchors
        if (Pattern.StartsWith('^') && Pattern.EndsWith('$') && Pattern.Length > 2)
        {
            SearchBar.UseRegex = true;
            Pattern = Pattern[1..^1]; // Strip ^ from start and $ from end
        }

        if (!SearchBar.UseRegex)
            Pattern = Pattern.EscapeSpecialRegexChars(SearchBar.ExactMatch);

        try
        {
            // When using pattern mode with inline flags, rely on the inline flags for case sensitivity
            // Otherwise, use RegexOptions for backward compatibility
            bool usingPatternMode = SearchBar.UseRegex;
            bool exactMatch = SearchBar.ExactMatch;
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
                RawText = m.Value,
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

    /// <summary>
    /// Finds every entity the recognizer detects in the source text and lists them as
    /// <see cref="FindResult"/>s. When <paramref name="narrowText"/> is supplied, only matches
    /// whose text contains it are kept (the chip + free-text case). Leaves <see cref="Matches"/>
    /// null (like spreadsheet search), so regex-based replace/navigation is disabled in recognizer mode.
    /// </summary>
    private void SearchByRecognizer(BuiltInRecognizer recognizer, string narrowText = "")
    {
        RefreshSourceTextFromEditor();

        Matches = null;

        IReadOnlyList<RecognizerMatch> recognizerMatches = RecognizerExecutor.GetMatches(recognizer, StringFromWindow);

        if (!string.IsNullOrEmpty(narrowText))
            recognizerMatches = [.. recognizerMatches.Where(m => m.Text.Contains(narrowText, StringComparison.CurrentCultureIgnoreCase))];

        if (recognizerMatches.Count == 0)
        {
            MatchesText.Text = "0 Matches";
            return;
        }

        MatchesText.Text = recognizerMatches.Count == 1 ? "1 Match" : $"{recognizerMatches.Count} Matches";
        ResultsListView.IsEnabled = true;

        int count = 1;
        foreach (RecognizerMatch m in recognizerMatches)
        {
            FindResult fr = new()
            {
                Index = m.Start,
                Text = TextSearchUtilities.FormatMatchTextForDisplay(m.Text),
                RawText = m.Text,
                PreviewLeft = StringMethods.GetCharactersToLeftOfNewLine(ref stringFromWindow, m.Start, 12).MakeStringSingleLine(),
                PreviewRight = StringMethods.GetCharactersToRightOfNewLine(ref stringFromWindow, m.Start + m.Length, 12).MakeStringSingleLine(),
                Length = m.Length,
                Count = count,
            };
            FindResults.Add(fr);
            count++;
        }

        ResultsListView.ItemsSource = FindResults;

        if (textEditWindow is not null && this.IsFocused)
        {
            RecognizerMatch first = recognizerMatches[0];
            textEditWindow.PassedTextControl.Select(first.Start, first.Length);
            textEditWindow.PassedTextControl.Focus();
            this.Focus();
        }
    }

    private Regex? BuildCurrentRegex()
    {
        string rawPattern = SearchBar.SearchText;
        if (!TextSearchUtilities.HasSearchText(rawPattern)) return null;

        if (rawPattern.StartsWith('^') && rawPattern.EndsWith('$') && rawPattern.Length > 2)
            rawPattern = rawPattern[1..^1];

        if (!SearchBar.UseRegex)
            rawPattern = rawPattern.EscapeSpecialRegexChars(SearchBar.ExactMatch);

        try { return TextSearchUtilities.CreateReplacementRegex(rawPattern, SearchBar.ExactMatch); }
        catch { return null; }
    }

    private void SearchSpreadsheetCells()
    {
        FindResults.Clear();
        ResultsListView.ItemsSource = null;
        Matches = null;

        if (textEditWindow is null)
        {
            MatchesText.Text = "0 Matches";
            return;
        }

        textEditWindow.CommitSpreadsheetAndSync();

        List<FindResult> results;
        if (SearchBar.SelectedPattern is { Kind: PatternKind.Recognizer, Recognizer: not null } selectedPattern)
        {
            results = textEditWindow.SearchSpreadsheetCells(selectedPattern, SearchBar.SearchText);
        }
        else
        {
            if (!TextSearchUtilities.HasSearchText(SearchBar.SearchText))
            {
                MatchesText.Text = "0 Matches";
                return;
            }

            Regex? regex = BuildCurrentRegex();
            if (regex is null) { MatchesText.Text = "0 Matches"; return; }

            try { results = textEditWindow.SearchSpreadsheetCells(regex); }
            catch (RegexMatchTimeoutException) { MatchesText.Text = "Regex timeout"; return; }
        }

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
        e.CanExecute = FindResults.Count > 0
            && (IsSmartPatternSearch || !string.IsNullOrEmpty(SearchBar.SearchText));
    }

    private void CopyMatchesCmd_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (textEditWindow is null) return;

        if (FindResults.Count == 0)
            return;

        IList selection = ResultsListView.SelectedItems;
        if (selection.Count < 2)
            selection = ResultsListView.Items;

        string matchText = GetMatchTextForEditing(selection.OfType<FindResult>());
        if (string.IsNullOrEmpty(matchText))
            return;

        EditTextWindow etw = new();
        etw.AddThisText(matchText);
        etw.Show();
    }

    internal static string GetMatchTextForEditing(IEnumerable<FindResult> findResults)
    {
        return string.Join(Environment.NewLine, findResults.Select(findResult => findResult.RawText));
    }

    private void DeleteAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (IsSpreadsheetSearch)
        {
            e.CanExecute = !IsSmartPatternSearch
                && FindResults.Count > 0
                && !string.IsNullOrEmpty(SearchBar.SearchText);
            return;
        }

        if (Matches is not null && Matches.Count > 1 && !string.IsNullOrEmpty(SearchBar.SearchText))
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private async void DeleteAll_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (textEditWindow is null) return;

        if (IsSpreadsheetSearch)
        {
            if (IsSmartPatternSearch) return;
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

    private void EditTextWindow_EditorModeChanged(object? sender, EventArgs e)
    {
        ChangeFindTextTimer.Stop();
        SearchForText();
    }

    private void RefreshSourceTextFromEditor()
    {
        StringFromWindow = ResolveSearchSourceText(
            StringFromWindow,
            textEditWindow?.PassedTextControl.Text,
            IsSpreadsheetSearch);
    }

    internal static string ResolveSearchSourceText(string cachedText, string? editorText, bool isSpreadsheetSearch)
        => !isSpreadsheetSearch && editorText is not null ? editorText : cachedText;

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
        // Use inverse of the exact-match toggle: when exact match is OFF, ignore case
        bool ignoreCase = !SearchBar.ExactMatch;
        extractedPattern = new ExtractedPattern(selection, ignoreCase);

        int precisionLevel = (int)PrecisionSlider.Value;
        string simplePattern = extractedPattern.GetPattern(precisionLevel);

        SearchBar.UseRegex = true;
        SearchBar.SearchText = simplePattern;

        // Show the slider now that we have an extracted pattern
        PrecisionSliderPanel.Visibility = Visibility.Visible;

        SearchForText();
    }

    private void FindAndReplacedLoaded(object sender, RoutedEventArgs e)
    {
        if (IsSmartPatternSearch || TextSearchUtilities.HasSearchText(SearchBar.SearchText))
            SearchForText();

        // Update save button visibility on load
        UpdateSaveButtonVisibility();

        SearchBar.FocusInput();
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

    private void OptionsChangedRefresh(object? sender, EventArgs e)
    {
        bool ignoreCase = !SearchBar.ExactMatch;

        // If we have an extracted pattern and the case sensitivity changed, update it
        if (extractedPattern is not null)
        {
            if (extractedPattern.IgnoreCase != ignoreCase)
            {
                extractedPattern.IgnoreCase = ignoreCase;

                // Update the FindTextBox with the regenerated pattern
                int precisionLevel = (int)PrecisionSlider.Value;
                SearchBar.SearchText = extractedPattern.GetPattern(precisionLevel);
            }
        }
        else if (SearchBar.UseRegex && TextSearchUtilities.HasSearchText(SearchBar.SearchText))
        {
            // No extracted pattern, but we're in pattern mode - manually toggle (?i) flag
            string currentPattern = SearchBar.SearchText;
            bool hasIgnoreCaseFlag = currentPattern.StartsWith("(?i)");
            bool hasCaseSensitiveFlag = currentPattern.StartsWith("(?-i)");

            if (ignoreCase && !hasIgnoreCaseFlag)
            {
                // Need case-insensitive: add (?i) flag
                if (hasCaseSensitiveFlag)
                {
                    // Replace (?-i) with (?i)
                    SearchBar.SearchText = "(?i)" + currentPattern[5..];
                }
                else
                {
                    // Add (?i) at the beginning
                    SearchBar.SearchText = $"(?i){currentPattern}";
                }
            }
            else if (!ignoreCase && hasIgnoreCaseFlag)
            {
                // Need case-sensitive: remove (?i) flag
                SearchBar.SearchText = currentPattern[4..];
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
            e.CanExecute = !IsSmartPatternSearch
                && FindResults.Count > 0
                && !string.IsNullOrEmpty(ReplaceTextBox.Text);
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
            if (IsSmartPatternSearch) return;
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
            if (IsSmartPatternSearch) return;
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
        BulkActionsGrid.Visibility = optionsVisibility;
        MatchActionsGrid.Visibility = optionsVisibility;
        PatternActionsGrid.Visibility = optionsVisibility;
    }

    private void TextSearch_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = IsSmartPatternSearch || TextSearchUtilities.HasSearchText(SearchBar.SearchText);
    }

    private void TextSearch_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SearchForText();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        ChangeFindTextTimer.Tick -= ChangeFindText_Tick;
        PrecisionSliderTimer.Tick -= PrecisionSlider_Tick;
        if (textEditWindow is not null)
        {
            textEditWindow.PassedTextControl.TextChanged -= EditTextBoxChanged;
            textEditWindow.EditorModeChanged -= EditTextWindow_EditorModeChanged;
        }
    }
    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (TextSearchUtilities.HasSearchText(SearchBar.SearchText))
                SearchBar.SearchText = string.Empty;
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
        if (!SearchBar.UseRegex)
            return;

        int precisionLevel = (int)e.NewValue;

        // Get the pre-generated pattern at this precision level (instant, no recalculation!)
        string pattern = extractedPattern.GetPattern(precisionLevel);

        SearchBar.SearchText = pattern;

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
        string pattern = SearchBar.SearchText;

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

    /// <summary>
    /// Re-runs the search (debounced) whenever the shared search bar's text, regex/exact toggles,
    /// or selected pattern change. Keyboard specifics (Enter, clearing an extracted pattern) are
    /// handled in <see cref="FindTextBox_KeyUp"/>.
    /// </summary>
    private void SearchBar_SearchChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded)
            return;

        UpdateSaveButtonVisibility();
        ChangeFindTextTimer.Stop();
        ChangeFindTextTimer.Start();
    }

    private void UpdateSaveButtonVisibility()
    {
        // Show save button only when:
        // 1. Using regex mode
        // 2. Find text is not empty
        // 3. Pattern doesn't already exist in saved patterns
        SavePatternButton.Visibility =
            (SearchBar.UseRegex &&
             !string.IsNullOrWhiteSpace(SearchBar.SearchText) &&
             !IsPatternAlreadySaved(SearchBar.SearchText))
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
        bool ignoreCase = !SearchBar.ExactMatch;
        extractedPattern.IgnoreCase = ignoreCase;

        // If a precision level was provided, use it; otherwise use the current slider value
        int levelToUse = precisionLevel ?? (int)PrecisionSlider.Value;

        // Update the slider to reflect the precision level being used
        PrecisionSlider.Value = levelToUse;

        SearchBar.SearchText = pattern.GetPattern(levelToUse);

        SearchBar.UseRegex = true;

        // Show the slider now that we have an extracted pattern
        PrecisionSliderPanel.Visibility = Visibility.Visible;

        // Update save button visibility
        UpdateSaveButtonVisibility();

        SearchForText();
    }

    #endregion Methods
}
