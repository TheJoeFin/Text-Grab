using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Editing;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Text_Grab;

public partial class EditTextWindow
{
    public void SetBottomBarButtons()
    {
        BottomBarButtons.Children.Clear();

        List<CollapsibleButton> buttons = CustomBottomBarUtilities.GetBottomBarButtons(this);

        if (DefaultSettings.ScrollBottomBar)
            BottomBarScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        else
            BottomBarScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        if (DefaultSettings.ShowCursorText)
            BottomBarText.Visibility = Visibility.Visible;
        else
            BottomBarText.Visibility = Visibility.Collapsed;

        LiveTranscriptionToggleButton.Visibility =
            DefaultSettings.EtwShowTranscribe && AudioTranscriptionUtilities.IsAudioTranscriptionSupported()
                ? Visibility.Visible
                : Visibility.Collapsed;

        foreach (CollapsibleButton collapsibleButton in buttons)
            BottomBarButtons.Children.Add(collapsibleButton);

        if (DefaultSettings.EtwShowLangPicker)
        {
            LanguagePicker languagePicker = new();
            languagePicker.LanguageChanged -= LanguagePicker_LanguageChanged;
            languagePicker.LanguageChanged += LanguagePicker_LanguageChanged;
            BottomBarButtons.Children.Add(languagePicker);
        }
    }

    private void HideSelectionSpecificUi()
    {
        MatchCountButton.Visibility = Visibility.Collapsed;
        RegexPatternButton.Visibility = Visibility.Collapsed;
        SimilarMatchesButton.Visibility = Visibility.Collapsed;
        CharDetailsButton.Visibility = Visibility.Collapsed;
    }

