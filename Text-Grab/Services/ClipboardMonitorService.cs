using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Text_Grab.Core.Abstractions;
using Windows.ApplicationModel.DataTransfer;

namespace Text_Grab.Services;

/// <summary>
/// The clipboard's change notification, subscribed once for the whole app (v5 F5). Before this,
/// every Edit Text Window subscribed to WinRT <c>Clipboard.ContentChanged</c> itself.
///
/// Two events, for two kinds of listener:
/// <list type="bullet">
/// <item><see cref="ContentChanged"/> forwards every change, Text Grab's own writes included, on
/// the thread WinRT raised it. The Edit Text Window's clipboard watcher listens here, so it sees
/// exactly what it saw when it subscribed directly: it still collects grabs, which reach the
/// clipboard through Text Grab itself.</item>
/// <item><see cref="Triggered"/>, the <see cref="ITriggerSource"/> side, carries the clipboard's
/// text and skips changes Text Grab marked as its own with <see cref="SuppressSelf"/>. Without that,
/// a consumer that writes the clipboard in response to a clipboard trigger would retrigger itself
/// forever.</item>
/// </list>
///
/// Call <see cref="Start"/> on the UI thread. Checked on 2026-11-09: subscribed on the WPF UI
/// thread, WinRT raises <c>ContentChanged</c> on that same thread, which the Edit Text Window's
/// handler relies on because it reads UI state directly. Subscribed on an MTA thread with no
/// message pump, WinRT never raises it at all. A headless consumer must therefore start the
/// monitor through the UI thread (for example with <see cref="UiThreadAccess"/>).
/// </summary>
internal sealed class ClipboardMonitorService : ITriggerSource
{
    /// <summary>How long after a self-write ends a matching change is still taken to be that write.</summary>
    public static readonly TimeSpan SuppressionWindow = TimeSpan.FromSeconds(1);

    private readonly IClipboardChangeSource _source;
    private readonly Func<Task<string?>> _readText;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly List<Suppression> _suppressions = [];
    private bool _started;

    public ClipboardMonitorService()
        : this(new WinRtClipboardChangeSource(), ReadClipboardTextAsync, TimeProvider.System)
    {
    }

    /// <summary>For tests: a fake change source, clipboard reader and clock.</summary>
    internal ClipboardMonitorService(IClipboardChangeSource source, Func<Task<string?>> readText, TimeProvider time)
    {
        _source = source;
        _readText = readText;
        _time = time;
    }

    /// <summary>Every clipboard change, Text Grab's own included, raised on WinRT's thread.</summary>
    public event EventHandler<object>? ContentChanged;

    /// <summary>
    /// Clipboard changes that are not Text Grab's own. <see cref="TriggerPayload.Text"/> is the
    /// clipboard's text, or null when it holds none.
    /// </summary>
    public event EventHandler<TriggerPayload>? Triggered;

    /// <inheritdoc/>
    public void Start()
    {
        lock (_gate)
        {
            if (_started)
                return;

            _source.Changed += OnSourceChanged;
            _started = true;
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        lock (_gate)
        {
            if (!_started)
                return;

            _source.Changed -= OnSourceChanged;
            _started = false;
        }
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Marks a clipboard write as Text Grab's own until the returned scope is disposed and for
    /// <see cref="SuppressionWindow"/> after, which covers the gap before the change notification
    /// arrives. With <paramref name="expectedText"/>, only changes to exactly that text are skipped,
    /// so something the user copies meanwhile still triggers. Without it, every change in that time
    /// is skipped. Affects <see cref="Triggered"/> only, never <see cref="ContentChanged"/>.
    /// </summary>
    public IDisposable SuppressSelf(string? expectedText = null)
    {
        Suppression suppression = new(expectedText);
        lock (_gate)
            _suppressions.Add(suppression);

        return new SuppressionScope(this, suppression);
    }

    private async void OnSourceChanged(object? sender, object e)
    {
        try
        {
            ContentChanged?.Invoke(this, e);

            // Nobody listening for triggers: do not read the clipboard for nothing.
            if (Triggered is null)
                return;

            string? text;
            bool textKnown;
            try
            {
                text = await _readText();
                textKnown = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Clipboard monitor could not read the clipboard: {ex.Message}");
                text = null;
                textKnown = false;
            }

            if (IsSuppressed(text, textKnown))
                return;

            Triggered?.Invoke(this, new TriggerPayload(TriggerKind.ClipboardChanged, Text: text));
        }
        catch (Exception ex)
        {
            // async void: a listener's exception would otherwise take the process down.
            Debug.WriteLine($"Clipboard monitor listener failed: {ex}");
        }
    }

    private bool IsSuppressed(string? text, bool textKnown)
    {
        lock (_gate)
        {
            DateTimeOffset now = _time.GetUtcNow();
            _suppressions.RemoveAll(s => s.EndedAt is DateTimeOffset ended && now - ended > SuppressionWindow);

            foreach (Suppression suppression in _suppressions)
            {
                // Unreadable content cannot be told apart from our own write; skipping it is the
                // side that cannot loop.
                if (suppression.ExpectedText is null || !textKnown
                    || string.Equals(suppression.ExpectedText, text, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void End(Suppression suppression)
    {
        lock (_gate)
            suppression.EndedAt ??= _time.GetUtcNow();
    }

    private static async Task<string?> ReadClipboardTextAsync()
    {
        DataPackageView view = Clipboard.GetContent();
        return view.Contains(StandardDataFormats.Text) ? await view.GetTextAsync() : null;
    }

    private sealed class Suppression(string? expectedText)
    {
        public string? ExpectedText { get; } = expectedText;
        public DateTimeOffset? EndedAt { get; set; }
    }

    private sealed class SuppressionScope(ClipboardMonitorService owner, Suppression suppression) : IDisposable
    {
        public void Dispose() => owner.End(suppression);
    }
}

/// <summary>Where clipboard change notifications come from. A seam so tests never touch the real one.</summary>
internal interface IClipboardChangeSource
{
    event EventHandler<object>? Changed;
}

/// <summary>WinRT <c>Clipboard.ContentChanged</c>.</summary>
internal sealed class WinRtClipboardChangeSource : IClipboardChangeSource
{
    public event EventHandler<object>? Changed
    {
        add => Clipboard.ContentChanged += value;
        remove => Clipboard.ContentChanged -= value;
    }
}
