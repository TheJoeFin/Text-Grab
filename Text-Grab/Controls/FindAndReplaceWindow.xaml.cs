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

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for FindAndReplaceWindow.xaml
/// </summary>
public partial class FindAndReplaceWindow : Window
{
    public string StringFromWindow { get; set; } = "";

    private EditTextWindow? textEditWindow;
    public EditTextWindow? TextEditWindow
    {
        get
        {
            return textEditWindow;
        }
        set
        {
            textEditWindow = value;

            if (textEditWindow is not null)
                textEditWindow.PassedTextControl.TextChanged += EditTextBoxChanged;
        }
    }

    public static RoutedCommand TextSearchCmd = new();
    public static RoutedCommand ReplaceOneCmd = new();
    public static RoutedCommand ReplaceAllCmd = new();
    public static RoutedCommand ExtractPatternCmd = new();
    public static RoutedCommand DeleteAllCmd = new();
    public static RoutedCommand CopyMatchesCmd = new();
    DispatcherTimer ChangeFindTextTimer = new();

    public List<FindResult> FindResults { get; set; } = new();

    private string? Pattern { get; set; }

    private MatchCollection? Matches;

    public FindAndReplaceWindow()
    {
        InitializeComponent();

        ChangeFindTextTimer.Interval = TimeSpan.FromMilliseconds(400);
        ChangeFindTextTimer.Tick -= ChangeFindText_Tick;
        ChangeFindTextTimer.Tick += ChangeFindText_Tick;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        ChangeFindTextTimer.Tick -= ChangeFindText_Tick;
    }

    public void ShouldCloseWithThisETW(EditTextWindow etw)
    {
        if (TextEditWindow is not null && etw == TextEditWindow)
            Close();
    }

    private void ChangeFindText_Tick(object? sender, EventArgs? e)
    {
        ChangeFindTextTimer.Stop();
        SearchForText();
    }

    private void FindAndReplacedLoaded(object sender, RoutedEventArgs e)
    {
        if (TextEditWindow != null)
        {
            double etwMidTop = TextEditWindow.Top + (TextEditWindow.Height / 2);
            double etwMidLeft = TextEditWindow.Left + (TextEditWindow.Width / 2);

            double thisMidTop = etwMidTop - (this.Height / 2);
            double thisMidLeft = etwMidLeft - (this.Width / 2);

            this.Top = thisMidTop;
            this.Left = thisMidLeft;
        }

        if (!string.IsNullOrWhiteSpace(FindTextBox.Text))
            SearchForText();


        FindTextBox.Focus();
    }

    private void EditTextBoxChanged(object sender, TextChangedEventArgs e)
    {
        ChangeFindTextTimer.Stop();
        if (TextEditWindow != null)
            StringFromWindow = TextEditWindow.PassedTextControl.Text;

        ChangeFindTextTimer.Start();
    }

