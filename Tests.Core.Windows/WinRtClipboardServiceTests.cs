using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Text_Grab.Services;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// The system clipboard and <see cref="UiThreadAccess"/> are both process-global, so these never
/// run alongside other tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ClipboardCollectionDefinition
{
    public const string Name = "Clipboard";
}

/// <summary>
/// <see cref="WinRtClipboardService"/> (v5 F4) in a host with no UI thread. The fake-backend tests
/// pin which thread the clipboard calls run on; one test uses the real clipboard and restores
/// whatever text it held.
/// </summary>
[Collection(ClipboardCollectionDefinition.Name)]
public sealed class WinRtClipboardServiceTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task RealClipboard_RoundTripsText_WithNoUiThread()
    {
        Assert.False(UiThreadAccess.IsConfigured);
        using WinRtClipboardService clipboard = new();
        CancellationToken ct = TestContext.Current.CancellationToken;

        string? previous = await clipboard.GetTextAsync(ct);
        string marker = $"Text Grab clipboard test {Guid.NewGuid():N}";
        try
        {
            await clipboard.SetTextAsync(marker, ct);

            Assert.Equal(marker, await clipboard.GetTextAsync(ct));
            Assert.NotNull(clipboard.WorkerThreadId);
        }
        finally
        {
            if (previous is not null)
                await clipboard.SetTextAsync(previous, CancellationToken.None);
        }
    }

    [Fact]
    public async Task MtaCaller_WithNoUiThread_RunsOnTheServicesStaThread()
    {
        Recorder recorder = new();
        using WinRtClipboardService clipboard = new(recorder.Read, recorder.Write, Short, Short);

        await clipboard.SetTextAsync("written", TestContext.Current.CancellationToken);
        string? text = await clipboard.GetTextAsync(TestContext.Current.CancellationToken);

        Assert.Equal("written", text);
        Assert.Equal(ApartmentState.STA, recorder.Apartment);
        Assert.Equal(clipboard.WorkerThreadId, recorder.ThreadId);
    }

    [Fact]
    public void StaCaller_RunsInline()
    {
        Recorder recorder = new();
        using WinRtClipboardService clipboard = new(recorder.Read, recorder.Write, Short, Short);
        int staThreadId = 0;
        bool completedSynchronously = false;

        Thread sta = new(() =>
        {
            staThreadId = Environment.CurrentManagedThreadId;
            Task write = clipboard.SetTextAsync("inline");
            completedSynchronously = write.IsCompletedSuccessfully;
        });
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();
        sta.Join();

        Assert.True(completedSynchronously);
        Assert.Equal(staThreadId, recorder.ThreadId);
        Assert.Null(clipboard.WorkerThreadId);
    }

    [Fact]
    public async Task RegisteredUiThread_RunsTheOperation()
    {
        using FakeUiThread ui = new();
        Recorder recorder = new();
        using WinRtClipboardService clipboard = new(recorder.Read, recorder.Write, TimeSpan.FromSeconds(10), Short);
        try
        {
            UiThreadAccess.SetPoster(ui.Post);

            await clipboard.SetTextAsync("via ui", TestContext.Current.CancellationToken);

            Assert.Equal(ui.ThreadId, recorder.ThreadId);
            Assert.Null(clipboard.WorkerThreadId);
        }
        finally
        {
            UiThreadAccess.ClearPoster();
        }
    }

    [Fact]
    public async Task UiPosterThatDropsTheWork_FallsBackToTheStaThread()
    {
        // What the app's poster does while there is no WPF application: accept and drop.
        Recorder recorder = new();
        using WinRtClipboardService clipboard = new(recorder.Read, recorder.Write, Short, Short);
        try
        {
            UiThreadAccess.SetPoster(_ => { });

            await clipboard.SetTextAsync("fallback", TestContext.Current.CancellationToken);

            Assert.Equal("fallback", recorder.Text);
            Assert.Equal(clipboard.WorkerThreadId, recorder.ThreadId);
            Assert.Equal(1, recorder.Writes);
        }
        finally
        {
            UiThreadAccess.ClearPoster();
        }
    }

    [Fact]
    public async Task BusyClipboard_IsRetried()
    {
        int attempts = 0;
        using WinRtClipboardService clipboard = new(
            () => ++attempts < 3 ? throw ClipboardBusy() : "finally",
            _ => { },
            Short,
            TimeSpan.FromMilliseconds(1));

        string? text = await clipboard.GetTextAsync(TestContext.Current.CancellationToken);

        Assert.Equal("finally", text);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task BusyClipboard_GivesUpAfterTheLastAttempt()
    {
        int attempts = 0;
        using WinRtClipboardService clipboard = new(
            () => { attempts++; throw ClipboardBusy(); },
            _ => { },
            Short,
            TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAsync<COMException>(() => clipboard.GetTextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(WinRtClipboardService.MaxAttempts, attempts);
    }

    [Fact]
    public async Task OtherFailures_AreNotRetried()
    {
        int attempts = 0;
        using WinRtClipboardService clipboard = new(
            () => { attempts++; throw new InvalidOperationException("not a busy clipboard"); },
            _ => { },
            Short,
            TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => clipboard.GetTextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task AlreadyCancelledToken_TouchesNothing()
    {
        Recorder recorder = new();
        using WinRtClipboardService clipboard = new(recorder.Read, recorder.Write, Short, Short);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => clipboard.SetTextAsync("x", new CancellationToken(canceled: true)));
        Assert.Equal(0, recorder.Writes);
    }

    private static COMException ClipboardBusy() => new("CLIPBRD_E_CANT_OPEN", unchecked((int)0x800401D0));

    private sealed class Recorder
    {
        public string? Text { get; private set; }
        public int Writes { get; private set; }
        public int? ThreadId { get; private set; }
        public ApartmentState? Apartment { get; private set; }

        public string? Read()
        {
            Record();
            return Text;
        }

        public void Write(string text)
        {
            Record();
            Text = text;
            Writes++;
        }

        private void Record()
        {
            ThreadId = Environment.CurrentManagedThreadId;
            Apartment = Thread.CurrentThread.GetApartmentState();
        }
    }

    /// <summary>A stand-in UI thread: an STA thread that runs whatever is posted to it.</summary>
    private sealed class FakeUiThread : IDisposable
    {
        private readonly BlockingCollection<Action> _queue = [];
        private readonly Thread _thread;

        public FakeUiThread()
        {
            _thread = new Thread(() =>
            {
                foreach (Action action in _queue.GetConsumingEnumerable())
                    action();
            })
            { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public int ThreadId => _thread.ManagedThreadId;

        public void Post(Action action) => _queue.Add(action);

        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join();
        }
    }
}
