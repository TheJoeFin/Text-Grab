using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Navigation;
using Text_Grab.Controls;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for ManipulateTextWindow.xaml
    /// </summary>

    public partial class EditTextWindow : Window
    {
        public string CopiedText { get; set; } = "";

        public CurrentCase CaseStatusOfToggle { get; set; } = CurrentCase.Lower;

        public bool WrapText { get; set; } = false;

        public static RoutedCommand SplitOnSelectionCmd = new RoutedCommand();

        public static RoutedCommand IsolateSelectionCmd = new RoutedCommand();

        public static RoutedCommand SingleLineCmd = new RoutedCommand();

        public EditTextWindow()
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

        public EditTextWindow(string rawPassedString)
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

            RoutedCommand selectLineCommand = new RoutedCommand();
            _ = selectLineCommand.InputGestures.Add(new KeyGesture(Key.L, ModifierKeys.Control));
            _ = CommandBindings.Add(new CommandBinding(selectLineCommand, SelectLine));

            RoutedCommand moveLineUpCommand = new RoutedCommand();
            _ = moveLineUpCommand.InputGestures.Add(new KeyGesture(Key.Up, ModifierKeys.Alt));
            _ = CommandBindings.Add(new CommandBinding(moveLineUpCommand, MoveLineUp));

            RoutedCommand moveLineDownCommand = new RoutedCommand();
            _ = moveLineDownCommand.InputGestures.Add(new KeyGesture(Key.Down, ModifierKeys.Alt));
            _ = CommandBindings.Add(new CommandBinding(moveLineDownCommand, MoveLineDown));

            RoutedCommand toggleCaseCommand = new RoutedCommand();
            _ = toggleCaseCommand.InputGestures.Add(new KeyGesture(Key.F3, ModifierKeys.Shift));
            _ = CommandBindings.Add(new CommandBinding(toggleCaseCommand, ToggleCase));

            SetFontFromSettings();

            if (Settings.Default.EditWindowStartFullscreen)
            {
                WindowUtilities.LaunchFullScreenGrab(true);
                LaunchFullscreenOnLoad.IsChecked = true;
                WindowState = WindowState.Minimized;
            }
        }

        private void SetFontFromSettings()
        {
            PassedTextControl.FontFamily = new System.Windows.Media.FontFamily(Settings.Default.FontFamilySetting);
            PassedTextControl.FontSize = Settings.Default.FontSizeSetting;
            if (Settings.Default.IsFontBold == true)
                PassedTextControl.FontWeight = FontWeights.Bold;
            if (Settings.Default.IsFontItalic == true)
                PassedTextControl.FontStyle = FontStyles.Italic;

            TextDecorationCollection tdc = new TextDecorationCollection();
            if (Settings.Default.IsFontUnderline) tdc.Add(TextDecorations.Underline);
            if (Settings.Default.IsFontStrikeout) tdc.Add(TextDecorations.Strikethrough);
            PassedTextControl.TextDecorations = tdc;

        }

        private void PassedTextControl_TextChanged(object sender, TextChangedEventArgs e)
        {
            WindowState = WindowState.Normal;
            PassedTextControl.Focus();
        }

        private void ToggleCase(object sender, ExecutedRoutedEventArgs e)
        {
            switch (CaseStatusOfToggle)
            {
                case CurrentCase.Lower:
                    PassedTextControl.SelectedText = PassedTextControl.SelectedText.ToLower();
                    CaseStatusOfToggle = CurrentCase.Camel;
                    break;
                case CurrentCase.Camel:
                    PassedTextControl.SelectedText = PassedTextControl.SelectedText.ToCamel();
                    CaseStatusOfToggle = CurrentCase.Upper;
                    break;
                case CurrentCase.Upper:
                    PassedTextControl.SelectedText = PassedTextControl.SelectedText.ToUpper();
                    CaseStatusOfToggle = CurrentCase.Lower;
                    break;
                default:
                    break;
            }
        }

        private void MoveLineDown(object sender, ExecutedRoutedEventArgs e)
        {
            SelectLine(sender, e);

            string lineText = PassedTextControl.SelectedText;
            PassedTextControl.SelectedText = "";
            string textBoxText = PassedTextControl.Text;
            int selectionIndex = PassedTextControl.SelectionStart;
            int indexOfNextNewline = textBoxText.Length;

            bool foundNewLine = false;

            for (int j = selectionIndex; j < textBoxText.Length; j++)
            {
                char charToCheck = textBoxText[j];
                if (charToCheck == '\n'
                    || charToCheck == '\r')
                {
                    foundNewLine = true;
                    indexOfNextNewline = j;
                }
                else
                {
                    if (foundNewLine == true)
                    {
                        break;
                    }
                }
            }

            PassedTextControl.Select(indexOfNextNewline, 0);
            PassedTextControl.SelectedText = lineText;
        }

        private void MoveLineUp(object sender, ExecutedRoutedEventArgs e)
        {
            SelectLine(sender, e);
            string lineText = PassedTextControl.SelectedText;
            PassedTextControl.SelectedText = "";
            string textBoxText = PassedTextControl.Text;
            int selectionIndex = PassedTextControl.SelectionStart;
            int indexOfPreviousNewline = 0;

            bool foundThroughNewLines = false;

            for (int i = selectionIndex - 2; i >= 0; i--)
            {
                char charToCheck = textBoxText[i];
                if (charToCheck == '\n'
                    || charToCheck == '\r')
                {
                    indexOfPreviousNewline = i;

                    if (foundThroughNewLines)
                        break;
                }
                else
                {
                    foundThroughNewLines = true;
                }

                if (i == 0)
                    indexOfPreviousNewline = i;
            }

            PassedTextControl.Select(indexOfPreviousNewline, 0);
            PassedTextControl.SelectedText = lineText;
        }

        private void keyedCtrlF(object sender, ExecutedRoutedEventArgs e)
        {
            WindowUtilities.LaunchFullScreenGrab(true);
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

        private void SingleLineCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (PassedTextControl.SelectedText.Length > 0)
                PassedTextControl.SelectedText = PassedTextControl.SelectedText.MakeStringSingleLine();
            else
                PassedTextControl.Text = PassedTextControl.Text.MakeStringSingleLine();
        }

        private void SingleLineCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            string textToOperateOn;

            if (PassedTextControl.SelectedText.Length > 0)
                textToOperateOn = PassedTextControl.SelectedText;
            else
                textToOperateOn = PassedTextControl.Text;

            int n = 0;
            foreach (var c in textToOperateOn)
            {
                if (c == '\n' || c == '\r')
                    n++;
            }

            if (n < 2)
                e.CanExecute = false;
            else
                e.CanExecute = true;
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
            List<string> stringSplit = workingString.Split(Environment.NewLine).ToList();

            string finalString = "";
            foreach (string line in stringSplit)
            {
                if (string.IsNullOrWhiteSpace(line) == false)
                    finalString += line.Trim() + Environment.NewLine;
            }

            PassedTextControl.Text = finalString;
        }

        public void AddThisText(string textToAdd)
        {
            PassedTextControl.SelectedText = textToAdd;
        }

        private void TryToNumberMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string workingString = string.Empty;

            if (PassedTextControl.SelectionLength == 0)
                workingString = PassedTextControl.Text;
            else
                workingString = PassedTextControl.SelectedText;

            workingString = workingString.TryFixToNumbers();

            if (PassedTextControl.SelectionLength == 0)
                PassedTextControl.Text = workingString;
            else
                PassedTextControl.SelectedText = workingString;
        }
        private void TryToAlphaMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string workingString = string.Empty;

            if (PassedTextControl.SelectionLength == 0)
                workingString = PassedTextControl.Text;
            else
                workingString = PassedTextControl.SelectedText;

            workingString = workingString.TryFixToLetters();

            if (PassedTextControl.SelectionLength == 0)
                PassedTextControl.Text = workingString;
            else
                PassedTextControl.SelectedText = workingString;
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

            textToManipulate = textToManipulate.Replace(selectedText, Environment.NewLine + selectedText);

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
            WindowUtilities.LaunchFullScreenGrab(true);
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
            Font currentFont = new Font(PassedTextControl.FontFamily.ToString(), (float)((PassedTextControl.FontSize * 72.0) / 96.0));
            fd.Font = currentFont;
            var result = fd.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Debug.WriteLine(fd.Font);

                Settings.Default.FontFamilySetting = fd.Font.Name;
                Settings.Default.FontSizeSetting = (fd.Font.Size * 96.0 / 72.0);
                Settings.Default.IsFontBold = fd.Font.Bold;
                Settings.Default.IsFontItalic = fd.Font.Italic;
                Settings.Default.IsFontUnderline = fd.Font.Underline;
                Settings.Default.IsFontStrikeout = fd.Font.Strikeout;
                Settings.Default.Save();

                SetFontFromSettings();
            }
        }

        private void SelectLine(object sender, ExecutedRoutedEventArgs e)
        {
            int selectionIndex = PassedTextControl.SelectionStart;
            int selectionEndIndex = PassedTextControl.SelectionStart + PassedTextControl.SelectionLength - (Environment.NewLine.Length);
            if (selectionEndIndex < selectionIndex)
                selectionEndIndex = selectionIndex;
            int selectionLength = PassedTextControl.SelectionLength;
            string textBoxText = PassedTextControl.Text;

            IEnumerable<int> allNewLines = textBoxText.AllIndexesOf(Environment.NewLine);

            int startSelectionIndex = 0;
            int stopSelectionIndex = 0;

            foreach (int newLineIndex in allNewLines)
            {
                if (PassedTextControl.SelectionStart > newLineIndex)
                    startSelectionIndex = newLineIndex;

                if (newLineIndex >= selectionEndIndex)
                {
                    stopSelectionIndex = newLineIndex;
                    break;
                }
            }

            if (startSelectionIndex == 0)
                selectionIndex = startSelectionIndex;
            else
                selectionIndex = startSelectionIndex + Environment.NewLine.Length;

            selectionLength = (stopSelectionIndex) - selectionIndex + Environment.NewLine.Length;
            if (selectionLength < 0)
                selectionLength = 0;

            PassedTextControl.Select(selectionIndex, selectionLength);
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            FindAndReplaceWindow farw = new FindAndReplaceWindow();
            farw.StringFromWindow = PassedTextControl.Text;
            farw.TextEditWindow = this;
            farw.Show();

            if (PassedTextControl.SelectedText.Length > 0)
            {
                farw.FindTextBox.Text = PassedTextControl.SelectedText;
            }
        }

        private void LaunchFullscreenOnLoad_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.EditWindowStartFullscreen = LaunchFullscreenOnLoad.IsChecked;
            Settings.Default.Save();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            PassedTextControl.Focus();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            foreach (Window window in allWindows)
            {
                if (window is GrabFrame grabFrame)
                {
                    grabFrame.IsfromEditWindow = false;
                }
                if (window is FullscreenGrab fullscreenGrab)
                {
                    fullscreenGrab.IsFromEditWindow = false;
                }
                if (window is FindAndReplaceWindow findAndReplaceWindow)
                {
                    findAndReplaceWindow.Close();
                }

            }
        }
    }
}
