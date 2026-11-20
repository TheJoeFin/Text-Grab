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
}
