namespace Text_Grab.Core.Editing;

/// <summary>
/// What a brace-language's comments and strings look like, so <see cref="BraceFolding"/> can skip
/// brackets that are only text.
/// </summary>
/// <param name="Brackets">The bracket pairs that fold, as "{}" or "[]".</param>
/// <param name="LineComments">Prefixes that comment out the rest of the line.</param>
/// <param name="BlockComments">Opening and closing delimiters of block comments.</param>
/// <param name="Quotes">Characters that open and close a string or character literal on one line.</param>
/// <param name="EscapeCharacter">The character that escapes the next one inside a string.</param>
/// <param name="MultiLineQuotes">Quote characters whose strings may span lines (JavaScript's backtick).</param>
/// <param name="VerbatimStrings">C#-style <c>@"..."</c> strings: multi-line, <c>""</c> escapes a quote.</param>
/// <param name="TripleQuotedStrings"><c>"""..."""</c> raw strings and text blocks (C#, Java).</param>
public sealed record BraceFoldingSyntax(
    IReadOnlyList<string> Brackets,
    IReadOnlyList<string> LineComments,
    IReadOnlyList<(string Open, string Close)> BlockComments,
    string Quotes,
    char EscapeCharacter = '\\',
    string MultiLineQuotes = "",
    bool VerbatimStrings = false,
    bool TripleQuotedStrings = false)
{
    public static BraceFoldingSyntax CStyle { get; } = new(["{}"], ["//"], [("/*", "*/")], "\"'");
}

/// <summary>
/// Finds the regions between matching brackets that span more than one line, for code folding.
/// </summary>
/// <remarks>
/// A single pass with a stack. Brackets inside comments and strings are skipped. Mismatched or
/// unclosed brackets, common in OCR'd code, are tolerated: a closing bracket pairs with the nearest
/// open bracket of its kind, and anything left open is ignored.
/// </remarks>
public static class BraceFolding
{
    /// <summary>
    /// The foldable regions of <paramref name="text"/>, sorted by start. Each runs from just before its
    /// opening bracket to just after its closing one. When the opening bracket starts its own line
    /// (C#'s usual style), the region starts at the end of the previous line instead, so the collapsed
    /// line still shows the signature: <c>void Run()[...]</c>.
    /// </summary>
    public static IReadOnlyList<TextSpan> Find(string text, BraceFoldingSyntax syntax)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(syntax);

        List<TextSpan> folds = [];
        List<(char Open, int Offset, int Line)> open = [];
        int line = 0;
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (c == '\n')
            {
                line++;
                i++;
                continue;
            }

            if (TrySkipComment(text, ref i, ref line, syntax) || TrySkipString(text, ref i, ref line, syntax))
                continue;

            if (IndexOfBracket(syntax, c, opening: true) >= 0)
            {
                open.Add((c, i, line));
            }
            else if (IndexOfBracket(syntax, c, opening: false) is int pair and >= 0)
            {
                char opener = syntax.Brackets[pair][0];
                int match = open.FindLastIndex(entry => entry.Open == opener);
                if (match >= 0)
                {
                    (_, int openOffset, int openLine) = open[match];
                    open.RemoveRange(match, open.Count - match);

                    if (line > openLine)
                    {
                        int start = FoldStart(text, openOffset);
                        folds.Add(new TextSpan(start, i + 1 - start));
                    }
                }
            }

