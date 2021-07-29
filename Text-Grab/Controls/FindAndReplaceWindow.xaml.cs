using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
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

        private string Pattern { get; set; }

        private int MatchLength { get; set; }

        public FindAndReplaceWindow()
        {
            InitializeComponent();
        }

        private void FindAndReplacedLoaded(object sender, RoutedEventArgs e)
        {
            if (TextEditWindow != null)
                TextEditWindow.PassedTextControl.TextChanged += EditTextBoxChanged;
        }

        private void EditTextBoxChanged(object sender, TextChangedEventArgs e)
        {
            StringFromWindow = TextEditWindow.PassedTextControl.Text;
        }

        private void FindTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ResultsListView.Items.Clear();

            Pattern = FindTextBox.Text.ToLower();

            MatchCollection matches = null;
            try
            {
                matches = Regex.Matches(StringFromWindow.ToLower(), Pattern);
            }
            catch (Exception)
            {

                return;
            }

            if (matches.Count == 0 || string.IsNullOrWhiteSpace(FindTextBox.Text))
            {
                MatchesText.Text = "0 Matches";
                ResultsListView.Items.Add("No Matches");
                ResultsListView.IsEnabled = false;
            }
            else
            {
                MatchesText.Text = $"{matches.Count} Matches";
                ResultsListView.IsEnabled = true;
                foreach (Match m in matches)
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

                    string previewString = "";

                    if (atBeginning == false)
                        previewString += "...";

                    previewString += StringFromWindow.Substring(previewBeginning, previewEnd - previewBeginning).MakeStringSingleLine();

                    if (atEnd == false)
                        previewString += "...";

                    ResultsListView.Items.Add($"At index {m.Index} \t\t {previewString}");
                }
            }

            if (matches.Count > 0)
            {
                Match fm = matches[0];

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

            stringToParse = stringToParse.Split('\t').FirstOrDefault();

            int.TryParse(stringToParse.Substring(8), out int selectionStartIndex);

            TextEditWindow.PassedTextControl.Select(selectionStartIndex, MatchLength);
            TextEditWindow.PassedTextControl.Focus();
            this.Focus();
        }

        private void ExtractSimplePattern_Click(object sender, RoutedEventArgs e)
        {
            string selection = TextEditWindow.PassedTextControl.SelectedText;

            string simplePattern = selection.ExtractSimplePattern();

            FindTextBox.Text = simplePattern;
        }
    }
}
