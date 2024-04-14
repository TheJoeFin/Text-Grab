using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for TesseractSettings.xaml
/// </summary>
public partial class TesseractSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool settingsSet = false;


    public TesseractSettings()
    {
        InitializeComponent();
    }

    private void TesseractPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!settingsSet)
            return;

        if (sender is not System.Windows.Controls.TextBox pathTextbox || pathTextbox.Text is not string pathText)
            return;

        if (File.Exists(pathText))
            UseTesseractCheckBox.IsEnabled = true;
        else
            UseTesseractCheckBox.IsEnabled = false;

        DefaultSettings.TesseractPath = pathText;
    }

    private void OpenPathButton_Click(object sender, RoutedEventArgs args)
    {
        if (TesseractPathTextBox.Text is not string pathTextBox || !File.Exists(TesseractPathTextBox.Text))
            return;

        string? tesseractExePath = Path.GetDirectoryName(pathTextBox);

        if (tesseractExePath is null)
            return;

        Uri source = new(tesseractExePath, UriKind.Absolute);
        RequestNavigateEventArgs e = new(source, tesseractExePath);
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void WinGetCodeCopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(WinGetInstallTextBox.Text);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void UseTesseractCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        if (sender is not ToggleSwitch useTesseractSwitch)
            return;

        DefaultSettings.UseTesseract = useTesseractSwitch.IsChecked is true;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (TesseractHelper.CanLocateTesseractExe())
        {
            UseTesseractCheckBox.IsChecked = DefaultSettings.UseTesseract;
            TesseractPathTextBox.Text = DefaultSettings.TesseractPath;
            return;
        }

        UseTesseractCheckBox.IsChecked = false;
        UseTesseractCheckBox.IsEnabled = false;
        DefaultSettings.UseTesseract = false;

        settingsSet = true;
    }
}
