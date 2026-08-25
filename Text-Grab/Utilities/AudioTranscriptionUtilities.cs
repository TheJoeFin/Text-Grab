using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;
using NAudio.Utils;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    /// <summary>Rolled over into <c>audio-debug.prev.log</c> once the live file passes this size.</summary>
    private const long MaxLogBytes = 1024 * 1024;

    private static long _writtenBytes = -1;   // -1 until the size of an existing log is read once

    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Text-Grab", "Logs");

    /// <summary>Stable per-user log path so a run can be found and collected after the fact.</summary>
    public static string LogPath { get; } = Path.Combine(LogDirectory, "audio-debug.log");

    /// <summary>The previous log, kept so a rollover mid-run doesn't lose the start of the session.</summary>
    private static string PreviousLogPath { get; } = Path.Combine(LogDirectory, "audio-debug.prev.log");

    public static void Write(string message)
    {
        // Environment.WorkingSet, not a Process object: a cached Process reports whatever it last
        // refreshed, and a fresh Process.GetCurrentProcess() per logged line is not free.
        long workingSetMb = 0;
        try { workingSetMb = Environment.WorkingSet / (1024 * 1024); } catch { }

        string line = $"{DateTime.Now:HH:mm:ss.fff} [WS {workingSetMb,6} MB] {message}";
        Debug.WriteLine("[AudioTranscription] " + line);
        try
        {
            lock (_lock)
            {
                if (_writtenBytes < 0)
                {
                    Directory.CreateDirectory(LogDirectory);
                    _writtenBytes = File.Exists(LogPath) ? new FileInfo(LogPath).Length : 0;
                }

                // Roll over instead of growing without bound: this log is always on.
                if (_writtenBytes > MaxLogBytes)
                {
                    File.Move(LogPath, PreviousLogPath, overwrite: true);
                    _writtenBytes = 0;
                }

                string text = line + Environment.NewLine;
                File.AppendAllText(LogPath, text);
                _writtenBytes += Encoding.UTF8.GetByteCount(text);
            }
        }
        catch { /* logging must never throw */ }
    }
}

/// <summary>
/// One loaded <see cref="WhisperFactory"/> plus the number of leases still using it. Disposing a
/// factory frees the native model, so a factory that is superseded (the user picks another model)
/// while a transcription is still decoding against it is <see cref="Retire"/>d instead: it is
/// disposed only once the last lease is returned.
/// </summary>
internal sealed class WhisperFactoryHandle
{
    private readonly object _lock = new();
    private int _users;
    private bool _retired;
    private bool _disposed;

    internal WhisperFactory Factory { get; }
    internal WhisperModelChoice Choice { get; }

    internal WhisperFactoryHandle(WhisperFactory factory, WhisperModelChoice choice)
    {
        Factory = factory;
        Choice = choice;
    }

    internal WhisperFactoryLease Lease()
    {
        lock (_lock)
            _users++;

        return new WhisperFactoryLease(this);
    }

    /// <summary>Marks the factory superseded; it is disposed as soon as the last lease is returned.</summary>
    internal void Retire()
    {
        lock (_lock)
        {
            _retired = true;
            DisposeIfIdle();
        }
    }

    internal void Return()
    {
        lock (_lock)
        {
            _users--;
            DisposeIfIdle();
        }
    }

    /// <summary>Caller must hold <see cref="_lock"/>.</summary>
    private void DisposeIfIdle()
    {
        if (_disposed || !_retired || _users > 0)
            return;

        _disposed = true;
        AudioDebugLog.Write($"WhisperFactoryHandle: disposing retired factory for {Choice}");
        try { Factory.Dispose(); } catch { }
    }
}

/// <summary>
/// A borrowed reference to a shared <see cref="WhisperFactory"/>. The factory owns the native model
/// that every <see cref="WhisperProcessor"/> built from it decodes against, so it must outlive them:
/// hold the lease for as long as any such processor lives, and dispose it after the processor.
/// </summary>
internal sealed class WhisperFactoryLease : IDisposable
{
    private WhisperFactoryHandle? _handle;

    internal WhisperFactoryLease(WhisperFactoryHandle handle) => _handle = handle;

    internal WhisperFactory Factory =>
        (_handle ?? throw new ObjectDisposedException(nameof(WhisperFactoryLease))).Factory;

    public void Dispose() => Interlocked.Exchange(ref _handle, null)?.Return();
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

