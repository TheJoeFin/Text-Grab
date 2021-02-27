using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
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
            var splitInput = rawPassedString.Split(',');
            
            CopiedText = splitInput[0];
            InitializeComponent();
            PassedTextControl.Text = CopiedText;
            XmlLanguage lang = XmlLanguage.GetLanguage(splitInput[1]);
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
