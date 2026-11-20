using System;
using System.Collections.Generic;
using System.Linq;

namespace Text_Grab.Core.Layouts;

/// <summary>
/// Decides whether a layout that parsed can be shown, and repairs what can be repaired without
/// guessing.
/// </summary>
/// <remarks>
/// <para>
/// A layout that cannot be shown opens Classic instead, and the window says why
/// (<see cref="FindProblem"/>): a pane ID nothing knows, the same pane twice, the editor inside a tab
/// group, or an empty split or tab group.
/// </para>
/// <para>
/// What can be repaired is repaired when the layout is shown (<see cref="Normalize"/>): a layout with
/// no editor gets one, and a size that is zero, negative or not a number becomes the pane's default.
/// </para>
/// </remarks>
public static class EtwLayoutValidator
{
    /// <summary>Why <paramref name="layout"/> cannot be shown, or null if it can.</summary>
    /// <param name="layout">The layout.</param>
    /// <param name="isKnownPane">Whether a pane ID other than <see cref="PaneIds.Editor"/> has a pane to make.</param>
    public static string? FindProblem(EtwLayoutDefinition layout, Func<string, bool> isKnownPane)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(isKnownPane);

        HashSet<string> seen = new(StringComparer.Ordinal);
        return FindProblem(layout.Root, isKnownPane, seen, insideTabGroup: false);
    }

    /// <summary>
    /// <paramref name="layout"/> as it is shown: with an editor pane if it had none. Does not change
    /// sizes; <see cref="SanitizeSize"/> does that when a size is used, so the file keeps what it said.
    /// </summary>
    public static EtwLayoutDefinition Normalize(EtwLayoutDefinition layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (ContainsPane(layout.Root, PaneIds.Editor))
            return layout;

        DockChild editor = new(new PaneNode(PaneIds.Editor), 1, DockSizeUnit.Star);
        DockNode root = layout.Root is SplitNode { Orientation: DockOrientation.Horizontal } split
            ? split with { Children = [editor, .. split.Children] }
            : new SplitNode(DockOrientation.Horizontal, [editor, new DockChild(layout.Root, 1, DockSizeUnit.Star)]);

        return layout with { Root = root };
    }

    /// <summary>
    /// A size that can be used: <paramref name="size"/> when it is a positive, finite number,
    /// otherwise <paramref name="fallback"/>.
    /// </summary>
    public static double SanitizeSize(double size, double fallback)
        => double.IsFinite(size) && size > 0 ? size : fallback;

    /// <summary>Every pane ID in the tree, in order, including tabs that are not selected.</summary>
    public static IEnumerable<string> PaneIdsIn(DockNode node) => node switch
    {
        PaneNode pane => [pane.PaneId],
        TabGroupNode tabs => tabs.Tabs.Select(tab => tab.PaneId),
        SplitNode split => split.Children.SelectMany(child => PaneIdsIn(child.Node)),
        _ => [],
    };

    public static bool ContainsPane(DockNode node, string paneId)
        => PaneIdsIn(node).Contains(paneId, StringComparer.Ordinal);

    private static string? FindProblem(DockNode node, Func<string, bool> isKnownPane, HashSet<string> seen, bool insideTabGroup)
    {
        switch (node)
        {
            case PaneNode pane:
                if (pane.PaneId == PaneIds.Editor && insideTabGroup)
                    return "The editor can't be one of the tabs in a tab group.";

                if (pane.PaneId != PaneIds.Editor && !isKnownPane(pane.PaneId))
                    return $"The layout names a pane this version of Text Grab doesn't have (\"{pane.PaneId}\").";

                if (!seen.Add(pane.PaneId))
                    return $"The layout has the same pane more than once (\"{pane.PaneId}\").";

                return null;

            case TabGroupNode tabs:
                if (tabs.Tabs.Count == 0)
                    return "The layout has a tab group with no tabs.";

                foreach (PaneNode tab in tabs.Tabs)
                {
                    if (FindProblem(tab, isKnownPane, seen, insideTabGroup: true) is string tabProblem)
                        return tabProblem;
                }

                return null;

            case SplitNode split:
                if (split.Children.Count == 0)
                    return "The layout has a split with nothing in it.";

                foreach (DockChild child in split.Children)
                {
                    if (FindProblem(child.Node, isKnownPane, seen, insideTabGroup) is string childProblem)
                        return childProblem;
                }

                return null;

            default:
                return "The layout has a part this version of Text Grab doesn't know.";
        }
    }
}