    private static WhisperFactoryHandle? _factoryHandle;
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

    private static string ModelPathFor(WhisperModelChoice choice)
    {
        string typeName = WhisperModelInfo.GgmlTypeFor(choice).ToString().ToLowerInvariant();
        QuantizationType quantization = WhisperModelInfo.QuantizationFor(choice);
        string suffix = quantization == QuantizationType.NoQuantization ? string.Empty : $"-{quantization.ToString().ToLowerInvariant()}";
        return Path.Combine(ModelDirectory, $"ggml-{typeName}{suffix}.bin");
    }

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

    /// <summary>An <see cref="Microsoft.Win32.OpenFileDialog"/>-compatible filter string for the supported audio/A-V extensions.</summary>
    public static string GetAudioFileFilter()
    {
        string extensions = string.Join(";", AudioExtensions.Select(ext => $"*{ext}"));
        return $"Audio/video files|{extensions}|All files (*.*)|*.*";
    }

    /// <summary>Basic file info (name, size, duration) for the "what to expect" panel — no decoding.</summary>
    internal readonly record struct AudioFileInfo(string FileName, long FileSizeBytes, TimeSpan Duration);

    /// <summary>
    /// Reads the file size and duration of an audio/A-V file without decoding it, for upfront user
    /// feedback before transcription starts. Throws if the file doesn't exist or can't be opened by
    /// Media Foundation (e.g. an unsupported or corrupt format) — callers should show that inline.
    /// </summary>
    internal static AudioFileInfo GetAudioFileInfo(string audioFilePath)
    {
        if (!File.Exists(audioFilePath))
            throw new FileNotFoundException("Audio file not found.", audioFilePath);

        long fileSizeBytes = new FileInfo(audioFilePath).Length;

        MediaFoundationApi.Startup();
        using MediaFoundationReader reader = new(audioFilePath);
        TimeSpan duration = reader.TotalTime;

        return new AudioFileInfo(Path.GetFileName(audioFilePath), fileSizeBytes, duration);
    }

    /// <summary>
    /// Whisper runs on the CPU on every supported Windows build (x64 / arm64, packaged or not), so
    /// audio transcription is always available. The model is fetched on first use.
    /// </summary>
    public static bool IsAudioTranscriptionSupported() => true;

    /// <summary>True once the selected Whisper model has been downloaded and is available locally.</summary>
    public static bool IsModelDownloaded() => IsModelDownloaded(CurrentModelChoice);

    /// <summary>True once the given Whisper model has been downloaded and is available locally.</summary>
    public static bool IsModelDownloaded(WhisperModelChoice choice) => File.Exists(ModelPathFor(choice));

    /// <summary>The on-disk size of an already-downloaded model, or null if it hasn't been downloaded yet.</summary>
    public static long? DownloadedModelSizeBytes(WhisperModelChoice choice)
    {
        string path = ModelPathFor(choice);
        return File.Exists(path) ? new FileInfo(path).Length : null;
    }

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
        QuantizationType quantization = WhisperModelInfo.QuantizationFor(choice);
        AudioDebugLog.Write($"EnsureModelDownloadedAsync: downloading Whisper '{ggmlType}' ({quantization}) model to {modelPath}");
        progress?.Report($"Downloading speech model ({WhisperModelInfo.DisplayName(choice)}, first run)…");

        string tempPath = modelPath + ".download";
        try
        {
            using (Stream modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType, quantization, cancellationToken).ConfigureAwait(false))
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
    /// Borrows the shared, cached <see cref="WhisperFactory"/> for the currently selected model,
    /// downloading the model if needed. The factory is expensive to create (it loads the model), so
    /// it is created once and reused. If the model choice changes, the old factory is retired and a
    /// new one is loaded — see <see cref="WhisperFactoryLease"/> for why the caller must hold the
    /// lease for as long as it uses processors built from the factory.
    /// </summary>
    internal static async Task<WhisperFactoryLease> AcquireFactoryAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        WhisperModelChoice choice = CurrentModelChoice;

