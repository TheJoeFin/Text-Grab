using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for ManipulateTextWindow.xaml
    /// </summary>
    public partial class ManipulateTextWindow : Window
    {
        public string CopiedText { get; set; } = "";

        public bool WrapText { get; set; } = false;

        public ManipulateTextWindow()
        {
            InitializeComponent();
        }

        public ManipulateTextWindow(string rawPassedString)
        {
            int lastCommaPosition = rawPassedString.AllIndexesOf(",").LastOrDefault();            
            CopiedText = rawPassedString.Substring(0,lastCommaPosition);
            InitializeComponent();
            PassedTextControl.Text = CopiedText;
            string langString = rawPassedString.Substring(lastCommaPosition + 1, (rawPassedString.Length - (lastCommaPosition + 1)));
            XmlLanguage lang = XmlLanguage.GetLanguage(langString);
            CultureInfo culture = lang.GetEquivalentCulture();
            if (culture.TextInfo.IsRightToLeft)
            {
                PassedTextControl.TextAlignment = TextAlignment.Right;
            }
        }

        private void CopyCloseBTN_Click(object sender, RoutedEventArgs e)
        {
            string clipboardText = PassedTextControl.Text;
            Clipboard.SetText(clipboardText);
            this.Close();
        }

        private void SaveBTN_Click(object sender, RoutedEventArgs e)
        {
            string fileText = PassedTextControl.Text;

            SaveFileDialog dialog = new SaveFileDialog()
            {
                Filter = "Text Files(*.txt)|*.txt|All(*.*)|*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, fileText);
            }
        }

        private void SingleLineBTN_Click(object sender, RoutedEventArgs e)
        {
            string textToEdit = PassedTextControl.Text;
            PassedTextControl.Text = "";
            textToEdit = textToEdit.Replace('\n', ' ');
            textToEdit = textToEdit.Replace('\r', ' ');
            textToEdit = textToEdit.Replace(Environment.NewLine, " ");
            Regex regex = new Regex("[ ]{2,}");
            textToEdit = regex.Replace(textToEdit, " ");
            textToEdit = textToEdit.Trim();
            PassedTextControl.Text = textToEdit;
        }

        private void WrapTextCHBOX_Checked(object sender, RoutedEventArgs e)
        {
            if ((bool)WrapTextMenuItem.IsChecked)
                PassedTextControl.TextWrapping = TextWrapping.Wrap;
            else
                PassedTextControl.TextWrapping = TextWrapping.NoWrap;
        }

        private void TrimEachLineMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string workingString = PassedTextControl.Text;
            List<string> stringSplit = workingString.Split('\n').ToList();

            string finalString = "";
            foreach (string line in stringSplit)
            {
                if(string.IsNullOrWhiteSpace(line) == false)
                    finalString += line.Trim() + "\n";
            }

            PassedTextControl.Text = finalString;
        }

        private void TryToNumberMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string workingString = PassedTextControl.Text;

            workingString = workingString.Replace('o', '0');
            workingString = workingString.Replace('O', '0');
            workingString = workingString.Replace('g', '9');
            workingString = workingString.Replace('i', '1');
            workingString = workingString.Replace('l', '1');
            workingString = workingString.Replace('Q', '0');

            PassedTextControl.Text = workingString;
        }
        private void TryToAlphaMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string workingString = PassedTextControl.Text;

            workingString = workingString.Replace('0', 'o');
            workingString = workingString.Replace('4', 'h');
            workingString = workingString.Replace('9', 'g');
            workingString = workingString.Replace('1', 'l');

            PassedTextControl.Text = workingString;
        }

        private void ClearSeachBTN_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = sender as TextBox;
            searchBox.Text = "";
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PassedTextControl.SelectedText = SearchTextBox.Text;
        }

        private void SplitLineBeforeSelectionMI_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RejoinLinesAtSelectionMI_Click(object sender, RoutedEventArgs e)
        {

        }

        private void AddTextBTN_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
