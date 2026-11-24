using System;
using System.Windows;
using System.Windows.Documents;
using WpfBlock = System.Windows.Documents.Block;
using WpfList = System.Windows.Documents.List;

namespace Text_Grab.Utilities;

/// <summary>
/// How lists look by depth. Markdown keeps one marker per kind (<c>-</c> and <c>1.</c>), so this is
/// display only: the marker a level shows is chosen here, never written to the file.
/// </summary>
public static partial class MarkdownFlowDocumentUtilities
{
    /// <summary>
    /// Bullets by depth: a dot, a ring, a small filled square, then around again. WPF's names are the
    /// reverse of what they sound like: <see cref="TextMarkerStyle.Box"/> is the small filled square,
    /// and <see cref="TextMarkerStyle.Square"/> is a hollow box that reads as a to-do checkbox.
    /// </summary>
    private static readonly TextMarkerStyle[] BulletMarkerCycle = [TextMarkerStyle.Disc, TextMarkerStyle.Circle, TextMarkerStyle.Box];

    /// <summary>Numbered lists by depth: 1., a., i., then around again.</summary>
    private static readonly TextMarkerStyle[] OrderedMarkerCycle = [TextMarkerStyle.Decimal, TextMarkerStyle.LowerLatin, TextMarkerStyle.LowerRoman];

    /// <summary>How far each level steps in. WPF's own automatic width is 40 to 65 px, depending on the marker.</summary>
    internal const double ListLevelIndent = 26;

    /// <summary>The gap between a marker and its text.</summary>
    internal const double ListMarkerGap = 6;

    /// <summary>The space above and below a list item's text; the default paragraph margin is twice this.</summary>
    internal const double ListItemSpacing = 2;

    internal static bool IsOrderedMarker(TextMarkerStyle style) => style
        is TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin
        or TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman;

    internal static TextMarkerStyle GetListMarkerStyle(bool isOrdered, int depth)
    {
        TextMarkerStyle[] cycle = isOrdered ? OrderedMarkerCycle : BulletMarkerCycle;
        return cycle[Math.Max(0, depth) % cycle.Length];
    }

    /// <summary>
    /// Gives every list its marker, indent and spacing for its depth. Run it after a document is built
    /// and after anything that changes how lists nest: WPF copies the parent's marker into a list it
    /// nests, and starts bullets and numbers of its own with its automatic indent.
    /// </summary>
    internal static void ApplyListStyling(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        StyleLists(document.Blocks, depth: 0);
    }

    private static void StyleLists(BlockCollection blocks, int depth)
    {
        foreach (WpfBlock block in blocks)
        {
            if (block is WpfList list)
                StyleList(list, depth);
        }
    }

    private static void StyleList(WpfList list, int depth)
    {
        // A list of only to-dos shows no marker and a footnote list numbers itself: both keep their look.
        bool isRestyled = list.MarkerStyle != TextMarkerStyle.None && GetBlockRole(list) == MarkdownBlockRole.None;
        if (isRestyled)
        {
            list.MarkerStyle = GetListMarkerStyle(IsOrderedMarker(list.MarkerStyle), depth);
            list.Padding = new Thickness(ListLevelIndent, 0, 0, 0);
            list.MarkerOffset = ListMarkerGap;
        }

        foreach (ListItem item in list.ListItems)
        {
            foreach (WpfBlock child in item.Blocks)
            {
                if (child is WpfList nested)
                    StyleList(nested, depth + 1);
                else if (isRestyled && child is Paragraph paragraph && IsPlainListParagraph(paragraph))
                    paragraph.Margin = new Thickness(0, ListItemSpacing, 0, ListItemSpacing);
            }
        }
    }

    private static bool IsPlainListParagraph(Paragraph paragraph) =>
        GetHeadingLevel(paragraph) == 0 && GetQuoteDepth(paragraph) == 0 && GetBlockRole(paragraph) == MarkdownBlockRole.None;
}
