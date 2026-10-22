using System.Collections.Concurrent;
using Text_Grab.Interfaces;
using Text_Grab.Services;

namespace Tests;

public class TtsServiceTests
{
    [Fact]
    public async Task DrainCallbackQueuingSpeech_DoesNotPublishIdleBetweenRequests()
    {
        ControlledTtsEngine engine = new();
        TtsService service = new() { Engine = engine };
        ConcurrentQueue<bool> busyEvents = new();
        TaskCompletionSource idle = new(TaskCreationOptions.RunContinuationsAsynchronously);

        service.BusyChanged += isBusy =>
        {
            busyEvents.Enqueue(isBusy);
            if (!isBusy)
                idle.TrySetResult();
        };

        service.Speak("first");
        await engine.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        service.RunWhenIdle(() => service.Speak("second"));
        engine.ReleaseFirst.TrySetResult();
        await engine.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal([true], busyEvents);

        engine.ReleaseSecond.TrySetResult();
        await idle.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal([true, false], busyEvents);
    }

    private sealed class ControlledTtsEngine : ITtsEngine
    {
        private int callCount;

        public TaskCompletionSource FirstStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirst { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseSecond { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SpeakAsync(string text, CancellationToken ct)
        {
            int call = Interlocked.Increment(ref callCount);
            TaskCompletionSource started = call == 1 ? FirstStarted : SecondStarted;
            TaskCompletionSource release = call == 1 ? ReleaseFirst : ReleaseSecond;

            started.TrySetResult();
            await release.Task.WaitAsync(ct);
        }
    }
}
