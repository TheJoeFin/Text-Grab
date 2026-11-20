using System;
using System.Collections.Generic;
using System.Linq;

namespace Text_Grab.Core.Layouts;

/// <summary>Which side of the editor area a new pane is added to.</summary>
public enum PaneSide
{
    Left,
    Right,
    Top,
    Bottom,
}

/// <summary>
/// Changes to a layout's dock tree made from the Panes menu (v5 E6 <c>picker</c>): adding a pane at its
/// default side, taking one out, and telling whether a layout still has the panes its baseline has.
/// Pure over the model, so the rules are tested without a window.
/// </summary>
public static class LayoutPaneEditor
{
    /// <summary>
    /// <paramref name="layout"/> with the pane added beside the editor area, on <paramref name="side"/>. A
    /// layout that already holds the pane comes back as it is.
    /// </summary>
    /// <param name="size">The pane's size along its side, in pixels.</param>
    public static EtwLayoutDefinition AddPane(EtwLayoutDefinition layout, string paneId, PaneSide side, double size)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrEmpty(paneId);

        if (EtwLayoutValidator.ContainsPane(layout.Root, paneId))
            return layout;

        DockChild pane = new(new PaneNode(paneId), EtwLayoutValidator.SanitizeSize(size, 200), DockSizeUnit.Pixel);
        bool horizontal = side is PaneSide.Left or PaneSide.Right;
        bool first = side is PaneSide.Left or PaneSide.Top;
        DockOrientation orientation = horizontal ? DockOrientation.Horizontal : DockOrientation.Vertical;

        if (layout.Root is SplitNode split && split.Orientation == orientation)
        {
            IReadOnlyList<DockChild> children = first ? [pane, .. split.Children] : [.. split.Children, pane];
            return layout with { Root = split with { Children = children } };
        }

        DockChild rest = new(layout.Root, 1, DockSizeUnit.Star);
        return layout with { Root = new SplitNode(orientation, first ? [pane, rest] : [rest, pane]) };
    }

    /// <summary>
    /// <paramref name="layout"/> without the pane. The editor can't be taken out. A split left holding a
    /// single split is flattened into its parent's place.
    /// </summary>
    public static EtwLayoutDefinition RemovePane(EtwLayoutDefinition layout, string paneId)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (paneId == PaneIds.Editor || layout.Root is not SplitNode root)
            return layout;

        return layout with { Root = Without(root, paneId) };
    }

    /// <summary>
    /// Whether <paramref name="live"/> has different panes, or panes shown differently, from
    /// <paramref name="baseline"/>. Sizes are ignored, and so is whether the calc pane is shown: that is
    /// the window's own state, kept across layouts.
    /// </summary>
    public static bool HasPaneChanges(EtwLayoutDefinition live, EtwLayoutDefinition baseline)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(baseline);

        return !Describe(EtwLayoutValidator.Normalize(live).Root).SequenceEqual(Describe(EtwLayoutValidator.Normalize(baseline).Root), StringComparer.Ordinal);
    }

    private static DockNode Without(SplitNode split, string paneId)
    {
        List<DockChild> children = [];
        foreach (DockChild child in split.Children)
        {
            switch (child.Node)
            {
                case PaneNode pane when pane.PaneId == paneId:
                    continue;
                case SplitNode nested:
                    DockNode reduced = Without(nested, paneId);
                    children.Add(child with { Node = reduced });
                    break;
                default:
                    children.Add(child);
                    break;
            }
        }

        // A split holding one split adds nothing; its parent shows that split directly.
        if (children.Count == 1 && children[0].Node is SplitNode only)
            return only;

        return split with { Children = children };
    }

    private static IEnumerable<string> Describe(DockNode node) => node switch
    {
        PaneNode pane => [pane.PaneId],
        TabGroupNode tabs => tabs.Tabs.Select(tab => tab.PaneId),
        SplitNode split => split.Children.SelectMany(child => child.Node is SplitNode
            ? Describe(child.Node)
            : Describe(child.Node).Select(id => child.Collapsed && id != PaneIds.Calc ? id + "#collapsed" : id)),
        _ => [],
    };
}
