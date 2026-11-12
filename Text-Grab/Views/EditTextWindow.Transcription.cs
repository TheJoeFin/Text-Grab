using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using Text_Grab.Core.Editing;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Pages;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Windows.Storage;
using Windows.Storage.Streams;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Text_Grab;

public partial class EditTextWindow
{
    /// <summary>
    /// Transcribes one or more dropped audio files on-device, streaming each Whisper segment into the
    /// editor as it is recognized. The status bar stays non-blocking so the user can cancel; because
    /// every segment is inserted as it arrives, cancelling keeps all text transcribed so far.
    /// <paramref name="hotWords"/> (if provided) biases Whisper toward names/jargon it might otherwise
    /// mishear; it applies only to this call, nothing is persisted.
    /// <paramref name="overallProgress"/> (if provided) reports progress across all files combined
    /// (0.0-1.0), so a caller such as <see cref="OpenMediaWindow"/> can drive its own progress bar.
    /// </summary>
    internal async Task TranscribeAudioFilesAsync(IList<string> audioFiles, string? hotWords = null, IProgress<double>? overallProgress = null)
    {
        AudioDebugLog.Write($"TranscribeAudioFilesAsync: START with {audioFiles.Count} file(s). Log: {AudioDebugLog.LogPath}");

        if (!AudioTranscriptionUtilities.IsAudioTranscriptionSupported())
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Audio Transcription Unavailable",
                Content = "Audio transcription isn't available on this device.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return;
        }

        // Guard against a second transcription starting while one is already running.
        if (_transcriptionCts is not null)
        {
            AudioDebugLog.Write("TranscribeAudioFilesAsync: ignored — a transcription is already in progress");
            return;
        }

        bool multiple = audioFiles.Count > 1;
        bool previousIsReadOnly = PassedTextControl.IsReadOnly;
        string? errorMessage = null;
        bool cancelled = false;

        // Everything from here runs inside the try: the guard above keys off _transcriptionCts, so a
        // throw during setup that left it assigned would block every later transcription in this window.
        try
        {
            _transcriptionCts = new CancellationTokenSource();
            CancellationToken cancellationToken = _transcriptionCts.Token;

            // Make the editor read-only (not disabled) so segments can stream in but the user can't type
            // in the middle of the stream. AppendText still works while read-only.
            PassedTextControl.IsReadOnly = true;
            TranscriptionCancelButton.IsEnabled = true;
            TranscriptionStatusText.Text = multiple ? "Transcribing audio files…" : "Transcribing audio…";
            TranscriptionProgressBar.Visibility = Visibility.Collapsed;
            TranscriptionProgressBar.Value = 0;
            TranscriptionStatusBar.Visibility = Visibility.Visible;

            // Status text (model download, "Transcribing…") updates the status bar; each recognized
            // segment is appended straight into the editor as it arrives.
            Progress<string> statusProgress = new(message => TranscriptionStatusText.Text = message);
            Progress<string> segmentProgress = new(AppendTranscriptionText);

            // Give the caret a clean starting line so streamed text doesn't run into existing content.
            EnsureTranscriptionInsertionPoint();

            for (int i = 0; i < audioFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string audioFile = audioFiles[i];

                if (multiple)
                    AppendTranscriptionText($"# {Path.GetFileName(audioFile)}{Environment.NewLine}");

                int fileIndex = i;
                Progress<double> clipProgress = new(fraction =>
                {
                    double overall = (fileIndex + fraction) / audioFiles.Count;
                    TranscriptionProgressBar.Visibility = Visibility.Visible;
                    TranscriptionProgressBar.Value = overall * 100;
                    TranscriptionStatusText.Text = multiple
                        ? $"Transcribing audio files… ({fileIndex + 1}/{audioFiles.Count}) {fraction:P0}"
                        : $"Transcribing audio… {fraction:P0}";
                    overallProgress?.Report(overall);
                });

                string transcription = await AudioTranscriptionUtilities.TranscribeAudioFileAsync(
                    audioFile, hotWords, statusProgress, segmentProgress,
                    includeTimecodes: DefaultSettings.IncludeTimecodesInTranscription,
                    clipProgress: clipProgress, cancellationToken);

                Singleton<HistoryService>.Instance.RecordRecentFile(audioFile, OpenContentKind.TextFile);

                if (string.IsNullOrWhiteSpace(transcription))
                    AppendTranscriptionText("(no speech recognized)");

                // Blank line between files (and after the last, trimmed on sync).
                AppendTranscriptionText(Environment.NewLine + Environment.NewLine);
            }
        }
        catch (OperationCanceledException)
        {
            // User cancelled: everything already streamed into the editor is kept — no error dialog.
            cancelled = true;
            AudioDebugLog.Write("TranscribeAudioFilesAsync: cancelled by user; keeping transcribed-so-far text");
        }
        catch (Exception ex)
        {
            // Surface the real reason (model not ready, unsupported format, etc.) instead of failing silently.
            Debug.WriteLine($"Audio transcription error: {ex}");
            AudioDebugLog.Write($"TranscribeAudioFilesAsync: ERROR {ex}");
            errorMessage = ex.Message;
        }
        finally
        {
            PassedTextControl.IsReadOnly = previousIsReadOnly;
            TranscriptionStatusBar.Visibility = Visibility.Collapsed;
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;

            // The transcript was streamed into the raw text box, so the active editor is what needs
            // updating. Syncing the other way (as this used to) pushed the markdown document / table
            // back over Session.Text, round-tripping the whole transcript through the
            // serializer for nothing.
            SyncActiveEditorFromText();
        }