        await _factoryLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_factoryHandle is not null && _factoryHandle.Choice == choice)
                return _factoryHandle.Lease();

            if (_factoryHandle is not null)
            {
                AudioDebugLog.Write($"AcquireFactoryAsync: model changed {_factoryHandle.Choice} -> {choice}, reloading");
                _factoryHandle.Retire();
                _factoryHandle = null;
            }

            string modelPath = await EnsureModelDownloadedAsync(choice, progress, cancellationToken).ConfigureAwait(false);
            AudioDebugLog.Write($"AcquireFactoryAsync: loading WhisperFactory for {choice} ({WhisperModelInfo.GgmlTypeFor(choice)})");
            _factoryHandle = new WhisperFactoryHandle(WhisperFactory.FromPath(modelPath), choice);
            AudioDebugLog.Write("AcquireFactoryAsync: WhisperFactory ready");
            return _factoryHandle.Lease();
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
    /// <paramref name="hotWords"/>, if given, is passed to Whisper as an initial prompt so it's biased
    /// toward names/jargon it might otherwise mishear; it applies only to this call, nothing persists.
    /// When <paramref name="includeTimecodes"/> is true, each segment is prefixed with its start time
    /// (e.g. <c>[01:23]</c>) and placed on its own line.
    /// <paramref name="clipProgress"/>, if given, reports how far playback has reached through the
    /// clip (0.0-1.0) after each segment, based on that segment's end time versus the clip's total
    /// duration — lets callers show a real progress bar instead of an indeterminate spinner.
    /// </summary>
    public static async Task<string> TranscribeAudioFileAsync(string audioFilePath, string? hotWords = null, IProgress<string>? statusProgress = null, IProgress<string>? segmentProgress = null, CancellationToken cancellationToken = default, bool includeTimecodes = false, IProgress<double>? clipProgress = null)
    {
        AudioDebugLog.Write($"TranscribeAudioFileAsync: START path='{audioFilePath}'");

        if (!File.Exists(audioFilePath))
            throw new FileNotFoundException("Audio file not found.", audioFilePath);

        // includeTimecodes needs Whisper's per-segment output; Windows AI Speech's RecognizeFromFile
        // returns one flat string, so that request always goes straight to Whisper below.
        if (!includeTimecodes && CurrentEngine == TranscriptionEngine.WindowsAiSpeech && await WindowsAiSpeechTranscriptionUtilities.RefreshSupportAsync().ConfigureAwait(false))
        {
            statusProgress?.Report("Transcribing audio (Windows AI Speech, NPU)…");
            string? winAiResult = await WindowsAiSpeechTranscriptionUtilities.TranscribeAudioFileAsync(audioFilePath, cancellationToken).ConfigureAwait(false);
            if (winAiResult is not null)
            {
                segmentProgress?.Report(winAiResult);
                clipProgress?.Report(1.0);
                AudioDebugLog.Write($"TranscribeAudioFileAsync: DONE via Windows AI Speech, result length={winAiResult.Length}");
                return winAiResult;
            }

            AudioDebugLog.Write("TranscribeAudioFileAsync: Windows AI Speech unavailable/failed, falling back to Whisper");
            statusProgress?.Report("Windows AI Speech unavailable - falling back to local Whisper…");
        }

        long fileSizeKb = new FileInfo(audioFilePath).Length / 1024;
        AudioDebugLog.Write($"TranscribeAudioFileAsync: file exists, size={fileSizeKb} KB, ext={Path.GetExtension(audioFilePath)}");

        // Whisper + audio decoding are CPU-bound; run off the UI thread.
        return await Task.Run(async () =>
        {
            // The lease is held for the whole decode: a model change (or a live session starting) part
            // way through must not free the native model this processor is still reading.
            using WhisperFactoryLease factoryLease = await AcquireFactoryAsync(statusProgress, cancellationToken).ConfigureAwait(false);

            statusProgress?.Report("Transcribing audio…");
            AudioDebugLog.Write("TranscribeAudioFileAsync: decoding audio to 16 kHz mono WAV");
            using MemoryStream wavStream = DecodeToWav16kMono(audioFilePath);
            AudioDebugLog.Write($"TranscribeAudioFileAsync: decoded WAV bytes={wavStream.Length}");

            // 16 kHz mono 16-bit PCM, 44-byte WAV header: 32,000 bytes/second of audio.
            double clipTotalSeconds = Math.Max(0, wavStream.Length - 44) / 32000.0;

            Stopwatch stopwatch = Stopwatch.StartNew();
            // Without this, whisper.cpp conditions each ~30s decode window on the text it just produced
            // for the previous window. That's fine for continuity, but once a window decodes badly
            // (applause, silence, cross-talk, a speaker handoff) the garbage becomes the prompt for the
            // next window, and whisper.cpp is prone to spiraling into repeated garbage tokens once it's
            // conditioned on its own bad output — corrupting the rest of a long file instead of just the
            // one bad segment. WithNoContext() decodes each window independently so a bad patch stays
            // contained to that patch. Matches the live path (see LiveAudioTranscriber.StartAsync).
            WhisperProcessorBuilder processorBuilder = factoryLease.Factory.CreateBuilder()
                .WithLanguage(WhisperModelInfo.LanguageFor(CurrentModelChoice))
                .WithThreads(Math.Max(1, Environment.ProcessorCount - 1))
                .WithNoContext();

            // CarryInitialPrompt re-applies the hot words to every decode window (not just the first),
            // so the bias holds across long files instead of fading out after the first segment.
            if (!string.IsNullOrWhiteSpace(hotWords))
                processorBuilder = processorBuilder.WithPrompt(hotWords.Trim()).WithCarryInitialPrompt(true);

            await using WhisperProcessor processor = processorBuilder.Build();

            StringBuilder builder = new();
            int segmentCount = 0;
            await foreach (SegmentData segment in processor.ProcessAsync(wavStream, cancellationToken).ConfigureAwait(false))
            {
                string segmentText = includeTimecodes
                    ? $"[{FormatTimecode(segment.Start)}]{segment.Text}{Environment.NewLine}"
                    : segment.Text;

                builder.Append(segmentText);
                segmentProgress?.Report(segmentText);
                segmentCount++;

                if (clipTotalSeconds > 0)
                    clipProgress?.Report(Math.Clamp(segment.End.TotalSeconds / clipTotalSeconds, 0.0, 1.0));
            }

            clipProgress?.Report(1.0);

            stopwatch.Stop();
            string text = CleanTranscript(builder.ToString());
            AudioDebugLog.Write($"TranscribeAudioFileAsync: DONE in {stopwatch.ElapsedMilliseconds} ms, {segmentCount} segments, result length={text.Length}");
            return text;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Formats a segment's start time as <c>mm:ss</c>, or <c>h:mm:ss</c> once past an hour.</summary>
    internal static string FormatTimecode(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");

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
        while ((read = resampler.Read(buffer)) > 0)
        {
            for (int i = 0; i + 1 < read; i += 2)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                samples.Add(sample / 32768f);
            }
        }
        return samples.ToArray();
    }

    /// <summary>
    /// Sums same-length-padded per-channel samples into one stream, clamping to [-1, 1] so two loud
    /// sources can't clip beyond either engine's expected range. A single channel is returned
    /// unchanged. Shared by <see cref="LiveAudioTranscriber"/> (Whisper) and
    /// <see cref="LiveWindowsAiSpeechTranscriber"/> (Windows AI Speech).
    /// </summary>
    internal static float[] MixChannels(List<float[]> perChannelSamples)
    {
        if (perChannelSamples.Count == 1)
            return perChannelSamples[0];

        int length = 0;
        foreach (float[] samples in perChannelSamples)
            length = Math.Max(length, samples.Length);

        float[] mixed = new float[length];
        foreach (float[] samples in perChannelSamples)
            for (int i = 0; i < samples.Length; i++)
                mixed[i] += samples[i];

        for (int i = 0; i < mixed.Length; i++)
            mixed[i] = Math.Clamp(mixed[i], -1f, 1f);

        return mixed;
    }

    /// <summary>Converts normalized [-1, 1] float samples back to 16-bit mono PCM bytes (the inverse of the fast path in <see cref="ConvertToSamples16kMono"/>) — the shape <see cref="Microsoft.Windows.AI.Speech.SpeechAudioProvider.PushData"/> consumes.</summary>
    internal static byte[] SamplesToPcm16(float[] samples)
    {
        byte[] pcm = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)Math.Clamp(samples[i] * 32768f, short.MinValue, short.MaxValue);
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return pcm;
    }

    /// <summary>The transcription engine currently selected in settings (defaults to local Whisper).</summary>
    public static TranscriptionEngine CurrentEngine => AppUtilities.TextGrabSettings.AudioTranscriptionEngine switch
    {
        "WindowsAiSpeech" => TranscriptionEngine.WindowsAiSpeech,
        _ => TranscriptionEngine.Whisper,
    };
}

