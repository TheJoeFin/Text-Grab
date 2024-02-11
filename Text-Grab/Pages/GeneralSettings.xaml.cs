using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for GeneralSettings.xaml
/// </summary>
public partial class GeneralSettings : Page
{
    private Settings DefaultSettings = Settings.Default;

    public GeneralSettings()
    {
        InitializeComponent();


        if (!ImplementAppOptions.IsPackaged())
            OpenExeFolderButton.Visibility = Visibility.Visible;
    }

    private void OpenExeFolderButton_Click(object sender, RoutedEventArgs args)
    {
        if (Path.GetDirectoryName(AppContext.BaseDirectory) is not string exePath)
            return;

        Uri source = new(exePath, UriKind.Absolute);
        System.Windows.Navigation.RequestNavigateEventArgs e = new(source, exePath);
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void AboutBTN_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<FirstRunWindow>();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        AppTheme appTheme = Enum.Parse<AppTheme>(DefaultSettings.AppTheme, true);
        switch (appTheme)
        {
            case AppTheme.System:
                SystemThemeRdBtn.IsChecked = true;
                break;
            case AppTheme.Dark:
                DarkThemeRdBtn.IsChecked = true;
                break;
            case AppTheme.Light:
                LightThemeRdBtn.IsChecked = true;
                break;
            default:
                SystemThemeRdBtn.IsChecked = true;
                break;
        }

        TextGrabMode defaultLaunchSetting = Enum.Parse<TextGrabMode>(DefaultSettings.DefaultLaunch, true);
        switch (defaultLaunchSetting)
        {
            case TextGrabMode.Fullscreen:
                FullScreenRDBTN.IsChecked = true;
                break;
            case TextGrabMode.GrabFrame:
                GrabFrameRDBTN.IsChecked = true;
                break;
            case TextGrabMode.EditText:
                EditTextRDBTN.IsChecked = true;
                break;
            case TextGrabMode.QuickLookup:
                QuickLookupRDBTN.IsChecked = true;
                break;
            default:
                FullScreenRDBTN.IsChecked = true;
                break;
        }

        ShowToastCheckBox.IsChecked = DefaultSettings.ShowToast;
        RunInBackgroundChkBx.IsChecked = DefaultSettings.RunInTheBackground;
        ReadBarcodesBarcode.IsChecked = DefaultSettings.TryToReadBarcodes;
        HistorySwitch.IsChecked = DefaultSettings.UseHistory;
    }

    private void FullScreenRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.DefaultLaunch = TextGrabMode.Fullscreen.ToString();
    }

    private void GrabFrameRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.DefaultLaunch = TextGrabMode.GrabFrame.ToString();
    }

    private void EditTextRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.DefaultLaunch = TextGrabMode.EditText.ToString();
    }

    private void QuickLookupRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.DefaultLaunch = TextGrabMode.QuickLookup.ToString();
    }

    private void RunInBackgroundChkBx_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch runInBackgroundSwitch)
            return;

        DefaultSettings.RunInTheBackground = runInBackgroundSwitch.IsChecked is true;
        ImplementAppOptions.ImplementBackgroundOption(DefaultSettings.RunInTheBackground);
    }

    private void SystemThemeRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.AppTheme = AppTheme.System.ToString();
        App.SetTheme();
    }

    private void LightThemeRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.AppTheme = AppTheme.Light.ToString();
        App.SetTheme();
    }

    private void DarkThemeRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.AppTheme = AppTheme.Dark.ToString();
        App.SetTheme();
    }

    private void ReadBarcodesBarcode_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.TryToReadBarcodes = true;
    }

    private void ReadBarcodesBarcode_Unchecked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.TryToReadBarcodes = false;
    }

    private void HistorySwitch_Checked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.UseHistory = true;
    }

    private void HistorySwitch_Unchecked(object sender, RoutedEventArgs e)
    {
        DefaultSettings.UseHistory = false;
    }
}
