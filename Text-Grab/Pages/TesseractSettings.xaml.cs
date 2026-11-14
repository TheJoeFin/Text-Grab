using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Pages;

/// <summary>
/// One row of the installed-languages list.
/// </summary>
public sealed record TesseractLanguageRow(TessdataLanguageFile File)
{
    public string Tag => File.Tag;
    public string DisplayName => new TessLang(File.Tag).CultureDisplayName;
    public string SourceText => File.Source == TessdataSource.Bundled ? "Bundled" : "Downloaded";
    public string SizeText => $"{File.SizeBytes / 1024.0 / 1024.0:0.0} MB";
    public string RemoveAutomationName => $"Remove {File.Tag}";

    // The bundled file is read-only; removing a downloaded 'eng' falls back to the bundled one.
    public Visibility RemoveVisibility => File.Source == TessdataSource.Downloaded ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>
/// Interaction logic for TesseractSettings.xaml. Two forms, chosen by the engine mode:
/// Bundled (everyone new) and External (existing tesseract.exe setups, frozen).
/// </summary>
public partial class TesseractSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool settingsSet = false;
    private string? statusDetails;
    private CancellationTokenSource? downloadCts;

    public TesseractSettings()
    {
        InitializeComponent();
    }

    private static bool IsBundled => TesseractHelper.Mode != TesseractEngineMode.External;

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        settingsSet = false;
        await LoadAsync();
        settingsSet = true;
    }

    private async Task LoadAsync()
    {
        bool bundled = IsBundled;
        BundledPanel.Visibility = bundled ? Visibility.Visible : Visibility.Collapsed;
        ExternalPanel.Visibility = bundled ? Visibility.Collapsed : Visibility.Visible;
        EngineStatusCard.Visibility = bundled ? Visibility.Visible : Visibility.Collapsed;

        if (bundled)
            await LoadBundledAsync();
        else
            LoadExternal();
    }

    private void LoadExternal()
    {
        CopyDetailsButton.Visibility = Visibility.Collapsed;
        TesseractPathTextBox.Text = DefaultSettings.TesseractPath;

        bool located = File.Exists(DefaultSettings.TesseractPath);
        CouldNotFindTessTxtBlk.Visibility = located ? Visibility.Collapsed : Visibility.Visible;

        if (located)
        {
            UseTesseractCheckBox.IsEnabled = true;
            UseTesseractCheckBox.IsChecked = DefaultSettings.UseTesseract;
            return;
        }

        // Never silently switch engines: the user gets a one-click switch to built-in below.
        UseTesseractCheckBox.IsChecked = false;
        UseTesseractCheckBox.IsEnabled = false;
        DefaultSettings.UseTesseract = false;
    }

    private async Task LoadBundledAsync()
    {
        UseTesseractCheckBox.IsChecked = DefaultSettings.UseTesseract;
        SwitchBackCard.Visibility = TesseractEngineModeResolver.CanSwitchToExternal(DefaultSettings)
            ? Visibility.Visible
            : Visibility.Collapsed;

        RefreshLanguageList();
        UpdateDataFolderInfo();

        EngineStatusCard.Description = "Checking the built-in engine...";
        UseTesseractCheckBox.IsEnabled = false;

        BundledTesseractStatus status = await TesseractHelper.GetBundledStatusAsync();
        statusDetails = status.ErrorDetails;

        switch (status.State)
        {
            case BundledTesseractState.Ready:
                EngineStatusCard.HeaderText = $"Built-in Tesseract {status.Version}";
                EngineStatusCard.Description = "Ready";
                break;
            case BundledTesseractState.NoLanguages:
                EngineStatusCard.HeaderText = "Built-in Tesseract";
                EngineStatusCard.Description = "No languages available. Download one below.";
                break;
            default:
                EngineStatusCard.HeaderText = "Built-in Tesseract";
                EngineStatusCard.Description = $"Library failed to load: {FirstLine(status.ErrorDetails)}";
                break;
        }

        CopyDetailsButton.Visibility = status.ErrorDetails is null ? Visibility.Collapsed : Visibility.Visible;
        UseTesseractCheckBox.IsEnabled = status.State == BundledTesseractState.Ready;
    }

    private static string FirstLine(string? text) =>
        text?.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

    private void RefreshLanguageList()
    {
        IReadOnlyList<TessdataLanguageFile> installed = TessdataLocator.GetInstalledLanguages();
        LanguagesListView.Items.Clear();
        foreach (TessdataLanguageFile file in installed)
            LanguagesListView.Items.Add(new TesseractLanguageRow(file));

        // Hide languages that are already downloaded; a bundled one stays so it can be replaced
        // by a larger model.
        HashSet<string> downloaded = installed
            .Where(f => f.Source == TessdataSource.Downloaded)
            .Select(f => f.Tag)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        AllLanguagesComboBox.Items.Clear();
        foreach (string fileName in TesseractGitHubFileDownloader.tesseractTrainedDataFileNames)
        {
            string tag = fileName.Split('.').First();
            if (tag == "osd" || downloaded.Contains(tag))
                continue;

            AllLanguagesComboBox.Items.Add($"{fileName.PadRight(26)}\t{new TessLang(tag).CultureDisplayName}");
        }
    }

    private void UpdateDataFolderInfo()
    {
        string folder = TessdataLocator.GetWritableFolder();
        DataFolderCard.Description = folder;

        if (TryCheckWritable(folder, out string? reason))
        {
            DataFolderWarningTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        DataFolderWarningTextBlock.Text = $"⚠️ Text Grab cannot write to this folder, so language downloads will fail. {reason}";
        DataFolderWarningTextBlock.Visibility = Visibility.Visible;
    }

    private static bool TryCheckWritable(string folder, out string? reason)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            reason = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            reason = ex.Message;
            return false;
        }
    }

    private async Task AfterLanguageChangeAsync()
    {
        await TesseractHelper.InvalidateBundledEngineAsync();
        LanguageUtilities.InvalidateAllCaches();
        RefreshLanguageList();
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        string? pickedFile = AllLanguagesComboBox.Text?.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(pickedFile) || !TesseractGitHubFileDownloader.tesseractTrainedDataFileNames.Contains(pickedFile))
        {
            DownloadResultTextBlock.Text = "Pick a language from the list first.";
            return;
        }

        string destination;
        try
        {
            destination = Path.Combine(TessdataLocator.EnsureWritableFolder(), pickedFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DownloadResultTextBlock.Text = $"Cannot create the language data folder: {ex.Message}";
            return;
        }

        downloadCts = new CancellationTokenSource();
        DownloadButton.IsEnabled = false;
        CancelDownloadButton.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 0;
        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadResultTextBlock.Text = $"Downloading {pickedFile}...";

        try
        {
            TesseractGitHubFileDownloader downloader = new(AppServices.Provider.GetRequiredService<HttpClient>());
            Progress<double> progress = new(value => DownloadProgressBar.Value = value);
            TesseractDownloadResult result = await downloader.DownloadFileAsync(pickedFile, destination, progress, downloadCts.Token);

            if (result.Success)
            {
                DownloadResultTextBlock.Text = $"Downloaded {pickedFile}.";
                await AfterLanguageChangeAsync();
                await LoadBundledAsync();
            }
            else
            {
                DownloadResultTextBlock.Text = result.Error ?? "Download failed.";
            }
        }
        catch (OperationCanceledException)
        {
            DownloadResultTextBlock.Text = "Download cancelled.";
        }
        finally
        {
            downloadCts?.Dispose();
            downloadCts = null;
            DownloadButton.IsEnabled = true;
            CancelDownloadButton.Visibility = Visibility.Collapsed;
            DownloadProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e) => downloadCts?.Cancel();

    private async void RemoveLanguageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string tag })
            return;

        TessdataLanguageFile? file = TessdataLocator.GetInstalledLanguages()
            .FirstOrDefault(f => f.Source == TessdataSource.Downloaded && string.Equals(f.Tag, tag, StringComparison.OrdinalIgnoreCase));
        if (file is null)
            return;

        try
        {
            await TesseractHelper.InvalidateBundledEngineAsync();
            File.Delete(file.Path);
            DownloadResultTextBlock.Text = $"Removed {tag}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DownloadResultTextBlock.Text = $"Could not remove {tag}: {ex.Message}";
        }

        await AfterLanguageChangeAsync();
        await LoadBundledAsync();
    }

    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start("explorer.exe", TessdataLocator.EnsureWritableFolder());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DownloadResultTextBlock.Text = $"Cannot open the language data folder: {ex.Message}";
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await AfterLanguageChangeAsync();
        await LoadBundledAsync();
    }

    private void CopyDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(statusDetails))
            Clipboard.SetText(statusDetails);
    }

    private async void SwitchToBundledButton_Click(object sender, RoutedEventArgs e)
    {
        Wpf.Ui.Controls.MessageBoxResult result = await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Switch to built-in Tesseract?",
            Content = "The built-in Tesseract uses its own languages. Languages installed with your own Tesseract are not used by it, and your install is left untouched. You can switch back later while the saved path to your tesseract.exe still exists.",
            PrimaryButtonText = "Switch",
            CloseButtonText = "Cancel"
        }.ShowDialogAsync();

        if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        await SwitchModeAsync(TesseractEngineMode.Bundled);
    }

    private async void SwitchToExternalButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TesseractEngineModeResolver.CanSwitchToExternal(DefaultSettings))
            return;

        await SwitchModeAsync(TesseractEngineMode.External);
    }

    private async Task SwitchModeAsync(TesseractEngineMode mode)
    {
        // Switching does not touch UseTesseract or the other engine's language files.
        TesseractEngineModeResolver.Set(DefaultSettings, mode);
        await TesseractHelper.InvalidateBundledEngineAsync();
        LanguageUtilities.InvalidateAllCaches();

        settingsSet = false;
        await LoadAsync();
        settingsSet = true;
    }

    private void TesseractPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!settingsSet)
            return;

        if (sender is not System.Windows.Controls.TextBox pathTextbox || pathTextbox.Text is not string pathText)
            return;

        bool located = File.Exists(pathText);
        UseTesseractCheckBox.IsEnabled = located;
        CouldNotFindTessTxtBlk.Visibility = located ? Visibility.Collapsed : Visibility.Visible;

        DefaultSettings.TesseractPath = pathText;
        DefaultSettings.Save();
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
        DefaultSettings.Save();
        LanguageUtilities.InvalidateAllCaches();
    }
}
