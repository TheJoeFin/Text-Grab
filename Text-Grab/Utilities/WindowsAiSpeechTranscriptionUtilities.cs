using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Speech;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Utilities;

/// <summary>
/// On-device audio transcription backed by the experimental <c>Microsoft.Windows.AI.Speech</c> WinRT
/// API, gated to Copilot+ PC / NPU-capable hardware via <see cref="WindowsAiUtilities.MeetsWindowsAiPrerequisites"/>.
/// This is a retry of an earlier attempt on this same hardware where <c>SpeechRecognitionModel.TryCreateAsync()</c>
/// hung forever and leaked multiple GB - traced to the Qualcomm QNN/HTP NPU execution provider throwing
/// in a retry loop while preparing the model, a runtime/EP bug outside app control. Every entry point
/// into the API here is wrapped by <see cref="RunGuardedAsync{T}"/>, a hard timeout backstop that
/// guarantees the caller's await returns even if that hang recurs, so this engine can never freeze
/// Text-Grab - callers (<see cref="AudioTranscriptionUtilities"/> and <c>EditTextWindow</c>) must treat
/// a null/false result as "fall back to Whisper", never as an error to surface directly.
/// </summary>
/// <remarks>
/// <b>Even <c>SpeechRecognitionModel.GetReadyState()</c> - a plain synchronous call, not an async
/// operation - has been observed to block the calling thread for a long time on this hardware/runtime
/// combination</b> (unlike the equivalent synchronous call for OCR/ImageDescriptionGenerator, which is
/// cheap). Calling it directly on the UI thread froze the app on startup during development. Because of
/// that, <see cref="IsSupported"/> never calls it directly and never blocks: it returns a cached answer
/// (optimistically <c>false</c> until the first probe resolves) and kicks off a one-time background
/// probe - guarded by <see cref="RunGuardedAsync{T}"/> like every other entry point here - the first
/// time it's asked. Callers that need a real, waited-for answer (an actual transcription attempt, not
/// just UI gating) should <c>await</c> <see cref="RefreshSupportAsync"/> instead.
/// </remarks>
public static class WindowsAiSpeechTranscriptionUtilities
{
    private static readonly TimeSpan ReadyStateTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ModelReadyTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan FileRecognitionTimeout = TimeSpan.FromSeconds(120);
    internal static readonly TimeSpan RecognitionStartTimeout = TimeSpan.FromSeconds(15);

    // Once a timeout is observed, the engine stops retrying for the rest of the process: a recurrence
    // of the known hang means every subsequent call is likely to hang too, and re-attempting it live
    // (e.g. on every live-transcription toggle) would just re-leak memory each time.
    private static volatile bool _enginePoisoned;

    // -1 = not probed yet, 0 = unsupported, 1 = supported. Plain int so reads/writes are atomic without
    // a lock; the actual probe is single-flighted via _supportProbeLock/_supportProbe below.
    private static volatile int _supportedCache = -1;
    private static Task<bool>? _supportProbe;
    private static readonly object _supportProbeLock = new();

    /// <summary>
    /// Raised after a background support probe (triggered by <see cref="IsSupported"/> or
    /// <see cref="RefreshSupportAsync"/>) completes, so UI that already rendered the optimistic "not
    /// supported yet" answer can refresh itself. Fires on a background thread; subscribers must marshal
    /// to their UI thread.
    /// </summary>
    public static event EventHandler? SupportChanged;

    private static SpeechRecognitionModel? _model;
    private static readonly SemaphoreSlim _modelLock = new(1, 1);

    /// <summary>
    /// True when this device is known to support Windows AI Speech transcription. Never blocks: if
    /// support hasn't been determined yet, this returns false and kicks off a one-time background probe
    /// (see the type-level remarks) - subscribe to <see cref="SupportChanged"/> or await
    /// <see cref="RefreshSupportAsync"/> to learn the real answer once it resolves.
    /// </summary>
    public static bool IsSupported()
    {
        if (_enginePoisoned)
            return false;

        int cached = _supportedCache;
        if (cached != -1)
            return cached == 1;

        _ = RefreshSupportAsync();
        return false;
    }