/// <summary>The engine <see cref="LiveAudioTranscriber"/>/<see cref="LiveWindowsAiSpeechTranscriber"/> and file transcription run on.</summary>
public enum TranscriptionEngine
{
    /// <summary>Local Whisper (whisper.cpp) via Whisper.net. CPU-based, always available.</summary>
    Whisper,

    /// <summary>The experimental Microsoft.Windows.AI.Speech engine, gated to Copilot+ PC / NPU-capable hardware. Falls back to Whisper whenever unavailable or it fails.</summary>
    WindowsAiSpeech,
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

    // Q5_0 keeps the small English-only models fast and cheap to load with negligible WER impact.
    // Multilingual models use the near-lossless Q8_0 instead, since quantization hurts accuracy more
    // on the less-represented languages those models exist to cover.
    public static QuantizationType QuantizationFor(WhisperModelChoice choice) => choice switch
    {
        WhisperModelChoice.TinyEnglish or WhisperModelChoice.BaseEnglish => QuantizationType.Q5_0,
        _ => QuantizationType.Q8_0,
    };

    public static string DisplayName(WhisperModelChoice choice) => choice switch
    {
        WhisperModelChoice.TinyEnglish => "Fastest — English",
        WhisperModelChoice.BaseEnglish => "Fast — English",
        WhisperModelChoice.SmallMultilingual => "Most accurate — multilingual",
        _ => "Balanced — multilingual",
    };

