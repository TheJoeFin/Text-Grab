using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Text_Grab.Core.Layouts;

/// <summary>
/// The built-in layout for a file type: the "default for the file type" step of the order a window's
/// layout is chosen in (decision D5, <see cref="LayoutResolver"/>). A fixed, read-only map from file
/// extension to layout ID, with no settings UI.
/// </summary>
/// <remarks>
/// It ships empty in E4a, so every file still opens in Classic, exactly as before. Each E5 run adds the
/// entries for its own layouts; in 4.17 the only entries are <c>.mmd</c> and <c>.mermaid</c> (E5
/// <c>mermaid</c>). Markdown files are deliberately not mapped: they keep opening in Markdown mode in
/// Classic, and the Markdown layout stays a choice. E6 <c>defaults</c> (5.0) layers people's own choices
/// on top of this map.
/// </remarks>
public static class LayoutFileTypeDefaults
{
    private static readonly ReadOnlyDictionary<string, string> Map = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
        });

    /// <summary>Extension (with its dot) to layout ID.</summary>
    public static IReadOnlyDictionary<string, string> Entries => Map;

    /// <summary>The built-in layout for <paramref name="filePath"/>'s type, or null for none.</summary>
    public static string? LayoutIdFor(string? filePath) => LayoutIdFor(filePath, Map);

    internal static string? LayoutIdFor(string? filePath, IReadOnlyDictionary<string, string> map)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        string extension = Path.GetExtension(filePath);
        return extension.Length > 0 && map.TryGetValue(extension, out string? layoutId) ? layoutId : null;
    }
}
