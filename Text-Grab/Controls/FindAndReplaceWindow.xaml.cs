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
    DispatcherTimer ChangeFindTextTimer = new();
    private MatchCollection? Matches;
    private string stringFromWindow = "";
    private EditTextWindow? textEditWindow;

    #endregion Fields

    #region Constructors

    public FindAndReplaceWindow()
    {
        InitializeComponent();

        ChangeFindTextTimer.Interval = TimeSpan.FromMilliseconds(400);
        ChangeFindTextTimer.Tick -= ChangeFindText_Tick;
        ChangeFindTextTimer.Tick += ChangeFindText_Tick;
    }

    #endregion Constructors

    #region Properties

    public List<FindResult> FindResults { get; set; } = new();

    public string StringFromWindow
    {
        get { return stringFromWindow; }
        set { stringFromWindow = value; }
    }
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
    private string? Pattern { get; set; }

    #endregion Properties

    #region Methods

    public void SearchForText()
    {
        FindResults.Clear();
        ResultsListView.ItemsSource = null;

        Pattern = FindTextBox.Text;

        if (UsePaternCheckBox.IsChecked is false && ExactMatchCheckBox.IsChecked is bool matchExactly)
            Pattern = Pattern.EscapeSpecialRegexChars(matchExactly);

        try
        {
            if (ExactMatchCheckBox.IsChecked is true)
                Matches = Regex.Matches(StringFromWindow, Pattern, RegexOptions.Multiline);
            else
                Matches = Regex.Matches(StringFromWindow, Pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
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
                Text = m.Value.MakeStringSingleLine(),
                PreviewLeft = StringMethods.GetCharactersToLeftOfNewLine(ref stringFromWindow, m.Index, 12),
                PreviewRight = StringMethods.GetCharactersToRightOfNewLine(ref stringFromWindow, m.Index + m.Length, 12),
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

    private void CopyMatchesCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (Matches is null || Matches.Count < 1 || string.IsNullOrEmpty(FindTextBox.Text))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void CopyMatchesCmd_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (Matches is null
            || textEditWindow is null
            || Matches.Count < 1)
            return;

        StringBuilder stringBuilder = new();

        var selection = ResultsListView.SelectedItems;
        if (selection.Count < 2)
            selection = ResultsListView.Items;

        foreach (var item in selection)
            if (item is FindResult findResult)
                stringBuilder.AppendLine(findResult.Text);

        EditTextWindow etw = new();
        etw.AddThisText(stringBuilder.ToString());
        etw.Show();
    }

    private void DeleteAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (Matches is not null && Matches.Count > 1 && !string.IsNullOrEmpty(FindTextBox.Text))
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private async void DeleteAll_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (Matches is null
            || Matches.Count < 1
            || textEditWindow is null)
            return;

        SetWindowToLoading();

        IList selection = ResultsListView.SelectedItems;
        StringBuilder stringBuilderOfText = new(textEditWindow.PassedTextControl.Text);

        await Task.Run(() =>
        {
            if (selection.Count < 2)
                selection = ResultsListView.Items;

            for (int j = selection.Count - 1; j >= 0; j--)
            {
                if (selection[j] is not FindResult selectedResult)
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
        ChangeFindTextTimer.Stop();
        if (textEditWindow is not null)
            StringFromWindow = textEditWindow.PassedTextControl.Text;

        ChangeFindTextTimer.Start();
    }

    private void ExtractPattern_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
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

        string simplePattern = selection.ExtractSimplePattern();

        UsePaternCheckBox.IsChecked = true;
        FindTextBox.Text = simplePattern;

        SearchForText();
    }

    private void FindAndReplacedLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(FindTextBox.Text))
            SearchForText();

        FindTextBox.Focus();
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

    private void MoreOptionsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        Visibility optionsVisibility = Visibility.Collapsed;
        if (MoreOptionsToggleButton.IsChecked is true)
            optionsVisibility = Visibility.Visible;

        SetExtraOptionsVisibility(optionsVisibility);
    }

    private void OptionsChangedRefresh(object sender, RoutedEventArgs e)
    {
        SearchForText();
    }

    private void Replace_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ReplaceTextBox.Text)
            || Matches is null
            || Matches.Count < 1)
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void Replace_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (Matches is null
            || textEditWindow is null
            || ResultsListView.Items.Count is 0)
            return;

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
        if (Matches is null
            || Matches.Count < 1
            || textEditWindow is null)
            return;

        SetWindowToLoading();

        StringBuilder stringBuilder = new(textEditWindow.PassedTextControl.Text);

        IList selection = ResultsListView.SelectedItems;
        string newText = ReplaceTextBox.Text;

        await Task.Run(() =>
        {
            if (selection.Count < 2)
                selection = ResultsListView.Items;

            for (int j = selection.Count - 1; j >= 0; j--)
            {
                if (selection[j] is not FindResult selectedResult)
                    continue;

                stringBuilder.Remove(selectedResult.Index, selectedResult.Length);
                stringBuilder.Insert(selectedResult.Index, newText);
            }
        });

        textEditWindow.PassedTextControl.Text = stringBuilder.ToString();

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
        if (ResultsListView.SelectedItem is not FindResult selectedResult)
            return;

        if (textEditWindow is not null)
        {
            textEditWindow.PassedTextControl.Focus();
            textEditWindow.PassedTextControl.Select(selectedResult.Index, selectedResult.Length);
            this.Focus();
        }
    }

    private void SetExtraOptionsVisibility(Visibility optionsVisibility)
    {
        ReplaceTextBox.Visibility = optionsVisibility;
        ReplaceButton.Visibility = optionsVisibility;
        ReplaceAllButton.Visibility = optionsVisibility;
        MoreOptionsHozStack.Visibility = optionsVisibility;
        EvenMoreOptionsHozStack.Visibility = optionsVisibility;
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

    private void Window_Closed(object? sender, EventArgs e)
    {
        ChangeFindTextTimer.Tick -= ChangeFindText_Tick;
        if (textEditWindow is not null)
            textEditWindow.PassedTextControl.TextChanged -= EditTextBoxChanged;
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

    #endregion Methods
}
