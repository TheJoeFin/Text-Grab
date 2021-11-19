using System.Diagnostics;
using System.Windows;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;
            ErrorCorrectBox.IsChecked = Settings.Default.CorrectErrors;
            NeverUseClipboardChkBx.IsChecked = Settings.Default.NeverAutoUseClipboard;
            RunInBackgroundChkBx.IsChecked = Settings.Default.RunInTheBackground;

            switch (Settings.Default.DefaultLaunch)
            {
                case "Fullscreen":
                    FullScreenRDBTN.IsChecked = true;
                    break;
                case "GrabFrame":
                    GrabFrameRDBTN.IsChecked = true;
                    break;
                case "EditText":
                    EditTextRDBTN.IsChecked = true;
                    break;
                default:
                    FullScreenRDBTN.IsChecked = true;
                    break;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void CloseBTN_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveBTN_Click(object sender, RoutedEventArgs e)
        {
            if (ShowToastCheckBox.IsChecked != null)
                Settings.Default.ShowToast = (bool)ShowToastCheckBox.IsChecked;

            if (FullScreenRDBTN.IsChecked == true)
                Settings.Default.DefaultLaunch = "Fullscreen";
            else if (GrabFrameRDBTN.IsChecked == true)
                Settings.Default.DefaultLaunch = "GrabFrame";
            else if (EditTextRDBTN.IsChecked == true)
                Settings.Default.DefaultLaunch = "EditText";

            if (ErrorCorrectBox.IsChecked != null)
                Settings.Default.CorrectErrors = (bool)ErrorCorrectBox.IsChecked;

            if (NeverUseClipboardChkBx.IsChecked != null)
                Settings.Default.NeverAutoUseClipboard = (bool)NeverUseClipboardChkBx.IsChecked;

            if (RunInBackgroundChkBx.IsChecked != null)
            {
                Settings.Default.RunInTheBackground = (bool)RunInBackgroundChkBx.IsChecked;

                if ((bool)RunInBackgroundChkBx.IsChecked == true)
                {
                    // Get strongly-typed current application
                    NotifyIconUtilities.SetupNotifyIcon();
                }
                else
                {
                    App app = (App)App.Current;
                    if (app.TextGrabIcon != null)
                        app.TextGrabIcon.Dispose();
                }
            }



            Settings.Default.Save();
            Close();
        }

        private void AboutBTN_Click(object sender, RoutedEventArgs e)
        {
            WindowCollection allWindows = System.Windows.Application.Current.Windows;

            foreach (Window window in allWindows)
            {
                if (window is FirstRunWindow firstRunWindowOpen)
                {
                    firstRunWindowOpen.Activate();
                    return;
                }
            }

            FirstRunWindow frw = new();
            frw.Show();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            WindowUtilities.ShouldShutDown();
        }
    }
}
