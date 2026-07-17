using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Properties;

namespace Text_Grab.Services;

public class TtsService
{
    private ITtsEngine _engine = new WindowsSpeechEngine();
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>();
    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource _speechCts = new();
    private readonly object _lock = new();
    private int _pendingCount = 0;
    private bool _isBusy;

    private event Action? Drained;

    /// <summary>
    /// Raised when the service transitions between idle and busy. May fire on a
    /// background thread, so UI subscribers must marshal to their dispatcher.
    /// </summary>
    public event Action<bool>? BusyChanged;

    public bool IsBusy
    {
        get { lock (_lock) return _isBusy; }
    }

    public ITtsEngine Engine
    {
        set => _engine = value;
    }

    public TtsService()
    {
        _ = Task.Run(DrainLoopAsync);
    }

    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = ApplyWordLimit(text);

        lock (_lock)
        {
            _pendingCount++;
            if (!_queue.Writer.TryWrite(text))
            {
                _pendingCount--;
                PublishIdleIfDrained();
                return;
            }

            if (!_isBusy)
            {
                _isBusy = true;
                BusyChanged?.Invoke(true);
            }
        }
    }

    public void Stop()
    {
        _speechCts.Cancel();
        _speechCts = new CancellationTokenSource();

        lock (_lock)
        {
            while (_queue.Reader.TryRead(out _))
                _pendingCount--;

            // If an item is still in flight it stays counted; the drain loop
            // publishes idle when its cancelled Speak returns.
            PublishIdleIfDrained();
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> immediately if nothing is queued or
    /// speaking, otherwise once the queue next drains. The idle check and the
    /// subscription happen under the same lock as the drain-completion check,
    /// so a drain that completes concurrently cannot slip between them and
    /// leave the caller waiting forever.
    /// </summary>
    public void RunWhenIdle(Action action)
    {
        lock (_lock)
        {
            if (_pendingCount == 0)
            {
                action();
                return;
            }

            void handler()
            {
                Drained -= handler;
                action();
            }

            Drained += handler;
        }
    }

    private static string ApplyWordLimit(string text)
    {
        int wordLimit = Settings.Default.TtsSpeakWordLimit;
        if (wordLimit <= 0)
            return text;

        string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length > wordLimit ? string.Join(' ', words[..wordLimit]) : text;
    }

    private async Task DrainLoopAsync()
    {
        CancellationToken lifecycleCt = _cts.Token;
        try
        {
            await foreach (string text in _queue.Reader.ReadAllAsync(lifecycleCt))
            {
                try
                {
                    await _engine.SpeakAsync(text, _speechCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // speech was stopped; continue so the loop can drain remaining items
                }
                catch (Exception)
                {
                    // swallow per-item errors so the queue keeps draining
                }
                finally
                {
                    lock (_lock)
                    {
                        _pendingCount--;
                        PublishIdleIfDrained();
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void PublishIdleIfDrained()
    {
        if (_pendingCount != 0)
            return;

        Drained?.Invoke();

        // A drain callback may synchronously queue more speech. Keep the existing
        // busy state in that case so an older completion cannot publish stale idle.
        if (_pendingCount == 0 && _isBusy)
        {
            _isBusy = false;
            BusyChanged?.Invoke(false);
        }
    }
}