    /// <summary>Longer description of the speed/accuracy/language tradeoff, shown once a model is picked.</summary>
    public static string Description(WhisperModelChoice choice) => choice switch
    {
        WhisperModelChoice.TinyEnglish =>
            "The smallest and fastest model here. English speech only, and the least accurate — best for quick drafts where speed matters more than getting every word right.",
        WhisperModelChoice.BaseEnglish =>
            "Still fast, with noticeably better accuracy than the tiny model. English speech only.",
        WhisperModelChoice.SmallMultilingual =>
            "The most accurate model here, but the slowest to process. Automatically detects the spoken language and covers dozens beyond English.",
        _ =>
            "A good default: balances speed and accuracy. Automatically detects the spoken language and covers dozens beyond English.",
    };

    /// <summary>Short label for the language coverage, shown alongside <see cref="Description"/>.</summary>
    public static string LanguageSummary(WhisperModelChoice choice) =>
        choice is WhisperModelChoice.TinyEnglish or WhisperModelChoice.BaseEnglish
            ? "English only"
            : "Multilingual — auto-detects language";
}

/// <summary>Where <see cref="LiveAudioTranscriber"/> pulls audio from.</summary>
public enum LiveCaptureSource
{
    /// <summary>The default microphone / recording device.</summary>
    Microphone,

    /// <summary>System output ("what you hear") via WASAPI loopback on the default render device.</summary>
    SystemAudio,

    /// <summary>Both the microphone and system output, mixed into a single stream before transcription.</summary>
    MicrophoneAndSystemAudio,
}

/// <summary>
/// Near-live transcription with Whisper from the microphone, system output (WASAPI loopback), or
/// both at once (mixed into a single stream), gated by Silero voice-activity detection. Instead of
/// transcribing fixed time windows (which waste compute on silence and cut words mid-phrase), it
/// buffers audio, runs cheap VAD on a short cadence to find speech regions, and only sends a region
/// to Whisper once it's complete (trailing silence detected). Each completed utterance raises
/// <see cref="PhraseRecognized"/>. Events fire on background threads; subscribers must marshal to
/// their UI thread.
/// </summary>
/// <summary>
/// A single capture device feeding a live transcriber (microphone or system-audio loopback), with its
/// own raw-PCM buffer so concurrent sources never share captured bytes. Shared between
/// <see cref="LiveAudioTranscriber"/> (Whisper) and <see cref="LiveWindowsAiSpeechTranscriber"/>
/// (Windows AI Speech) so the mic/loopback capture and lifecycle handling exists in exactly one place.
/// </summary>
internal sealed class AudioCaptureChannel
{
    public WaveFormat SourceFormat { get; }
    public MemoryStream PcmBuffer { get; } = new();
    public object BufferLock { get; } = new();
    private readonly IWaveIn? _waveIn;
    private readonly WasapiRecorder? _wasapiRecorder;
    private readonly EventHandler<WaveInEventArgs>? _waveInDataAvailableHandler;
    private readonly CaptureDataAvailableHandler? _wasapiDataAvailableHandler;