    /// <summary>
    /// Awaits the real, guarded support answer - the first call performs the (single-flighted) probe;
    /// later calls return the cached result instantly. Use this (not <see cref="IsSupported"/>) before
    /// an actual transcription attempt, since callers there are already async and can afford to wait
    /// out the guard's timeout for an accurate answer instead of a possibly-stale cached one.
    /// </summary>
    public static Task<bool> RefreshSupportAsync()
    {
        if (_enginePoisoned)
            return Task.FromResult(false);

        lock (_supportProbeLock)
            return _supportProbe ??= ProbeSupportAsync();
    }

    private static async Task<bool> ProbeSupportAsync()
    {
        bool supported = await ProbeSupportCoreAsync().ConfigureAwait(false);
        _supportedCache = supported ? 1 : 0;
        SupportChanged?.Invoke(null, EventArgs.Empty);
        return supported;
    }

    private static async Task<bool> ProbeSupportCoreAsync()
    {
        try
        {
            if (!WindowsAiUtilities.MeetsWindowsAiPrerequisites())
                return false;
        }
        catch (Exception ex)
        {
            AudioDebugLog.Write($"WindowsAiSpeech: prerequisite check failed: {ex.Message}");
            return false;
        }

        (bool gotState, AIFeatureReadyState state) = await RunGuardedAsync(
            ct => Task.Run(SpeechRecognitionModel.GetReadyState, ct),
            ReadyStateTimeout, "GetReadyState (support probe)").ConfigureAwait(false);

        return gotState && state != AIFeatureReadyState.NotSupportedOnCurrentSystem;
    }

    /// <summary>
    /// Borrows the shared, cached <see cref="SpeechRecognitionModel"/>, creating it on first use. This
    /// is exactly where the known hang previously occurred (<c>GetReadyState</c>/<c>EnsureReadyAsync</c>/
    /// <c>TryCreateAsync</c>), so every call goes through <see cref="RunGuardedAsync{T}"/>. Returns null
    /// when unsupported, unavailable, or a guarded call failed/timed out - never throws for those cases.
    /// </summary>
    internal static async Task<SpeechRecognitionModel?> AcquireModelAsync()
    {
        if (!await RefreshSupportAsync().ConfigureAwait(false))
            return null;

        if (_model is not null)
            return _model;

        await _modelLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_model is not null)
                return _model;
            if (_enginePoisoned)
                return null;

            (bool gotState, AIFeatureReadyState readyState) = await RunGuardedAsync(
                ct => Task.Run(SpeechRecognitionModel.GetReadyState, ct),
                ReadyStateTimeout, "GetReadyState").ConfigureAwait(false);

            if (!gotState)
                return null;
            if (readyState == AIFeatureReadyState.NotSupportedOnCurrentSystem)
                return null;

            if (readyState != AIFeatureReadyState.Ready)
            {
                AudioDebugLog.Write($"WindowsAiSpeech: EnsureReadyAsync (state={readyState}) - this is where the known QNN/HTP hang previously occurred");
                (bool ensured, AIFeatureReadyResult? ensureResult) = await RunGuardedAsync(
                    ct => SpeechRecognitionModel.EnsureReadyAsync().AsTask(ct),
                    ModelReadyTimeout, "EnsureReadyAsync").ConfigureAwait(false);

                if (!ensured)
                    return null;

                if (ensureResult is not null && ensureResult.Status != AIFeatureReadyResultState.Success)
                {
                    AudioDebugLog.Write($"WindowsAiSpeech: EnsureReadyAsync did not succeed: {ensureResult.Status}");
                    return null;
                }
            }

            AudioDebugLog.Write("WindowsAiSpeech: TryCreateAsync - creating SpeechRecognitionModel");
            (bool created, SpeechRecognitionModelResult? result) = await RunGuardedAsync(
                ct => SpeechRecognitionModel.TryCreateAsync().AsTask(ct),
                ModelReadyTimeout, "TryCreateAsync").ConfigureAwait(false);

            if (!created || result is null)
                return null;

            if (result.ExtendedError is not null)
            {
                AudioDebugLog.Write($"WindowsAiSpeech: TryCreateAsync returned an error: {result.ExtendedError.Message}");
                return null;
            }

