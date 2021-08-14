using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Text_Grab.Utilities;

namespace Text_Grab.Controls
{
    /// <summary>
    /// Interaction logic for FindAndReplaceWindow.xaml
    /// </summary>
    public partial class FindAndReplaceWindow : Window
    {
        public string StringFromWindow { get; set; } = "";

        public EditTextWindow TextEditWindow = null;

        public static RoutedCommand TextSearchCmd = new RoutedCommand();
        public static RoutedCommand ReplaceOneCmd = new RoutedCommand();
        public static RoutedCommand ReplaceAllCmd = new RoutedCommand();

        private string Pattern { get; set; }

        private int MatchLength { get; set; }

        private MatchCollection Matches;

        public FindAndReplaceWindow()
        {
            InitializeComponent();
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
            StringFromWindow = TextEditWindow.PassedTextControl.Text;
        }

        public void SearchForText()
        {
            ResultsListView.Items.Clear();

            Pattern = FindTextBox.Text;

            if (UsePaternCheckBox.IsChecked == false)
            {
                Pattern = Pattern.EscapeSpecialRegexChars();
            }

            Matches = null;
            try
            {
                if (ExactMatchCheckBox.IsChecked == true)
                    Matches = Regex.Matches(StringFromWindow.ToLower(), Pattern, RegexOptions.Multiline );
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
                foreach (Match m in Matches)
                {
                    MatchLength = m.Length;
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

                    StringBuilder previewString = new StringBuilder();

                    if (atBeginning == false)
                        previewString.Append("...");

                    previewString.Append(StringFromWindow.Substring(previewBeginning, previewEnd - previewBeginning).MakeStringSingleLine());

                    if (atEnd == false)
                        previewString.Append("...");

                    ResultsListView.Items.Add($"At index {m.Index} \t\t {previewString.ToString().MakeStringSingleLine()}");
                }
            }

            if (Matches.Count > 0)
            {
                Match fm = Matches[0];

                TextEditWindow.PassedTextControl.Select(fm.Index, fm.Value.Length);
                TextEditWindow.PassedTextControl.Focus();
                this.Focus();
            }
        }

        private void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string stringToParse = ResultsListView.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(stringToParse))
            {
                ResultsListView.Items.Clear();
                return;
            }
            
            int selectedResultIndex = ResultsListView.SelectedIndex;
            Match sameMatch = Matches[selectedResultIndex];

            TextEditWindow.PassedTextControl.Select(sameMatch.Index, sameMatch.Length);
            TextEditWindow.PassedTextControl.Focus();
            this.Focus();
        }

        private void ExtractSimplePattern_Click(object sender, RoutedEventArgs e)
        {
            string selection = TextEditWindow.PassedTextControl.SelectedText;

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

        }

        private void Replace_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ReplaceTextBox.Text)
                || Matches.Count < 1)
                e.CanExecute = false;
            else
                e.CanExecute = true;
        }

        private void Replace_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int selectedResultIndex = ResultsListView.SelectedIndex;
            Match sameMatch = Matches[selectedResultIndex];
            TextEditWindow.PassedTextControl.Select(sameMatch.Index, sameMatch.Length);
            TextEditWindow.PassedTextControl.SelectedText = ReplaceTextBox.Text;

            // TODO there is a bug here where clicking replace again will mess up the text
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
            if (e.Key == Key.Enter)
                SearchForText();
        }

        private void OptionsChangedRefresh(object sender, RoutedEventArgs e)
        {
            SearchForText();
        }
    }
}