    public AudioCaptureChannel(IWaveIn capture)
    {
        _waveIn = capture;
        SourceFormat = capture.WaveFormat;
        _waveInDataAvailableHandler = (_, e) =>
        {
            lock (BufferLock)
                PcmBuffer.Write(e.Buffer, 0, e.BytesRecorded);
        };
        capture.DataAvailable += _waveInDataAvailableHandler;
    }

    public AudioCaptureChannel(WasapiRecorder capture)
    {
        _wasapiRecorder = capture;
        SourceFormat = capture.WaveFormat;
        _wasapiDataAvailableHandler = (buffer, _, _, _) =>
        {
            lock (BufferLock)
                PcmBuffer.Write(buffer);
        };
        capture.DataAvailable += _wasapiDataAvailableHandler;
    }

    public void StartRecording()
    {
        if (_waveIn is not null)
            _waveIn.StartRecording();
        else
            _wasapiRecorder!.StartRecording();
    }

    public void StopRecording()
    {
        if (_waveIn is not null)
            _waveIn.StopRecording();
        else
            _wasapiRecorder!.StopRecording();
    }

    public void Dispose()
    {
        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= _waveInDataAvailableHandler;
            try { _waveIn.Dispose(); } catch { }
        }
        else if (_wasapiRecorder is not null)
        {
            _wasapiRecorder.DataAvailable -= _wasapiDataAvailableHandler;
            try { _wasapiRecorder.Dispose(); } catch { }
        }
    }
}

public sealed class LiveAudioTranscriber : IDisposable
{
    private const int SampleRate = 16000;
    private const int TimerIntervalMs = 200;
    private const double MinAudioSeconds = 0.4;         // don't bother running VAD on less than this
    private const double CompletionSilenceSeconds = 0.25; // trailing silence that marks an utterance done
    private const double MaxUtteranceSeconds = 20.0;     // hard cap so a long monologue still flushes

    private readonly List<AudioCaptureChannel> _channels = new();
    private WhisperFactoryLease? _factoryLease;
    private WhisperProcessor? _processor;
    private WhisperVadProcessor? _vadProcessor;
    private readonly SemaphoreSlim _processingGate = new(1, 1);

    // Serializes StartAsync/StopAsync so a restart (e.g. changing the model or capture source while a
    // session is live) can never overlap a start with an in-flight stop. Without this, a caller that
    // fires Stop() (fire-and-forget) and then immediately awaits StartAsync() — as the source/model
    // menu handlers used to — could race: the new session's capture channels get added to the same
    // list the old session's Cleanup() is disposing/clearing on a background thread, corrupting shared
    // state and crashing the app. With the lock, StartAsync simply waits for the prior StopAsync to
    // finish flushing and cleaning up before building the new session.
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private System.Timers.Timer? _chunkTimer;
    private volatile bool _isRunning;

    // Timer.Stop() does not cancel Elapsed callbacks already queued to the thread pool, so one can
    // still take the processing gate after StopAsync's flush releases it and run against state
    // Cleanup() is tearing down. Teardown sets this, and a timed pass that sees it bails out.
    private volatile bool _stopping;
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