            _model = result.SpeechModel;
            AudioDebugLog.Write("WindowsAiSpeech: model ready");
            return _model;
        }
        finally
        {
            _modelLock.Release();
        }
    }

    /// <summary>
    /// Transcribes a complete audio file with Windows AI Speech. Returns null (never throws for engine
    /// failures) when the engine is unsupported, unavailable, or a guarded call failed/timed out -
    /// callers must fall back to <see cref="AudioTranscriptionUtilities.TranscribeAudioFileAsync"/> in
    /// that case.
    /// </summary>
    public static async Task<string?> TranscribeAudioFileAsync(string audioFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
            throw new FileNotFoundException("Audio file not found.", audioFilePath);

        SpeechRecognitionModel? model = await AcquireModelAsync().ConfigureAwait(false);
        if (model is null)
            return null;

        AudioDebugLog.Write($"WindowsAiSpeech: RecognizeFromFile START path='{audioFilePath}'");
        using BatchRecognition batch = new(model);
        (bool ok, string? text) = await RunGuardedAsync(
            ct => batch.RecognizeFromFile(audioFilePath).AsTask(ct),
            FileRecognitionTimeout, "RecognizeFromFile").ConfigureAwait(false);

        if (!ok)
            return null;

        string cleaned = AudioTranscriptionUtilities.CleanTranscript(text ?? string.Empty);
        AudioDebugLog.Write($"WindowsAiSpeech: RecognizeFromFile DONE, result length={cleaned.Length}");
        return cleaned;
    }

    /// <summary>
    /// Runs a call into the experimental Speech API with a hard timeout backstop: even if the known
    /// QNN/HTP hang recurs and the underlying WinRT call never completes, this always returns instead
    /// of freezing the caller. A <see cref="CancellationTokenSource"/> is threaded through in case the
    /// runtime does honor cancellation, but the <see cref="Task.WhenAny(Task, Task)"/> race is what
    /// guarantees the return - cancellation alone isn't trusted given the native hang this guards
    /// against. On timeout or failure, poisons the engine for the rest of the process (every later
    /// <see cref="IsSupported"/> call returns false) and logs via <see cref="AudioDebugLog"/>.
    /// </summary>
    internal static async Task<(bool ok, T? value)> RunGuardedAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan timeout, string opName)
    {
        using CancellationTokenSource cts = new(timeout);
        Task<T> operationTask = operation(cts.Token);
        Task winner = await Task.WhenAny(operationTask, Task.Delay(timeout + TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        if (winner != operationTask)
        {
            _enginePoisoned = true;
            AudioDebugLog.Write($"WindowsAiSpeech: '{opName}' timed out after {timeout.TotalSeconds}s (known QNN/HTP hang risk) - falling back to Whisper for the rest of this session");
            return (false, default);
        }

        try
        {
            T value = await operationTask.ConfigureAwait(false);
            return (true, value);
        }
        catch (Exception ex)
        {
            AudioDebugLog.Write($"WindowsAiSpeech: '{opName}' failed: {ex.Message}");
            return (false, default);
        }
    }

    /// <summary>Void-returning overload of <see cref="RunGuardedAsync{T}"/> for WinRT <c>IAsyncAction</c> calls.</summary>
    internal static async Task<bool> RunGuardedAsync(Func<CancellationToken, Task> operation, TimeSpan timeout, string opName)
    {
        (bool ok, bool _) = await RunGuardedAsync(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return true;
        }, timeout, opName).ConfigureAwait(false);
        return ok;
    }
}

/// <summary>
/// Near-live transcription with Windows AI Speech from the microphone, system output (WASAPI
/// loopback), or both at once, mirroring <see cref="LiveAudioTranscriber"/>'s public shape so
/// <c>EditTextWindow</c> can swap engines without changing its call sites. Unlike the Whisper path,
/// there's no local VAD/buffering step: captured audio is pushed to a <see cref="SpeechAudioProvider"/>
/// as it arrives and the Windows AI Speech engine does its own endpointing, raising
/// <see cref="PhraseRecognized"/> once per finalized utterance (<see cref="StreamingRecognizedEventArgs.IsFinal"/>).
/// Events fire on background threads; subscribers must marshal to their UI thread.
/// </summary>
public sealed class LiveWindowsAiSpeechTranscriber : IDisposable
{
    private const int TimerIntervalMs = 200;