        AudioDebugLog.Write($"TranscribeAudioFilesAsync: END (cancelled={cancelled}, error={errorMessage is not null})");

        if (errorMessage is not null)
        {
            // A failure raised as the window closes has nowhere to show: the dialog would be owned by
            // a torn-down window.
            if (IsLoaded)
                await new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Audio Transcription Failed",
                    Content = errorMessage,
                    CloseButtonText = "OK"
                }.ShowDialogAsync();
        }
        else if (!cancelled && DefaultSettings.NotifyOnTranscriptionComplete)
        {
            string fileDescription = multiple
                ? $"{audioFiles.Count} files"
                : Path.GetFileName(audioFiles[0]);
            NotificationUtilities.ShowTranscriptionCompleteToast(fileDescription, WindowId);
        }
    }

    private void TranscriptionCancelButton_Click(object sender, RoutedEventArgs e) => CancelAudioTranscription();

    /// <summary>
    /// Requests cancellation of the running audio-file transcription. Text already streamed into the
    /// editor is preserved. Called from the status bar's own Cancel button, and externally by
    /// <see cref="OpenMediaWindow"/> so its Cancel button can stop a transcription it started.
    /// </summary>
    internal void CancelAudioTranscription()
    {
        if (_transcriptionCts is null)
            return;

        TranscriptionCancelButton.IsEnabled = false;
        TranscriptionStatusText.Text = "Cancelling…";
        _transcriptionCts.Cancel();
    }

    /// <summary>
    /// Ensures the caret sits on a fresh line at the end of the document before streaming begins, so
    /// the first transcribed segment doesn't run into whatever text is already there.
    /// </summary>
    private void EnsureTranscriptionInsertionPoint()
    {
        string existingText = Session.Text;
        if (!string.IsNullOrEmpty(existingText) && existingText[^1] is not '\n' and not '\r')
            Session.AppendText(Environment.NewLine);

        Session.Selection = new TextSpan(Session.Text.Length, 0);
        Session.ScrollToEnd();
    }

    /// <summary>
    /// Appends a streamed transcription fragment to the end of the editor. Works while the editor is
    /// read-only (AppendText bypasses the read-only guard), keeping the caret and view at the end.
    /// </summary>
    private void AppendTranscriptionText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Session.AppendText(text);
        Session.Selection = new TextSpan(Session.Text.Length, 0);
        Session.ScrollToEnd();
    }

    /// <summary>
    /// Inserts transcribed text into the raw-text editor at the caret, adding a leading separator
    /// when appending to existing content.
    /// </summary>
    private void InsertTranscribedText(string text, bool separateWithSpace = false)
    {
        if (string.IsNullOrEmpty(text))
            return;

        string existingText = Session.Text;
        if (!string.IsNullOrEmpty(existingText) && Session.Selection.Start == existingText.Length)
        {
            char lastChar = existingText[^1];
            if (separateWithSpace)
            {
                if (!char.IsWhiteSpace(lastChar))
                    text = " " + text;
            }
            else if (lastChar is not '\n' and not '\r')
            {
                text = Environment.NewLine + text;
            }
        }

        AddCopiedTextToTextBox(text);

        // Same as the file path: the phrase went into the raw text box, so the sync has to run in that
        // direction. Pulling from the active editor instead round-tripped every phrase through the
        // markdown/table serializer.
        SyncActiveEditorFromText();
    }

    private async Task StopLiveTranscriptionAndCloseAsync()
    {
        try
        {
            // Invalidate a pending start before awaiting it, then flush before either the
            // pending-file prompt or history snapshots read the document.
            ++_liveTranscriptionRequest;
            LiveTranscriptionToggleButton.IsChecked = false;
            await StopLiveTranscriptionAsync(dispose: true);
        }
        finally
        {
            _isStoppingLiveTranscriptionForClose = false;
        }

        if (!_isClosed)
            Close();
    }

    private async void LiveTranscriptionToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        int request = ++_liveTranscriptionRequest;
        LiveCaptureSource source = _liveCaptureSource;
        bool showStartFailure = false;
        await _liveTranscriptionGate.WaitAsync();
        try
        {
            if (request != _liveTranscriptionRequest || _isStoppingLiveTranscriptionForClose || _isClosed)
                return;

            _liveTranscriber ??= new LiveAudioTranscriber();
            _liveTranscriber.PhraseRecognized -= LiveTranscriber_PhraseRecognized;
            _liveTranscriber.PhraseRecognized += LiveTranscriber_PhraseRecognized;

            SetLiveTranscriptionUi(true, source switch
            {
                LiveCaptureSource.SystemAudio => "Starting (system)…",
                LiveCaptureSource.MicrophoneAndSystemAudio => "Starting (mic + system)…",
                _ => "Starting…",
            });

            bool started;
            try
            {
                started = await _liveTranscriber.StartAsync(source);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Live transcription failed to start: {ex}");
                started = false;
            }

            // A newer toggle or close owns the next transition. In particular, don't detach
            // here: its stop still needs to receive the final phrase from this session.
            if (request != _liveTranscriptionRequest || _isStoppingLiveTranscriptionForClose || _isClosed)
                return;

            if (started)
            {
                SetLiveTranscriptionUi(true);
            }
            else
            {
                SetLiveTranscriptionUi(false);
                LiveTranscriptionToggleButton.IsChecked = false;
                showStartFailure = true;
            }
        }
        finally
        {
            _liveTranscriptionGate.Release();
        }

        if (showStartFailure && !_isStoppingLiveTranscriptionForClose && !_isClosed)
        {
            string reason = source switch
            {
                LiveCaptureSource.SystemAudio => "Couldn't capture system audio. Make sure a playback device is active.",
                LiveCaptureSource.MicrophoneAndSystemAudio => "Couldn't start microphone and system audio capture. Make sure a microphone is connected, Text Grab has microphone access in Windows privacy settings, and a playback device is active.",
                _ => "Couldn't start microphone capture. Make sure a microphone is connected and that Text Grab has microphone access in Windows privacy settings.",
            };
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Couldn't Start Transcription",
                Content = reason,
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
    }

    private async void LiveTranscriptionToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        ++_liveTranscriptionRequest;
        SetLiveTranscriptionUi(false);
        await StopLiveTranscriptionAsync();
    }

    private async Task StopLiveTranscriptionAsync(bool dispose = false)
    {
        await _liveTranscriptionGate.WaitAsync();
        try
        {
            LiveAudioTranscriber? transcriber = _liveTranscriber;
            if (transcriber is null)
                return;

            await StopAndDrainLiveTranscriptionAsync(transcriber, LiveTranscriber_PhraseRecognized, Dispatcher);

            if (dispose)
            {
                transcriber.Dispose();
                _liveTranscriber = null;
            }
        }
        finally
        {
            _liveTranscriptionGate.Release();
        }
    }

    internal static async Task StopAndDrainLiveTranscriptionAsync(
        LiveAudioTranscriber transcriber,
        EventHandler<string> phraseHandler,
        Dispatcher dispatcher)
    {
        try
        {
            await transcriber.StopAsync().ConfigureAwait(false);
            // StopAsync waits for recognition, not its BeginInvoke deliveries. Queue a barrier
            // behind those Normal-priority callbacks before detaching or allowing another start.
            if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            {
                try
                {
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Normal).Task.ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    // Shutdown can abort the barrier; there is no live editor left to drain.
                }
            }
        }
        finally
        {
            transcriber.PhraseRecognized -= phraseHandler;
        }
    }

    private void CaptureTranscribeAudioMenuItem_Click(object sender, RoutedEventArgs e)
    {
        bool transcribe = CaptureTranscribeAudioMenuItem.IsChecked;
        if (LiveTranscriptionToggleButton.IsChecked != transcribe)
            LiveTranscriptionToggleButton.IsChecked = transcribe;
    }

    private void OpenAudioVideoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenMediaWindow openMediaWindow = new() { Owner = this };
        openMediaWindow.Show();
    }

    private void LiveSourceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string tag
            || !Enum.TryParse(tag, out LiveCaptureSource selectedSource))
            return;

        _liveCaptureSource = selectedSource;
        LiveSourceMicMenuItem.IsChecked = selectedSource == LiveCaptureSource.Microphone;
        LiveSourceSystemMenuItem.IsChecked = selectedSource == LiveCaptureSource.SystemAudio;
        LiveSourceBothMenuItem.IsChecked = selectedSource == LiveCaptureSource.MicrophoneAndSystemAudio;
        CaptureLiveSourceMicMenuItem.IsChecked = selectedSource == LiveCaptureSource.Microphone;
        CaptureLiveSourceSystemMenuItem.IsChecked = selectedSource == LiveCaptureSource.SystemAudio;
        CaptureLiveSourceBothMenuItem.IsChecked = selectedSource == LiveCaptureSource.MicrophoneAndSystemAudio;

        // If a session is already running, restart it on the newly chosen source.
        if (LiveTranscriptionToggleButton.IsChecked is true)
        {
            LiveTranscriptionToggleButton.IsChecked = false; // stops via Unchecked
            LiveTranscriptionToggleButton.IsChecked = true;  // restarts via Checked with new source
        }
        else
        {
            SetLiveTranscriptionUi(false);
        }
    }

    /// <summary>
    /// Rebuilds a "Transcription model" flyout — shared by the Capture menu and the mic button's
    /// right-click context menu — every time it's opened. These two flyouts drive live (near-real-time)
    /// transcription, so they only ever offer <see cref="WhisperModelInfo.LiveEligibleModels"/> — a
    /// small, fixed set of fast models, always shown (whether downloaded yet or not) and always
    /// checkable, so the menu looks and behaves the same from a first run with nothing downloaded
    /// through to having everything downloaded. A trailing "More models..." item opens the full Models
    /// settings page, where the larger, file-transcription-only models live. Rebuilding on open (instead
    /// of keeping static XAML items in sync by hand) means the check mark can never go stale.
    /// </summary>
    private void TranscriptionModelSubmenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem submenu)
            return;

        submenu.Items.Clear();

        WhisperModelChoice current = AudioTranscriptionUtilities.CurrentLiveModelChoice;
        foreach (WhisperModelChoice choice in WhisperModelInfo.LiveEligibleModels)
        {
            MenuItem item = new()
            {
                Header = WhisperModelInfo.DisplayName(choice),
                IsCheckable = true,
                IsChecked = choice == current,
                Tag = choice.ToString(),
            };
            item.Click += TranscriptionModelMenuItem_Click;
            submenu.Items.Add(item);
        }

        submenu.Items.Add(new Separator());

        MenuItem moreItem = new() { Header = "More models..." };
        moreItem.Click += MoreModelsMenuItem_Click;
        submenu.Items.Add(moreItem);
    }

    /// <summary>
    /// Picks a live-transcription model: switches to it immediately, restarting a running session so
    /// the new model takes effect, and — if it isn't downloaded yet — fetches it in the background
    /// (live-eligible models are all small, so this finishes quickly) with a toast on completion or
    /// failure.
    /// </summary>
    private async void TranscriptionModelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string tag)
            return;

        WhisperModelChoice choice = WhisperModelInfo.Parse(tag);
        DefaultSettings.LiveTranscriptionModel = tag;
        DefaultSettings.Save();

        bool wasRunning = LiveTranscriptionToggleButton.IsChecked is true;
        if (wasRunning)
        {
            // Restarting loads the new model via AcquireFactoryAsync, which downloads it if needed.
            LiveTranscriptionToggleButton.IsChecked = false; // stops via Unchecked
            LiveTranscriptionToggleButton.IsChecked = true;  // restarts via Checked with new model
        }

        if (wasRunning || AudioTranscriptionUtilities.IsModelDownloaded(choice))
            return;

        try
        {
            await AudioTranscriptionUtilities.DownloadModelAsync(choice);
            NotificationUtilities.ShowModelDownloadCompleteToast(WhisperModelInfo.DisplayName(choice), WindowId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Transcription model download failed: {ex}");
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Download failed",
                Content = $"Couldn't download the \"{WhisperModelInfo.DisplayName(choice)}\" model:\n{ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
    }

    /// <summary>Opens (or activates) Settings and navigates straight to the Models page.</summary>
    private void MoreModelsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SettingsWindow settingsWindow = WindowUtilities.OpenOrActivateWindow<SettingsWindow>();
        settingsWindow.SettingsNavView.Navigate(typeof(ModelsSettings));
    }

    private void TranscribeJustIconMenuItem_Click(object sender, RoutedEventArgs e)
    {
        bool justIcon = TranscribeJustIconMenuItem.IsChecked;
        DefaultSettings.TranscribeButtonJustIcon = justIcon;
        DefaultSettings.Save();
        LiveTranscriptionLabel.Visibility = justIcon ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LiveTranscriber_PhraseRecognized(object? sender, string recognizedText)
    {
        // Recognition events arrive on a background thread; marshal to the UI thread without
        // blocking the recognizer (BeginInvoke, not Invoke).
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosed)
                return;

            string phrase = recognizedText.Trim();
            if (phrase.Length == 0)
                return;

            // Insert at the end of the document, separating phrases with a single space.
            Session.Selection = new TextSpan(Session.Text.Length, 0);
            InsertTranscribedText(phrase, separateWithSpace: true);
        });
    }

    private void SetLiveTranscriptionUi(bool active, string? label = null)
    {
        bool systemAudio = _liveCaptureSource == LiveCaptureSource.SystemAudio;
        bool both = _liveCaptureSource == LiveCaptureSource.MicrophoneAndSystemAudio;
        CaptureTranscribeAudioMenuItem.IsChecked = active;

        if (label is not null)
            LiveTranscriptionLabel.Text = label;
        else if (active)
            LiveTranscriptionLabel.Text = both ? "Listening (mic + system)…" : systemAudio ? "Listening (system)…" : "Listening…";
        else
            LiveTranscriptionLabel.Text = both ? "Transcribe (mic + system)" : systemAudio ? "Transcribe (system)" : "Transcribe";

        // Speaker icon for system audio, mic icon for microphone (and mic+system); pulse variant while active.
        LiveTranscriptionIcon.Symbol = systemAudio && !both
            ? SymbolRegular.Speaker224
            : (active ? SymbolRegular.MicPulse24 : SymbolRegular.Mic24);

        if (active)
            LiveTranscriptionIcon.Foreground = System.Windows.Media.Brushes.OrangeRed;
        else
            LiveTranscriptionIcon.ClearValue(ForegroundProperty);
    }
}
