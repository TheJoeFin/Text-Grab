using Text_Grab.Core.Abstractions;
using Text_Grab.Services;
using Text_Grab.Tests.Core;

namespace Tests;

/// <summary>
/// <see cref="ClipboardMonitorService"/>'s fan-out and self-suppression (v5 F5), driven by a fake
/// change source, a fake clipboard reader and a fake clock. The real WinRT event is never touched.
/// The reader completes synchronously, so each <see cref="Copy"/> runs the whole handler.
/// </summary>
public class ClipboardMonitorServiceTests
{
    private readonly FakeChangeSource _source = new();
    private readonly ManualTimeProvider _time = new();
    private readonly List<TriggerPayload> _triggered = [];
    private string? _clipboardText;
    private Exception? _readFailure;
    private int _rawChanges;
    private int _reads;

    [Fact]
    public void ExternalCopy_RaisesBothEvents_WithTheText()
    {
        using ClipboardMonitorService monitor = CreateStarted();

        Copy("copied in another app");

        Assert.Equal(1, _rawChanges);
        Assert.Equal(new TriggerPayload(TriggerKind.ClipboardChanged, Text: "copied in another app"), Assert.Single(_triggered));
    }

    [Fact]
    public void NonTextContent_TriggersWithNullText()
    {
        using ClipboardMonitorService monitor = CreateStarted();

        Copy(null);

        Assert.Equal(new TriggerPayload(TriggerKind.ClipboardChanged), Assert.Single(_triggered));
    }

    [Fact]
    public void SelfWrite_IsSkippedForTriggers_ButStillReachesRawListeners()
    {
        using ClipboardMonitorService monitor = CreateStarted();

        using (monitor.SuppressSelf("grabbed text"))
            Copy("grabbed text");

        Assert.Empty(_triggered);
        Assert.Equal(1, _rawChanges);
    }

    [Fact]
    public void SelfWrite_NotificationsArrivingAfterTheScope_AreSkippedWithinTheWindow()
    {
        // One write can notify twice (SetContent, then Flush), and the notifications are queued
        // after the write has returned.
        using ClipboardMonitorService monitor = CreateStarted();

        monitor.SuppressSelf("grabbed text").Dispose();
        _time.Advance(ClipboardMonitorService.SuppressionWindow / 2);
        Copy("grabbed text");
        Copy("grabbed text");

        Assert.Empty(_triggered);
        Assert.Equal(2, _rawChanges);
    }

    [Fact]
    public void AfterTheWindow_TheSameTextTriggersAgain()
    {
        using ClipboardMonitorService monitor = CreateStarted();

        monitor.SuppressSelf("grabbed text").Dispose();
        _time.Advance(ClipboardMonitorService.SuppressionWindow + TimeSpan.FromMilliseconds(1));
        Copy("grabbed text");

        Assert.Equal("grabbed text", Assert.Single(_triggered).Text);
    }

    [Fact]
    public void UserCopiesSomethingElse_DuringTheWindow_StillTriggers()
    {
        using ClipboardMonitorService monitor = CreateStarted();

        using (monitor.SuppressSelf("grabbed text"))
        {
            Copy("grabbed text");
            Copy("what the user copied meanwhile");
        }

        Assert.Equal("what the user copied meanwhile", Assert.Single(_triggered).Text);
    }

    [Fact]
    public void SuppressionWithoutText_SkipsEveryChangeUntilTheWindowCloses()
    {
        using ClipboardMonitorService monitor = CreateStarted();

        monitor.SuppressSelf().Dispose();
        Copy("anything");
        _time.Advance(ClipboardMonitorService.SuppressionWindow + TimeSpan.FromMilliseconds(1));
        Copy("later");

        Assert.Equal("later", Assert.Single(_triggered).Text);
    }