    private readonly List<AudioCaptureChannel> _channels = new();
    private SpeechAudioProvider? _audioProvider;
    private StreamingRecognition? _recognition;
    private readonly SemaphoreSlim _pushGate = new(1, 1);

    // Same rationale as LiveAudioTranscriber._lifecycleLock: a restart (source/engine change) fires
    // Stop() then immediately awaits StartAsync() without waiting for the stop to finish, so without
    // this lock a new session's channels could be added while the old session's Cleanup() is disposing
    // the same list on a background thread.
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private System.Timers.Timer? _pushTimer;
    private volatile bool _isRunning;
    private volatile bool _stopping;
    private bool _disposed;

    /// <summary>Raised with recognized text for each finalized utterance.</summary>
    public event EventHandler<string>? PhraseRecognized;

    public bool IsRunning => _isRunning;

    /// <summary>The source the current (or most recent) session is capturing from.</summary>
    public LiveCaptureSource Source { get; private set; } = LiveCaptureSource.Microphone;

    /// <summary>
    /// Starts capturing from the requested source and streaming it to Windows AI Speech. Returns false
    /// when the device/engine isn't available, the capture device is missing, or startup otherwise
    /// fails (including a guarded timeout) - callers should fall back to <see cref="LiveAudioTranscriber"/>.
    /// </summary>
    public async Task<bool> StartAsync(LiveCaptureSource source = LiveCaptureSource.Microphone)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isRunning)
                return true;

            _stopping = false;
            Source = source;

            bool wantsMic = source is LiveCaptureSource.Microphone or LiveCaptureSource.MicrophoneAndSystemAudio;
            bool wantsSystem = source is LiveCaptureSource.SystemAudio or LiveCaptureSource.MicrophoneAndSystemAudio;

            if (wantsMic && WaveIn.DeviceCount <= 0)
            {
                AudioDebugLog.Write("LiveWindowsAiSpeechTranscriber: no microphone capture device found");
                return false;
            }

            SpeechRecognitionModel? model = await WindowsAiSpeechTranscriptionUtilities.AcquireModelAsync().ConfigureAwait(false);
            if (model is null)
            {
                AudioDebugLog.Write("LiveWindowsAiSpeechTranscriber: Windows AI Speech model unavailable");
                return false;
            }

            _audioProvider = new SpeechAudioProvider();
            AudioConfiguration audioConfig = AudioConfiguration.ForProvider(_audioProvider);
            _recognition = new StreamingRecognition(audioConfig, model);
            _recognition.Recognized += OnRecognized;

            bool started = await WindowsAiSpeechTranscriptionUtilities.RunGuardedAsync(
                ct => _recognition.StartContinuousRecognitionAsync().AsTask(ct),
                WindowsAiSpeechTranscriptionUtilities.RecognitionStartTimeout, "StartContinuousRecognitionAsync").ConfigureAwait(false);

            if (!started)
            {
                Cleanup();
                return false;
            }

            // Same capture building blocks as LiveAudioTranscriber (AudioCaptureChannel is shared),
            // just fed to SpeechAudioProvider instead of buffered for VAD + Whisper.
            if (wantsMic)
                _channels.Add(new AudioCaptureChannel(new WaveIn { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 }));
            if (wantsSystem)
                _channels.Add(new AudioCaptureChannel(new WasapiRecorderBuilder()
                    .WithLoopbackCapture()
                    .WithBufferLength(100)
                    .Build()));

            foreach (AudioCaptureChannel channel in _channels)
                AudioDebugLog.Write($"LiveWindowsAiSpeechTranscriber: source={source} format={channel.SourceFormat.Encoding} {channel.SourceFormat.SampleRate}Hz {channel.SourceFormat.Channels}ch {channel.SourceFormat.BitsPerSample}bit");

            foreach (AudioCaptureChannel channel in _channels)
                channel.StartRecording();

            _pushTimer = new System.Timers.Timer(TimerIntervalMs) { AutoReset = true };
            _pushTimer.Elapsed += (_, _) => PushBufferedAudio();
            _pushTimer.Start();

            _isRunning = true;
            AudioDebugLog.Write("LiveWindowsAiSpeechTranscriber: started");
            return true;
        }
        catch (Exception ex)
        {
            AudioDebugLog.Write($"LiveWindowsAiSpeechTranscriber: failed to start ({source}): {ex.Message}");
            Cleanup();
            return false;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Stops capturing, pushes any remaining buffered audio, then releases resources. Awaitable so a
    /// restart can wait for a clean teardown - see <see cref="LiveAudioTranscriber.StopAsync"/> for why
    /// this matters.
    /// </summary>
    public async Task StopAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_isRunning && _channels.Count == 0)
                return;

            _isRunning = false;
            _stopping = true;
            _pushTimer?.Stop();

            foreach (AudioCaptureChannel channel in _channels)
                try { channel.StopRecording(); } catch { }

            try { await PushBufferedAudioAsync(flush: true).ConfigureAwait(false); } catch { }

            await _pushGate.WaitAsync().ConfigureAwait(false);
            try
            {
                Cleanup();
            }
            finally
            {
                _pushGate.Release();
            }

            AudioDebugLog.Write("LiveWindowsAiSpeechTranscriber: stopped");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>Fire-and-forget stop for callers that can't await (see <see cref="StopAsync"/>).</summary>
    public void Stop() => _ = StopAsync();

    private void OnRecognized(StreamingRecognition sender, StreamingRecognizedEventArgs args)
    {
        if (!args.IsFinal)
            return;

        string text = args.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return;

        PhraseRecognized?.Invoke(this, text);
    }

    private void PushBufferedAudio() => _ = PushBufferedAudioAsync();

    /// <summary>
    /// Drains every channel's buffered PCM, mixes it down (reusing the same conversion/mix helpers as
    /// <see cref="LiveAudioTranscriber"/>), and pushes it to <see cref="_audioProvider"/>. Unlike the
    /// Whisper path there's no VAD/segmentation here - the Speech engine endpoints on its own - so each
    /// pass simply flushes whatever has accumulated since the last one.
    /// </summary>
    private async Task PushBufferedAudioAsync(bool flush = false)
    {
        if (flush)
            await _pushGate.WaitAsync().ConfigureAwait(false);
        else if (!await _pushGate.WaitAsync(0).ConfigureAwait(false))
            return;

        try
        {
            if (_stopping && !flush)
                return;

            if (_channels.Count == 0 || _audioProvider is null)
                return;

            List<float[]> perChannelSamples = new(_channels.Count);
            bool anyData = false;
            foreach (AudioCaptureChannel channel in _channels)
            {
                byte[] raw;
                lock (channel.BufferLock)
                {
                    if (channel.PcmBuffer.Length == 0)
                    {
                        perChannelSamples.Add(Array.Empty<float>());
                        continue;
                    }
                    raw = channel.PcmBuffer.ToArray();
                    channel.PcmBuffer.SetLength(0);
                }
                anyData = true;
                perChannelSamples.Add(AudioTranscriptionUtilities.ConvertToSamples16kMono(raw, raw.Length, channel.SourceFormat));
            }
            if (!anyData)
                return;

            float[] mixed = AudioTranscriptionUtilities.MixChannels(perChannelSamples);
            if (mixed.Length == 0)
                return;

            byte[] pcm = AudioTranscriptionUtilities.SamplesToPcm16(mixed);
            _audioProvider.PushData(pcm);
        }
        catch (Exception ex)
        {
            AudioDebugLog.Write($"LiveWindowsAiSpeechTranscriber: push error: {ex.Message}");
        }
        finally
        {
            _pushGate.Release();
        }
    }

    private void Cleanup()
    {
        if (_pushTimer is not null)
        {
            _pushTimer.Stop();
            _pushTimer.Dispose();
            _pushTimer = null;
        }

        foreach (AudioCaptureChannel channel in _channels)
            channel.Dispose();
        _channels.Clear();

        if (_recognition is not null)
        {
            _recognition.Recognized -= OnRecognized;
            try { _recognition.StopContinuousRecognition(); } catch { }
            try { _recognition.Dispose(); } catch { }
            _recognition = null;
        }

        if (_audioProvider is not null)
        {
            try { _audioProvider.Dispose(); } catch { }
            _audioProvider = null;
        }

        // The SpeechRecognitionModel is shared/cached by WindowsAiSpeechTranscriptionUtilities, not
        // owned by a single live session, so it isn't disposed here.
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}
