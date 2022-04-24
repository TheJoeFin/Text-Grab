using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Windows.ApplicationModel;

namespace Text_Grab;

/// <summary>
/// Interaction logic for FirstRunWindow.xaml
/// </summary>
public partial class FirstRunWindow : Window
{
    public FirstRunWindow()
    {
        InitializeComponent();
    }

    private void OkayButton_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current.Windows.Count == 1)
        {
            switch (Settings.Default.DefaultLaunch)
            {
                case "Fullscreen":
                    WindowUtilities.LaunchFullScreenGrab(true);
                    break;
                case "GrabFrame":
                    WindowUtilities.OpenOrActivateWindow<GrabFrame>();
                    break;
                case "EditText":
                    WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
                    break;
                default:
                    break;
            }
        }

        this.Close();
    }

    private async void FirstRun_Loaded(object sender, RoutedEventArgs e)
    {
        // ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;

        switch (Settings.Default.DefaultLaunch)
        {
            case "Fullscreen":
                FullScreenRDBTN.IsChecked = true;
                break;
            case "GrabFrame":
                GrabFrameRDBTN.IsChecked = true;
                break;
            case "EditText":
                EditWindowRDBTN.IsChecked = true;
                break;
            default:
                EditWindowRDBTN.IsChecked = true;
                break;
        }

        if (ImplementAppOptions.IsPackaged())
        {
            StartupTask startupTask = await StartupTask.GetAsync("StartTextGrab");

            switch (startupTask.State)
            {
                case StartupTaskState.Disabled:
                    // Task is disabled but can be enabled.
                    StartupCheckbox.IsChecked = false;
                    break;
                case StartupTaskState.DisabledByUser:
                    // Task is disabled and user must enable it manually.
                    StartupCheckbox.IsChecked = false;
                    StartupCheckbox.IsEnabled = false;

                    StartupTextblock.Text += "\nDisabled in Task Manager";
                    StartupTextblock.Foreground = new SolidColorBrush(Colors.Gray);
                    break;
                case StartupTaskState.Enabled:
                    StartupCheckbox.IsChecked = true;
                    break;
            }
        }
        else
        {
            StartupCheckbox.IsChecked = Settings.Default.StartupOnLogin;
        }

        BackgroundCheckBox.IsChecked = Settings.Default.RunInTheBackground;

        NotificationsCheckBox.IsChecked = Settings.Default.ShowToast;
    }

    private void RadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (this.IsLoaded != true)
            return;

        if (GrabFrameRDBTN.IsChecked != null && (bool)GrabFrameRDBTN.IsChecked)
            Settings.Default.DefaultLaunch = "GrabFrame";
        else if (FullScreenRDBTN.IsChecked != null && (bool)FullScreenRDBTN.IsChecked)
            Settings.Default.DefaultLaunch = "Fullscreen";
        else
            Settings.Default.DefaultLaunch = "EditText";

        Settings.Default.Save();
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<SettingsWindow>();

        this.Close();
    }

    private void TryFullscreen_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab(true);
    }

    private void TryGrabFrame_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<GrabFrame>();
    }

    private void TryEditWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        WindowUtilities.ShouldShutDown();
    }

    private void NotificationsCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.IsChecked is not null)
        {
            Settings.Default.ShowToast = (bool)checkBox.IsChecked;
            Settings.Default.Save();
        }
    }

    private void BackgroundCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.IsChecked is not null)
        {
            Settings.Default.RunInTheBackground = (bool)checkBox.IsChecked;
            ImplementAppOptions.ImplementBackgroundOption(Settings.Default.RunInTheBackground);
            Settings.Default.Save();
        }
    }

    private async void StartupCheckbox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.IsChecked is not null)
        {
            Settings.Default.StartupOnLogin = (bool)checkBox.IsChecked;
            await ImplementAppOptions.ImplementStartupOption(Settings.Default.StartupOnLogin);
            Settings.Default.Save();
        }
    }
}
