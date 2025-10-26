using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Diagnostics;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Microsoft.Win32;

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
            
            MessageBoxResult result = MessageBox.Show(
                $"Bug report saved to:\n{filePath}\n\nWould you like to open the file location?", 
                "Bug Report Generated", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Information);
            
            if (result == MessageBoxResult.Yes)
            {
                // Open the file location in File Explorer
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(
                $"Failed to generate bug report:\n{ex.Message}", 
                "Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
    }

    private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult areYouSure = MessageBox.Show("Are you sure you want to reset all settings to default and delete all history?", "Reset Settings to Default", MessageBoxButton.YesNo);

        if (areYouSure != MessageBoxResult.Yes)
            return;

        DefaultSettings.Reset();
        Singleton<HistoryService>.Instance.DeleteHistory();
        App.Current.Shutdown();
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult areYouSure = MessageBox.Show("Are you sure you want to delete all history?", "Reset Settings to Default", MessageBoxButton.YesNo);

        if (areYouSure != MessageBoxResult.Yes)
            return;

        Singleton<HistoryService>.Instance.DeleteHistory();
    }

    private async void ExportSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            bool includeHistory = IncludeHistoryCheckBox.IsChecked ?? false;
            string filePath = await SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory);
            
            MessageBoxResult result = MessageBox.Show(
                $"Settings exported successfully to:\n{filePath}\n\nWould you like to open the file location?", 
                "Export Successful", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Information);
            
            if (result == MessageBoxResult.Yes)
            {
                // Open the file location in File Explorer
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(
                $"Failed to export settings:\n{ex.Message}", 
                "Export Error", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
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

            MessageBoxResult confirmation = MessageBox.Show(
                "Importing settings will overwrite your current settings. The application will restart after import.\n\nDo you want to continue?",
                "Confirm Import",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            await SettingsImportExportUtilities.ImportSettingsFromZipAsync(openFileDialog.FileName);

            MessageBox.Show(
                "Settings imported successfully. The application will now restart.",
                "Import Successful",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Restart the application
            RestartApplication();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(
                $"Failed to import settings:\n{ex.Message}",
                "Import Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RestartApplication()
    {
        // Get the executable path
        string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        
        if (!string.IsNullOrEmpty(exePath))
        {
            // Start a new instance of the application
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });
        }
        
        // Shutdown the current instance
        App.Current.Shutdown();
    }
}
