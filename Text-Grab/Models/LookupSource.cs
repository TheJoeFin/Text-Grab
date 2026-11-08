using System;
using System.Collections.Generic;
using Text_Grab.Properties;

namespace Text_Grab.Models;

/// <summary>
/// The kinds of data Quick Simple Lookup can show in its grid.
/// </summary>
[Flags]
public enum LookupSource
{
    None = 0,
    Data = 1,
    EditHistory = 2,
    GrabHistory = 4,
    PdfHistory = 8,
    GrabTemplates = 16,
    All = Data | EditHistory | GrabHistory | PdfHistory | GrabTemplates,
}

/// <summary>
/// Reads and writes the persisted <see cref="LookupSource"/> selection.
/// </summary>
internal static class LookupSourceSetting
{
    public static IReadOnlyList<(LookupSource Source, string Label)> Options { get; } =
    [
        (LookupSource.Data, "Lookup data"),
        (LookupSource.EditHistory, "Edit Text Window history"),
        (LookupSource.GrabHistory, "Grab Frame history"),
        (LookupSource.PdfHistory, "PDF history"),
        (LookupSource.GrabTemplates, "Grab Templates"),
    ];

    public static LookupSource Get(Settings settings)
    {
        string stored = settings.LookupSources;

        if (string.IsNullOrWhiteSpace(stored))
        {
            // Never customized: show everything, honoring the older "show history" toggle.
            return settings.LookupSearchHistory
                ? LookupSource.All
                : LookupSource.Data | LookupSource.GrabTemplates;
        }

        return Enum.TryParse(stored, out LookupSource parsed) ? parsed & LookupSource.All : LookupSource.All;
    }

    public static void Set(Settings settings, LookupSource sources)
    {
        settings.LookupSources = (sources & LookupSource.All).ToString();
        settings.Save();
    }
}

/// <summary>
/// How Quick Simple Lookup presents itself.
/// </summary>
public enum LookupWindowStyle
{
    /// <summary>A normal window with a title bar, taskbar button and alt-tab entry.</summary>
    Window = 0,

    /// <summary>A command-palette style popup: no title bar, not in the taskbar or alt-tab, closes when it loses focus.</summary>
    Popup = 1,
}

internal static class LookupWindowStyleSetting
{
    public static LookupWindowStyle Get(Settings settings)
        => Enum.TryParse(settings.LookupWindowStyle, out LookupWindowStyle style) ? style : LookupWindowStyle.Window;

    public static void Set(Settings settings, LookupWindowStyle style)
    {
        settings.LookupWindowStyle = style.ToString();
        settings.Save();
    }
}
