using System;
using System.Collections.Generic;
using System.Linq;
using Text_Grab.Models;

namespace Text_Grab.Services;

/// <summary>
/// Builds, merges, filters and caps the items shown in the tray flyout. Pure logic, no UI.
/// </summary>
public static class TrayFlyoutItemProvider
{
    public const int DefaultItemCount = 20;
    public const int MinItemCount = 1;
    public const int MaxItemCount = 100;

    /// <summary>
    /// Merges the three history sources into one list, newest first. Sources that are not enabled are skipped.
    /// </summary>
    public static List<TrayFlyoutItem> Merge(
        IEnumerable<HistoryInfo> editWindows,
        IEnumerable<HistoryInfo> grabs,
        IEnumerable<HistoryInfo> files,
        TrayFlyoutSources sources)
    {
        List<TrayFlyoutItem> items = [];

        if (sources.HasFlag(TrayFlyoutSources.EditText))
            items.AddRange(editWindows.Select(h => new TrayFlyoutItem(TrayFlyoutItemKind.EditText, h)));

        if (sources.HasFlag(TrayFlyoutSources.GrabFrames))
            items.AddRange(grabs.Select(h => new TrayFlyoutItem(TrayFlyoutItemKind.GrabFrame, h)));

        if (sources.HasFlag(TrayFlyoutSources.Files))
            items.AddRange(files.Select(h => new TrayFlyoutItem(TrayFlyoutItemKind.File, h)));

        return [.. items.OrderByDescending(i => i.Timestamp)];
    }

    /// <summary>
    /// Filters by a case-insensitive substring match on every whitespace separated term, then caps the result.
    /// The filter runs before the cap so searching reaches past the visible count.
    /// </summary>
    public static List<TrayFlyoutItem> Query(IEnumerable<TrayFlyoutItem> items, string? query, int maxCount)
    {
        maxCount = ClampItemCount(maxCount);

        string[] terms = string.IsNullOrWhiteSpace(query)
            ? []
            : query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        IEnumerable<TrayFlyoutItem> matching = terms.Length == 0
            ? items
            : items.Where(item => terms.All(term =>
                item.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || item.SearchText.Contains(term, StringComparison.CurrentCultureIgnoreCase)));

        return [.. matching.Take(maxCount)];
    }

    public static int ClampItemCount(int count)
        => Math.Clamp(count, MinItemCount, MaxItemCount);

    public static TrayFlyoutSources ParseSources(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return TrayFlyoutSources.All;

        TrayFlyoutSources result = TrayFlyoutSources.None;
        foreach (string part in stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse(part, true, out TrayFlyoutSources parsed) && parsed != TrayFlyoutSources.All)
                result |= parsed;
        }

        return result;
    }

    public static string FormatSources(TrayFlyoutSources sources)
    {
        List<string> names = [];
        foreach (TrayFlyoutSources flag in new[] { TrayFlyoutSources.EditText, TrayFlyoutSources.GrabFrames, TrayFlyoutSources.Files })
        {
            if (sources.HasFlag(flag))
                names.Add(flag.ToString());
        }

        // An empty string means "never configured" (all sources), so an explicit none is spelled out.
        return names.Count == 0 ? nameof(TrayFlyoutSources.None) : string.Join(',', names);
    }

    /// <summary>
    /// Reads the current history. Must be called on the UI thread, like the other HistoryService readers.
    /// </summary>
    public static List<TrayFlyoutItem> LoadAll(HistoryService history, TrayFlyoutSources sources)
    {
        return Merge(
            sources.HasFlag(TrayFlyoutSources.EditText) ? history.GetEditWindows() : [],
            sources.HasFlag(TrayFlyoutSources.GrabFrames) ? history.GetRecentGrabs() : [],
            sources.HasFlag(TrayFlyoutSources.Files) ? history.GetRecentFiles() : [],
            sources);
    }
}
