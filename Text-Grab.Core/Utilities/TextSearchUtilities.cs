using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Text_Grab.Utilities;

public static class TextSearchUtilities
{
    private static readonly TimeSpan DefaultRegexTimeout = TimeSpan.FromSeconds(5);

    public static bool HasSearchText(string? searchText) => !string.IsNullOrEmpty(searchText);

    public static string FormatMatchTextForDisplay(string matchText)
    {
        if (!matchText.All(char.IsWhiteSpace))
            return matchText.MakeStringSingleLine();

        StringBuilder displayText = new();
        for (int i = 0; i < matchText.Length; i++)
        {
            if (matchText[i] == '\r' && i + 1 < matchText.Length && matchText[i + 1] == '\n')
            {
                displayText.Append('⏎');
                i++;
                continue;
            }

            char character = matchText[i];
            displayText.Append(character switch
            {
                ' ' => '·',
                '\t' => '⇥',
                '\r' => '␍',
                '\n' => '⏎',
                _ => '␣'
            });
        }

        return displayText.ToString();
    }

    public static Regex CreateFindAndReplaceSearchRegex(string pattern, bool usePatternMode, bool exactMatch)
    {
        RegexOptions options = RegexOptions.Multiline;

        if (!exactMatch && !usePatternMode)
            options |= RegexOptions.IgnoreCase;

        return new Regex(pattern, options, DefaultRegexTimeout);
    }

    public static Regex CreateReplacementRegex(string pattern, bool exactMatch)
    {
        RegexOptions options = exactMatch ? RegexOptions.None : RegexOptions.IgnoreCase;
        return new Regex(pattern, options, DefaultRegexTimeout);
    }

    public static Regex CreateGrabFrameSearchRegex(string pattern, bool exactMatch)
    {
        RegexOptions options = exactMatch ? RegexOptions.Multiline : RegexOptions.Multiline | RegexOptions.IgnoreCase;
        return new Regex(pattern, options, DefaultRegexTimeout);
    }

    public static (string Text, IReadOnlyList<(int SourceIndex, int Start, int Length)> Segments) BuildSearchText(
        IReadOnlyList<(string Text, double Left)> sourceItems,
        bool isSpaceJoining,
        bool isRightToLeft)
    {
        IEnumerable<(string Text, double Left, int SourceIndex)> orderedItems = sourceItems
            .Select((item, index) => (item.Text, item.Left, SourceIndex: index));
        orderedItems = isRightToLeft
            ? orderedItems.OrderByDescending(item => item.Left)
            : orderedItems.OrderBy(item => item.Left);

        string separator = isSpaceJoining ? " " : string.Empty;
        StringBuilder text = new();
        List<(int SourceIndex, int Start, int Length)> segments = [];

        foreach ((string itemText, double _, int sourceIndex) in orderedItems)
        {
            if (text.Length > 0)
                text.Append(separator);

            int start = text.Length;
            text.Append(itemText);
            segments.Add((sourceIndex, start, itemText.Length));
        }

        return (text.ToString(), segments);
    }

    public static bool SpansOverlap(int firstStart, int firstLength, int secondStart, int secondLength)
    {
        return firstLength > 0
            && secondLength > 0
            && firstStart < secondStart + secondLength
            && secondStart < firstStart + firstLength;
    }
}