    public void SearchForText()
    {
        FindResults.Clear();
        ResultsListView.ItemsSource = null;

        Pattern = FindTextBox.Text;

        if (UsePaternCheckBox.IsChecked is false)
            Pattern = Pattern.EscapeSpecialRegexChars();

        try
        {
            if (ExactMatchCheckBox.IsChecked is true)
                Matches = Regex.Matches(StringFromWindow.ToLower(), Pattern, RegexOptions.Multiline);
            else
                Matches = Regex.Matches(StringFromWindow.ToLower(), Pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
        }
        catch (Exception ex)
        {
            MatchesText.Text = "Error searching: " + ex.GetType().ToString();
            return;
        }

        if (Matches.Count < 1 || string.IsNullOrWhiteSpace(FindTextBox.Text))
        {
            MatchesText.Text = "0 Matches";
            return;
        }

        MatchesText.Text = $"{Matches.Count} Matches";
        ResultsListView.IsEnabled = true;
        int count = 1;
        foreach (Match m in Matches)
        {
            FindResult fr = new()
            {
                Index = m.Index,
                Text = m.Value,
                PreviewLeft = GetCharactersToLeftOfNewLine(m.Index, 12),
                PreviewRight = GetCharactersToRightOfNewLine(m.Index + m.Length, 12),
                Count = count
            };
            FindResults.Add(fr);

            count++;
        }

        ResultsListView.ItemsSource = FindResults;

        Match? firstMatch = Matches[0];

        if (TextEditWindow != null
            && firstMatch != null
            && this.IsFocused)
        {
            TextEditWindow.PassedTextControl.Select(firstMatch.Index, firstMatch.Value.Length);
            TextEditWindow.PassedTextControl.Focus();
            this.Focus();
        }
    }

    // a method which uses GetNewLineIndexToLeft and returns the string from the given index to the newLine character to the left of the given index
    // if the string is longer the x number of characters, it will return the last x number of characters
    // and if the string is at the beginning don't add "..." to the beginning
    private string GetCharactersToLeftOfNewLine(int index, int numberOfCharacters)
    {
        int newLineIndex = GetNewLineIndexToLeft(index);

        if (newLineIndex < 1)
            return StringFromWindow.Substring(0, index);

        newLineIndex++;

        if (index - newLineIndex < numberOfCharacters)
            return "..." + StringFromWindow.Substring(newLineIndex, index - newLineIndex);

        return "..." + StringFromWindow.Substring(index - numberOfCharacters, numberOfCharacters);
    }

    // same as GetCharactersToLeftOfNewLine but to the right
    private string GetCharactersToRightOfNewLine(int index, int numberOfCharacters)
    {
        int newLineIndex = GetNewLineIndexToRight(index);
        if (newLineIndex < 1)
            return StringFromWindow.Substring(index);

        if (newLineIndex - index > numberOfCharacters)
            return StringFromWindow.Substring(index, numberOfCharacters) + "...";

        if (newLineIndex == StringFromWindow.Length)
            return StringFromWindow.Substring(index);

        return StringFromWindow.Substring(index, newLineIndex - index) + "...";
    }

    // a method which returns the nearst newLine character index to the left of the given index
    private int GetNewLineIndexToLeft(int index)
    {
        char newLineChar = Environment.NewLine.ToArray().Last();

        int newLineIndex = index;
        while (newLineIndex > 0 && StringFromWindow[newLineIndex] != newLineChar)
            newLineIndex--;

        return newLineIndex;
    }

    // a method which returns the nearst newLine character index to the right of the given index
    private int GetNewLineIndexToRight(int index)
    {
        char newLineChar = Environment.NewLine.ToArray().First();

        int newLineIndex = index;
        while (newLineIndex < StringFromWindow.Length && StringFromWindow[newLineIndex] != newLineChar)
            newLineIndex++;

        return newLineIndex;
    }

    private void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsListView.SelectedItem is not FindResult selectedResult)
            return;

