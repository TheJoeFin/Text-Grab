using System.Reflection;
using System.Windows.Threading;
using Text_Grab;
using Text_Grab.Utilities;

namespace Tests;

public class EditTextWindowLiveTranscriptionTests
{
    [WpfFact]
    public async Task StopAndDrainLiveTranscriptionAsync_KeepsFinalPhraseHandlerUntilStopFinishes()
    {
        using LiveAudioTranscriber transcriber = new();
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        List<string> delivered = [];
        EventHandler<string> handler = (_, phrase) => dispatcher.BeginInvoke(() => delivered.Add(phrase));
        transcriber.PhraseRecognized += handler;

        // Hold the real stop at its lifecycle gate, without opening an audio device or model.
        SemaphoreSlim lifecycleGate = GetLifecycleGate(transcriber);
        await lifecycleGate.WaitAsync();
        Task stop = EditTextWindow.StopAndDrainLiveTranscriptionAsync(transcriber, handler, dispatcher);
        try
        {
            Assert.False(stop.IsCompleted);
            await Task.Run(() => GetPhraseHandlers(transcriber)?.Invoke(transcriber, "final speech"));
            Assert.NotNull(GetPhraseHandlers(transcriber));
        }
        finally
        {
            lifecycleGate.Release();
            await stop;
        }

        Assert.Equal(["final speech"], delivered);
        Assert.Null(GetPhraseHandlers(transcriber));
    }

    [WpfFact]
    public async Task StopAndDrainLiveTranscriptionAsync_DrainsQueuedPhrasesBeforeDetaching()
    {
        using LiveAudioTranscriber transcriber = new();
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        List<string> delivered = [];
        bool subscribedDuringDelivery = false;
        EventHandler<string> handler = (_, phrase) => dispatcher.BeginInvoke(() =>
        {
            subscribedDuringDelivery = GetPhraseHandlers(transcriber) is not null;
            delivered.Add(phrase);
        });
        transcriber.PhraseRecognized += handler;

        GetPhraseHandlers(transcriber)!.Invoke(transcriber, "last queued phrase");
        Task stop = EditTextWindow.StopAndDrainLiveTranscriptionAsync(transcriber, handler, dispatcher);

        Assert.False(stop.IsCompleted);
        Assert.Empty(delivered);
        Assert.NotNull(GetPhraseHandlers(transcriber));
        await stop;

        Assert.True(subscribedDuringDelivery);
        Assert.Equal(["last queued phrase"], delivered);
        Assert.Null(GetPhraseHandlers(transcriber));
    }

    [WpfFact]
    public async Task StopAndDrainLiveTranscriptionAsync_AllowsResubscribingWithoutDuplicatingPhrases()
    {
        using LiveAudioTranscriber transcriber = new();
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        List<string> delivered = [];
        EventHandler<string> handler = (_, phrase) => dispatcher.BeginInvoke(() => delivered.Add(phrase));

        transcriber.PhraseRecognized += handler;
        GetPhraseHandlers(transcriber)!.Invoke(transcriber, "first session");
        await EditTextWindow.StopAndDrainLiveTranscriptionAsync(transcriber, handler, dispatcher);

        transcriber.PhraseRecognized += handler;
        GetPhraseHandlers(transcriber)!.Invoke(transcriber, "second session");
        await EditTextWindow.StopAndDrainLiveTranscriptionAsync(transcriber, handler, dispatcher);

        Assert.Equal(["first session", "second session"], delivered);
        Assert.Null(GetPhraseHandlers(transcriber));
    }

    private static SemaphoreSlim GetLifecycleGate(LiveAudioTranscriber transcriber)
    {
        FieldInfo field = typeof(LiveAudioTranscriber).GetField("_lifecycleLock", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsType<SemaphoreSlim>(field.GetValue(transcriber));
    }

    private static EventHandler<string>? GetPhraseHandlers(LiveAudioTranscriber transcriber)
    {
        FieldInfo field = typeof(LiveAudioTranscriber).GetField(nameof(LiveAudioTranscriber.PhraseRecognized), BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (EventHandler<string>?)field.GetValue(transcriber);
    }
}
