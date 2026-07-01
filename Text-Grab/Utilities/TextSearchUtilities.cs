using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Text_Grab.Utilities;

internal static class TextSearchUtilities
{
    private static readonly TimeSpan DefaultRegexTimeout = TimeSpan.FromSeconds(5);

    internal static bool HasSearchText(string? searchText) => !string.IsNullOrEmpty(searchText);

    internal static string FormatMatchTextForDisplay(string matchText)
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

    internal static Regex CreateFindAndReplaceSearchRegex(string pattern, bool usePatternMode, bool exactMatch)
    {
        RegexOptions options = RegexOptions.Multiline;

        if (!exactMatch && !usePatternMode)
            options |= RegexOptions.IgnoreCase;

        return new Regex(pattern, options, DefaultRegexTimeout);
    }

    internal static Regex CreateReplacementRegex(string pattern, bool exactMatch)
    {
        RegexOptions options = exactMatch ? RegexOptions.None : RegexOptions.IgnoreCase;
        return new Regex(pattern, options, DefaultRegexTimeout);
    }

    internal static Regex CreateGrabFrameSearchRegex(string pattern, bool exactMatch)
    {
        RegexOptions options = exactMatch ? RegexOptions.Multiline : RegexOptions.Multiline | RegexOptions.IgnoreCase;
        return new Regex(pattern, options, DefaultRegexTimeout);
    }
}
