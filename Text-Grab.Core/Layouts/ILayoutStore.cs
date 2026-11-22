using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Core.Layouts;

/// <summary>A layout's ID and name, for listing without loading every layout.</summary>
public sealed record LayoutSummary(string Id, string Name, bool IsBuiltIn);

/// <summary>
/// Where layouts live: the built-in ones (<see cref="BuiltInLayouts"/>), which are read-only, and the
/// ones people have saved.
/// </summary>
public interface ILayoutStore
{
    /// <summary>Every layout, built-in ones first.</summary>
    Task<IReadOnlyList<LayoutSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The layout with this ID, or why it cannot be had: no such layout, an ID that cannot be one, or
    /// a file that does not read. Never throws for any of those.
    /// </summary>
    Task<LayoutReadResult> LoadAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Saves a layout under its ID, replacing any saved layout with that ID.</summary>
    /// <exception cref="System.ArgumentException">The ID cannot be a layout ID.</exception>
    /// <exception cref="System.InvalidOperationException">The ID is a built-in layout's.</exception>
    Task SaveAsync(EtwLayoutDefinition layout, CancellationToken cancellationToken = default);

    /// <summary>Deletes a saved layout. False if there was none; built-in layouts cannot be deleted.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="LoadAsync"/>, synchronously. Only for a window choosing the layout it renders first, which
    /// it must know before it is made; the file is one small JSON document.
    /// </summary>
    LayoutReadResult Load(string id);

    /// <summary>The store's metadata (<see cref="LayoutStoreDefaults"/>), synchronously, for the same reason as <see cref="Load"/>.</summary>
    LayoutStoreDefaults ReadDefaults();

    /// <summary>The store's metadata. Never throws for a missing or malformed file.</summary>
    Task<LayoutStoreDefaults> ReadDefaultsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets (or, with null, clears) the layout every window opens in when nothing more specific applies.
    /// Keeps every other member of the metadata file.
    /// </summary>
    /// <exception cref="System.ArgumentException">The ID cannot be a layout ID.</exception>
    Task SetGlobalDefaultAsync(string? id, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the store's metadata file says (<c>layouts/.store.json</c>; E6 <c>defaults</c> adds per-file-kind
/// entries to the same file).
/// </summary>
/// <param name="GlobalDefaultId">The layout new windows open in, or null for Classic.</param>
/// <param name="Error">Why the file could not be used, or null. The default is then Classic.</param>
public sealed record LayoutStoreDefaults(string? GlobalDefaultId, string? Error = null);
