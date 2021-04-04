using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
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
            if ((bool)WrapTextCHBOX.IsChecked)
                PassedTextControl.TextWrapping = TextWrapping.Wrap;
            else
                PassedTextControl.TextWrapping = TextWrapping.NoWrap;
        }
    }
}
