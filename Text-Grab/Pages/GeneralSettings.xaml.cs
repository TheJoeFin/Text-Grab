using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for GeneralSettings.xaml
/// </summary>
public partial class GeneralSettings : Page
{
    public GeneralSettings()
    {
        InitializeComponent();


        if (!ImplementAppOptions.IsPackaged())
            OpenExeFolderButton.Visibility = Visibility.Visible;
    }

    private void OpenExeFolderButton_Click(object sender, RoutedEventArgs args)
    {
        if (Path.GetDirectoryName(System.AppContext.BaseDirectory) is not string exePath)
            return;

        Uri source = new(exePath, UriKind.Absolute);
        System.Windows.Navigation.RequestNavigateEventArgs e = new(source, exePath);
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void AboutBTN_Click(object sender, RoutedEventArgs e)
    {
        //ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;
        //ErrorCorrectBox.IsChecked = Settings.Default.CorrectErrors;
        //NeverUseClipboardChkBx.IsChecked = Settings.Default.NeverAutoUseClipboard;
        //RunInBackgroundChkBx.IsChecked = Settings.Default.RunInTheBackground;
        //TryInsertCheckbox.IsChecked = Settings.Default.TryInsert;
        //GlobalHotkeysCheckbox.IsChecked = Settings.Default.GlobalHotkeysEnabled;
        //ReadBarcodesBarcode.IsChecked = Settings.Default.TryToReadBarcodes;
        //CorrectToLatin.IsChecked = Settings.Default.CorrectToLatin;


        WindowUtilities.OpenOrActivateWindow<FirstRunWindow>();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        AppTheme appTheme = Enum.Parse<AppTheme>(Settings.Default.AppTheme, true);
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
    }
}