            i++;
        }

        folds.Sort((a, b) => a.Start.CompareTo(b.Start));
        return folds;
    }

    /// <summary>
    /// The end of the previous line when the bracket at <paramref name="bracketOffset"/> is the first
    /// thing on its line and the previous line is a header; otherwise the bracket itself.
    /// </summary>
    private static int FoldStart(string text, int bracketOffset)
    {
        int lineStart = bracketOffset;
        while (lineStart > 0 && text[lineStart - 1] != '\n')
        {
            if (!char.IsWhiteSpace(text[lineStart - 1]))
                return bracketOffset;

            lineStart--;
        }

        if (lineStart == 0)
            return bracketOffset;

        int previousLineEnd = lineStart - 1;
        if (previousLineEnd > 0 && text[previousLineEnd - 1] == '\r')
            previousLineEnd--;

        int previousLineStart = previousLineEnd;
        while (previousLineStart > 0 && text[previousLineStart - 1] != '\n')
            previousLineStart--;

        // Only a header (a signature, "else", "new Foo") is worth keeping in view; a line that ends a
        // statement or a block, or opens one of its own, is not. A trailing comma means the previous
        // line is a sibling item (JSON's "}," between array elements), and starting there would make
        // neighbouring folds overlap on one line.
        ReadOnlySpan<char> previousLine = text.AsSpan(previousLineStart, previousLineEnd - previousLineStart).Trim();
        bool previousLineIsHeader = previousLine.Length > 0 && previousLine[^1] is not ('{' or '}' or ';' or ',' or '[');
        return previousLineIsHeader ? previousLineEnd : bracketOffset;
    }

    private static int IndexOfBracket(BraceFoldingSyntax syntax, char c, bool opening)
    {
        for (int pair = 0; pair < syntax.Brackets.Count; pair++)
        {
            if (syntax.Brackets[pair][opening ? 0 : 1] == c)
                return pair;
        }

        return -1;
    }

    private static bool TrySkipComment(string text, ref int i, ref int line, BraceFoldingSyntax syntax)
    {
        foreach (string prefix in syntax.LineComments)
        {
            if (string.CompareOrdinal(text, i, prefix, 0, prefix.Length) == 0)
            {
                int end = text.IndexOf('\n', i);
                i = end < 0 ? text.Length : end;
                return true;
            }
        }

        foreach ((string opening, string closing) in syntax.BlockComments)
        {
            if (string.CompareOrdinal(text, i, opening, 0, opening.Length) == 0)
            {
                int end = text.IndexOf(closing, i + opening.Length, StringComparison.Ordinal);
                int stop = end < 0 ? text.Length : end + closing.Length;
                line += CountLines(text, i, stop);
                i = stop;
                return true;
            }
        }

        return false;
    }

    private static bool TrySkipString(string text, ref int i, ref int line, BraceFoldingSyntax syntax)
    {
        char c = text[i];

        if (syntax.TripleQuotedStrings && string.CompareOrdinal(text, i, "\"\"\"", 0, 3) == 0)
        {
            int end = text.IndexOf("\"\"\"", i + 3, StringComparison.Ordinal);
            int stop = end < 0 ? text.Length : end + 3;
            line += CountLines(text, i, stop);
            i = stop;
            return true;
        }

        if (syntax.VerbatimStrings && c == '@' && i + 1 < text.Length && text[i + 1] == '"')
        {
            int j = i + 2;
            while (j < text.Length)
            {
                if (text[j] == '"')
                {
                    if (j + 1 < text.Length && text[j + 1] == '"')
                    {
                        j += 2;
                        continue;
                    }

                    j++;
                    break;
                }

                j++;
            }

            line += CountLines(text, i, j);
            i = j;
            return true;
        }

        if (syntax.Quotes.IndexOf(c) < 0 && syntax.MultiLineQuotes.IndexOf(c) < 0)
            return false;

        bool multiLine = syntax.MultiLineQuotes.IndexOf(c) >= 0;
        int k = i + 1;
        while (k < text.Length)
        {
            char current = text[k];
            if (current == syntax.EscapeCharacter)
            {
                k += 2;
                continue;
            }

            if (current == c)
            {
                k++;
                break;
            }

            // A one-line string ends at the line break even when unterminated, so a stray quote
            // (an apostrophe in a comment-less language, an OCR error) can't swallow the file.
            if (current == '\n' && !multiLine)
                break;

            k++;
        }

        k = Math.Min(k, text.Length);
        line += CountLines(text, i, k);
        i = k;
        return true;
    }

    private static int CountLines(string text, int start, int end)
    {
        int count = 0;
        for (int index = start; index < end; index++)
        {
            if (text[index] == '\n')
                count++;
        }

        return count;
    }
}
