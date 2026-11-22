using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Text_Grab.Core.Layouts;

// The window's menus and buttons as data (E4b). E4a reads and writes these unchanged but renders none
// of them: a layout with chrome members still opens with the window's hand-written menus.

/// <summary>What kind of command surface a layout uses.</summary>
public enum ChromeKind
{
    MenuBar,
    Ribbon,
    None,
}

/// <summary>What a <see cref="ChromeItem"/> is.</summary>
public enum ChromeItemKind
{
    Action,
    Separator,
    Submenu,
    DynamicList,
}

/// <summary>A layout's menus (E4b).</summary>
public sealed record ChromeDefinition(ChromeKind Kind, IReadOnlyList<ChromeGroup> Groups)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownMembers { get; init; }
}

/// <summary>One top-level menu or ribbon tab (E4b).</summary>
public sealed record ChromeGroup(string Title, string? AccessKey, IReadOnlyList<ChromeItem> Items)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownMembers { get; init; }
}

/// <summary>One menu entry (E4b).</summary>
/// <param name="Kind">What the entry is.</param>
/// <param name="ActionId">The action an <see cref="ChromeItemKind.Action"/> runs.</param>
/// <param name="Title">The entry's text, when it is not the action's.</param>
/// <param name="DynamicSourceId">What fills a <see cref="ChromeItemKind.DynamicList"/> when it opens.</param>
/// <param name="Children">A submenu's entries.</param>
public sealed record ChromeItem(
    ChromeItemKind Kind,
    string? ActionId = null,
    string? Title = null,
    string? DynamicSourceId = null,
    IReadOnlyList<ChromeItem>? Children = null)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownMembers { get; init; }
}
