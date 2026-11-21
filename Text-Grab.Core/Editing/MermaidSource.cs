using System;
using System.Collections.Generic;
using Text_Grab.Models;

namespace Text_Grab.Core.Editing;

/// <summary>A fenced <c>mermaid</c> block of a Markdown document.</summary>
/// <param name="Source">The diagram's source: the lines between the fences, joined with <c>\n</c>.</param>
/// <param name="Block">The whole block, from the start of its opening fence's line to the end of its closing fence's line.</param>
public sealed record MermaidBlock(string Source, TextSpan Block);

/// <summary>
/// Chooses the text the Mermaid pane draws (v5 E5 <c>mermaid</c>): the whole document, or when the document
/// is Markdown, the fenced <c>mermaid</c> block the caret is in.
/// </summary>
public static class MermaidSource
{
    /// <summary>
    /// The text to render for a document in <paramref name="mode"/>. In Markdown mode, the block at
    /// <paramref name="caretOffset"/> or null when the caret is outside every block; in any other mode the
    /// whole document.
    /// </summary>
    public static string? ForDocument(string text, EtwEditorMode mode, int caretOffset)
        => mode == EtwEditorMode.Markdown ? FindBlockAt(text, caretOffset)?.Source : text;

    /// <summary>
    /// The <c>mermaid</c> block whose lines (fences included) contain <paramref name="caretOffset"/>, or
    /// null. Fences may sit in a block quote or list item, as Markdown mode draws them; the info string's
    /// first word must be <c>mermaid</c>, in any case. A block never closed runs to the end of the text.
    /// </summary>
    public static MermaidBlock? FindBlockAt(string text, int caretOffset)
    {
        int caret = Math.Clamp(caretOffset, 0, text.Length);
        foreach (MermaidBlock block in FindBlocks(text))
        {
            if (caret >= block.Block.Start && caret <= block.Block.End)
                return block;
        }

        return null;
    }

    /// <summary>The first <c>mermaid</c> block in the text, or null.</summary>
    public static MermaidBlock? FindFirstBlock(string text) => FindBlocks(text) is [MermaidBlock first, ..] ? first : null;

    /// <summary>Every <c>mermaid</c> block in the text, in order.</summary>
    public static IReadOnlyList<MermaidBlock> FindBlocks(string text)
    {
        List<MermaidBlock> blocks = [];
        if (string.IsNullOrEmpty(text))
            return blocks;

        int position = 0;

        while (position <= text.Length)
        {
            (int lineEnd, int nextLine) = LineBounds(text, position);
            if (!TryOpeningFence(text.AsSpan(position, lineEnd - position), out int prefixLength, out char fenceChar, out int fenceLength))
            {
                position = nextLine;
                continue;
            }

            List<string> lines = [];
            int cursor = nextLine;
            int blockEnd = text.Length;
            while (cursor < text.Length)
            {
                (int end, int next) = LineBounds(text, cursor);
                ReadOnlySpan<char> line = text.AsSpan(cursor, end - cursor);
                cursor = next;
                if (IsClosingFence(line, prefixLength, fenceChar, fenceLength))
                {
                    blockEnd = end;
                    break;
                }

                lines.Add(StripPrefix(line, prefixLength));
            }

            blocks.Add(new MermaidBlock(string.Join('\n', lines), new TextSpan(position, blockEnd - position)));
            position = cursor;
        }

        return blocks;
    }

    // The end of the line's content, and where the next line starts (past the text's end after the last line).
    private static (int ContentEnd, int NextLine) LineBounds(string text, int start)
    {
        int newline = text.AsSpan(start).IndexOfAny('\r', '\n');
        if (newline < 0)
            return (text.Length, text.Length + 1);

        int end = start + newline;
        int next = end + 1;
        if (text[end] == '\r' && next < text.Length && text[next] == '\n')
            next++;

        return (end, next);
    }

    private static bool TryOpeningFence(ReadOnlySpan<char> line, out int prefixLength, out char fenceChar, out int fenceLength)
    {
        prefixLength = 0;
        fenceChar = '\0';
        fenceLength = 0;

        int i = SkipContainerPrefix(line);
        if (i >= line.Length || (line[i] != '`' && line[i] != '~'))
            return false;

        char marker = line[i];
        int run = 0;
        while (i + run < line.Length && line[i + run] == marker)
            run++;

        if (run < 3)
            return false;

        ReadOnlySpan<char> info = line[(i + run)..].Trim();
        if (marker == '`' && info.Contains('`'))
            return false;

        int wordEnd = info.IndexOfAny(' ', '\t', '{');
        ReadOnlySpan<char> word = wordEnd < 0 ? info : info[..wordEnd];
        if (!word.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
            return false;

        prefixLength = i;
        fenceChar = marker;
        fenceLength = run;
        return true;
    }

    private static bool IsClosingFence(ReadOnlySpan<char> line, int prefixLength, char fenceChar, int fenceLength)
    {
        ReadOnlySpan<char> content = StripPrefix(line, prefixLength).AsSpan().Trim();
        if (content.Length < fenceLength)
            return false;

        foreach (char c in content)
        {
            if (c != fenceChar)
                return false;
        }

        return true;
    }

    private static bool IsContainerChar(char c) => c is ' ' or '\t' or '>';

    // Leading spaces and block quote markers, then at most one list marker ('-', '*' or '+' and a space).
    private static int SkipContainerPrefix(ReadOnlySpan<char> line)
    {
        int i = 0;
        while (i < line.Length && IsContainerChar(line[i]))
            i++;

        if (i + 1 < line.Length && line[i] is '-' or '*' or '+' && line[i + 1] == ' ')
        {
            i += 2;
            while (i < line.Length && IsContainerChar(line[i]))
                i++;
        }

        return i;
    }

    // Removes up to prefixLength leading spaces and quote markers: the container's indentation, not the diagram's.
    private static string StripPrefix(ReadOnlySpan<char> line, int prefixLength)
    {
        int i = 0;
        while (i < line.Length && i < prefixLength && IsContainerChar(line[i]))
            i++;

        return line[i..].ToString();
    }
}
