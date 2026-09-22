using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Utilities;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for ModelsSettings.xaml
/// </summary>
public partial class ModelsSettings : Page
{
    private readonly record struct ModelRow(TextBlock StatusText, Button DownloadButton, Button DeleteButton);

    private Dictionary<WhisperModelChoice, ModelRow> modelRows = [];
    private bool _loaded;

    public ModelsSettings()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        modelRows = new()
        {
            [WhisperModelChoice.TinyEnglish] = new(TinyEnglishStatusText, TinyEnglishDownloadButton, TinyEnglishDeleteButton),
            [WhisperModelChoice.BaseEnglish] = new(BaseEnglishStatusText, BaseEnglishDownloadButton, BaseEnglishDeleteButton),
            [WhisperModelChoice.BaseMultilingual] = new(BaseMultilingualStatusText, BaseMultilingualDownloadButton, BaseMultilingualDeleteButton),
            [WhisperModelChoice.SmallMultilingual] = new(SmallMultilingualStatusText, SmallMultilingualDownloadButton, SmallMultilingualDeleteButton),
        };

        WhisperModelChoice currentChoice = AudioTranscriptionUtilities.CurrentModelChoice;
        foreach (ComboBoxItem item in DefaultModelComboBox.Items)
        {
            if (item.Tag is string tag && tag == currentChoice.ToString())
            {
                DefaultModelComboBox.SelectedItem = item;
                break;
            }
        }

        foreach (WhisperModelChoice choice in modelRows.Keys)
            RefreshModelRow(choice);

        _loaded = true;
    }

    /// <summary>Updates the download status text and which of Download/Delete is shown for a model.</summary>
    private void RefreshModelRow(WhisperModelChoice choice)
    {
        if (!modelRows.TryGetValue(choice, out ModelRow row))
            return;

        long? downloadedBytes = AudioTranscriptionUtilities.DownloadedModelSizeBytes(choice);

        row.StatusText.Text = downloadedBytes is long bytes
            ? $"{bytes / (1024.0 * 1024.0):0.#} MB downloaded"
            : "Not downloaded";

        row.DownloadButton.Visibility = downloadedBytes is null ? Visibility.Visible : Visibility.Collapsed;
        row.DeleteButton.Visibility = downloadedBytes is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void DefaultModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded)
            return;

        if (DefaultModelComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
            return;

        AppUtilities.TextGrabSettings.AudioTranscriptionModel = tag;
        AppUtilities.TextGrabSettings.Save();

        foreach (WhisperModelChoice choice in modelRows.Keys)
            RefreshModelRow(choice);
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tag)
            return;

        WhisperModelChoice choice = WhisperModelInfo.Parse(tag);
        if (!modelRows.TryGetValue(choice, out ModelRow row))
            return;

        row.DownloadButton.IsEnabled = false;
        Progress<string> progress = new(message => row.StatusText.Text = message);

        try
        {
            await AudioTranscriptionUtilities.DownloadModelAsync(choice, progress);
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Download failed",
                Content = $"Couldn't download this model:\n{ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
        finally
        {
            row.DownloadButton.IsEnabled = true;
            RefreshModelRow(choice);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tag)
            return;

        WhisperModelChoice choice = WhisperModelInfo.Parse(tag);

        bool isDefault = AudioTranscriptionUtilities.CurrentModelChoice == choice;
        string content = isDefault
            ? $"Delete the downloaded \"{WhisperModelInfo.DisplayName(choice)}\" model? It's your default model, so Text Grab will automatically download it again the next time you transcribe."
            : $"Delete the downloaded \"{WhisperModelInfo.DisplayName(choice)}\" model? You can download it again later.";

        Wpf.Ui.Controls.MessageBoxResult result = await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Delete model",
            Content = content,
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel"
        }.ShowDialogAsync();

        if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        AudioTranscriptionUtilities.DeleteModel(choice);
        RefreshModelRow(choice);
    }

    private void OpenModelsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AudioTranscriptionUtilities.ModelDirectory);
        Process.Start(new ProcessStartInfo(AudioTranscriptionUtilities.ModelDirectory) { UseShellExecute = true });
    }

    private void GoToLanguagesButton_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is SettingsWindow settingsWindow)
            settingsWindow.SettingsNavView.Navigate(typeof(LanguageSettings));
    }
}
