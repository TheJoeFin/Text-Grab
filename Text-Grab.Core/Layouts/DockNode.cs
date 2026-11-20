using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Text_Grab.Core.Layouts;

/// <summary>A node of a layout's dock tree. The JSON names its kind in <c>"type"</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SplitNode), "split")]
[JsonDerivedType(typeof(PaneNode), "pane")]
[JsonDerivedType(typeof(TabGroupNode), "tabs")]
public abstract record DockNode
{
    /// <summary>Members this version does not know, kept so that they survive a save.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownMembers { get; init; }
}

/// <summary>Children laid out side by side or stacked, with a splitter between each pair.</summary>
public sealed record SplitNode(DockOrientation Orientation, IReadOnlyList<DockChild> Children) : DockNode;

/// <summary>One child of a <see cref="SplitNode"/> and the space it takes along the split.</summary>
/// <param name="Node">The child.</param>
/// <param name="Size">Its width (in a horizontal split) or height (in a vertical one), in <paramref name="Unit"/>.</param>
/// <param name="Unit">Whether <paramref name="Size"/> is a star share or pixels.</param>
/// <param name="Collapsed">Hidden, keeping its place and size for when it is shown again.</param>
public sealed record DockChild(
    DockNode Node,
    double Size,
    DockSizeUnit Unit,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Collapsed = false);

/// <summary>A pane, named by its ID. <see cref="PaneIds.Editor"/> is the editor area.</summary>
/// <param name="PaneId">The pane's ID, such as <c>textgrab.pane.calc</c>.</param>
/// <param name="Config">What the pane saved about itself, given back to it when it is attached.</param>
public sealed record PaneNode(string PaneId, JsonElement? Config = null) : DockNode;

/// <summary>
/// Panes sharing one place, one shown at a time. E4a shows only the selected one and constructs no
/// other; the rest stay in the definition so they survive a save.
/// </summary>
public sealed record TabGroupNode(IReadOnlyList<PaneNode> Tabs, int SelectedIndex = 0) : DockNode;
