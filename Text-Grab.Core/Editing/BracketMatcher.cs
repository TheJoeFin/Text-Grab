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
        => text is null ? null : Find(text.Length, index => text[index], caretOffset);

    /// <summary>
    /// <see cref="Find(string, int)"/> over text read one character at a time by <paramref name="charAt"/>,
    /// so an editor's document need not be copied out to look beside the caret.
    /// </summary>
    /// <remarks>
    /// Only the two characters beside the caret are read unless one is a bracket; then at most
    /// <see cref="MaxScanDistance"/> more.
    /// </remarks>
    public static (int Bracket, int Partner)? Find(int textLength, Func<int, char> charAt, int caretOffset)
    {
        ArgumentNullException.ThrowIfNull(charAt);

        if (textLength <= 0 || caretOffset < 0 || caretOffset > textLength)
            return null;

        if (caretOffset > 0 && TryMatch(textLength, charAt, caretOffset - 1) is int before)
            return (caretOffset - 1, before);

        if (caretOffset < textLength && TryMatch(textLength, charAt, caretOffset) is int after)
            return (caretOffset, after);

        return null;
    }

    private static int? TryMatch(int textLength, Func<int, char> charAt, int offset)
    {
        char c = charAt(offset);
        int open = Openers.IndexOf(c);
        if (open >= 0)
            return Scan(textLength, charAt, offset, c, Closers[open], 1);

        int close = Closers.IndexOf(c);
        if (close >= 0)
            return Scan(textLength, charAt, offset, c, Openers[close], -1);

        return null;
    }

    private static int? Scan(int textLength, Func<int, char> charAt, int start, char same, char opposite, int direction)
    {
        int depth = 0;
        int limit = Math.Min(MaxScanDistance, direction > 0 ? textLength - start : start + 1);

        for (int step = 0; step < limit; step++)
        {
            int index = start + (step * direction);
            char c = charAt(index);

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
