using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for DangerSettings.xaml
/// </summary>
public partial class DangerSettings : System.Windows.Controls.Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool _loadingDangerSettings;

    public DangerSettings()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _loadingDangerSettings = true;
        OverrideArchCheckWinAI.IsChecked = DefaultSettings.OverrideAiArchCheck;
        EnableFileBackedManagedSettingsToggle.IsChecked = DefaultSettings.EnableFileBackedManagedSettings;
        _loadingDangerSettings = false;
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
        await ExportSettingsAsync();
    }

    private async Task ExportSettingsAsync()
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

    private async void BackupSettingsHyperlink_Click(object sender, RoutedEventArgs e)
    {
        await ExportSettingsAsync();
    }

    private async void ImportSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Set default directory to Documents folder (where exports are saved)
            string documentsPath = AutomationProfile.Current?.OutputDirectory
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

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

    private async void RetrySettingTrayButton_Click(object sender, RoutedEventArgs e)
    {
        await NotifyIconUtilities.ResetNotifyIcon();
    }

    private void OverrideArchCheckWinAI_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.ToggleSwitch ts)
            return;

        DefaultSettings.OverrideAiArchCheck = ts.IsChecked ?? false;
        DefaultSettings.Save();
    }

    private async void EnableFileBackedManagedSettingsToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_loadingDangerSettings)
            return;

        bool isEnabled = EnableFileBackedManagedSettingsToggle.IsChecked is true;
        if (DefaultSettings.EnableFileBackedManagedSettings == isEnabled)
            return;

        DefaultSettings.EnableFileBackedManagedSettings = isEnabled;
        DefaultSettings.Save();

        string message = isEnabled
            ? "Experimental file-backed settings storage will be preferred after you restart Text Grab. Restart is required because Text Grab applies this storage preference when it starts so it can safely keep the legacy strings and file-backed copies in sync. Backup your settings before using it if you have not already."
            : "Legacy settings storage will be preferred again after you restart Text Grab.";

        await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Restart Required",
            Content = message,
            CloseButtonText = "OK"
        }.ShowDialogAsync();
    }
}