        if (TextEditWindow != null)
        {
            TextEditWindow.PassedTextControl.Select(selectedResult.Index, selectedResult.Length);
            TextEditWindow.PassedTextControl.Focus();
            this.Focus();
        }
    }

    private void ExtractPattern_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (TextEditWindow != null
                && TextEditWindow.PassedTextControl.SelectedText.Length > 0)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void ExtractPattern_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (TextEditWindow == null)
            return;

        string? selection = TextEditWindow.PassedTextControl.SelectedText;

        string simplePattern = selection.ExtractSimplePattern();

        UsePaternCheckBox.IsChecked = true;
        FindTextBox.Text = simplePattern;

        SearchForText();
    }

    private void MoreOptionsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        Visibility optionsVisibility = Visibility.Collapsed;
        if (MoreOptionsToggleButton.IsChecked is true)
            optionsVisibility = Visibility.Visible;

        ReplaceTextBox.Visibility = optionsVisibility;
        ReplaceButton.Visibility = optionsVisibility;
        ReplaceAllButton.Visibility = optionsVisibility;
        MoreOptionsHozStack.Visibility = optionsVisibility;
        EvenMoreOptionsHozStack.Visibility = optionsVisibility;
    }

    private void ReplaceAll_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selection = ResultsListView.SelectedItems;

        if (Matches == null
            || Matches.Count < 1
            || textEditWindow is null)
            return;

        if (selection.Count < 2)
        {
            for (int i = Matches.Count - 1; i >= 0; i--)
            {
                Match matchItem = Matches[i];
                textEditWindow.PassedTextControl.Select(matchItem.Index, matchItem.Length);
                textEditWindow.PassedTextControl.SelectedText = ReplaceTextBox.Text;
            }
        }
        else
        {
            for (int j = selection.Count - 1; j >= 0; j--)
            {
                if (selection[j] is not FindResult selectedResult)
                    continue;

                textEditWindow.PassedTextControl.Select(selectedResult.Index, selectedResult.Length);
                textEditWindow.PassedTextControl.SelectedText = ReplaceTextBox.Text;
            }
        }

        SearchForText();
    }

    private void Replace_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ReplaceTextBox.Text)
            || Matches == null
            || Matches.Count < 1)
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void Replace_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        int selectedResultIndex = ResultsListView.SelectedIndex;

        if (selectedResultIndex == -1)
        {
            if (ResultsListView.Items.Count < 1)
                return;
            else
                selectedResultIndex = 0;
        }

        if (Matches == null)
            return;

        Match sameMatch = Matches[selectedResultIndex];

        if (TextEditWindow != null)
        {
            TextEditWindow.PassedTextControl.Select(sameMatch.Index, sameMatch.Length);
            TextEditWindow.PassedTextControl.SelectedText = ReplaceTextBox.Text;
        }

        SearchForText();
    }

    private void DeleteAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (Matches == null || Matches.Count < 1 || string.IsNullOrEmpty(FindTextBox.Text))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void DeleteAll_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selection = ResultsListView.SelectedItems;

        if (Matches == null
            || Matches.Count < 1)
            return;

        if (selection.Count < 2)
        {
            for (int i = Matches.Count - 1; i >= 0; i--)
            {
                Match matchItem = Matches[i];
                if (TextEditWindow != null)
                {
                    TextEditWindow.PassedTextControl.Select(matchItem.Index, matchItem.Length);
                    TextEditWindow.PassedTextControl.SelectedText = "";
                }
            }
        }
        else
        {
            for (int j = selection.Count - 1; j >= 0; j--)
            {
                string? selectionItem = selection[j] as string;
                if (selectionItem != null && TextEditWindow != null)
                {
                    string? intString = selectionItem.Split('\t').FirstOrDefault();
                    if (intString != null)
                    {
                        int currentIndex = int.Parse(intString);
                        currentIndex--;
                        Match match = Matches[currentIndex];
                        TextEditWindow.PassedTextControl.Select(match.Index, match.Length);
                        TextEditWindow.PassedTextControl.SelectedText = "";
                    }
                }
            }
        }

        SearchForText();
    }

    private void CopyMatchesCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (Matches == null || Matches.Count < 1 || string.IsNullOrEmpty(FindTextBox.Text))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void CopyMatchesCmd_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selection = ResultsListView.SelectedItems;

        if (Matches == null
            || Matches.Count < 1)
            return;

        StringBuilder stringBuilder = new();

        if (selection.Count < 2)
        {
            for (int i = Matches.Count - 1; i >= 0; i--)
            {
                stringBuilder.AppendLine(Matches[i].Value);
            }
        }
        else
        {
            for (int j = selection.Count - 1; j >= 0; j--)
            {
                string? selectionItem = selection[j] as string;
                if (selectionItem != null && TextEditWindow != null)
                {
                    string? intString = selectionItem.Split('\t').FirstOrDefault();
                    if (intString != null)
                    {
                        int currentIndex = int.Parse(intString);
                        currentIndex--;
                        Match match = Matches[currentIndex];
                        stringBuilder.AppendLine(Matches[currentIndex].Value);
                    }
                }
            }
        }

        EditTextWindow etw = new();
        etw.AddThisText(stringBuilder.ToString());
        etw.Show();
    }

    private void TextSearch_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FindTextBox.Text))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void TextSearch_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SearchForText();
    }

    private void FindTextBox_KeyUp(object sender, KeyEventArgs e)
    {
        ChangeFindTextTimer.Stop();

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

    private void OptionsChangedRefresh(object sender, RoutedEventArgs e)
    {
        SearchForText();
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrWhiteSpace(FindTextBox.Text))
                FindTextBox.Clear();
            else
                this.Close();
        }
    }
}
