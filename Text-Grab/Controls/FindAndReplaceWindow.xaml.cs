using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Text_Grab.Utilities;

namespace Text_Grab.Controls
{
    /// <summary>
    /// Interaction logic for FindAndReplaceWindow.xaml
    /// </summary>
    public partial class FindAndReplaceWindow : Window
    {
        public string StringFromWindow { get; set; } = "";

        public EditTextWindow? TextEditWindow = null;

        public static RoutedCommand TextSearchCmd = new();
        public static RoutedCommand ReplaceOneCmd = new();
        public static RoutedCommand ReplaceAllCmd = new();
        public static RoutedCommand ExtractPatternCmd = new();
        DispatcherTimer ChangeFindTextTimer = new();

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
            ChangeFindTextTimer.Tick -= ChangeFindText_Tick;
        }

        private void ChangeFindText_Tick(object? sender, EventArgs? e)
        {
            ChangeFindTextTimer.Stop();
            SearchForText();
        }

        private void FindAndReplacedLoaded(object sender, RoutedEventArgs e)
        {
            if (TextEditWindow != null)
                TextEditWindow.PassedTextControl.TextChanged += EditTextBoxChanged;

            if (string.IsNullOrWhiteSpace(FindTextBox.Text) == false)
                SearchForText();
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
            ResultsListView.Items.Clear();

            Pattern = FindTextBox.Text;

            if (UsePaternCheckBox.IsChecked == false)
            {
                Pattern = Pattern.EscapeSpecialRegexChars();
            }

            try
            {
                if (ExactMatchCheckBox.IsChecked == true)
                    Matches = Regex.Matches(StringFromWindow.ToLower(), Pattern, RegexOptions.Multiline);
                else
                    Matches = Regex.Matches(StringFromWindow.ToLower(), Pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

            }
            catch (Exception ex)
            {
                MatchesText.Text = "Error searching: " + ex.GetType().ToString();
                return;
            }

            if (Matches.Count == 0 || string.IsNullOrWhiteSpace(FindTextBox.Text))
            {
                MatchesText.Text = "0 Matches";
                ResultsListView.Items.Add("No Matches");
                ResultsListView.IsEnabled = false;
            }
            else
            {
                MatchesText.Text = $"{Matches.Count} Matches";
                ResultsListView.IsEnabled = true;
                int count = 1;
                foreach (Match m in Matches)
                {
                    int previewLengths = 16;
                    int previewBeginning = 0;
                    int previewEnd = 0;
                    bool atBeginning = false;
                    bool atEnd = false;

                    if (m.Index - previewLengths < 0)
                    {
                        atBeginning = true;
                        previewBeginning = 0;
                    }
                    else
                        previewBeginning = m.Index - previewLengths;

                    if (m.Index + previewLengths > StringFromWindow.Length)
                    {
                        atEnd = true;
                        previewEnd = StringFromWindow.Length;
                    }
                    else
                        previewEnd = m.Index + previewLengths;

                    StringBuilder previewString = new();

                    if (atBeginning == false)
                        previewString.Append("...");

                    previewString.Append(StringFromWindow.Substring(previewBeginning, previewEnd - previewBeginning).MakeStringSingleLine());

                    if (atEnd == false)
                        previewString.Append("...");

                    ResultsListView.Items.Add($"{count} \t At index {m.Index} \t\t {previewString.ToString().MakeStringSingleLine()}");
                    count++;
                }
            }

            if (Matches.Count > 0)
            {
                Match? fm = Matches[0];

                if (TextEditWindow != null && fm != null)
                {
                    TextEditWindow.PassedTextControl.Select(fm.Index, fm.Value.Length);
                    TextEditWindow.PassedTextControl.Focus();
                    this.Focus();
                }
            }
        }

        private void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? stringToParse = ResultsListView.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(stringToParse)
                || Matches is null
                || Matches.Count < 1)
            {
                ResultsListView.Items.Clear();
                return;
            }

            int selectedResultIndex = ResultsListView.SelectedIndex;
            Match sameMatch = Matches[selectedResultIndex];

            if (TextEditWindow != null)
            {
                TextEditWindow.PassedTextControl.Select(sameMatch.Index, sameMatch.Length);
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
            if (MoreOptionsToggleButton.IsChecked == true)
                optionsVisibility = Visibility.Visible;

            ReplaceTextBox.Visibility = optionsVisibility;
            ReplaceButton.Visibility = optionsVisibility;
            ReplaceAllButton.Visibility = optionsVisibility;
            MoreOptionsHozStack.Visibility = optionsVisibility;
        }

        private void ReplaceAll_Executed(object sender, ExecutedRoutedEventArgs e)
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
                        TextEditWindow.PassedTextControl.SelectedText = ReplaceTextBox.Text;
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
                            TextEditWindow.PassedTextControl.SelectedText = ReplaceTextBox.Text;
                        }
                    }
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
                if (string.IsNullOrWhiteSpace(FindTextBox.Text) == false)
                    FindTextBox.Clear();
                else
                    this.Close();
            }
        }
    }
}
