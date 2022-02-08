using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.ApplicationModel;

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

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;
            ErrorCorrectBox.IsChecked = Settings.Default.CorrectErrors;
            NeverUseClipboardChkBx.IsChecked = Settings.Default.NeverAutoUseClipboard;
            RunInBackgroundChkBx.IsChecked = Settings.Default.RunInTheBackground;
            TryInsertCheckbox.IsChecked = Settings.Default.TryInsert;

            if (IsPackaged())
            {
                StartupTask startupTask = await StartupTask.GetAsync("StartTextGrab");

                switch (startupTask.State)
                {
                    case StartupTaskState.Disabled:
                        // Task is disabled but can be enabled.
                        StartupOnLoginCheckBox.IsChecked = false;
                        break;
                    case StartupTaskState.DisabledByUser:
                        // Task is disabled and user must enable it manually.
                        StartupOnLoginCheckBox.IsChecked = false;
                        StartupOnLoginCheckBox.IsEnabled = false;

                        StartupTextBlock.Text += "\nDisabled in Task Manager";
                        break;
                    case StartupTaskState.Enabled:
                        StartupOnLoginCheckBox.IsChecked = true;
                        break;
                }
            }
            else
            {
                StartupOnLoginCheckBox.IsChecked = Settings.Default.StartupOnLogin;
            }

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

        private async void SaveBTN_Click(object sender, RoutedEventArgs e)
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

            if (TryInsertCheckbox.IsChecked != null)
                Settings.Default.TryInsert = (bool)TryInsertCheckbox.IsChecked;

            if (StartupOnLoginCheckBox.IsChecked != null)
            {
                Settings.Default.StartupOnLogin = (bool)StartupOnLoginCheckBox.IsChecked;

                if (Settings.Default.StartupOnLogin == true)
                    await SetForStartup();
                else
                    RemoveFromStartup();
            }


            Settings.Default.Save();
            Close();
        }

        internal static bool IsPackaged()
        {
            try
            {
                // If we have a package ID then we are running in a packaged context
                var dummy = Package.Current.Id;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async void RemoveFromStartup()
        {
            if (IsPackaged())
            {
                StartupTask startupTask = await StartupTask.GetAsync("StartTextGrab");
                Debug.WriteLine("Startup is " + startupTask.State.ToString());

                startupTask.Disable();
            }
            else
            {
                string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, true);
                if (key is not null)
                {
                    try { key.DeleteValue("Text-Grab"); }
                    catch (Exception) { }
                }
            }
        }

        private static async Task SetForStartup()
        {
            if (IsPackaged())
            {
                StartupTask startupTask = await StartupTask.GetAsync("StartTextGrab");
                Debug.WriteLine("Startup is " + startupTask.State.ToString());

                StartupTaskState newState = await startupTask.RequestEnableAsync();
            }
            else
            {
                string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                string? BaseDir = System.IO.Path.GetDirectoryName(System.AppContext.BaseDirectory);
                RegistryKey? key = Registry.CurrentUser.OpenSubKey(path, true);
                if (key is not null
                    && BaseDir is not null)
                {
                    key.SetValue("Text-Grab", $"\"{BaseDir}\\Text-Grab.exe\"");
                }
            }
            await Task.CompletedTask;
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

        private void Window_Closed(object? sender, EventArgs e)
        {
            WindowUtilities.ShouldShutDown();
        }
    }
}
