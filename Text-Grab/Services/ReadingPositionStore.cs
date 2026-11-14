using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Text_Grab.Core.Editing;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Services;

/// <summary>
/// Remembers where the user stopped reading in long documents of the Edit Text Window (files and
/// history items, in every editor mode), and which code regions were folded, so the window can offer a
/// "pick up where you left off" button and restore the folds the next time the document opens.
/// </summary>
/// <remarks>
/// A position's unit depends on the editor mode (a character index for text and Markdown, a line for
/// code, a row for spreadsheets), so an entry is only handed back for the mode it was saved in.
/// </remarks>
internal static class ReadingPositionStore
{
    private const string FileName = "ReadingPositions.json";
    private const int MaxEntries = 50;
    private static readonly object gate = new();

    /// <summary>Text, Markdown and code documents shorter than this don't need a bookmark.</summary>
    internal const int MinimumTextLength = 3000;

    /// <summary>Spreadsheets with fewer rows than this don't need a bookmark.</summary>
    internal const int MinimumSpreadsheetRows = 100;

    internal sealed record Entry(string Key, string Mode, int Position, DateTimeOffset SavedAt, FoldSnapshot? Folds = null);

    internal static string KeyForFile(string path) => Path.GetFullPath(path);

    internal static string KeyForHistoryItem(string historyId) => $"history:{historyId}";

    internal static int? GetPosition(string key, string mode)
    {
        try
        {
            Entry? entry = Load().FirstOrDefault(e => IsMatch(e, key));
            if (entry is null || entry.Position <= 0 || !string.Equals(entry.Mode, mode, StringComparison.Ordinal))
                return null;
            return entry.Position;
        }
        catch
        {
            return null;
        }
    }

    internal static FoldSnapshot? GetFolds(string key)
    {
        try
        {
            Entry? entry = Load().FirstOrDefault(e => IsMatch(e, key));
            return entry is not null && string.Equals(entry.Mode, nameof(EtwEditorMode.Code), StringComparison.Ordinal)
                ? entry.Folds
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Saves the position, or forgets it when it isn't worth keeping. Collapsed regions are kept.</summary>
    internal static void SetPosition(string key, string mode, int position, bool isLongDocument)
        => Update(key, existing =>
        {
            FoldSnapshot? folds = existing is not null && existing.Mode == mode ? existing.Folds : null;
            return isLongDocument ? Normalize(new Entry(key, mode, Math.Max(position, 0), DateTimeOffset.Now, folds)) : null;
        });

    /// <summary>Saves which code regions are collapsed, or forgets them. The position is kept.</summary>
    internal static void SetFolds(string key, FoldSnapshot? folds)
        => Update(key, existing =>
        {
            string mode = nameof(EtwEditorMode.Code);
            int position = existing is not null && existing.Mode == mode ? existing.Position : 0;
            return Normalize(new Entry(key, mode, position, DateTimeOffset.Now, folds));
        });

    // Nothing worth remembering: no position and no collapsed regions.
    private static Entry? Normalize(Entry entry)
        => entry.Position > 0 || entry.Folds is { Marks.Count: > 0 } ? entry : null;

    private static void Update(string key, Func<Entry?, Entry?> change)
    {
        try
        {
            lock (gate)
            {
                List<Entry> entries = Load();
                Entry? existing = entries.FirstOrDefault(e => IsMatch(e, key));
                entries.RemoveAll(e => IsMatch(e, key));
                if (change(existing) is Entry updated)
                    entries.Insert(0, updated);
                string json = JsonSerializer.Serialize(entries.Take(MaxEntries));
                Task.Run(() => FileUtilities.SaveTextFile(json, FileName, FileStorageKind.WithHistory)).GetAwaiter().GetResult();
            }
        }
        catch
        {
            // Bookmarks are a convenience; never block closing or opening a document.
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
            .Where(e => e.Key is not null && e.Mode is not null).ToList() ?? [];
    }
}