    private void EditBottomBarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        BottomBarSettings bbs = new()
        {
            Owner = this
        };
        bbs.ShowDialog();
    }

    private void ProcessBottomBarKeyPress(System.Windows.Input.KeyEventArgs e)
    {
        UIElementCollection bottomBarButtons = BottomBarButtons.Children;

        int keyNumberPressed = (int)e.Key - 35;

        // D1 is 35
        // ...
        // D9 is 43
        // D0 is 34

        if (keyNumberPressed is < -1
            or > 8)
            return;

        // since D9 is next to D0 it makes sense
        // to call buttons next to each other as well
        if (keyNumberPressed == -1)
            keyNumberPressed += 10;

        if (bottomBarButtons.Count <= keyNumberPressed)
            return;

        if (bottomBarButtons[keyNumberPressed] is not CollapsibleButton correspondingButton)
            return;

        e.Handled = true;

        if (correspondingButton.Command is ICommand buttonCommand)
            buttonCommand.Execute(null);
        else
            correspondingButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    /// <summary>
    /// Selects the given range of the raw text in whichever editor is currently visible and scrolls
    /// it into view; see <see cref="IEditorSession.SelectInEditor"/>.
    /// </summary>
    public void SelectInEditor(int index, int length) => Session.SelectInEditor(new TextSpan(index, length));

    /// <summary>
    /// <see cref="MarkdownSurface"/>'s half of <see cref="IEditorSession.SelectInEditor"/>: selecting
    /// in the raw text box alone has no visible effect while it is hidden behind the rendered document.
    /// </summary>
    /// <remarks>
    /// In markdown mode the rendered document's plain text is not the same string as the raw
    /// markdown <paramref name="index"/>/<paramref name="length"/> are measured against — bold
    /// markers, heading <c>#</c>s, list bullets, link brackets, etc. are stripped on render. The
    /// index is translated through the offset map built alongside the rendered document
    /// (<see cref="MarkdownFlowDocumentUtilities.BuildOffsetMap"/>) rather than applied directly.
    /// </remarks>
    private void SelectInMarkdownEditor(int index, int length)
    {
        SyncMarkdownTextFromDocument();
        if (markdownOffsetMapSourceText != Session.Text)
            LoadMarkdownDocumentFromText(Session.Text);

        if (MarkdownEditorControl.Document is not null)
        {
            TextPointer start = MarkdownFlowDocumentUtilities.MapRawOffsetToPosition(MarkdownEditorControl.Document, markdownOffsetMap, index);
            TextPointer end = MarkdownFlowDocumentUtilities.MapRawOffsetToPosition(MarkdownEditorControl.Document, markdownOffsetMap, index + length);
            MarkdownEditorControl.Selection.Select(start, end);
            MarkdownEditorControl.Focus();
            start.Paragraph?.BringIntoView();
        }
    }

    private void HideBottomBarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is MenuItem bbMi && bbMi.IsChecked)
        {
            BottomBar.Visibility = Visibility.Collapsed;
            DefaultSettings.EditWindowBottomBarIsHidden = true;
        }
        else
        {
            BottomBar.Visibility = Visibility.Visible;
            DefaultSettings.EditWindowBottomBarIsHidden = false;
        }
    }

    private void SelectAllMenuItem_Click(Object? sender = null, RoutedEventArgs? e = null)
    {
        if (!IsLoaded)
            return;

        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            SpreadsheetDataGrid.SelectAllCells();
            SpreadsheetDataGrid.Focus();
            return;
        }

        Session.Selection = new TextSpan(0, Session.Text.Length);
    }

    private void SelectionContainsNewLinesCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        // Unstack reshapes the flat document text into rows, which is redundant with the
        // grid structure Spreadsheet mode already provides, so keep it disabled there.
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            e.CanExecute = false;
            return;
        }

        if (Session.SelectedText.Contains(Environment.NewLine)
            || Session.SelectedText.Contains('\r')
            || Session.SelectedText.Contains('\n'))
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void SelectLine(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        (int lineStart, int lineLength) = Session.Text.GetStartAndLengthOfLineAtPosition(Session.Selection.Start);

        Session.Selection = new TextSpan(lineStart, lineLength);
    }

    private void SelectLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SelectLine();
    }

    private void SelectNoneMenuItem_Click(Object? sender = null, RoutedEventArgs? e = null)
    {
        if (!IsLoaded)
            return;

        Session.Selection = new TextSpan(0, 0);
    }

    private void SelectWord(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        if (TrySelectSpreadsheetWord())
            return;

        (int wordStart, int wordLength) = Session.Text.CursorWordBoundaries(Session.Selection.Start);

        Session.Selection = new TextSpan(wordStart, wordLength);
    }

    private void SelectWordMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SelectWord();
    }

    private void UpdateLineAndColumnText()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            HideSelectionSpecificUi();

            int rowCount = spreadsheetTable.Rows.Count;
            int columnCount = spreadsheetTable.Columns.Count;

            if (SpreadsheetDataGrid.SelectedCells.Count == 0)
            {
                if (SpreadsheetDataGrid.CurrentCell.Column is not null)
                {
                    int currentRowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
                    int currentColumnIndex = SpreadsheetDataGrid.CurrentCell.Column.DisplayIndex;

                    BottomBarText.Text = currentRowIndex >= 0
                        ? $"Rows {rowCount}, Cols {columnCount}, Row {currentRowIndex + 1}, Col {currentColumnIndex + 1}"
                        : $"Rows {rowCount}, Cols {columnCount}";
                }
                else
                {
                    BottomBarText.Text = $"Rows {rowCount}, Cols {columnCount}";
                }

                return;
            }

            int selectedRowCount = SpreadsheetDataGrid.SelectedCells
                .Select(cell => SpreadsheetDataGrid.Items.IndexOf(cell.Item))
                .Where(index => index >= 0)
                .Distinct()
                .Count();
            int selectedColumnCount = SpreadsheetDataGrid.SelectedCells
                .Select(cell => cell.Column.DisplayIndex)
                .Distinct()
                .Count();

            BottomBarText.Text =
                $"Rows {rowCount}, Cols {columnCount}, Selected {SpreadsheetDataGrid.SelectedCells.Count} cells ({selectedRowCount} rows x {selectedColumnCount} cols)";
            return;
        }

        if (editorMode == EtwEditorMode.Markdown)
        {
            HideSelectionSpecificUi();

            string plainText = MarkdownEditorControl.Document is null
                ? string.Empty
                : MarkdownFlowDocumentUtilities.GetDocumentPlainText(MarkdownEditorControl.Document);
            string selectedText = MarkdownEditorControl.Selection.Text.TrimEnd('\r', '\n');

            BottomBarText.Text = string.IsNullOrEmpty(selectedText)
                ? $"Markdown, Chars {plainText.Length}"
                : $"Markdown, Selected {selectedText.Length} chars";
            return;
        }

        char[] delimiters = [' ', '\r', '\n'];

        // Line and column come from the text box's layout (wrapped lines count), so they stay on the control.
        TextSpan selection = Session.Selection;
        if (selection.Length < 1)
        {
            int lineNumber = PassedTextControl.GetLineIndexFromCharacterIndex(selection.Start);
            int columnNumber = selection.Start - PassedTextControl.GetCharacterIndexFromLineIndex(lineNumber);
            int words = Session.Text.RemoveNonWordChars().Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Length;

            string text = DefaultSettings.EtwShowWordCount
                ? $"Wrds {words}, Ln {lineNumber + 1}, Col {columnNumber}"
                : $"Ln {lineNumber + 1}, Col {columnNumber}";

            BottomBarText.Text = text;

            // Hide selection-specific UI elements
            MatchCountButton.Visibility = Visibility.Collapsed;
            RegexPatternButton.Visibility = Visibility.Collapsed;
            SimilarMatchesButton.Visibility = Visibility.Collapsed;
            CharDetailsButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            int selectionStartIndex = selection.Start;
            int selectionStopIndex = selection.End;
            int words = Session.Text.Split(delimiters, StringSplitOptions.RemoveEmptyEntries).Length;

            int selStartLine = PassedTextControl.GetLineIndexFromCharacterIndex(selectionStartIndex);

            if (selStartLine < 0)
            {
                BottomBarText.Text = $"Ln -, Col -";
                return;
            }

            int selStartCol = selectionStartIndex - PassedTextControl.GetCharacterIndexFromLineIndex(selStartLine);
            int selStopLine = PassedTextControl.GetLineIndexFromCharacterIndex(selectionStopIndex); ;
            int selStopCol = selectionStopIndex - PassedTextControl.GetCharacterIndexFromLineIndex(selStopLine); ;
            int selLength = selection.Length;
            int numbOfSelectedLines = selStopLine - selStartLine;

            if (numbOfSelectedLines > 0)
                BottomBarText.Text = $"Ln {selStartLine + 1}:{selStopLine + 1}, Col {selStartCol}:{selStopCol}, Len {selLength}, Lines {numbOfSelectedLines + 1}";
            else
                BottomBarText.Text = $"Ln {selStartLine + 1}, Col {selStartCol}:{selStopCol}, Len {selLength}";

            // Update selection-specific UI elements
            UpdateSelectionSpecificUI();
        }
    }

    private void UpdateSelectionSpecificUI()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            HideSelectionSpecificUi();
            return;
        }

        string selectedText = Session.SelectedText;

        if (string.IsNullOrEmpty(selectedText))
        {
            MatchCountButton.Visibility = Visibility.Collapsed;
            SimilarMatchesButton.Visibility = Visibility.Collapsed;
            RegexPatternButton.Visibility = Visibility.Collapsed;
            CharDetailsButton.Visibility = Visibility.Collapsed;
            return;
        }

        // Show character details for single character selection
        if (DefaultSettings.EtwShowCharDetails && selectedText.Length == 1)
        {
            char selectedChar = selectedText[0];
            int codePoint = char.ConvertToUtf32(selectedText, 0);
            string unicodeHex = $"U+{codePoint:X4}";

            CharDetailsButtonText.Text = unicodeHex;
            CharDetailsButton.ToolTip = $"{unicodeHex}: {CharacterUtilities.GetUnicodeCategory(selectedChar)}";
            CharDetailsButton.Visibility = Visibility.Visible;
        }
        else if (DefaultSettings.EtwShowCharDetails && selectedText.Length > 1)
        {
            CharDetailsButtonText.Text = $"{selectedText.Length} chars";
            CharDetailsButton.ToolTip = "Click to see character details";
            CharDetailsButton.Visibility = Visibility.Visible;
        }
        else
        {
            CharDetailsButton.Visibility = Visibility.Collapsed;
        }

        // Show match count
        if (DefaultSettings.EtwShowMatchCount && !string.IsNullOrEmpty(selectedText))
        {
            int matchCount = StringMethods.CountMatches(Session.Text, selectedText);
            if (MatchCountButton.Content is TextBlock matchButton)
            {
                matchButton.Text = matchCount == 1 ? "1 match" : $"{matchCount} matches";
            }
            MatchCountButton.Visibility = matchCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            MatchCountButton.Visibility = Visibility.Collapsed;
        }

        // Show similar matches count using regex pattern
        if (DefaultSettings.EtwShowSimilarMatches && !string.IsNullOrEmpty(selectedText) && selectedText.Length > 0 && selectedText.Length <= 50)
        {
            // Generate and store the ExtractedPattern if the selection changed
            if (currentExtractedPattern is null || currentExtractedPattern.OriginalText != selectedText)
            {
                currentExtractedPattern = new ExtractedPattern(selectedText, ignoreCase: true);
                currentPrecisionLevel = ExtractedPattern.DefaultPrecisionLevel;
            }

            string regexPattern = currentExtractedPattern.GetPattern(currentPrecisionLevel);
            int similarCount = StringMethods.CountRegexMatches(Session.Text, regexPattern);
            if (SimilarMatchesButton.Content is TextBlock similarButton)
            {
                similarButton.Text = similarCount == 1 ? "1 similar" : $"{similarCount} similar";
            }
            string levelLabel = ExtractedPattern.GetLevelLabel(currentPrecisionLevel);
            SimilarMatchesButton.ToolTip = $"Click to Find and Replace with: {regexPattern}\n(Precision: {levelLabel})\nScroll mouse wheel to adjust precision";
            SimilarMatchesButton.Visibility = similarCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            SimilarMatchesButton.Visibility = Visibility.Collapsed;
        }

        // Show regex pattern
        if (DefaultSettings.EtwShowRegexPattern && !string.IsNullOrEmpty(selectedText) && selectedText.Length > 0 && selectedText.Length <= 50)
        {
            // Generate and store the ExtractedPattern if the selection changed
            if (currentExtractedPattern is null || currentExtractedPattern.OriginalText != selectedText)
            {
                currentExtractedPattern = new ExtractedPattern(selectedText, ignoreCase: true);
                currentPrecisionLevel = ExtractedPattern.DefaultPrecisionLevel;
            }

            string regexPattern = currentExtractedPattern.GetPattern(currentPrecisionLevel);
            if (RegexPatternButton.Content is TextBlock regexButton)
            {
                regexButton.Text = regexPattern.Length > 30
                    ? $"Regex: {regexPattern[..27]}..."
                    : $"Regex: {regexPattern}";
            }
            string levelLabel = ExtractedPattern.GetLevelLabel(currentPrecisionLevel);
            RegexPatternButton.ToolTip = $"Click to Find and Replace with: {regexPattern}\n(Precision: {levelLabel})\nScroll mouse wheel to adjust precision";
            RegexPatternButton.Visibility = Visibility.Visible;
        }
        else
        {
            RegexPatternButton.Visibility = Visibility.Collapsed;
        }
    }

    private string GenerateRegexPattern(string text)
    {
        // Use the stored ExtractedPattern if available and matches current text
        if (currentExtractedPattern is not null && currentExtractedPattern.OriginalText == text)
        {
            return currentExtractedPattern.GetPattern(currentPrecisionLevel);
        }

        // Otherwise create new pattern at default precision
        ExtractedPattern extractedPattern = new(text);
        return extractedPattern.GetPattern(ExtractedPattern.DefaultPrecisionLevel);
    }

    private void MatchCountButton_Click(object sender, RoutedEventArgs e)
    {
        // Open find and replace with the selection pre-loaded
        LaunchFindAndReplace();
    }

    private void SimilarMatchesButton_Click(object sender, RoutedEventArgs e)
    {
        string selectedText = Session.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
            return;

        // Use the stored ExtractedPattern if available, otherwise create new one
        ExtractedPattern extractedPattern;
        if (currentExtractedPattern is not null && currentExtractedPattern.OriginalText == selectedText)
        {
            extractedPattern = currentExtractedPattern;
        }
        else
        {
            extractedPattern = new ExtractedPattern(selectedText, ignoreCase: true);
        }

        // Launch Find and Replace with regex enabled and execute search
        FindAndReplaceWindow findAndReplaceWindow = WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>();
        findAndReplaceWindow.TextEditWindow = this;
        findAndReplaceWindow.StringFromWindow = Session.Text;
        findAndReplaceWindow.FindByPattern(extractedPattern, currentPrecisionLevel);
        findAndReplaceWindow.Show();
    }

    private void RegexPatternButton_Click(object sender, RoutedEventArgs e)
    {
        string selectedText = Session.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
            return;

        // Use the stored ExtractedPattern if available, otherwise create new one
        ExtractedPattern extractedPattern;
        if (currentExtractedPattern is not null && currentExtractedPattern.OriginalText == selectedText)
        {
            extractedPattern = currentExtractedPattern;
        }
        else
        {
            extractedPattern = new ExtractedPattern(selectedText, ignoreCase: true);
        }

        // Launch Find and Replace with regex enabled and execute search
        FindAndReplaceWindow findAndReplaceWindow = WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>();
        findAndReplaceWindow.TextEditWindow = this;
        findAndReplaceWindow.StringFromWindow = Session.Text;
        findAndReplaceWindow.FindByPattern(extractedPattern, currentPrecisionLevel);
        findAndReplaceWindow.Show();
    }

    private void PatternButton_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Only handle if we have a valid ExtractedPattern
        if (currentExtractedPattern is null)
            return;

        // Determine scroll direction for animation
        bool scrollingUp = e.Delta > 0;

        // Adjust precision level based on scroll direction
        if (scrollingUp)
        {
            // Scroll up = increase precision (more specific pattern)
            currentPrecisionLevel = Math.Min(currentPrecisionLevel + 1, ExtractedPattern.MaxPrecisionLevel);
        }
        else if (e.Delta < 0)
        {
            // Scroll down = decrease precision (more general pattern)
            currentPrecisionLevel = Math.Max(currentPrecisionLevel - 1, ExtractedPattern.MinPrecisionLevel);
        }

        // Update the UI to reflect the new precision level
        UpdateSelectionSpecificUI();

        // Add visual feedback animation to make the precision change more obvious
        AnimatePrecisionChange(sender, scrollingUp);

        // Mark the event as handled so it doesn't bubble up
        e.Handled = true;
    }

    private void AnimatePrecisionChange(object sender, bool scrollingUp)
    {
        if (sender is not System.Windows.Controls.Button button)
            return;

        // Ensure the button has a RenderTransform for translation
        if (button.RenderTransform is not TranslateTransform)
        {
            button.RenderTransform = new TranslateTransform(0, 0);
        }

        TranslateTransform translateTransform = (TranslateTransform)button.RenderTransform;

        // Create a slide animation based on scroll direction
        // Scrolling up (increasing precision) = slide text up (negative Y)
        // Scrolling down (decreasing precision) = slide text down (positive Y)
        double slideDistance = 10;
        double startY = scrollingUp ? slideDistance : -slideDistance;

        DoubleAnimation slideAnimation = new()
        {
            From = startY,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // Apply the animation to Y translation
        translateTransform.BeginAnimation(TranslateTransform.YProperty, slideAnimation);
    }

    private void CharDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        string selectedText = Session.SelectedText;

        if (string.IsNullOrEmpty(selectedText))
            return;

        CharDetailsPopupContent.Children.Clear();

        if (selectedText.Length == 1)
        {
            // Show details for single character in multi-line TextBox
            char c = selectedText[0];
            string details = CharacterUtilities.GetCharacterDetailsText(c);

            System.Windows.Controls.TextBox detailsTextBox = new()
            {
                Text = details,
                FontSize = 12,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.Wrap,
                Cursor = System.Windows.Input.Cursors.Arrow,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            CharDetailsPopupContent.Children.Add(detailsTextBox);
        }
        else
        {
            // Show details for multiple characters in one multi-line TextBox
            StringBuilder allDetails = new();
            allDetails.AppendLine($"Character Details ({selectedText.Length} characters)");
            allDetails.AppendLine();

            // Limit to first 10 characters to avoid huge popup
            int charLimit = Math.Min(selectedText.Length, 10);
            for (int i = 0; i < charLimit; i++)
            {
                char c = selectedText[i];
                allDetails.AppendLine(CharacterUtilities.GetCharacterDetailsText(c));

                if (i < charLimit - 1)
                    allDetails.AppendLine(); // Add blank line between characters
            }

            if (selectedText.Length > charLimit)
            {
                allDetails.AppendLine();
                allDetails.AppendLine($"... and {selectedText.Length - charLimit} more");
            }

            System.Windows.Controls.TextBox detailsTextBox = new()
            {
                Text = allDetails.ToString(),
                FontSize = 12,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.Wrap,
                Cursor = System.Windows.Input.Cursors.Arrow,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 400
            };
            CharDetailsPopupContent.Children.Add(detailsTextBox);
        }

        CharDetailsPopup.IsOpen = true;
    }

    private void SaveCurrentPatternToRegexManager()
    {
        if (currentExtractedPattern is null)
            return;

        string pattern = currentExtractedPattern.GetPattern(currentPrecisionLevel);
        string sourceText = currentExtractedPattern.OriginalText;

        RegexManager regexManager = WindowUtilities.OpenOrActivateWindow<RegexManager>();
        regexManager.AddPatternFromText(pattern, sourceText, this);
        regexManager.Show();
    }

    private void LoadStoredPatternToSelection(StoredRegex storedPattern)
    {
        if (storedPattern is null)
            return;

        // Open Find and Replace window with this pattern and execute search
        FindAndReplaceWindow findWindow = WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>();
        findWindow.SetFindText(storedPattern.Pattern, useRegex: true);
        findWindow.TextEditWindow = this;
        findWindow.StringFromWindow = Session.Text;
        findWindow.SearchForText();
        findWindow.Show();
    }

    private void ShowRegexExplanation()
    {
        if (currentExtractedPattern is null)
            return;

        string pattern = currentExtractedPattern.GetPattern(currentPrecisionLevel);
        string explanation = pattern.ExplainRegexPattern();

        Wpf.Ui.Controls.MessageBox messageBox = new()
        {
            Title = "Regex Pattern Explanation",
            Content = explanation,
            CloseButtonText = "Close"
        };
        _ = messageBox.ShowDialogAsync();
    }

    private void ExplainPatternMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string? pattern = currentExtractedPattern?.GetPattern(currentPrecisionLevel);

        if (string.IsNullOrEmpty(pattern))
            return;

        // Clear previous content
        CharDetailsPopupContent.Children.Clear();

        // Create explanation text
        string explanation = pattern.ExplainRegexPattern();

        System.Windows.Controls.TextBox explanationTextBox = new()
        {
            Text = explanation,
            FontSize = 12,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            TextWrapping = TextWrapping.Wrap,
            Cursor = System.Windows.Input.Cursors.Arrow,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400,
            Padding = new Thickness(8)
        };

        CharDetailsPopupContent.Children.Add(explanationTextBox);
        CharDetailsPopup.IsOpen = true;
    }

    private void SavePatternMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string? pattern = currentExtractedPattern?.GetPattern(currentPrecisionLevel);

        if (string.IsNullOrEmpty(pattern))
            return;

        // open the RegexManager and save this pattern
        RegexManager manager = WindowUtilities.OpenOrActivateWindow<RegexManager>();
        manager.AddPatternFromText(pattern, GetSelectedTextOrAllText(), this);
        manager.Show();

    }

    private void PatternContextOpening(object sender, ContextMenuEventArgs e)
    {
        // sender should be a button if not return
        if (sender is not System.Windows.Controls.Button button)
            return;

        // get the context menu
        if (button.ContextMenu is null)
            return;

        ContextMenu contextMenu = button.ContextMenu;

        // Clear existing dynamic items (keep original static items)
        // Find if "Use Pattern" menu item already exists, remove it to rebuild
        MenuItem? existingUsePatternItem = null;
        foreach (object? item in contextMenu.Items)
        {
            if (item is MenuItem mi && mi.Header?.ToString() == "Use Pattern")
            {
                existingUsePatternItem = mi;
                break;
            }
        }

        if (existingUsePatternItem is not null)
            contextMenu.Items.Remove(existingUsePatternItem);

        // make a context menu item for "use this pattern"
        MenuItem usePatternMenuItem = new()
        {
            Header = "Use Pattern"
        };

        // add all patterns from regex manager as menu items as children to the new "use this pattern" item
        List<StoredRegex> storedPatterns = LoadRegexPatterns();

        if (storedPatterns.Count == 0)
        {
            MenuItem noPatternItem = new()
            {
                Header = "No saved patterns",
                IsEnabled = false
            };
            usePatternMenuItem.Items.Add(noPatternItem);
        }
        else
        {
            foreach (StoredRegex storedPattern in storedPatterns)
            {
                MenuItem patternItem = new()
                {
                    Header = storedPattern.Name,
                    ToolTip = storedPattern.Pattern,
                    Tag = storedPattern
                };

                // wire up click event to override the currentExtractedPattern
                patternItem.Click += (s, args) =>
                {
                    if (s is MenuItem clickedItem && clickedItem.Tag is StoredRegex selectedPattern)
                    {
                        // Create a new ExtractedPattern from the stored pattern
                        // Use the pattern's description or name as the source text
                        string sourceText = string.IsNullOrWhiteSpace(selectedPattern.Description)
                            ? selectedPattern.Name
                            : selectedPattern.Description;

                        // Override the current extracted pattern with the selected stored pattern
                        currentExtractedPattern = new ExtractedPattern(sourceText, ignoreCase: true);
                        currentPrecisionLevel = ExtractedPattern.DefaultPrecisionLevel;

                        // Update the UI to reflect the new pattern
                        UpdateSelectionSpecificUI();

                        // Optionally open Find and Replace with this pattern
                        LoadStoredPatternToSelection(selectedPattern);
                    }
                };

                usePatternMenuItem.Items.Add(patternItem);
            }
        }

        // Add separator before the "Use Pattern" item
        contextMenu.Items.Add(new Separator());

        // Add the "Use Pattern" menu item to the context menu
        contextMenu.Items.Add(usePatternMenuItem);
    }

    private List<StoredRegex> LoadRegexPatterns()
    {
        List<StoredRegex> returnRegexes = [];
        returnRegexes.AddRange(AppUtilities.TextGrabSettingsService.LoadStoredRegexes());

        // Add default patterns if list is empty
        if (returnRegexes.Count == 0)
        {
            foreach (StoredRegex defaultPattern in StoredRegex.GetDefaultPatterns())
                returnRegexes.Add(defaultPattern);
        }

        return returnRegexes;
    }
}
