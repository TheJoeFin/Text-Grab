using System;
using System.Collections.Generic;
using System.Windows;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Views;

public partial class OpenMediaWindow : FluentWindow
{
    private string? selectedFilePath;

    public OpenMediaWindow()
    {
        InitializeComponent();
        App.SetTheme();

        NotifyOnCompleteToggle.IsChecked = AppUtilities.TextGrabSettings.NotifyOnTranscriptionComplete;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFileDialog dlg = new()
        {
            Filter = AudioTranscriptionUtilities.GetAudioFileFilter(),
            DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        bool? result = dlg.ShowDialog();
        if (result is true)
            UpdateFileInfo(dlg.FileName);
    }

    private void UpdateFileInfo(string path)
    {
        FilePathTextBox.Text = path;
        FileErrorText.Visibility = Visibility.Collapsed;
        FileInfoPanel.Visibility = Visibility.Collapsed;
        selectedFilePath = null;
        StartTranscriptionButton.IsEnabled = false;

        try
        {
            AudioTranscriptionUtilities.AudioFileInfo info = AudioTranscriptionUtilities.GetAudioFileInfo(path);

            FileNameText.Text = info.FileName;
            FileSizeText.Text = $"Size: {info.FileSizeBytes / (1024.0 * 1024.0):0.#} MB";
            FileDurationText.Text = $"Duration: {info.Duration:mm\\:ss}";
            FileModelText.Text = $"Model: {WhisperModelInfo.DisplayName(AudioTranscriptionUtilities.CurrentModelChoice)}";
            FileInfoPanel.Visibility = Visibility.Visible;

            selectedFilePath = path;
            StartTranscriptionButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            FileErrorText.Text = $"⚠ Couldn't read this file: {ex.Message}";
            FileErrorText.Visibility = Visibility.Visible;
        }
    }

    private void HotWordsLookupButton_Click(object sender, RoutedEventArgs e)
    {
        QuickSimpleLookup qsl = new()
        {
            DestinationTextBox = HotWordsTextBox,
            IsPickerMode = true,
        };
        qsl.Owner = this;
        qsl.Show();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void NotifyOnCompleteToggle_Checked(object sender, RoutedEventArgs e)
    {
        AppUtilities.TextGrabSettings.NotifyOnTranscriptionComplete = true;
        AppUtilities.TextGrabSettings.Save();
    }

    private void NotifyOnCompleteToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        AppUtilities.TextGrabSettings.NotifyOnTranscriptionComplete = false;
        AppUtilities.TextGrabSettings.Save();
    }

    private async void StartTranscriptionButton_Click(object sender, RoutedEventArgs e)
    {
        if (Owner is EditTextWindow etw && selectedFilePath is not null)
        {
            etw.Activate();
            await etw.TranscribeAudioFilesAsync([selectedFilePath], HotWordsTextBox.Text.Trim());
        }

        Close();
    }
}
