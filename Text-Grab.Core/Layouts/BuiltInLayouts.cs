using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Text_Grab.Core.Layouts;

/// <summary>
/// The layouts that ship with Text Grab, embedded in this assembly (<c>Layouts/BuiltIn/*.json</c>).
/// They are read-only: a store refuses to save over one, and customizing one means saving a copy
/// under a new ID.
/// </summary>
public static class BuiltInLayouts
{
    private const string ResourcePrefix = "Text_Grab.Core.Layouts.BuiltIn.";

    private static readonly Lazy<IReadOnlyDictionary<string, EtwLayoutDefinition>> layouts = new(Load);

    /// <summary>Today's window, and what every window falls back to.</summary>
    public static EtwLayoutDefinition Classic => layouts.Value[LayoutIds.Classic];

    public static IReadOnlyCollection<EtwLayoutDefinition> All => [.. layouts.Value.Values.OrderBy(layout => layout.Id == LayoutIds.Classic ? 0 : 1).ThenBy(layout => layout.Id, StringComparer.Ordinal)];

    public static bool IsBuiltIn(string? id) => id is not null && layouts.Value.ContainsKey(id);

    public static bool TryGet(string? id, out EtwLayoutDefinition layout)
    {
        if (id is not null && layouts.Value.TryGetValue(id, out EtwLayoutDefinition? found))
        {
            layout = found;
            return true;
        }

        layout = null!;
        return false;
    }

    private static IReadOnlyDictionary<string, EtwLayoutDefinition> Load()
    {
        Assembly assembly = typeof(BuiltInLayouts).Assembly;
        Dictionary<string, EtwLayoutDefinition> found = new(StringComparer.Ordinal);

        foreach (string name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using Stream stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Missing built-in layout resource {name}.");
            using StreamReader reader = new(stream);
            LayoutReadResult read = EtwLayoutSerializer.Read(reader.ReadToEnd());

            // A built-in layout that does not load is a build error, not something to recover from.
            EtwLayoutDefinition layout = read.Layout
                ?? throw new InvalidOperationException($"The built-in layout {name} does not load: {read.Error}");
            found.Add(layout.Id, layout);
        }

        if (!found.ContainsKey(LayoutIds.Classic))
            throw new InvalidOperationException("The Classic layout is not built in.");

        return found;
    }
}
