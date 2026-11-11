using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Core.Abstractions;

/// <summary>
/// Plain-text clipboard access that does not depend on the caller's thread. Implementations take
/// care of running the clipboard calls on a thread that can make them.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// The clipboard's text, or <c>null</c> when it holds no text.
    /// </summary>
    Task<string?> GetTextAsync(CancellationToken ct = default);

    /// <summary>
    /// Replaces the clipboard's contents with <paramref name="text"/>. The text stays on the
    /// clipboard after the process exits.
    /// </summary>
    Task SetTextAsync(string text, CancellationToken ct = default);
}
