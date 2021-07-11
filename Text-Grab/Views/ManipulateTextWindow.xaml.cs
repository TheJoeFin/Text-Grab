using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for ManipulateTextWindow.xaml
    /// </summary>
    public partial class ManipulateTextWindow : Window
    {
        public string CopiedText { get; set; } = "";

        public bool WrapText { get; set; } = false;

        public static RoutedCommand SplitOnSelectionCmd = new RoutedCommand();

        public static RoutedCommand IsolateSelectionCmd = new RoutedCommand();

        public ManipulateTextWindow()
        {
            InitializeComponent();

            string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;
            XmlLanguage lang = XmlLanguage.GetLanguage(inputLang);
            CultureInfo culture = lang.GetEquivalentCulture();
            if (culture.TextInfo.IsRightToLeft)
            {
                PassedTextControl.TextAlignment = TextAlignment.Right;
            }
        }

        public ManipulateTextWindow(string rawPassedString)
        {
            int lastCommaPosition = rawPassedString.AllIndexesOf(",").LastOrDefault();
            CopiedText = rawPassedString.Substring(0, lastCommaPosition);
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RoutedCommand newFullscreenGrab = new RoutedCommand();
            _ = newFullscreenGrab.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control));
            _ = CommandBindings.Add(new CommandBinding(newFullscreenGrab, keyedCtrlF));

            RoutedCommand newGrabFrame = new RoutedCommand();
            _ = newGrabFrame.InputGestures.Add(new KeyGesture(Key.G, ModifierKeys.Control));
            _ = CommandBindings.Add(new CommandBinding(newGrabFrame, keyedCtrlG));

            PassedTextControl.Focus();
        }

        private void keyedCtrlF(object sender, ExecutedRoutedEventArgs e)
        {
            WindowUtilities.NormalLaunch(true);
        }

        private void keyedCtrlG(object sender, ExecutedRoutedEventArgs e)
        {
            CheckForGrabFrameOrLaunch();
        }

        private void CopyCloseBTN_Click(object sender, RoutedEventArgs e)
        {
            string clipboardText = PassedTextControl.Text;
            System.Windows.Clipboard.SetText(clipboardText);
            this.Close();
        }

        private void SaveBTN_Click(object sender, RoutedEventArgs e)
        {
            string fileText = PassedTextControl.Text;

            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog()
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
            if (IsLoaded == false)
                return;

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
                if (string.IsNullOrWhiteSpace(line) == false)
                    finalString += line.Trim() + "\n";
            }

            PassedTextControl.Text = finalString;
        }

        public void AddThisText(string textToAdd)
        {
            if (string.IsNullOrWhiteSpace(PassedTextControl.Text) == false)
                textToAdd = "\n" + textToAdd;

            PassedTextControl.Text += textToAdd;

            PassedTextControl.ScrollToEnd();
        }

        private void TryToNumberMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string workingString = PassedTextControl.Text;

            workingString = workingString.TryFixToNumbers();

            PassedTextControl.Text = workingString;
        }
        private void TryToAlphaMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string workingString = PassedTextControl.Text;

            workingString = workingString.TryFixToLetters();

            PassedTextControl.Text = workingString;
        }

        private void ClearSeachBTN_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox searchBox = sender as System.Windows.Controls.TextBox;
            searchBox.Text = "";
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PassedTextControl.SelectedText = SearchTextBox.Text;
        }

        private void SplitOnSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            string selectedText = PassedTextControl.SelectedText;

            if (string.IsNullOrEmpty(selectedText))
            {
                System.Windows.MessageBox.Show("No text selected", "Did not split lines");
                return;
            }

            string textToManipulate = PassedTextControl.Text;

            textToManipulate = textToManipulate.Replace(selectedText, "\n" + selectedText);

            PassedTextControl.Text = textToManipulate;
        }

        private void SplitOnSelectionCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PassedTextControl.SelectedText))
                e.CanExecute = false;
            else
                e.CanExecute = true;
        }

        private void IsolateSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            PassedTextControl.Text = PassedTextControl.SelectedText;
        }

        private void IsolateSelectionCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PassedTextControl.SelectedText))
                e.CanExecute = false;
            else
                e.CanExecute = true;
        }

        private void CheckForGrabFrameOrLaunch()
        {
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            foreach (Window window in allWindows)
            {
                if (window is GrabFrame grabFrame)
                {
                    grabFrame.Activate();
                    grabFrame.IsfromEditWindow = true;
                    return;
                }
            }

            GrabFrame gf = new GrabFrame();
            gf.IsfromEditWindow = true;
            gf.Show();
        }

        private void OpenGrabFrame_Click(object sender, RoutedEventArgs e)
        {
            CheckForGrabFrameOrLaunch();
        }

        private void NewFullscreen_Click(object sender, RoutedEventArgs e)
        {
            WindowUtilities.NormalLaunch(true);
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            foreach (Window window in allWindows)
            {
                if (window is SettingsWindow sw)
                {
                    sw.Activate();
                    return;
                }
            }

            SettingsWindow nsw = new SettingsWindow();
            nsw.Show();
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AlwaysOnTop_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded == false)
                return;

            if (Topmost == false)
                Topmost = true;
            else
                Topmost = false;
        }

        private void HideBottomBarMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (BottomBar.Visibility == Visibility.Visible)
                BottomBar.Visibility = Visibility.Collapsed;
            else
                BottomBar.Visibility = Visibility.Visible;
        }

        private void FeedbackMenuItem_Click(object sender, RoutedEventArgs ev)
        {
            Uri source = new Uri("https://github.com/TheJoeFin/Text-Grab/issues", UriKind.Absolute);
            RequestNavigateEventArgs e = new RequestNavigateEventArgs(source, "https://github.com/TheJoeFin/Text-Grab/issues");
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void FontMenuItem_Click(object sender, RoutedEventArgs e)
        {
            FontDialog fd = new FontDialog();
            var result = fd.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Debug.WriteLine(fd.Font);

                PassedTextControl.FontFamily = new FontFamily(fd.Font.Name);
                PassedTextControl.FontSize = fd.Font.Size * 96.0 / 72.0;
                PassedTextControl.FontWeight = fd.Font.Bold ? FontWeights.Bold : FontWeights.Regular;
                PassedTextControl.FontStyle = fd.Font.Italic ? FontStyles.Italic : FontStyles.Normal;

                TextDecorationCollection tdc = new TextDecorationCollection();
                if (fd.Font.Underline) tdc.Add(TextDecorations.Underline);
                if (fd.Font.Strikeout) tdc.Add(TextDecorations.Strikethrough);
                PassedTextControl.TextDecorations = tdc;
            }
        }
    }
}
