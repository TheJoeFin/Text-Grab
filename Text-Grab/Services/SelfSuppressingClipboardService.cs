using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Core.Abstractions;

namespace Text_Grab.Services;

/// <summary>
/// The app's <see cref="IClipboardService"/> (v5 F5): the Core.Windows clipboard service, with
/// every write marked as Text Grab's own through <see cref="ClipboardMonitorService.SuppressSelf"/>
/// so it does not fire a clipboard trigger. The monitor is app-side, so the marking happens here,
/// around the inner service, rather than inside it.
///
/// A write that completes synchronously - any write from the UI thread - still does, because the
/// scope is entered and left around an already-completed task.
/// </summary>
internal sealed class SelfSuppressingClipboardService(IClipboardService inner, ClipboardMonitorService monitor) : IClipboardService
{
    public Task<string?> GetTextAsync(CancellationToken ct = default) => inner.GetTextAsync(ct);

    public async Task SetTextAsync(string text, CancellationToken ct = default)
    {
        using (monitor.SuppressSelf(text))
            await inner.SetTextAsync(text, ct);
    }
}
