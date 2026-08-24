using System;
using System.Collections.Generic;
using System.Windows;
using Text_Grab.Utilities;
using Wpf.Ui.Controls;

namespace Text_Grab.Views;

public partial class OpenMediaWindow : FluentWindow
{
    private string? selectedFilePath;
    private EditTextWindow? transcribingOwner;

    public OpenMediaWindow()
    {
        InitializeComponent();
        App.SetTheme();

        NotifyOnCompleteToggle.IsChecked = AppUtilities.TextGrabSettings.NotifyOnTranscriptionComplete;
        IncludeTimecodesToggle.IsChecked = AppUtilities.TextGrabSettings.IncludeTimecodesInTranscription;
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
        if (transcribingOwner is not null)
        {
            // A transcription is running: stop it instead of just closing over it. The window
            // closes itself once StartTranscriptionButton_Click's await returns.
            transcribingOwner.CancelAudioTranscription();
            CancelButton.IsEnabled = false;
            TranscribingStatusText.Text = "Cancelling…";
            return;
        }

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

    private void IncludeTimecodesToggle_Checked(object sender, RoutedEventArgs e)
    {
        AppUtilities.TextGrabSettings.IncludeTimecodesInTranscription = true;
        AppUtilities.TextGrabSettings.Save();
    }

    private void IncludeTimecodesToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        AppUtilities.TextGrabSettings.IncludeTimecodesInTranscription = false;
        AppUtilities.TextGrabSettings.Save();
    }

    private async void StartTranscriptionButton_Click(object sender, RoutedEventArgs e)
    {
        if (Owner is EditTextWindow etw && selectedFilePath is not null)
        {
            transcribingOwner = etw;
            etw.Activate();
            SetTranscribingState(true);

            Progress<double> progress = new(fraction =>
            {
                TranscribingProgressBar.Value = fraction * 100;
                TranscribingStatusText.Text = $"Transcribing… {fraction:P0}";
            });

            await etw.TranscribeAudioFilesAsync([selectedFilePath], HotWordsTextBox.Text.Trim(), progress);
        }

        Close();
    }

    /// <summary>
    /// Toggles this window between "pick a file" and "transcription in progress": inputs and the
    /// Start button are disabled/hidden, and the Cancel button switches to cancelling the running
    /// transcription (owned by the main editor window) rather than just closing over it.
    /// </summary>
    private void SetTranscribingState(bool transcribing)
    {
        BrowseButton.IsEnabled = !transcribing;
        HotWordsTextBox.IsEnabled = !transcribing;
        HotWordsLookupButton.IsEnabled = !transcribing;
        NotifyOnCompleteToggle.IsEnabled = !transcribing;
        IncludeTimecodesToggle.IsEnabled = !transcribing;

        StartTranscriptionButton.Visibility = transcribing ? Visibility.Collapsed : Visibility.Visible;
        TranscribingPanel.Visibility = transcribing ? Visibility.Visible : Visibility.Collapsed;
        TranscribingProgressBar.Value = 0;
        TranscribingStatusText.Text = "Transcribing…";
        CancelButton.IsEnabled = true;
    }
}
