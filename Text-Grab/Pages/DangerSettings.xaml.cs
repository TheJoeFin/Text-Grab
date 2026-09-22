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
    private bool _backedUpThisSession;

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

        RefreshPortableChecklist();
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

        if (isEnabled)
        {
            Wpf.Ui.Controls.MessageBoxResult confirmation = await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Enable Experimental File-Backed Settings Storage?",
                Content = "Experimental file-backed settings storage will be preferred after you restart Text Grab. Restart is required because Text Grab applies this storage preference when it starts so it can safely keep the legacy strings and file-backed copies in sync.\n\nBackup your settings before enabling it if you have not already.",
                PrimaryButtonText = "Enable",
                CloseButtonText = "Cancel"
            }.ShowDialogAsync();

            if (confirmation != Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                RevertEnableFileBackedManagedSettingsToggle();
                return;
            }

            DefaultSettings.EnableFileBackedManagedSettings = true;
            DefaultSettings.Save();

            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Restart Required",
                Content = "Restart Text Grab to apply the new storage preference.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();

            return;
        }

        DefaultSettings.EnableFileBackedManagedSettings = false;
        DefaultSettings.Save();

        await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Restart Required",
            Content = "Legacy settings storage will be preferred again after you restart Text Grab.",
            CloseButtonText = "OK"
        }.ShowDialogAsync();
    }

    private void RevertEnableFileBackedManagedSettingsToggle()
    {
        _loadingDangerSettings = true;
        EnableFileBackedManagedSettingsToggle.IsChecked = DefaultSettings.EnableFileBackedManagedSettings;
        _loadingDangerSettings = false;
    }

    /// <summary>Re-evaluates every readiness check and updates the checklist UI to match.</summary>
    private void RefreshPortableChecklist()
    {
        bool alreadyPortable = DefaultSettings.FullyPortable;
        bool isPackaged = AppUtilities.IsPackaged();

        PortableChecklistPanel.Visibility = (!isPackaged && !alreadyPortable) ? Visibility.Visible : Visibility.Collapsed;
        RecheckPortableButton.Visibility = (!isPackaged && !alreadyPortable) ? Visibility.Visible : Visibility.Collapsed;
        EnableFullyPortableButton.Visibility = alreadyPortable ? Visibility.Collapsed : Visibility.Visible;
        DisableFullyPortableButton.Visibility = alreadyPortable ? Visibility.Visible : Visibility.Collapsed;

        if (isPackaged)
        {
            PortableStatusText.Text = "Fully portable mode only applies to unpackaged installs.";
            EnableFullyPortableButton.IsEnabled = false;
            return;
        }

        if (alreadyPortable)
        {
            PortableStatusText.Text = "Fully portable mode is enabled. Restart Text Grab if you haven't already.";
            return;
        }

        PortableReadinessCheck fileBacked = PortableModeReadiness.CheckFileBackedSettings(DefaultSettings);
        SetCheckRow(FileBackedStatusIcon, FileBackedDetailText, FileBackedFixButton, fileBacked);

        PortableReadinessCheck models = PortableModeReadiness.CheckTranscriptionModels();
        SetCheckRow(ModelsStatusIcon, ModelsDetailText, ModelsFixButton, models);

        PortableReadinessCheck startup = PortableModeReadiness.CheckStartupOnLogin(DefaultSettings);
        SetCheckRow(StartupStatusIcon, StartupDetailText, StartupFixButton, startup);

        PortableReadinessCheck registry = PortableModeReadiness.CheckRegistryIntegration(DefaultSettings);
        SetCheckRow(RegistryStatusIcon, RegistryDetailText, RegistryFixButton, registry);

        PortableReadinessCheck backup = PortableModeReadiness.CheckRecentBackup(_backedUpThisSession);
        SetCheckRow(BackupStatusIcon, BackupDetailText, BackupFixButton, backup);

        bool ready = fileBacked.IsSatisfied && models.IsSatisfied && startup.IsSatisfied && registry.IsSatisfied;
        EnableFullyPortableButton.IsEnabled = ready;
        PortableStatusText.Text = ready
            ? "All required checks pass. You're ready to enable fully portable mode."
            : "Resolve the required items above (backup is optional), then recheck.";
    }

    private static void SetCheckRow(Wpf.Ui.Controls.SymbolIcon icon, System.Windows.Controls.TextBlock detailText, System.Windows.Controls.Button fixButton, PortableReadinessCheck check)
    {
        icon.Symbol = check.IsSatisfied ? Wpf.Ui.Controls.SymbolRegular.Checkmark24 : Wpf.Ui.Controls.SymbolRegular.Warning24;
        icon.Foreground = check.IsSatisfied
            ? System.Windows.Media.Brushes.SeaGreen
            : System.Windows.Media.Brushes.DarkOrange;
        detailText.Text = check.Detail;
        fixButton.Visibility = check.IsSatisfied ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RecheckPortableButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshPortableChecklist();
    }

    private void FileBackedFixButton_Click(object sender, RoutedEventArgs e)
    {
        PortableModeReadiness.EnableFileBackedSettings(DefaultSettings);
        RefreshPortableChecklist();
    }

    private void ModelsFixButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PortableModeReadiness.MoveDataToPortableFolder();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to move data to the portable folder: {ex.Message}");
        }

        RefreshPortableChecklist();
    }

    private async void StartupFixButton_Click(object sender, RoutedEventArgs e)
    {
        StartupFixButton.IsEnabled = false;
        await PortableModeReadiness.DisableStartupOnLoginAsync(DefaultSettings);
        RefreshPortableChecklist();
    }

    private void RegistryFixButton_Click(object sender, RoutedEventArgs e)
    {
        PortableModeReadiness.RemoveRegistryIntegration(DefaultSettings);
        RefreshPortableChecklist();
    }

    private async void BackupFixButton_Click(object sender, RoutedEventArgs e)
    {
        await ExportSettingsAsync();
        _backedUpThisSession = true;
        RefreshPortableChecklist();
    }

    private async void EnableFullyPortableButton_Click(object sender, RoutedEventArgs e)
    {
        Wpf.Ui.Controls.MessageBoxResult confirmation = await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Enable Fully Portable Mode?",
            Content = "Fully portable mode will be active after you restart Text Grab: settings, history, Whisper models and logs will move into Text Grab's own folder, and registry-based OS integration (startup on login, context menu, file/protocol associations) will stop being written.",
            PrimaryButtonText = "Enable",
            CloseButtonText = "Cancel"
        }.ShowDialogAsync();

        if (confirmation != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        DefaultSettings.FullyPortable = true;
        DefaultSettings.Save();

        await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Restart Required",
            Content = "Restart Text Grab to apply fully portable mode.",
            CloseButtonText = "OK"
        }.ShowDialogAsync();

        RefreshPortableChecklist();
    }

    private async void DisableFullyPortableButton_Click(object sender, RoutedEventArgs e)
    {
        Wpf.Ui.Controls.MessageBoxResult confirmation = await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Turn Off Fully Portable Mode?",
            Content = "Fully portable mode will be turned off after you restart Text Grab. Data already moved beside the executable is not moved back automatically.",
            PrimaryButtonText = "Turn Off",
            CloseButtonText = "Cancel"
        }.ShowDialogAsync();

        if (confirmation != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        DefaultSettings.FullyPortable = false;
        DefaultSettings.Save();

        await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Restart Required",
            Content = "Restart Text Grab to apply the change.",
            CloseButtonText = "OK"
        }.ShowDialogAsync();

        RefreshPortableChecklist();
    }
}
