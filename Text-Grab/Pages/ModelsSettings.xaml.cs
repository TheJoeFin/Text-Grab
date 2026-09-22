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
    private readonly record struct ModelRow(Grid RowGrid, TextBlock NameText, TextBlock LanguageText, TextBlock StatusText, Button DownloadButton, Button DeleteButton);

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
            [WhisperModelChoice.TinyEnglish] = new(TinyEnglishRowGrid, TinyEnglishNameText, TinyEnglishLanguageText, TinyEnglishStatusText, TinyEnglishDownloadButton, TinyEnglishDeleteButton),
            [WhisperModelChoice.BaseEnglish] = new(BaseEnglishRowGrid, BaseEnglishNameText, BaseEnglishLanguageText, BaseEnglishStatusText, BaseEnglishDownloadButton, BaseEnglishDeleteButton),
            [WhisperModelChoice.BaseMultilingual] = new(BaseMultilingualRowGrid, BaseMultilingualNameText, BaseMultilingualLanguageText, BaseMultilingualStatusText, BaseMultilingualDownloadButton, BaseMultilingualDeleteButton),
            [WhisperModelChoice.SmallMultilingual] = new(SmallMultilingualRowGrid, SmallMultilingualNameText, SmallMultilingualLanguageText, SmallMultilingualStatusText, SmallMultilingualDownloadButton, SmallMultilingualDeleteButton),
            [WhisperModelChoice.MediumEnglish] = new(MediumEnglishRowGrid, MediumEnglishNameText, MediumEnglishLanguageText, MediumEnglishStatusText, MediumEnglishDownloadButton, MediumEnglishDeleteButton),
            [WhisperModelChoice.MediumMultilingual] = new(MediumMultilingualRowGrid, MediumMultilingualNameText, MediumMultilingualLanguageText, MediumMultilingualStatusText, MediumMultilingualDownloadButton, MediumMultilingualDeleteButton),
            [WhisperModelChoice.LargeTurboMultilingual] = new(LargeTurboMultilingualRowGrid, LargeTurboMultilingualNameText, LargeTurboMultilingualLanguageText, LargeTurboMultilingualStatusText, LargeTurboMultilingualDownloadButton, LargeTurboMultilingualDeleteButton),
            [WhisperModelChoice.LargeMultilingual] = new(LargeMultilingualRowGrid, LargeMultilingualNameText, LargeMultilingualLanguageText, LargeMultilingualStatusText, LargeMultilingualDownloadButton, LargeMultilingualDeleteButton),
        };

        // Every row and combo box entry's text is drawn from WhisperModelInfo — the single source of
        // truth also used by the live-transcription flyouts and Open Media — so a model's name, size,
        // and description can never drift out of sync between touch points.
        foreach ((WhisperModelChoice choice, ModelRow row) in modelRows)
        {
            row.NameText.Text = WhisperModelInfo.DisplayName(choice);
            row.LanguageText.Text = WhisperModelInfo.ShortLanguageLabel(choice);
            row.RowGrid.ToolTip = WhisperModelInfo.Description(choice);
        }

        WhisperModelChoice currentChoice = AudioTranscriptionUtilities.CurrentModelChoice;
        foreach (ComboBoxItem item in DefaultModelComboBox.Items)
        {
            if (item.Tag is not string tag)
                continue;

            item.Content = WhisperModelInfo.DisplayNameWithSize(WhisperModelInfo.Parse(tag));
            if (tag == currentChoice.ToString())
                DefaultModelComboBox.SelectedItem = item;
        }

        WhisperModelChoice currentLiveChoice = AudioTranscriptionUtilities.CurrentLiveModelChoice;
        foreach (ComboBoxItem item in LiveDefaultModelComboBox.Items)
        {
            if (item.Tag is not string tag)
                continue;

            item.Content = WhisperModelInfo.DisplayNameWithSize(WhisperModelInfo.Parse(tag));
            if (tag == currentLiveChoice.ToString())
                LiveDefaultModelComboBox.SelectedItem = item;
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
            : $"Not downloaded ({WhisperModelInfo.ApproxDownloadSize(choice)})";

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

    private void LiveDefaultModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded)
            return;

        if (LiveDefaultModelComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
            return;

        AppUtilities.TextGrabSettings.LiveTranscriptionModel = tag;
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

        bool isFileDefault = AudioTranscriptionUtilities.CurrentModelChoice == choice;
        bool isLiveDefault = AudioTranscriptionUtilities.CurrentLiveModelChoice == choice;
        string content = (isFileDefault, isLiveDefault) switch
        {
            (true, true) => $"Delete the downloaded \"{WhisperModelInfo.DisplayName(choice)}\" model? It's your default for both file and live transcription, so Text Grab will automatically download it again the next time you transcribe.",
            (true, false) => $"Delete the downloaded \"{WhisperModelInfo.DisplayName(choice)}\" model? It's your file transcription default, so Text Grab will automatically download it again the next time you transcribe.",
            (false, true) => $"Delete the downloaded \"{WhisperModelInfo.DisplayName(choice)}\" model? It's your live transcription default, so Text Grab will automatically download it again the next time you transcribe.",
            _ => $"Delete the downloaded \"{WhisperModelInfo.DisplayName(choice)}\" model? You can download it again later.",
        };

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
