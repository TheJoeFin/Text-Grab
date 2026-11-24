using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Services;

/// <summary>
/// Remembers, per opened file, the layout, editor mode and code language the Edit Text Window last showed
/// it in, so that reopening the file does not fall back to the defaults for its type. Sits beside
/// <see cref="FileWindowPlacementStore"/>, which does the same for size and position.
/// </summary>
internal static class FileViewStateStore
{
    private const string FileName = "FileViewStates.json";
    private const int MaxEntries = 200;
    private static readonly object gate = new();

    /// <param name="LayoutId">The layout the window showed, or null when none was recorded.</param>
    /// <param name="Mode">The editor mode the window was in.</param>
    /// <param name="CodeLanguage">The code language name, "" for Plain Text, or null when none was chosen.</param>
    internal sealed record Entry(string Key, string? LayoutId, EtwEditorMode Mode, string? CodeLanguage, DateTimeOffset SavedAt);

    internal static string KeyForFile(string path) => Path.GetFullPath(path);

    internal static Entry? Get(string path)
    {
        try
        {
            string key = KeyForFile(path);
            return Load().FirstOrDefault(e => IsMatch(e, key));
        }
        catch
        {
            return null;
        }
    }

    internal static void Set(string path, string? layoutId, EtwEditorMode mode, string? codeLanguage)
    {
        try
        {
            string key = KeyForFile(path);
            lock (gate)
            {
                List<Entry> entries = Load();
                entries.RemoveAll(e => IsMatch(e, key));
                entries.Insert(0, new Entry(key, layoutId, mode, codeLanguage, DateTimeOffset.Now));
                string json = JsonSerializer.Serialize(entries.Take(MaxEntries));
                Task.Run(() => FileUtilities.SaveTextFile(json, FileName, FileStorageKind.WithHistory)).GetAwaiter().GetResult();
            }
        }
        catch
        {
            // Remembering the view is a convenience; never block closing a window or opening a file over it.
        }
    }

    private static bool IsMatch(Entry entry, string key)
        => entry.Key is not null && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase);

    private static List<Entry> Load()
    {
        string historyFolder = Task.Run(FileUtilities.GetPathToHistory).GetAwaiter().GetResult();
        string file = Path.Combine(historyFolder, FileName);
        if (!File.Exists(file))
            return [];
        return JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(file))?
            .Where(e => e.Key is not null && Enum.IsDefined(e.Mode)).ToList() ?? [];
    }
}
