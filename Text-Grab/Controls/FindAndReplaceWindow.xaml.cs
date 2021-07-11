using System.Text.RegularExpressions;
using System.Windows;

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
            MatchesResults.Text = string.Empty;

            string pattern = FindTextBox.Text.ToLower();

            MatchCollection matches = Regex.Matches(StringFromWindow.ToLower(), pattern);

            if (matches.Count == 0)
                MatchesResults.Text = "No matches";
            else
                foreach (Match m in matches)
                    MatchesResults.Text += $"{m.Value} at index {m.Index}\n";

            if (matches.Count > 0)
            {
                Match fm = matches[0];

                TextEditWindow.PassedTextControl.Select(fm.Index, fm.Value.Length);
                TextEditWindow.Activate();
                this.Activate();
            }

        }
    }
}
