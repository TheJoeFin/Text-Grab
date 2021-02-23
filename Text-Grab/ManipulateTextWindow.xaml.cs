using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for ManipulateTextWindow.xaml
    /// </summary>
    public partial class ManipulateTextWindow : Window
    {
        public string CopiedText { get; set; } = "";

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
    }
}
