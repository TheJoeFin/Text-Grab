namespace Text_Grab.Core.Editing;

/// <summary>
/// Finds the elements, multi-line tags and comments of XML or HTML that span more than one line, for
/// code folding.
/// </summary>
/// <remarks>
/// <para>
/// A forgiving scanner rather than an XML parser. AvalonEdit's own XML folding reads the text with a
/// strict XML reader and stops at the first error, which is most of the time for OCR'd markup and for
/// ordinary HTML (<c>&lt;br&gt;</c>, unclosed <c>&lt;p&gt;</c>). Here a closing tag pairs with the nearest
/// open tag of the same name, ignoring case, and whatever cannot be paired is left unfolded.
/// </para>
/// <para>
/// What stays visible when a region is collapsed: an element whose start tag fits on one line keeps both
/// tags, <c>&lt;div class="x"&gt;[...]&lt;/div&gt;</c>; one whose start tag spans lines collapses from its
/// name, <c>&lt;Button[...]&lt;/Button&gt;</c>; a multi-line self-closing tag collapses to
/// <c>&lt;TextBox[...]/&gt;</c>; a comment to <c>&lt;!--[...]--&gt;</c>.
/// </para>
/// </remarks>
public static class TagFolding
{
    /// <summary>HTML elements that never have a closing tag.</summary>
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr",
    };

    /// <summary>HTML elements whose content is not markup and must not be scanned for tags.</summary>
    private static readonly HashSet<string> RawTextElements = new(StringComparer.OrdinalIgnoreCase) { "script", "style" };

    /// <summary>The foldable regions of <paramref name="text"/>, sorted by start.</summary>
    /// <param name="isHtml">Treat void elements as never closed, and script and style content as text.</param>
    public static IReadOnlyList<TextSpan> Find(string text, bool isHtml)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<TextSpan> folds = [];
        List<OpenElement> open = [];
        int i = 0;

        while (i < text.Length)
        {
            int lt = text.IndexOf('<', i);
            if (lt < 0)
                break;

            if (StartsWith(text, lt, "<!--"))
            {
                int end = text.IndexOf("-->", lt + 4, StringComparison.Ordinal);
                if (end < 0)
                    break;

                AddIfMultiLine(text, folds, lt + 4, end);
                i = end + 3;
            }
            else if (StartsWith(text, lt, "<![CDATA["))
            {
                int end = text.IndexOf("]]>", lt + 9, StringComparison.Ordinal);
                if (end < 0)
                    break;

                AddIfMultiLine(text, folds, lt + 9, end);
                i = end + 3;
            }
            else if (StartsWith(text, lt, "<?") || StartsWith(text, lt, "<!"))
            {
                int end = text.IndexOf('>', lt + 2);
                i = end < 0 ? text.Length : end + 1;
            }
            else if (StartsWith(text, lt, "</"))
            {
                i = ReadClosingTag(text, lt, open, folds);
            }
            else if (lt + 1 < text.Length && IsNameStart(text[lt + 1]))
            {
                i = ReadStartTag(text, lt, isHtml, open, folds);
            }
            else
            {
                // A '<' that starts no tag, as in "a < b".
                i = lt + 1;
            }
        }

        folds.Sort((a, b) => a.Start.CompareTo(b.Start));
        return folds;
    }

    private static int ReadStartTag(string text, int lt, bool isHtml, List<OpenElement> open, List<TextSpan> folds)
    {
        int nameEnd = lt + 1;
        while (nameEnd < text.Length && IsNameCharacter(text[nameEnd]))
            nameEnd++;

        string name = text[(lt + 1)..nameEnd];
        int gt = FindTagEnd(text, nameEnd);
        if (gt < 0)
            return nameEnd; // A broken tag: skip it and keep scanning.

        bool selfClosing = text[gt - 1] == '/';
        bool tagSpansLines = ContainsLineBreak(text, lt, gt);

        if (selfClosing || (isHtml && VoidElements.Contains(name)))
        {
            if (tagSpansLines)
                AddIfMultiLine(text, folds, nameEnd, selfClosing ? gt - 1 : gt);

            return gt + 1;
        }

        int contentStart = gt + 1;
        int foldStart = tagSpansLines ? nameEnd : contentStart;

        if (isHtml && RawTextElements.Contains(name))
        {
            int close = text.IndexOf("</" + name, contentStart, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
                return text.Length;

            AddIfMultiLine(text, folds, foldStart, close);
            int closeEnd = text.IndexOf('>', close);
            return closeEnd < 0 ? text.Length : closeEnd + 1;
        }

        open.Add(new OpenElement(name, foldStart));
        return contentStart;
    }

    private static int ReadClosingTag(string text, int lt, List<OpenElement> open, List<TextSpan> folds)
    {
        int nameEnd = lt + 2;
        while (nameEnd < text.Length && IsNameCharacter(text[nameEnd]))
            nameEnd++;

        string name = text[(lt + 2)..nameEnd];
        int gt = text.IndexOf('>', nameEnd);

        int match = open.FindLastIndex(element => string.Equals(element.Name, name, StringComparison.OrdinalIgnoreCase));
        if (match >= 0)
        {
            AddIfMultiLine(text, folds, open[match].FoldStart, lt);
            open.RemoveRange(match, open.Count - match);
        }

        return gt < 0 ? text.Length : gt + 1;
    }

    /// <summary>The offset of the '&gt;' ending a tag, skipping any inside quoted attribute values.</summary>
    private static int FindTagEnd(string text, int start)
    {
        char quote = '\0';
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return i;
            }
            else if (c == '<')
            {
                // A new tag starts before this one closed (OCR'd or broken markup).
                return -1;
            }
        }

        return -1;
    }

    private static void AddIfMultiLine(string text, List<TextSpan> folds, int start, int end)
    {
        if (end > start && ContainsLineBreak(text, start, end))
            folds.Add(new TextSpan(start, end - start));
    }

    private static bool ContainsLineBreak(string text, int start, int end)
        => text.AsSpan(start, end - start).IndexOfAny('\r', '\n') >= 0;

    private static bool StartsWith(string text, int index, string value)
        => string.CompareOrdinal(text, index, value, 0, value.Length) == 0;

    private static bool IsNameStart(char c) => char.IsLetter(c) || c is '_' or ':';

    private static bool IsNameCharacter(char c) => char.IsLetterOrDigit(c) || c is '_' or ':' or '-' or '.';

    private readonly record struct OpenElement(string Name, int FoldStart);
}
