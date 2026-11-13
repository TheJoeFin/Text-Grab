namespace Text_Grab.Core.Editing;

/// <summary>
/// Finds the bracket that pairs with the one beside the caret, for the code editor's bracket highlight.
/// </summary>
/// <remarks>
/// A plain nesting count: it does not know about strings or comments, so a bracket inside either can
/// throw the match off. That is fine for a highlight hint, and it keeps this language-agnostic.
/// </remarks>
public static class BracketMatcher
{
    /// <summary>The farthest the matcher scans, so a huge document with no match stays fast.</summary>
    public const int MaxScanDistance = 20_000;

    private const string Openers = "([{";
    private const string Closers = ")]}";

    /// <summary>
    /// The offsets of the bracket beside <paramref name="caretOffset"/> and its partner, or null when the
    /// caret is not beside a bracket or the bracket is unmatched. The bracket before the caret wins over
    /// the one after it, as in most editors.
    /// </summary>
    public static (int Bracket, int Partner)? Find(string text, int caretOffset)
    {
        if (string.IsNullOrEmpty(text) || caretOffset < 0 || caretOffset > text.Length)
            return null;

        if (caretOffset > 0 && TryMatch(text, caretOffset - 1) is int before)
            return (caretOffset - 1, before);

        if (caretOffset < text.Length && TryMatch(text, caretOffset) is int after)
            return (caretOffset, after);

        return null;
    }

    private static int? TryMatch(string text, int offset)
    {
        char c = text[offset];
        int open = Openers.IndexOf(c);
        if (open >= 0)
            return Scan(text, offset, c, Closers[open], 1);

        int close = Closers.IndexOf(c);
        if (close >= 0)
            return Scan(text, offset, c, Openers[close], -1);

        return null;
    }

    private static int? Scan(string text, int start, char same, char opposite, int direction)
    {
        int depth = 0;
        int limit = Math.Min(MaxScanDistance, direction > 0 ? text.Length - start : start + 1);

        for (int step = 0; step < limit; step++)
        {
            int index = start + (step * direction);
            char c = text[index];

            if (c == same)
            {
                depth++;
            }
            else if (c == opposite)
            {
                depth--;
                if (depth == 0)
                    return index;
            }
        }

        return null;
    }
}