    [Fact]
    public void UnreadableClipboard_IsSkippedWhileASuppressionIsLive_AndTriggersOtherwise()
    {
        using ClipboardMonitorService monitor = CreateStarted();
        _readFailure = new System.Runtime.InteropServices.COMException("CLIPBRD_E_CANT_OPEN", unchecked((int)0x800401D0));

        using (monitor.SuppressSelf("grabbed text"))
            Copy("unreadable");
        _time.Advance(ClipboardMonitorService.SuppressionWindow * 2);
        Copy("unreadable");

        Assert.Equal(new TriggerPayload(TriggerKind.ClipboardChanged), Assert.Single(_triggered));
    }

    [Fact]
    public void WithNoTriggerListener_TheClipboardIsNotRead()
    {
        using ClipboardMonitorService monitor = new(_source, Read, _time);
        monitor.ContentChanged += (_, _) => _rawChanges++;
        monitor.Start();

        Copy("x");

        Assert.Equal(1, _rawChanges);
        Assert.Equal(0, _reads);
    }

    [Fact]
    public void Start_SubscribesOnce_AndStopUnsubscribes()
    {
        using ClipboardMonitorService monitor = CreateStarted();
        monitor.Start();
        Assert.Equal(1, _source.Subscribers);

        monitor.Stop();
        Copy("ignored");

        Assert.Equal(0, _source.Subscribers);
        Assert.Equal(0, _rawChanges);
        Assert.Empty(_triggered);
    }

    [Fact]
    public void AThrowingListener_DoesNotStopLaterChanges()
    {
        using ClipboardMonitorService monitor = CreateStarted();
        monitor.Triggered += (_, _) => throw new InvalidOperationException("listener bug");

        Copy("first");
        Copy("second");

        Assert.Equal(2, _rawChanges);
        Assert.Equal(["first", "second"], _triggered.Select(p => p.Text));
    }

    [Fact]
    public async Task AppClipboardService_MarksItsWritesAsSelf_AndStaysSynchronous()
    {
        using ClipboardMonitorService monitor = CreateStarted();
        FakeClipboard inner = new(onWrite: Copy);
        SelfSuppressingClipboardService clipboard = new(inner, monitor);

        Task write = clipboard.SetTextAsync("grab result", TestContext.Current.CancellationToken);

        Assert.True(write.IsCompletedSuccessfully);
        await write;
        Assert.Equal("grab result", inner.Text);
        Assert.Empty(_triggered);
        Assert.Equal(1, _rawChanges);

        Copy("user copy");
        Assert.Equal("user copy", Assert.Single(_triggered).Text);

        // Reads pass straight through to the inner service.
        Assert.Equal("grab result", await clipboard.GetTextAsync(TestContext.Current.CancellationToken));
    }

    private ClipboardMonitorService CreateStarted()
    {
        ClipboardMonitorService monitor = new(_source, Read, _time);
        monitor.ContentChanged += (_, _) => _rawChanges++;
        monitor.Triggered += (_, payload) => _triggered.Add(payload);
        monitor.Start();
        return monitor;
    }

    private Task<string?> Read()
    {
        _reads++;
        return _readFailure is null ? Task.FromResult(_clipboardText) : Task.FromException<string?>(_readFailure);
    }

    private void Copy(string? text)
    {
        _clipboardText = text;
        _source.Raise();
    }

    private sealed class FakeChangeSource : IClipboardChangeSource
    {
        private EventHandler<object>? _changed;

        public int Subscribers { get; private set; }

        public event EventHandler<object>? Changed
        {
            add
            {
                _changed += value;
                Subscribers++;
            }
            remove
            {
                _changed -= value;
                Subscribers--;
            }
        }

        public void Raise() => _changed?.Invoke(null, new object());
    }

    /// <summary>Completes synchronously, like the real service does on the UI thread.</summary>
    private sealed class FakeClipboard(Action<string> onWrite) : IClipboardService
    {
        public string? Text { get; private set; }

        public Task<string?> GetTextAsync(CancellationToken ct = default) => Task.FromResult(Text);

        public Task SetTextAsync(string text, CancellationToken ct = default)
        {
            Text = text;
            onWrite(text);
            return Task.CompletedTask;
        }
    }
}
