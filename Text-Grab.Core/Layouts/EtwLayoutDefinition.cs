using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Text_Grab.Models;

namespace Text_Grab.Core.Layouts;

/// <summary>
/// What an Edit Text Window is made of: the panes around the editor (<see cref="Root"/>) and, from
/// E4b, its menus, bottom bar and status items. Stored as JSON (<see cref="EtwLayoutSerializer"/>).
/// </summary>
/// <param name="SchemaVersion">The file format's version, <c>major.minor</c>. A newer major version is refused.</param>
/// <param name="Id">The layout's ID: lower-case letters, digits, '.', '-' and '_' (<see cref="LayoutIds.IsValid"/>).</param>
/// <param name="Name">The layout's name as people see it.</param>
/// <param name="Root">The dock tree. <see cref="PaneIds.Editor"/> is the editor area.</param>
/// <param name="DefaultMode">The editor mode to open in, or null for whatever the file or history item says.</param>
/// <param name="DefaultCodeLanguage">
/// The code editor's language when the mode is Code, as named in <c>CodeLanguages</c>. An unknown name
/// falls back to detection.
/// </param>
/// <param name="Window">Window options, or null for the window's own settings.</param>
/// <param name="Chrome">E4b. Null means the window's built-in menus, which is all E4a renders.</param>
/// <param name="BottomBarActionIds">E4b. Null means the bottom bar from settings.</param>
/// <param name="StatusItemIds">E4b. Null means the built-in status items.</param>
public sealed record EtwLayoutDefinition(
    string SchemaVersion,
    string Id,
    string Name,
    DockNode Root,
    EtwEditorMode? DefaultMode = null,
    string? DefaultCodeLanguage = null,
    WindowOptions? Window = null,
    ChromeDefinition? Chrome = null,
    IReadOnlyList<string>? BottomBarActionIds = null,
    IReadOnlyList<string>? StatusItemIds = null)
{
    /// <summary>
    /// Members this version does not know, kept so that a layout written by a newer Text Grab
    /// survives being opened and saved by this one.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownMembers { get; init; }
}

/// <summary>Window options a layout can set.</summary>
public sealed record WindowOptions(bool AlwaysOnTop, bool RestorePosition, bool ShowBottomBar);

/// <summary>The direction a <see cref="SplitNode"/> lays out its children.</summary>
public enum DockOrientation
{
    /// <summary>Side by side, left to right.</summary>
    Horizontal,

    /// <summary>Stacked, top to bottom.</summary>
    Vertical,
}

/// <summary>How a <see cref="DockChild.Size"/> is measured.</summary>
public enum DockSizeUnit
{
    /// <summary>A share of the space left after the pixel-sized children.</summary>
    Star,

    /// <summary>Device-independent pixels.</summary>
    Pixel,
}

/// <summary>The well-known pane IDs a layout names.</summary>
public static class PaneIds
{
    /// <summary>The editor area: the four editors and the reading bookmark, as one unit.</summary>
    public const string Editor = "textgrab.pane.editor";

    /// <summary>The calc pane.</summary>
    public const string Calc = "textgrab.pane.calc";

    /// <summary>The recent items pane (v5 E5).</summary>
    public const string Recent = "textgrab.pane.recent";
}
