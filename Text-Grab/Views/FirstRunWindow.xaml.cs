using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Windows.ApplicationModel;
using Wpf.Ui.Controls;

namespace Text_Grab;

/// <summary>
/// Interaction logic for FirstRunWindow.xaml
/// </summary>
public partial class FirstRunWindow : FluentWindow
{
    #region Constructors

    public FirstRunWindow()
    {
        InitializeComponent();
        App.SetTheme();
    }

    #endregion Constructors

    #region Methods

    private void BackgroundCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.IsChecked is not null)
        {
            Settings.Default.RunInTheBackground = (bool)checkBox.IsChecked;
            ImplementAppOptions.ImplementBackgroundOption(Settings.Default.RunInTheBackground);
            Settings.Default.Save();
        }
    }

    private async void FirstRun_Loaded(object sender, RoutedEventArgs e)
    {
        TextGrabMode defaultLaunchSetting = Enum.Parse<TextGrabMode>(Settings.Default.DefaultLaunch, true);
        switch (defaultLaunchSetting)
        {
            case TextGrabMode.Fullscreen:
                FullScreenRDBTN.IsChecked = true;
                break;
            case TextGrabMode.GrabFrame:
                GrabFrameRDBTN.IsChecked = true;
                break;
            case TextGrabMode.EditText:
                EditWindowRDBTN.IsChecked = true;
                break;
            case TextGrabMode.QuickLookup:
                QuickLookupRDBTN.IsChecked = true;
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

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void NotificationsCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.IsChecked is not null)
        {
            Settings.Default.ShowToast = (bool)checkBox.IsChecked;
            Settings.Default.Save();
        }
    }

    private void OkayButton_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current.Windows.Count == 1)
        {
            TextGrabMode defaultLaunchSetting = Enum.Parse<TextGrabMode>(Settings.Default.DefaultLaunch, true);
            switch (defaultLaunchSetting)
            {
                case TextGrabMode.Fullscreen:
                    WindowUtilities.LaunchFullScreenGrab();
                    break;
                case TextGrabMode.GrabFrame:
                    WindowUtilities.OpenOrActivateWindow<GrabFrame>();
                    break;
                case TextGrabMode.EditText:
                    WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
                    break;
                case TextGrabMode.QuickLookup:
                    WindowUtilities.OpenOrActivateWindow<QuickSimpleLookup>();
                    break;
                default:
                    break;
            }
        }

        this.Close();
    }
    private void RadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (this.IsLoaded != true)
            return;

        if (GrabFrameRDBTN.IsChecked is bool gfOn && gfOn)
            Settings.Default.DefaultLaunch = "GrabFrame";
        else if (FullScreenRDBTN.IsChecked is bool fsgOn && fsgOn)
            Settings.Default.DefaultLaunch = "Fullscreen";
        else if (QuickLookupRDBTN.IsChecked is bool qslOn && qslOn)
            Settings.Default.DefaultLaunch = "QuickLookup";
        else
            Settings.Default.DefaultLaunch = "EditText";

        Settings.Default.Save();
    }
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<SettingsWindow>();

        this.Close();
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

    private void TryEditWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
    }

    private void TryFullscreen_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab();
    }

    private void TryGrabFrame_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<GrabFrame>();
    }
    private void TryQuickLookup_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<QuickSimpleLookup>();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        WindowUtilities.ShouldShutDown();
    }

    #endregion Methods
}
