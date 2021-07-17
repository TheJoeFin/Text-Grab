using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Controls
{
    /// <summary>
    /// Interaction logic for FindAndReplaceWindow.xaml
    /// </summary>
    public partial class FindAndReplaceWindow : Window
    {
        public string StringFromWindow { get; set; } = "";

        public ManipulateTextWindow TextEditWindow = null;

        public FindAndReplaceWindow()
        {
            InitializeComponent();
        }

        private void FindTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ResultsListView.Items.Clear();

            string pattern = FindTextBox.Text.ToLower();

            MatchCollection matches = Regex.Matches(StringFromWindow.ToLower(), pattern);

            if (matches.Count == 0 || string.IsNullOrWhiteSpace(FindTextBox.Text))
            {
                ResultsListView.Items.Add("No Matches");
                ResultsListView.IsEnabled = false;
            }
            else
            {
                ResultsListView.IsEnabled = true;
                foreach (Match m in matches)
                {
                    int previewLengths = 16;
                    int previewBeginning =  0;
                    int previewEnd =  0;
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

            TextEditWindow.PassedTextControl.Select(selectionStartIndex, FindTextBox.Text.Length);
            TextEditWindow.PassedTextControl.Focus();
            this.Focus();
        }
    }
}
