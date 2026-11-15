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
/// Remembers the normal size, position, and maximized state of the Edit Text Window per opened file.
/// This is separate from the "last used" placement that new, empty windows restore.
/// </summary>
internal static class FileWindowPlacementStore
{
    private const string FileName = "FileWindowPlacements.json";
    private const int MaxEntries = 100;
    private static readonly object gate = new();

    /// <param name="Bounds">"left,top,width,height,state" in device-independent pixels; older entries omit state.</param>
    internal sealed record Entry(string Key, string Bounds, DateTimeOffset SavedAt);

    internal static string KeyForFile(string path) => Path.GetFullPath(path);

    internal static string? GetBounds(string path)
    {
        try
        {
            string key = KeyForFile(path);
            return Load().FirstOrDefault(e => IsMatch(e, key))?.Bounds;
        }
        catch
        {
            return null;
        }
    }

    internal static void SetBounds(string path, string bounds)
    {
        try
        {
            string key = KeyForFile(path);
            lock (gate)
            {
                List<Entry> entries = Load();
                entries.RemoveAll(e => IsMatch(e, key));
                entries.Insert(0, new Entry(key, bounds, DateTimeOffset.Now));
                string json = JsonSerializer.Serialize(entries.Take(MaxEntries));
                Task.Run(() => FileUtilities.SaveTextFile(json, FileName, FileStorageKind.WithHistory)).GetAwaiter().GetResult();
            }
        }
        catch
        {
            // Window placement is a convenience; never block closing a window over it.
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
            .Where(e => e.Key is not null && e.Bounds is not null).ToList() ?? [];
    }
}