        // Waits out any in-flight StopAsync (e.g. a restart triggered by a model/source change) so a
        // new session never starts while the previous one is still flushing/cleaning up.
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
                AudioDebugLog.Write("LiveAudioTranscriber: no microphone capture device found");
                return false;
            }

            WhisperModelChoice choice = AudioTranscriptionUtilities.CurrentModelChoice;

            // Held for the life of the session (released in Cleanup, after the processor): the shared
            // factory owns the native model _processor decodes against.
            _factoryLease = await AudioTranscriptionUtilities.AcquireFactoryAsync(null, CancellationToken.None).ConfigureAwait(false);
            _processor = _factoryLease.Factory.CreateBuilder()
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
            // Each source gets its own channel/buffer; when both are requested they're captured
            // independently and mixed down to one stream per processing pass (see MixChannels).
            if (wantsMic)
                _channels.Add(new AudioCaptureChannel(new WaveIn { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 }));
            if (wantsSystem)
                _channels.Add(new AudioCaptureChannel(new WasapiRecorderBuilder()
                    .WithLoopbackCapture()
                    .WithBufferLength(100)
                    .Build()));

            foreach (AudioCaptureChannel channel in _channels)
                AudioDebugLog.Write($"LiveAudioTranscriber: source={source} model={choice} format={channel.SourceFormat.Encoding} {channel.SourceFormat.SampleRate}Hz {channel.SourceFormat.Channels}ch {channel.SourceFormat.BitsPerSample}bit");

            foreach (AudioCaptureChannel channel in _channels)
                channel.StartRecording();

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
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Stops capturing, flushes and transcribes any remaining buffered speech, then releases
    /// resources. Awaitable so a restart (source/model change) can wait for a clean teardown. Holds
    /// the same lifecycle lock as <see cref="StartAsync"/>, so a start already waiting on this stop
    /// resumes only once the flush/cleanup below has fully finished.
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
            _chunkTimer?.Stop();

            foreach (AudioCaptureChannel channel in _channels)
                try { channel.StopRecording(); } catch { }

            // Flush whatever remains (this waits for any in-flight pass), then clean up.
            try { await ProcessBufferedChunkAsync(flush: true).ConfigureAwait(false); } catch { }

            // Tear down under the same gate the timed passes take, so a callback queued before
            // Stop() can never be inside ProcessBufferedChunkAsync while state is being disposed.
            await _processingGate.WaitAsync().ConfigureAwait(false);
            try
            {
                Cleanup();
            }
            finally
            {
                _processingGate.Release();
            }

            AudioDebugLog.Write("LiveAudioTranscriber: stopped");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>Fire-and-forget stop for callers that can't await (see <see cref="StopAsync"/>).</summary>
    public void Stop() => _ = StopAsync();

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
            // A pass queued before Stop() must not start once teardown has begun.
            if (_stopping && !flush)
                return;

            WhisperProcessor? processor = _processor;
            WhisperVadProcessor? vad = _vadProcessor;
            if (processor is null || vad is null || _channels.Count == 0)
                return;

            // Snapshot without clearing — audio keeps arriving while we work; we trim precisely later.
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
                }
                anyData = true;
                perChannelSamples.Add(AudioTranscriptionUtilities.ConvertToSamples16kMono(raw, raw.Length, channel.SourceFormat));
            }
            if (!anyData)
                return;

            float[] samples = AudioTranscriptionUtilities.MixChannels(perChannelSamples);
            double totalSeconds = samples.Length / (double)SampleRate;
            if (!flush && totalSeconds < MinAudioSeconds)
                return;

            IReadOnlyList<VadSegmentData> speech = await vad.DetectSpeechAsync(samples).ConfigureAwait(false);
            if (speech.Count == 0)
            {
                // Only silence so far: keep just the tail so the buffer doesn't grow during quiet.
                TrimAllChannelsFront(totalSeconds - 0.5);
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
                TrimAllChannelsFront(cutSeconds);

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
    /// Drops the first <paramref name="seconds"/> of buffered audio (rounded to a whole sample frame)
    /// from every capture channel. Only the consumed prefix is removed, so audio captured during
    /// processing is preserved. Each channel is trimmed using its own format, but by the same real-time
    /// duration, so mixed channels stay in sync.
    /// </summary>
    private void TrimAllChannelsFront(double seconds)
    {
        if (seconds <= 0)
            return;

        foreach (AudioCaptureChannel channel in _channels)
        {
            int bytesToRemove = (int)(seconds * channel.SourceFormat.AverageBytesPerSecond);
            int blockAlign = channel.SourceFormat.BlockAlign;
            if (blockAlign > 0)
                bytesToRemove -= bytesToRemove % blockAlign;
            if (bytesToRemove <= 0)
                continue;

            lock (channel.BufferLock)
            {
                byte[] current = channel.PcmBuffer.ToArray();
                int remove = Math.Min(bytesToRemove, current.Length);
                channel.PcmBuffer.SetLength(0);
                if (current.Length > remove)
                    channel.PcmBuffer.Write(current, remove, current.Length - remove);
            }
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

        foreach (AudioCaptureChannel channel in _channels)
            channel.Dispose();
        _channels.Clear();

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

        // Return the factory only after the processor built from it is disposed. The VAD factory is
        // shared and owned by AudioTranscriptionUtilities; nothing to release there.
        _factoryLease?.Dispose();
        _factoryLease = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}
