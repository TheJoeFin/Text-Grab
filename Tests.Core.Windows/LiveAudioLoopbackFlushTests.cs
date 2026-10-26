using NAudio.Wave;
using Text_Grab.Utilities;
using static Text_Grab.Utilities.LiveAudioTranscriber;

namespace Text_Grab.Tests.Core.Windows;

public sealed class LiveAudioLoopbackFlushTests
{
    [Fact]
    public async Task NoPackets_CommitsTailAfterIdleDelayOnlyOnce()
    {
        ManualTimeProvider clock = new();
        CaptureBuffer buffer = CreateBuffer(clock);
        buffer.Write(PcmSeconds(1));
        int transcriptions = 0;
        Task<string> Transcribe(ReadOnlyMemory<float> _)
        {
            transcriptions++;
            return Task.FromResult(" final words ");
        }

        Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        Assert.Equal(0, transcriptions);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("final words", await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        Assert.Empty(buffer.GetSnapshot().Audio);

        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        Assert.Equal(1, transcriptions);
    }

    [Fact]
    public async Task OngoingPackets_RestartIdleDeadline()
    {
        ManualTimeProvider clock = new();
        CaptureBuffer buffer = CreateBuffer(clock);
        buffer.Write(PcmSeconds(1));

        for (int i = 0; i < 5; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(400));
            buffer.Write(PcmSeconds(0.1));
            Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        }

        clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("tail", await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
    }

    [Fact]
    public async Task PacketReceivedDuringVad_InvalidatesIdleSnapshot()
    {
        ManualTimeProvider clock = new();
        CaptureBuffer buffer = CreateBuffer(clock);
        buffer.Write(PcmSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));

        Task<IReadOnlyList<SpeechSegment>> DetectAndReceivePacket(float[] samples)
        {
            buffer.Write(PcmSeconds(0.1));
            return DetectTail(samples);
        }

        Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectAndReceivePacket, Transcribe));
        Assert.Equal(PcmSeconds(1.1).Length, buffer.GetSnapshot().Audio.Length);

        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal("tail", await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
    }

    [Fact]
    public async Task PacketReceivedDuringTranscription_IsPreservedForNextUtterance()
    {
        ManualTimeProvider clock = new();
        CaptureBuffer buffer = CreateBuffer(clock);
        buffer.Write(PcmSeconds(1));
        clock.Advance(TimeSpan.FromMilliseconds(500));

        Task<string> TranscribeAndReceivePacket(ReadOnlyMemory<float> _)
        {
            buffer.Write(PcmSeconds(0.6));
            return Task.FromResult("first");
        }

        Assert.Equal("first", await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, TranscribeAndReceivePacket));
        Assert.Equal(PcmSeconds(0.6).Length, buffer.GetSnapshot().Audio.Length);
        Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));

        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal("tail", await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        Assert.Empty(buffer.GetSnapshot().Audio);
    }

    [Theory]
    [InlineData(LiveCaptureSource.Microphone)]
    [InlineData(LiveCaptureSource.MicrophoneAndSystemAudio)]
    public async Task OtherSources_DoNotForceCompletionForPacketGaps(LiveCaptureSource source)
    {
        ManualTimeProvider clock = new();
        CaptureBuffer microphone = CreateBuffer(clock);
        CaptureBuffer loopback = CreateBuffer(clock);
        microphone.Write(PcmSeconds(1));
        loopback.Write(PcmSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(5));
        CaptureBuffer[] buffers = source == LiveCaptureSource.Microphone ? [microphone] : [microphone, loopback];

        Assert.Empty(await ProcessBufferedAudioAsync(buffers, source, DetectTail, Transcribe));
        Assert.NotEmpty(microphone.GetSnapshot().Audio);
        Assert.Equal("tail", await ProcessBufferedAudioAsync(buffers, source, DetectTail, Transcribe, flush: true));
    }

    [Fact]
    public async Task ClearAndRestart_ResetIdleTimingIncludingShortUtterances()
    {
        ManualTimeProvider clock = new();
        CaptureBuffer buffer = CreateBuffer(clock);
        buffer.Write(PcmSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(5));
        buffer.Clear();

        Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        buffer.Write(PcmSeconds(0.3));
        Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("tail", await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));

        CaptureBuffer restarted = CreateBuffer(clock);
        restarted.Write(PcmSeconds(0.3));
        Assert.Empty(await ProcessBufferedAudioAsync([restarted], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal("tail", await ProcessBufferedAudioAsync([restarted], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
    }

    [Fact]
    public async Task NaturalTrailingSilence_CompletesWithoutIdleGap()
    {
        ManualTimeProvider clock = new();
        CaptureBuffer buffer = CreateBuffer(clock);
        buffer.Write(PcmSeconds(1));
        Task<IReadOnlyList<SpeechSegment>> DetectCompletedSpeech(float[] _) =>
            Task.FromResult<IReadOnlyList<SpeechSegment>>([new(TimeSpan.Zero, TimeSpan.FromSeconds(0.6))]);

        Assert.Equal("tail", await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectCompletedSpeech, Transcribe));
        Assert.Equal(PcmSeconds(0.4).Length, buffer.GetSnapshot().Audio.Length);
    }

    [Fact]
    public async Task IdleSilence_IsDrainedWithoutTranscription()
    {
        ManualTimeProvider clock = new();
        CaptureBuffer buffer = CreateBuffer(clock);
        buffer.Write(PcmSeconds(1));
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Task<IReadOnlyList<SpeechSegment>> DetectSilence(float[] _) =>
            Task.FromResult<IReadOnlyList<SpeechSegment>>([]);
        Task<string> UnexpectedTranscription(ReadOnlyMemory<float> _) =>
            throw new InvalidOperationException("Silence must not be transcribed.");

        Assert.Empty(await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectSilence, UnexpectedTranscription));
        Assert.Empty(buffer.GetSnapshot().Audio);
    }

    [Fact]
    public async Task EmptyPacket_DoesNotPostponeIdleFlush()
    {
        ManualTimeProvider clock = new();
        CaptureBuffer buffer = CreateBuffer(clock);
        buffer.Write(PcmSeconds(1));
        clock.Advance(TimeSpan.FromMilliseconds(400));
        buffer.Write([]);
        clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal("tail", await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
    }

    [Fact]
    public async Task StopFlush_CommitsShortUtteranceWithoutWaitingForIdle()
    {
        ManualTimeProvider clock = new();
        CaptureBuffer buffer = CreateBuffer(clock);
        buffer.Write(PcmSeconds(0.3));

        Assert.Equal("tail", await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe, flush: true));
        Assert.Empty(buffer.GetSnapshot().Audio);
    }

    [Fact]
    public async Task MaximumUtterance_StillCompletesWithActivePackets()
    {
        ManualTimeProvider clock = new();
        CaptureBuffer buffer = CreateBuffer(clock);
        buffer.Write(PcmSeconds(20));

        Assert.Equal("tail", await ProcessBufferedAudioAsync([buffer], LiveCaptureSource.SystemAudio, DetectTail, Transcribe));
        Assert.Empty(buffer.GetSnapshot().Audio);
    }

    private static CaptureBuffer CreateBuffer(TimeProvider clock) => new(new WaveFormat(16000, 16, 1), clock);

    private static byte[] PcmSeconds(double seconds) => new byte[(int)(seconds * 16000) * 2];

    private static Task<IReadOnlyList<SpeechSegment>> DetectTail(float[] samples) =>
        Task.FromResult<IReadOnlyList<SpeechSegment>>([new(TimeSpan.Zero, TimeSpan.FromSeconds(samples.Length / 16000.0))]);

    private static Task<string> Transcribe(ReadOnlyMemory<float> _) => Task.FromResult("tail");

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

        internal void Advance(TimeSpan duration) => Interlocked.Add(ref _timestamp, duration.Ticks);
    }
}
