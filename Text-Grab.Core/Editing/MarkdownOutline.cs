namespace Text_Grab.Core.Editing;

/// <summary>A heading and the headings under it.</summary>
/// <param name="Heading">The heading.</param>
/// <param name="Index">Its position in the list of headings the outline was made from.</param>
/// <param name="Children">The headings nested under it, in document order.</param>
public sealed record MarkdownOutlineNode(MarkdownHeading Heading, int Index, IReadOnlyList<MarkdownOutlineNode> Children)
{
    public string Title => Heading.Title;

    public int Level => Heading.Level;
}

/// <summary>Nests a flat list of headings by level, for the table of contents tree.</summary>
public static class MarkdownOutline
{
    /// <summary>
    /// The top-level headings of <paramref name="headings"/>, each with those under it. A heading goes
    /// under the nearest heading before it that has a smaller level, so a document that jumps from
    /// <c>#</c> to <c>###</c> still nests it, and one that starts with <c>##</c> has it at the top.
    /// </summary>
    public static IReadOnlyList<MarkdownOutlineNode> Build(IReadOnlyList<MarkdownHeading> headings)
    {
        ArgumentNullException.ThrowIfNull(headings);

        List<Builder> roots = [];
        List<Builder> open = [];

        for (int i = 0; i < headings.Count; i++)
        {
            Builder node = new(headings[i], i);

            while (open.Count > 0 && open[^1].Heading.Level >= node.Heading.Level)
                open.RemoveAt(open.Count - 1);

            if (open.Count == 0)
                roots.Add(node);
            else
                open[^1].Children.Add(node);

            open.Add(node);
        }

        return [.. roots.Select(static root => root.ToNode())];
    }

    /// <summary>Whether two lists of headings read the same in a tree: the same levels and titles in the same order, wherever they sit in the text.</summary>
    public static bool HaveSameShape(IReadOnlyList<MarkdownHeading> first, IReadOnlyList<MarkdownHeading> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count != second.Count)
            return false;

        for (int i = 0; i < first.Count; i++)
        {
            if (first[i].Level != second[i].Level || !string.Equals(first[i].Title, second[i].Title, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private sealed class Builder(MarkdownHeading heading, int index)
    {
        public MarkdownHeading Heading { get; } = heading;

        public List<Builder> Children { get; } = [];

        public MarkdownOutlineNode ToNode() => new(Heading, index, [.. Children.Select(static child => child.ToNode())]);
    }
}
