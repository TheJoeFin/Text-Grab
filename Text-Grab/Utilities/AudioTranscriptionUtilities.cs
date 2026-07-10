using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;
using NAudio.Utils;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace Text_Grab.Utilities;

/// <summary>
/// Lightweight, always-on file logger for the audio transcription path. Writes timestamped lines
/// (with current process working set) to a stable, easy-to-find location so a run can be diagnosed
/// after the fact. Also mirrors to <see cref="Debug"/>.
/// </summary>
public static class AudioDebugLog
{
    private static readonly object _lock = new();

    /// <summary>Fixed, non-virtualized log path so it's findable after a run.</summary>
    public static string LogPath { get; } = Path.Combine(
        Environment.GetEnvironmentVariable("USERPROFILE") ?? Path.GetTempPath(),
        "TextGrab-audio-debug.log");

    public static void Write(string message)
    {
        long workingSetMb = 0;
        try { workingSetMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024); } catch { }

        string line = $"{DateTime.Now:HH:mm:ss.fff} [WS {workingSetMb,6} MB] {message}";
        Debug.WriteLine("[AudioTranscription] " + line);
        try
        {
            lock (_lock)
                File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch { /* logging must never throw */ }
    }
}

/// <summary>
/// On-device audio transcription backed by local Whisper (whisper.cpp) models via Whisper.net.
/// Runs entirely on the CPU, works packaged or unpackaged on x64 and arm64, and does not depend on
/// any experimental OS runtime. Arbitrary audio is decoded/resampled to the 16 kHz mono WAV that
/// Whisper requires using NAudio's Media Foundation reader/resampler.
/// </summary>
public static class AudioTranscriptionUtilities
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".m4a", ".aac", ".flac", ".ogg", ".oga", ".opus", ".wma", ".mp4", ".mov",
    };

    private static WhisperFactory? _whisperFactory;
    private static WhisperModelChoice _loadedModelChoice;
    private static readonly SemaphoreSlim _factoryLock = new(1, 1);

    // Silero VAD (voice activity detection) lets live transcription skip silence and cut on natural
    // speech boundaries instead of fixed time windows. The factory is small and shared.
    private static WhisperVadFactory? _vadFactory;
    private static readonly SemaphoreSlim _vadFactoryLock = new(1, 1);

    private static string ModelDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Text-Grab", "WhisperModels");

    /// <summary>The transcription model currently selected in settings (defaults to multilingual base).</summary>
    public static WhisperModelChoice CurrentModelChoice => WhisperModelInfo.Parse(AppUtilities.TextGrabSettings.AudioTranscriptionModel);

    private static string ModelPathFor(WhisperModelChoice choice) =>
        Path.Combine(ModelDirectory, $"ggml-{WhisperModelInfo.GgmlTypeFor(choice).ToString().ToLowerInvariant()}.bin");

    private static string VadModelPath => Path.Combine(ModelDirectory, "ggml-silero-vad-v5.bin");

    /// <summary>
    /// Returns true when the given path points to a file with a recognized audio (or A/V) extension.
    /// </summary>
    public static bool IsAudioFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return AudioExtensions.Contains(Path.GetExtension(path));
    }

    /// <summary>
    /// Whisper runs on the CPU on every supported Windows build (x64 / arm64, packaged or not), so
    /// audio transcription is always available. The model is fetched on first use.
    /// </summary>
    public static bool IsAudioTranscriptionSupported() => true;

    /// <summary>True once the selected Whisper model has been downloaded and is available locally.</summary>
    public static bool IsModelDownloaded() => File.Exists(ModelPathFor(CurrentModelChoice));

    /// <summary>
    /// Downloads a GGML model to LocalAppData if it isn't already present, returning its path. The
    /// download is written to a temp file first, then moved into place so a cancelled or failed
    /// download never leaves a corrupt model behind.
    /// </summary>
    private static async Task<string> EnsureModelDownloadedAsync(WhisperModelChoice choice, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        string modelPath = ModelPathFor(choice);
        if (File.Exists(modelPath))
            return modelPath;

        Directory.CreateDirectory(ModelDirectory);
        GgmlType ggmlType = WhisperModelInfo.GgmlTypeFor(choice);
        AudioDebugLog.Write($"EnsureModelDownloadedAsync: downloading Whisper '{ggmlType}' model to {modelPath}");
        progress?.Report($"Downloading speech model ({WhisperModelInfo.DisplayName(choice)}, first run)…");

        string tempPath = modelPath + ".download";
        try
        {
            using (Stream modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType).ConfigureAwait(false))
            using (FileStream fileWriter = File.Create(tempPath))
                await modelStream.CopyToAsync(fileWriter, cancellationToken).ConfigureAwait(false);

            if (File.Exists(modelPath))
                File.Delete(modelPath);
            File.Move(tempPath, modelPath);
            AudioDebugLog.Write("EnsureModelDownloadedAsync: download complete");
            return modelPath;
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }
    }

    /// <summary>
    /// Returns the shared, cached <see cref="WhisperFactory"/> for the currently selected model,
    /// downloading the model if needed. The factory is expensive to create (it loads the model), so
    /// it is created once and reused. If the model choice changes, the old factory is disposed and a
    /// new one is loaded.
    /// </summary>
    internal static async Task<WhisperFactory> GetFactoryAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        WhisperModelChoice choice = CurrentModelChoice;
        if (_whisperFactory is not null && _loadedModelChoice == choice)
            return _whisperFactory;

        await _factoryLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_whisperFactory is not null && _loadedModelChoice == choice)
                return _whisperFactory;

            if (_whisperFactory is not null)
            {
                AudioDebugLog.Write($"GetFactoryAsync: model changed {_loadedModelChoice} -> {choice}, reloading");
                try { _whisperFactory.Dispose(); } catch { }
                _whisperFactory = null;
            }

            string modelPath = await EnsureModelDownloadedAsync(choice, progress, cancellationToken).ConfigureAwait(false);
            AudioDebugLog.Write($"GetFactoryAsync: loading WhisperFactory for {choice} ({WhisperModelInfo.GgmlTypeFor(choice)})");
            _whisperFactory = WhisperFactory.FromPath(modelPath);
            _loadedModelChoice = choice;
            AudioDebugLog.Write("GetFactoryAsync: WhisperFactory ready");
            return _whisperFactory;
        }
        finally
        {
            _factoryLock.Release();
        }
    }

    /// <summary>
    /// Downloads the Silero VAD model to LocalAppData if needed (same temp-then-move pattern), then
    /// returns the shared, cached <see cref="WhisperVadFactory"/>. The VAD model is tiny (~a few MB).
    /// </summary>
    internal static async Task<WhisperVadFactory> GetVadFactoryAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (_vadFactory is not null)
            return _vadFactory;

        await _vadFactoryLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_vadFactory is not null)
                return _vadFactory;

            if (!File.Exists(VadModelPath))
            {
                Directory.CreateDirectory(ModelDirectory);
                AudioDebugLog.Write("GetVadFactoryAsync: downloading Silero VAD model");
                progress?.Report("Downloading voice-activity model…");

                string tempPath = VadModelPath + ".download";
                try
                {
                    using (Stream vadStream = await WhisperGgmlDownloader.Default.GetGgmlSileroVadModelAsync(SileroVadType.V5_1_2, cancellationToken).ConfigureAwait(false))
                    using (FileStream fileWriter = File.Create(tempPath))
                        await vadStream.CopyToAsync(fileWriter, cancellationToken).ConfigureAwait(false);

                    if (File.Exists(VadModelPath))
                        File.Delete(VadModelPath);
                    File.Move(tempPath, VadModelPath);
                }
                catch
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    throw;
                }
            }

            _vadFactory = WhisperVadFactory.FromPath(VadModelPath);
            AudioDebugLog.Write("GetVadFactoryAsync: WhisperVadFactory ready");
            return _vadFactory;
        }
        finally
        {
            _vadFactoryLock.Release();
        }
    }

    /// <summary>
    /// Transcribes a complete audio file on-device with Whisper and returns the recognized text.
    /// Whisper.net's <c>ProcessAsync</c> yields <see cref="SegmentData"/> incrementally as whisper.cpp
    /// finishes each segment of the audio, so <paramref name="segmentProgress"/> receives each
    /// segment's text as it becomes available (giving "text as it comes in" for long files). The full
    /// transcript is still returned. Cancellation stops after the current segment; every segment
    /// already surfaced via <paramref name="segmentProgress"/> is preserved by the caller.
    /// </summary>
    public static async Task<string> TranscribeAudioFileAsync(string audioFilePath, IProgress<string>? statusProgress = null, IProgress<string>? segmentProgress = null, CancellationToken cancellationToken = default)
    {
        AudioDebugLog.Write($"TranscribeAudioFileAsync: START path='{audioFilePath}'");

        if (!File.Exists(audioFilePath))
            throw new FileNotFoundException("Audio file not found.", audioFilePath);

        long fileSizeKb = new FileInfo(audioFilePath).Length / 1024;
        AudioDebugLog.Write($"TranscribeAudioFileAsync: file exists, size={fileSizeKb} KB, ext={Path.GetExtension(audioFilePath)}");

        // Whisper + audio decoding are CPU-bound; run off the UI thread.
        return await Task.Run(async () =>
        {
            WhisperFactory factory = await GetFactoryAsync(statusProgress, cancellationToken).ConfigureAwait(false);

            statusProgress?.Report("Transcribing audio…");
            AudioDebugLog.Write("TranscribeAudioFileAsync: decoding audio to 16 kHz mono WAV");
            using MemoryStream wavStream = DecodeToWav16kMono(audioFilePath);
            AudioDebugLog.Write($"TranscribeAudioFileAsync: decoded WAV bytes={wavStream.Length}");

            Stopwatch stopwatch = Stopwatch.StartNew();
            await using WhisperProcessor processor = factory.CreateBuilder()
                .WithLanguage(WhisperModelInfo.LanguageFor(CurrentModelChoice))
                .WithThreads(Math.Max(1, Environment.ProcessorCount - 1))
                .Build();

            StringBuilder builder = new();
            int segmentCount = 0;
            await foreach (SegmentData segment in processor.ProcessAsync(wavStream, cancellationToken).ConfigureAwait(false))
            {
                builder.Append(segment.Text);
                segmentProgress?.Report(segment.Text);
                segmentCount++;
            }

            stopwatch.Stop();
            string text = CleanTranscript(builder.ToString());
            AudioDebugLog.Write($"TranscribeAudioFileAsync: DONE in {stopwatch.ElapsedMilliseconds} ms, {segmentCount} segments, result length={text.Length}");
            return text;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Collapses whisper's leading spaces / stray whitespace into a tidy transcript.</summary>
    internal static string CleanTranscript(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        return raw.Replace("\r\n", "\n").Trim();
    }

    /// <summary>
    /// Decodes any Media Foundation-supported audio (wav, mp3, m4a, aac, wma, mp4, …) to a 16 kHz
    /// mono 16-bit PCM WAV in memory — the format Whisper expects.
    /// </summary>
    internal static MemoryStream DecodeToWav16kMono(string audioFilePath)
    {
        MediaFoundationApi.Startup();

        using MediaFoundationReader reader = new(audioFilePath);
        WaveFormat targetFormat = new(16000, 16, 1);

        MemoryStream memoryStream = new();
        using (MediaFoundationResampler resampler = new(reader, targetFormat) { ResamplerQuality = 60 })
        {
            // WriteWavFileToStream wraps the stream in an IgnoreDisposeStream, so memoryStream stays open.
            WaveFileWriter.WriteWavFileToStream(memoryStream, resampler);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>Wraps raw 16 kHz mono 16-bit PCM bytes in an in-memory WAV stream for Whisper.</summary>
    internal static MemoryStream PcmToWav16kMono(byte[] pcm, int count)
    {
        MemoryStream memoryStream = new();
        using (WaveFileWriter writer = new(new IgnoreDisposeStream(memoryStream), new WaveFormat(16000, 16, 1)))
            writer.Write(pcm, 0, count);

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Converts a raw captured buffer in an arbitrary <paramref name="sourceFormat"/> (e.g. the
    /// 32-bit float stereo mix from WASAPI loopback, or a mic's PCM) to a 16 kHz mono 16-bit WAV
    /// stream for Whisper. Uses a fast path when the buffer is already in Whisper's format.
    /// </summary>
    internal static MemoryStream ConvertToWav16kMono(byte[] raw, int count, WaveFormat sourceFormat)
    {
        if (sourceFormat.Encoding == WaveFormatEncoding.Pcm
            && sourceFormat.SampleRate == 16000
            && sourceFormat.Channels == 1
            && sourceFormat.BitsPerSample == 16)
        {
            return PcmToWav16kMono(raw, count);
        }

        MediaFoundationApi.Startup();
        using RawSourceWaveStream rawStream = new(new MemoryStream(raw, 0, count), sourceFormat);
        WaveFormat targetFormat = new(16000, 16, 1);

        MemoryStream memoryStream = new();
        using (MediaFoundationResampler resampler = new(rawStream, targetFormat) { ResamplerQuality = 60 })
            WaveFileWriter.WriteWavFileToStream(memoryStream, resampler);

        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>
    /// Converts a raw captured buffer in an arbitrary <paramref name="sourceFormat"/> to normalized
    /// 16 kHz mono float samples in [-1, 1] — the shape both Silero VAD and Whisper consume directly,
    /// avoiding a WAV round-trip. Uses a fast path when the buffer is already 16 kHz mono 16-bit PCM.
    /// </summary>
    internal static float[] ConvertToSamples16kMono(byte[] raw, int count, WaveFormat sourceFormat)
    {
        if (sourceFormat.Encoding == WaveFormatEncoding.Pcm
            && sourceFormat.SampleRate == 16000
            && sourceFormat.Channels == 1
            && sourceFormat.BitsPerSample == 16)
        {
            int sampleCount = count / 2;
            float[] fast = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                fast[i] = sample / 32768f;
            }
            return fast;
        }

        MediaFoundationApi.Startup();
        using RawSourceWaveStream rawStream = new(new MemoryStream(raw, 0, count), sourceFormat);
        WaveFormat targetFormat = new(16000, 16, 1);
        using MediaFoundationResampler resampler = new(rawStream, targetFormat) { ResamplerQuality = 60 };

        List<float> samples = new(count / 4);
        byte[] buffer = new byte[16000 * 2]; // ~1 second of 16-bit mono
        int read;
        while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i + 1 < read; i += 2)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                samples.Add(sample / 32768f);
            }
        }
        return samples.ToArray();
    }
}

/// <summary>The Whisper model a user can pick, trading speed for accuracy / language coverage.</summary>
public enum WhisperModelChoice
{
    /// <summary>tiny.en — fastest, English only.</summary>
    TinyEnglish,

    /// <summary>base.en — fast, English only.</summary>
    BaseEnglish,

    /// <summary>base — balanced, multilingual with auto language detection (default).</summary>
    BaseMultilingual,

    /// <summary>small — most accurate offered here, multilingual, noticeably slower.</summary>
    SmallMultilingual,
}

/// <summary>Maps <see cref="WhisperModelChoice"/> to its GGML model, language, and display name.</summary>
internal static class WhisperModelInfo
{
    public static WhisperModelChoice Parse(string? value) => value switch
    {
        "TinyEnglish" => WhisperModelChoice.TinyEnglish,
        "BaseEnglish" => WhisperModelChoice.BaseEnglish,
        "SmallMultilingual" => WhisperModelChoice.SmallMultilingual,
        _ => WhisperModelChoice.BaseMultilingual,
    };

    public static GgmlType GgmlTypeFor(WhisperModelChoice choice) => choice switch
    {
        WhisperModelChoice.TinyEnglish => GgmlType.TinyEn,
        WhisperModelChoice.BaseEnglish => GgmlType.BaseEn,
        WhisperModelChoice.SmallMultilingual => GgmlType.Small,
        _ => GgmlType.Base,
    };

    // English-only models can't language-detect, so force English; multilingual models auto-detect.
    public static string LanguageFor(WhisperModelChoice choice) => choice switch
    {
        WhisperModelChoice.TinyEnglish or WhisperModelChoice.BaseEnglish => "en",
        _ => "auto",
    };

    public static string DisplayName(WhisperModelChoice choice) => choice switch
    {
        WhisperModelChoice.TinyEnglish => "Fastest — English",
        WhisperModelChoice.BaseEnglish => "Fast — English",
        WhisperModelChoice.SmallMultilingual => "Most accurate — multilingual",
        _ => "Balanced — multilingual",
    };
}

/// <summary>Where <see cref="LiveAudioTranscriber"/> pulls audio from.</summary>
public enum LiveCaptureSource
{
    /// <summary>The default microphone / recording device.</summary>
    Microphone,

    /// <summary>System output ("what you hear") via WASAPI loopback on the default render device.</summary>
    SystemAudio,
}

/// <summary>
/// Near-live transcription with Whisper from either the microphone or system output (WASAPI
/// loopback), gated by Silero voice-activity detection. Instead of transcribing fixed time windows
/// (which waste compute on silence and cut words mid-phrase), it buffers audio, runs cheap VAD on a
/// short cadence to find speech regions, and only sends a region to Whisper once it's complete
/// (trailing silence detected). Each completed utterance raises <see cref="PhraseRecognized"/>.
/// Events fire on background threads; subscribers must marshal to their UI thread.
/// </summary>
public sealed class LiveAudioTranscriber : IDisposable
{
    private const int SampleRate = 16000;
    private const int TimerIntervalMs = 500;
    private const double MinAudioSeconds = 0.6;         // don't bother running VAD on less than this
    private const double CompletionSilenceSeconds = 0.4; // trailing silence that marks an utterance done
    private const double MaxUtteranceSeconds = 20.0;     // hard cap so a long monologue still flushes

    private IWaveIn? _capture;
    private WaveFormat? _sourceFormat;
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private WhisperVadProcessor? _vadProcessor;
    private readonly MemoryStream _pcmBuffer = new();
    private readonly object _bufferLock = new();
    private readonly SemaphoreSlim _processingGate = new(1, 1);
    private System.Timers.Timer? _chunkTimer;
    private volatile bool _isRunning;
    private bool _disposed;

    /// <summary>Raised with recognized text for each completed (VAD-delimited) utterance.</summary>
    public event EventHandler<string>? PhraseRecognized;

    public bool IsRunning => _isRunning;

    /// <summary>The source the current (or most recent) session is capturing from.</summary>
    public LiveCaptureSource Source { get; private set; } = LiveCaptureSource.Microphone;

    /// <summary>
    /// Starts capturing from the requested source (microphone or system loopback) and transcribing
    /// VAD-delimited utterances. Returns false when the device isn't available or startup otherwise
    /// fails. The Whisper and VAD models are downloaded on first use, so the first call may take a while.
    /// </summary>
    public async Task<bool> StartAsync(LiveCaptureSource source = LiveCaptureSource.Microphone)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isRunning)
            return true;

        Source = source;

        try
        {
            if (source == LiveCaptureSource.Microphone && WaveInEvent.DeviceCount <= 0)
            {
                AudioDebugLog.Write("LiveAudioTranscriber: no microphone capture device found");
                return false;
            }

            WhisperModelChoice choice = AudioTranscriptionUtilities.CurrentModelChoice;
            _factory = await AudioTranscriptionUtilities.GetFactoryAsync(null, CancellationToken.None).ConfigureAwait(false);
            _processor = _factory.CreateBuilder()
                .WithLanguage(WhisperModelInfo.LanguageFor(choice))
                .WithNoContext()   // each utterance stands alone: faster and avoids cross-phrase drift
                .WithThreads(Math.Max(1, Environment.ProcessorCount - 1))
                .Build();

            WhisperVadFactory vadFactory = await AudioTranscriptionUtilities.GetVadFactoryAsync(null, CancellationToken.None).ConfigureAwait(false);
            _vadProcessor = vadFactory.CreateBuilder()
                .WithThreshold(0.5f)
                .WithMinSpeechDuration(TimeSpan.FromMilliseconds(250))
                .WithMinSilenceDuration(TimeSpan.FromMilliseconds(300))
                .WithSpeechPadding(TimeSpan.FromMilliseconds(64))
                .WithThreads(Math.Max(1, Environment.ProcessorCount - 1))
                .Build();

            // Microphone can be forced to 16 kHz mono (WinMM converts); loopback yields the render
            // device's mix format (usually 32-bit float stereo) which we resample when processing.
            _capture = source == LiveCaptureSource.SystemAudio
                ? new WasapiLoopbackCapture()
                : new WaveInEvent { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };

            _sourceFormat = _capture.WaveFormat;
            AudioDebugLog.Write($"LiveAudioTranscriber: source={source} model={choice} format={_sourceFormat.Encoding} {_sourceFormat.SampleRate}Hz {_sourceFormat.Channels}ch {_sourceFormat.BitsPerSample}bit");

            _capture.DataAvailable += Capture_DataAvailable;
            _capture.StartRecording();

            _chunkTimer = new System.Timers.Timer(TimerIntervalMs) { AutoReset = true };
            _chunkTimer.Elapsed += async (_, _) => await ProcessBufferedChunkAsync().ConfigureAwait(false);
            _chunkTimer.Start();

            _isRunning = true;
            AudioDebugLog.Write("LiveAudioTranscriber: started");
            return true;
        }
        catch (Exception ex)
        {
            AudioDebugLog.Write($"LiveAudioTranscriber: failed to start ({source}): {ex.Message}");
            Cleanup();
            return false;
        }
    }

    /// <summary>
    /// Stops capturing, flushes and transcribes any remaining buffered speech, then releases
    /// resources. Awaitable so a restart (source/model change) can wait for a clean teardown.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning && _capture is null)
            return;

        _isRunning = false;
        _chunkTimer?.Stop();

        try { _capture?.StopRecording(); } catch { }

        // Flush whatever remains (this waits for any in-flight pass), then clean up.
        try { await ProcessBufferedChunkAsync(flush: true).ConfigureAwait(false); } catch { }

        Cleanup();
        AudioDebugLog.Write("LiveAudioTranscriber: stopped");
    }

    /// <summary>Fire-and-forget stop for callers that can't await (see <see cref="StopAsync"/>).</summary>
    public void Stop() => _ = StopAsync();

    private void Capture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_bufferLock)
            _pcmBuffer.Write(e.Buffer, 0, e.BytesRecorded);
    }

    /// <summary>
    /// Runs VAD over the buffered audio and transcribes any completed speech regions. Normal (timed)
    /// passes skip if one is already running; a <paramref name="flush"/> pass waits its turn and
    /// forces transcription of whatever speech remains.
    /// </summary>
    private async Task ProcessBufferedChunkAsync(bool flush = false)
    {
        if (flush)
            await _processingGate.WaitAsync().ConfigureAwait(false);
        else if (!await _processingGate.WaitAsync(0).ConfigureAwait(false))
            return;

        try
        {
            WaveFormat? sourceFormat = _sourceFormat;
            WhisperProcessor? processor = _processor;
            WhisperVadProcessor? vad = _vadProcessor;
            if (sourceFormat is null || processor is null || vad is null)
                return;

            // Snapshot without clearing — audio keeps arriving while we work; we trim precisely later.
            byte[] raw;
            lock (_bufferLock)
            {
                if (_pcmBuffer.Length == 0)
                    return;
                raw = _pcmBuffer.ToArray();
            }

            float[] samples = AudioTranscriptionUtilities.ConvertToSamples16kMono(raw, raw.Length, sourceFormat);
            double totalSeconds = samples.Length / (double)SampleRate;
            if (!flush && totalSeconds < MinAudioSeconds)
                return;

            IReadOnlyList<VadSegmentData> speech = await vad.DetectSpeechAsync(samples).ConfigureAwait(false);
            if (speech.Count == 0)
            {
                // Only silence so far: keep just the tail so the buffer doesn't grow during quiet.
                TrimBufferFront(totalSeconds - 0.5, sourceFormat);
                return;
            }

            bool forced = flush || totalSeconds >= MaxUtteranceSeconds;
            double cutSeconds = 0;
            StringBuilder phrase = new();

            for (int i = 0; i < speech.Count; i++)
            {
                VadSegmentData seg = speech[i];
                bool complete = forced || (totalSeconds - seg.End.TotalSeconds) >= CompletionSilenceSeconds;
                if (!complete)
                    break;   // speech still in progress: leave this and later regions buffered

                int start = Math.Max(0, (int)(seg.Start.TotalSeconds * SampleRate) - SampleRate / 20);
                int end = Math.Min(samples.Length, (int)(seg.End.TotalSeconds * SampleRate) + SampleRate / 20);
                cutSeconds = seg.End.TotalSeconds;
                if (end <= start)
                    continue;

                ReadOnlyMemory<float> slice = new(samples, start, end - start);
                StringBuilder segmentText = new();
                await foreach (SegmentData s in processor.ProcessAsync(slice, CancellationToken.None).ConfigureAwait(false))
                    segmentText.Append(s.Text);

                string cleaned = AudioTranscriptionUtilities.CleanTranscript(segmentText.ToString());
                if (cleaned.Length > 0)
                {
                    if (phrase.Length > 0)
                        phrase.Append(' ');
                    phrase.Append(cleaned);
                }
            }

            if (cutSeconds > 0)
                TrimBufferFront(cutSeconds, sourceFormat);

            if (phrase.Length > 0)
                PhraseRecognized?.Invoke(this, phrase.ToString());
        }
        catch (Exception ex)
        {
            AudioDebugLog.Write($"LiveAudioTranscriber: chunk processing error: {ex.Message}");
        }
        finally
        {
            _processingGate.Release();
        }
    }

    /// <summary>
    /// Drops the first <paramref name="seconds"/> of buffered audio (rounded to a whole sample frame).
    /// Only the consumed prefix is removed, so audio captured during processing is preserved.
    /// </summary>
    private void TrimBufferFront(double seconds, WaveFormat sourceFormat)
    {
        if (seconds <= 0)
            return;

        int bytesToRemove = (int)(seconds * sourceFormat.AverageBytesPerSecond);
        int blockAlign = sourceFormat.BlockAlign;
        if (blockAlign > 0)
            bytesToRemove -= bytesToRemove % blockAlign;
        if (bytesToRemove <= 0)
            return;

        lock (_bufferLock)
        {
            byte[] current = _pcmBuffer.ToArray();
            int remove = Math.Min(bytesToRemove, current.Length);
            _pcmBuffer.SetLength(0);
            if (current.Length > remove)
                _pcmBuffer.Write(current, remove, current.Length - remove);
        }
    }

    private void Cleanup()
    {
        if (_chunkTimer is not null)
        {
            _chunkTimer.Stop();
            _chunkTimer.Dispose();
            _chunkTimer = null;
        }

        if (_capture is not null)
        {
            _capture.DataAvailable -= Capture_DataAvailable;
            try { _capture.Dispose(); } catch { }
            _capture = null;
        }

        _sourceFormat = null;

        if (_processor is not null)
        {
            try { _processor.Dispose(); } catch { }
            _processor = null;
        }

        if (_vadProcessor is not null)
        {
            try { _vadProcessor.Dispose(); } catch { }
            _vadProcessor = null;
        }

        // _factory / VAD factory are shared and owned by AudioTranscriptionUtilities; drop references only.
        _factory = null;

        lock (_bufferLock)
            _pcmBuffer.SetLength(0);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}
