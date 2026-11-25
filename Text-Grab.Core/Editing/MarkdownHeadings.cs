using System.Text;
using System.Text.RegularExpressions;

namespace Text_Grab.Core.Editing;

/// <summary>One heading in a Markdown document.</summary>
/// <param name="Level">1 to 6: the number of <c>#</c> characters.</param>
/// <param name="Title">The heading's text as people read it: inline markup such as <c>**</c> and links is removed.</param>
/// <param name="Offset">The offset of the start of the heading's line in the text it was found in.</param>
public sealed record MarkdownHeading(int Level, string Title, int Offset);

/// <summary>
/// Finds the headings of a Markdown document, for the table of contents pane and for deciding whether
/// there is anything to show one for.
/// </summary>
/// <remarks>
/// <para>
/// Only ATX headings (<c>## Title</c>) are found. In Markdown mode the session's text is the rendered
/// document serialized back to Markdown, which writes every heading that way, and that is where the table
/// of contents is offered. A <c>#</c> line inside a fenced code block or YAML front matter is not a
/// heading, and neither is <c>#tag</c>, which has no space after the hashes.
/// </para>
/// <para>
/// Offsets count a line break as the characters it is made of, like <see cref="TextSpan"/>.
/// </para>
/// </remarks>
public static partial class MarkdownHeadings
{
    private const int MaxLevel = 6;
    private const int MaxIndent = 3;

    /// <summary>Every heading in <paramref name="markdown"/>, in document order.</summary>
    public static IReadOnlyList<MarkdownHeading> Parse(string? markdown)
    {
        List<MarkdownHeading> headings = [];
        Scan(markdown, headings, stopAtFirst: false);
        return headings;
    }

    /// <summary>Whether <paramref name="markdown"/> has at least one heading. Stops reading at the first.</summary>
    public static bool HasAny(string? markdown)
    {
        List<MarkdownHeading> headings = [];
        Scan(markdown, headings, stopAtFirst: true);
        return headings.Count > 0;
    }

    private static void Scan(string? markdown, List<MarkdownHeading> headings, bool stopAtFirst)
    {
        if (string.IsNullOrEmpty(markdown))
            return;

        int position = 0;
        bool inFrontMatter = StartsFrontMatter(markdown);
        char fenceCharacter = '\0';
        int fenceLength = 0;
        bool firstLine = true;

        while (position < markdown.Length)
        {
            int lineStart = position;
            int lineEnd = lineStart;
            while (lineEnd < markdown.Length && markdown[lineEnd] is not ('\r' or '\n'))
                lineEnd++;

            position = lineEnd;
            if (position < markdown.Length)
                position += markdown[position] == '\r' && position + 1 < markdown.Length && markdown[position + 1] == '\n' ? 2 : 1;

            ReadOnlySpan<char> line = markdown.AsSpan(lineStart, lineEnd - lineStart);

            if (inFrontMatter)
            {
                // The opening line is the first; the block ends at the next --- or ... line.
                if (!firstLine && (line.TrimEnd().SequenceEqual("---") || line.TrimEnd().SequenceEqual("...")))
                    inFrontMatter = false;

                firstLine = false;
                continue;
            }

            firstLine = false;

            int indent = 0;
            while (indent < line.Length && line[indent] == ' ')
                indent++;

            if (indent > MaxIndent)
                continue;

            ReadOnlySpan<char> content = line[indent..];

            if (fenceLength > 0)
            {
                if (IsClosingFence(content, fenceCharacter, fenceLength))
                    fenceLength = 0;

                continue;
            }

            if (TryReadOpeningFence(content, out fenceCharacter, out fenceLength))
                continue;

            if (!TryReadHeading(content, out int level, out string title))
                continue;

            headings.Add(new MarkdownHeading(level, title, lineStart));
            if (stopAtFirst)
                return;
        }
    }

    private static bool StartsFrontMatter(string markdown)
    {
        int lineEnd = markdown.AsSpan().IndexOfAny('\r', '\n');
        ReadOnlySpan<char> first = lineEnd < 0 ? markdown : markdown.AsSpan(0, lineEnd);
        if (!first.TrimEnd().SequenceEqual("---"))
            return false;

        // A lone --- is a rule, not front matter: it needs a closing line.
        int position = lineEnd;
        while (position >= 0 && position < markdown.Length)
        {
            position += markdown[position] == '\r' && position + 1 < markdown.Length && markdown[position + 1] == '\n' ? 2 : 1;
            int next = markdown.AsSpan(position).IndexOfAny('\r', '\n');
            ReadOnlySpan<char> line = next < 0 ? markdown.AsSpan(position) : markdown.AsSpan(position, next);
            if (line.TrimEnd().SequenceEqual("---") || line.TrimEnd().SequenceEqual("..."))
                return true;

            if (next < 0)
                return false;

            position += next;
        }

        return false;
    }

    private static bool TryReadOpeningFence(ReadOnlySpan<char> content, out char character, out int length)
    {
        character = '\0';
        length = 0;
        if (content.Length < 3 || content[0] is not ('`' or '~'))
            return false;

        char marker = content[0];
        int run = 0;
        while (run < content.Length && content[run] == marker)
            run++;

        if (run < 3)
            return false;

        // A backtick fence's info string can't contain a backtick: ```code``` on one line is inline code.
        if (marker == '`' && content[run..].Contains('`'))
            return false;

        character = marker;
        length = run;
        return true;
    }

    private static bool IsClosingFence(ReadOnlySpan<char> content, char character, int openingLength)
    {
        int run = 0;
        while (run < content.Length && content[run] == character)
            run++;

        return run >= openingLength && content[run..].Trim().IsEmpty;
    }

    private static bool TryReadHeading(ReadOnlySpan<char> content, out int level, out string title)
    {
        level = 0;
        title = string.Empty;

        while (level < content.Length && content[level] == '#')
            level++;

        if (level is 0 or > MaxLevel)
            return false;

        if (level < content.Length && content[level] is not (' ' or '\t'))
            return false;

        ReadOnlySpan<char> rest = content[level..].Trim();

        // An optional closing run of # (preceded by a space) is not part of the title.
        int end = rest.Length;
        while (end > 0 && rest[end - 1] == '#')
            end--;

        if (end == 0)
            rest = [];
        else if (end < rest.Length && rest[end - 1] is ' ' or '\t')
            rest = rest[..end].TrimEnd();

        title = CleanTitle(rest.ToString());
        return title.Length > 0;
    }

    private static string CleanTitle(string text)
    {
        if (text.Length == 0)
            return text;

        string cleaned = LinkPattern().Replace(text, "$1");
        StringBuilder builder = new(cleaned.Length);
        for (int i = 0; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            // A backslash before punctuation escapes it, as in "\#1".
            if (c == '\\' && i + 1 < cleaned.Length && (char.IsPunctuation(cleaned[i + 1]) || char.IsSymbol(cleaned[i + 1])))
            {
                builder.Append(cleaned[++i]);
                continue;
            }

            // Emphasis and code markers; an underscore is left alone, as it is usually part of a word.
            if (c is '*' or '`')
                continue;

            if (c == '~' && i + 1 < cleaned.Length && cleaned[i + 1] == '~')
            {
                i++;
                continue;
            }

            builder.Append(c);
        }

        return WhitespacePattern().Replace(builder.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"!?\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
