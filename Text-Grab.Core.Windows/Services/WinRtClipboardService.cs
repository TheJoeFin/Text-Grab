using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Core.Abstractions;
using Windows.ApplicationModel.DataTransfer;

namespace Text_Grab.Services;

/// <summary>
/// <see cref="IClipboardService"/> over <c>Windows.ApplicationModel.DataTransfer.Clipboard</c>
/// (v5 F4).
///
/// Clipboard calls need a single-threaded apartment. Where they run:
/// <list type="number">
/// <item>A caller already on an STA thread - the app's UI thread included - runs them inline. That
/// keeps a write from the UI thread synchronous, so code that shuts the app down right after
/// writing (as <c>OutputUtilities.HandleTextFromOcr</c> does) cannot lose it in the dispatcher
/// queue.</item>
/// <item>Any other caller posts to the UI thread through <see cref="UiThreadAccess"/> when one is
/// registered. The app's poster silently drops work while there is no WPF application, so a post
/// that has not started within a short timeout is taken back and run as in 3.</item>
/// <item>Otherwise - a windowless or headless host - they run on a dedicated background STA thread
/// that this service owns.</item>
/// </list>
///
/// Another process holding the clipboard open makes calls fail with CLIPBRD_E_CANT_*. Those are
/// retried on the clipboard thread itself, synchronously, with the budget WPF's own
/// <c>Clipboard</c> uses (10 attempts, 100 ms apart), so a write from the UI thread still finishes
/// before the call returns. Writes are flushed, so the text stays on the clipboard after the
/// process exits, as the WPF <c>Clipboard.SetDataObject(text, copy: true)</c> it replaces did.
/// </summary>
public sealed class WinRtClipboardService : IClipboardService, IDisposable
{
    internal const int MaxAttempts = 10;

    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DefaultUiThreadStartTimeout = TimeSpan.FromSeconds(2);

    private readonly Func<string?> _read;
    private readonly Action<string> _write;
    private readonly TimeSpan _uiThreadStartTimeout;
    private readonly TimeSpan _retryDelay;
    private readonly Lazy<StaWorker> _worker = new(() => new StaWorker());

    public WinRtClipboardService()
        : this(ReadClipboardText, WriteClipboardText, DefaultUiThreadStartTimeout, DefaultRetryDelay)
    {
    }

    /// <summary>For tests: substitute the clipboard calls and the timings.</summary>
    internal WinRtClipboardService(Func<string?> read, Action<string> write, TimeSpan uiThreadStartTimeout, TimeSpan retryDelay)
    {
        _read = read;
        _write = write;
        _uiThreadStartTimeout = uiThreadStartTimeout;
        _retryDelay = retryDelay;
    }

    /// <summary>The dedicated STA thread, if it has been started. For tests.</summary>
    internal int? WorkerThreadId => _worker.IsValueCreated ? _worker.Value.ManagedThreadId : null;

    /// <inheritdoc/>
    public Task<string?> GetTextAsync(CancellationToken ct = default)
        => RunWithRetryAsync(_read, ct);

    /// <inheritdoc/>
    public Task SetTextAsync(string text, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return RunWithRetryAsync(() =>
        {
            _write(text);
            return true;
        }, ct);
    }

    public void Dispose()
    {
        if (_worker.IsValueCreated)
            _worker.Value.Dispose();
    }

    private Task<T> RunWithRetryAsync<T>(Func<T> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return RunOnClipboardThreadAsync(() => RunWithRetry(operation, ct), ct);
    }

    /// <summary>Runs on the clipboard thread, retrying there while another process holds the clipboard.</summary>
    private T RunWithRetry<T>(Func<T> operation, CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return operation();
            }
            catch (COMException ex) when (attempt < MaxAttempts && IsClipboardBusy(ex))
            {
                // Returns early, and the next check throws, if the caller cancels meanwhile.
                ct.WaitHandle.WaitOne(_retryDelay);
            }
        }
    }

    // CLIPBRD_E_CANT_OPEN, _CANT_EMPTY, _CANT_SET, _CANT_CLOSE: another process has it open.
    private static bool IsClipboardBusy(COMException ex)
        => (uint)ex.HResult is 0x800401D0 or 0x800401D1 or 0x800401D2 or 0x800401D4;

    private Task<T> RunOnClipboardThreadAsync<T>(Func<T> operation, CancellationToken ct)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            try
            {
                return Task.FromResult(operation());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        return UiThreadAccess.IsConfigured
            ? RunViaUiThreadAsync(operation, ct)
            : _worker.Value.Run(operation);
    }

    private async Task<T> RunViaUiThreadAsync<T>(Func<T> operation, CancellationToken ct)
    {
        TaskCompletionSource<T> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int claimed = 0;

        bool posted = UiThreadAccess.TryPost(() =>
        {
            if (Interlocked.Exchange(ref claimed, 1) != 0)
                return;

            try
            {
                result.TrySetResult(operation());
            }
            catch (Exception ex)
            {
                result.TrySetException(ex);
            }
        });

        if (!posted)
            return await _worker.Value.Run(operation);

        using CancellationTokenSource waitSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task first = await Task.WhenAny(result.Task, Task.Delay(_uiThreadStartTimeout, waitSource.Token));
        waitSource.Cancel();

        if (first == result.Task || Interlocked.Exchange(ref claimed, 1) != 0)
            return await result.Task;

        // Nothing picked the post up. It can no longer run - we hold the claim - so run it here.
        ct.ThrowIfCancellationRequested();
        return await _worker.Value.Run(operation);
    }

    private static string? ReadClipboardText()
    {
        DataPackageView view = Clipboard.GetContent();
        if (!view.Contains(StandardDataFormats.Text))
            return null;

        return view.GetTextAsync().AsTask().GetAwaiter().GetResult();
    }

    private static void WriteClipboardText(string text)
    {
        DataPackage package = new();
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
    }

    /// <summary>A background STA thread that runs queued clipboard calls one at a time.</summary>
    private sealed class StaWorker : IDisposable
    {
        private readonly BlockingCollection<Action> _queue = [];
        private readonly Thread _thread;

        public StaWorker()
        {
            _thread = new Thread(Pump) { IsBackground = true, Name = "Text Grab clipboard" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public int ManagedThreadId => _thread.ManagedThreadId;

        public Task<T> Run<T>(Func<T> operation)
        {
            TaskCompletionSource<T> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Add(() =>
            {
                try
                {
                    result.TrySetResult(operation());
                }
                catch (Exception ex)
                {
                    result.TrySetException(ex);
                }
            });
            return result.Task;
        }

        public void Dispose() => _queue.CompleteAdding();

        private void Pump()
        {
            foreach (Action action in _queue.GetConsumingEnumerable())
                action();
        }
    }
}
