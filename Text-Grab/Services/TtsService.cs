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
    private int _pendingCount = 0;

    public event Action? Drained;
    public bool IsBusy => Volatile.Read(ref _pendingCount) > 0;

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

        int wordLimit = Settings.Default.TtsSpeakWordLimit;
        if (wordLimit > 0)
        {
            string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > wordLimit)
                text = string.Join(' ', words[..wordLimit]);
        }

        Interlocked.Increment(ref _pendingCount);
        _queue.Writer.TryWrite(text);
    }

    public void Stop()
    {
        _speechCts.Cancel();
        _speechCts = new CancellationTokenSource();

        while (_queue.Reader.TryRead(out _))
            Interlocked.Decrement(ref _pendingCount);
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
                    if (Interlocked.Decrement(ref _pendingCount) == 0)
                        Drained?.Invoke();
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
