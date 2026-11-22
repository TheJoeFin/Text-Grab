using System;
using System.Threading;
using System.Threading.Tasks;

namespace Text_Grab.Core.Layouts;

/// <summary>What an Edit Text Window was asked to open with.</summary>
/// <param name="RequestedId">A layout the caller asked for: <c>--layout</c>, a <c>text-grab://</c> link, code.</param>
/// <param name="HistoryId">The layout the history item being reopened was saved with.</param>
/// <param name="FilePath">The file the window opens, whose type may have a default layout.</param>
/// <param name="GlobalDefaultId">The layout the person set as the default (the store's metadata), or null for Classic.</param>
public sealed record LayoutRequest(string? RequestedId = null, string? HistoryId = null, string? FilePath = null, string? GlobalDefaultId = null);

/// <summary>The layout a window opens with, and, when the one it was asked for could not be used, why.</summary>
public sealed record ResolvedLayout(EtwLayoutDefinition Layout, string? Notice);

/// <summary>
/// Chooses an Edit Text Window's layout (decision D5): the caller's, then the history item's, then the
/// file type's default (<see cref="LayoutFileTypeDefaults"/>), then the global default: the layout the
/// person set in the Layouts menu (<see cref="LayoutStoreDefaults"/>), else Classic.
/// </summary>
/// <remarks>
/// Only the first of those that is given is tried. A layout that cannot be loaded or shown (no such
/// layout, a malformed file, a pane this version does not have) gives Classic and a notice saying why,
/// rather than the next choice down: a person who asked for a layout should hear that they did not get it.
/// </remarks>
public sealed class LayoutResolver
{
    private readonly ILayoutStore store;
    private readonly Func<string, bool> isKnownPane;
    private readonly Func<string?, string?> layoutForFile;

    /// <param name="store">Where layouts are loaded from.</param>
    /// <param name="isKnownPane">Whether a pane ID has a pane to make (<see cref="EtwLayoutValidator.FindProblem"/>).</param>
    /// <param name="layoutForFile">A file's default layout; <see cref="LayoutFileTypeDefaults.LayoutIdFor(string?)"/> when null.</param>
    public LayoutResolver(ILayoutStore store, Func<string, bool> isKnownPane, Func<string?, string?>? layoutForFile = null)
    {
        this.store = store;
        this.isKnownPane = isKnownPane;
        this.layoutForFile = layoutForFile ?? LayoutFileTypeDefaults.LayoutIdFor;
    }

    /// <summary>The layout ID a request names first, or null for the global default.</summary>
    public string? ChooseId(LayoutRequest request) => ChooseId(request, layoutForFile);

    /// <summary>
    /// <see cref="ChooseId(LayoutRequest)"/> without a store: the choice depends only on the request and
    /// the file-type map, so a window can know the layout it opens in before anything is loaded.
    /// </summary>
    /// <param name="layoutForFile">A file's default layout; <see cref="LayoutFileTypeDefaults.LayoutIdFor(string?)"/> when null.</param>
    public static string? ChooseId(LayoutRequest request, Func<string?, string?>? layoutForFile)
        => NullIfBlank(request.RequestedId) ?? NullIfBlank(request.HistoryId) ?? NullIfBlank((layoutForFile ?? LayoutFileTypeDefaults.LayoutIdFor)(request.FilePath)) ?? NullIfBlank(request.GlobalDefaultId);

    public async Task<ResolvedLayout> ResolveAsync(LayoutRequest request, CancellationToken cancellationToken = default)
    {
        // The store's default only matters when nothing more specific was chosen.
        if (request.GlobalDefaultId is null && ChooseId(request) is null)
        {
            LayoutStoreDefaults defaults = await store.ReadDefaultsAsync(cancellationToken).ConfigureAwait(false);
            if (defaults.Error is not null)
                return new ResolvedLayout(BuiltInLayouts.Classic, $"{defaults.Error} Text Grab opened the Classic layout instead.");

            request = request with { GlobalDefaultId = defaults.GlobalDefaultId };
        }

        string? id = ChooseId(request);
        if (id is null || id == LayoutIds.Classic)
            return new ResolvedLayout(BuiltInLayouts.Classic, null);

        LayoutReadResult read = await store.LoadAsync(id, cancellationToken).ConfigureAwait(false);
        if (read.Layout is null)
            return new ResolvedLayout(BuiltInLayouts.Classic, $"{read.Error} Text Grab opened the Classic layout instead.");

        if (EtwLayoutValidator.FindProblem(read.Layout, isKnownPane) is string problem)
            return new ResolvedLayout(BuiltInLayouts.Classic, $"The layout \"{id}\" can't be shown. {problem} Text Grab opened the Classic layout instead.");

        return new ResolvedLayout(read.Layout, null);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
