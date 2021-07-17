using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Models;

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

            if (matches.Count == 0)
            {
                ResultsListView.Items.Add("No Matches");
                ResultsListView.IsEnabled = false;
            }
            else
            {
                ResultsListView.IsEnabled = true;
                foreach (Match m in matches)
                    ResultsListView.Items.Add($"At index {m.Index}");
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
                return;

            int.TryParse(stringToParse.Substring(8), out int selectionStartIndex);

            TextEditWindow.PassedTextControl.Select(selectionStartIndex, FindTextBox.Text.Length);
            TextEditWindow.PassedTextControl.Focus();
            this.Focus();
        }
    }
}
