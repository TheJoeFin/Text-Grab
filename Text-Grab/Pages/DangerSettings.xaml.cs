using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for DangerSettings.xaml
/// </summary>
public partial class DangerSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    public DangerSettings()
    {
        InitializeComponent();
    }

    private async void ExportBugReportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string filePath = await DiagnosticsUtilities.SaveBugReportToFileAsync();

            Wpf.Ui.Controls.MessageBoxResult result = await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Bug Report Generated",
                Content = $"Bug report saved to:\n{filePath}\n\nWould you like to open the file location?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No"
            }.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                // Open the file location in File Explorer
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Error",
                Content = $"Failed to generate bug report:\n{ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
    }

    private async void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        Wpf.Ui.Controls.MessageBoxResult areYouSure = await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Reset Settings to Default",
            Content = "Are you sure you want to reset all settings to default and delete all history?",
            PrimaryButtonText = "Yes",
            CloseButtonText = "No"
        }.ShowDialogAsync();

        if (areYouSure != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        DefaultSettings.Reset();
        Singleton<HistoryService>.Instance.DeleteHistory();
        App.Current.Shutdown();
    }

    private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        Wpf.Ui.Controls.MessageBoxResult areYouSure = await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Reset Settings to Default",
            Content = "Are you sure you want to delete all history?",
            PrimaryButtonText = "Yes",
            CloseButtonText = "No"
        }.ShowDialogAsync();

        if (areYouSure != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        Singleton<HistoryService>.Instance.DeleteHistory();
    }

    private async void ExportSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            bool includeHistory = IncludeHistoryCheckBox.IsChecked ?? false;
            string filePath = await SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory);

            Wpf.Ui.Controls.MessageBoxResult result = await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Export Successful",
                Content = $"Settings exported successfully to:\n{filePath}\n\nWould you like to open the file location?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No"
            }.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                // Open the file location in File Explorer
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
        }
        catch (System.Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Export Error",
                Content = $"Failed to export settings:\n{ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
    }

    private async void ImportSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Set default directory to Documents folder (where exports are saved)
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            OpenFileDialog openFileDialog = new()
            {
                Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*",
                Title = "Select Settings Export File",
                DefaultExt = ".zip",
                InitialDirectory = documentsPath
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            Wpf.Ui.Controls.MessageBoxResult confirmation = await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Confirm Import",
                Content = "Importing settings will overwrite your current settings. The application will restart after import.\n\nDo you want to continue?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No"
            }.ShowDialogAsync();

            if (confirmation != Wpf.Ui.Controls.MessageBoxResult.Primary)
                return;

            await SettingsImportExportUtilities.ImportSettingsFromZipAsync(openFileDialog.FileName);

            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Import Successful",
                Content = "Settings imported successfully. Open Text Grab again to fully apply all settings. Shutting down now...",
                CloseButtonText = "OK"
            }.ShowDialogAsync();

            // Shut down Text Grab
            App.Current.Shutdown();
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Import Error",
                Content = $"Failed to import settings:\n{ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
    }

    private void ShutdownButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void RetrySettingTrayButton_Click(object sender, RoutedEventArgs e)
    {
        NotifyIconUtilities.ResetNotifyIcon();
    }
}
