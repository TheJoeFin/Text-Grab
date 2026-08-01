using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

/// <summary>
/// Runs a unified <see cref="PatternItem"/> against text, dispatching to the right engine:
/// recognizers go through <see cref="RecognizerExecutor"/>, saved regexes through
/// <see cref="Regex"/>. This lets every UI surface (Edit Text Window apply, Grab Templates,
/// and the search features) treat both kinds the same.
///
/// Like <see cref="RecognizerExecutor"/>, the methods never throw — invalid regexes or
/// timeouts yield no matches.
/// </summary>
public static class PatternExecutor
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    /// <summary>True when the pattern finds at least one match in <paramref name="text"/>.</summary>
    public static bool HasMatch(PatternItem item, string text)
        => GetMatches(item, text).Count > 0;

    /// <summary>
    /// Returns every match in <paramref name="text"/>, ordered by position. Reuses the
    /// <see cref="RecognizerMatch"/> shape; saved-regex matches carry the matched text as
    /// both <see cref="RecognizerMatch.Text"/> and <see cref="RecognizerMatch.ResolvedValue"/>
    /// (a regex has no normalized resolution).
    /// </summary>
    public static IReadOnlyList<RecognizerMatch> GetMatches(PatternItem item, string text)
    {
        if (item is null || string.IsNullOrEmpty(text))
            return [];

        if (item.Kind == PatternKind.Recognizer && item.Recognizer is not null)
            return RecognizerExecutor.GetMatches(item.Recognizer, text);

        if (item.Kind == PatternKind.SavedRegex && item.SavedRegex is not null)
            return GetRegexMatches(item.SavedRegex.Pattern, text);

        return [];
    }

    /// <summary>
    /// Runs the pattern over <paramref name="text"/>, selects matches per
    /// <paramref name="mode"/> ("first"/"last"/"all"/nth/"1,3"), and joins them with
    /// <paramref name="separator"/>. For recognizers, <paramref name="output"/> chooses the
    /// resolved value or the matched text; saved regexes always emit the matched text.
    /// Returns an empty string when there are no matches.
    /// </summary>
    public static string Apply(
        PatternItem item,
        string text,
        string mode = "all",
        string separator = ", ",
        RecognizerOutputKind output = RecognizerOutputKind.ResolvedValue)
    {
        if (item is null)
            return string.Empty;

        if (item.Kind == PatternKind.Recognizer && item.Recognizer is not null)
            return RecognizerExecutor.ApplyRecognizer(item.Recognizer, text, mode, separator, output);

        IReadOnlyList<RecognizerMatch> matches = GetMatches(item, text);
        if (matches.Count == 0)
            return string.Empty;

        List<string> values = [.. matches.Select(m => m.Text)];
        return GrabTemplateExecutor.ExtractMatchesByMode(values, mode, separator);
    }

    private static IReadOnlyList<RecognizerMatch> GetRegexMatches(string pattern, string text)
    {
        if (string.IsNullOrEmpty(pattern))
            return [];

        try
        {
            MatchCollection matches = Regex.Matches(text, pattern, RegexOptions.Multiline, RegexTimeout);
            return [.. matches
                .Cast<Match>()
                .Where(m => m.Success)
                .Select(m => new RecognizerMatch(m.Index, m.Length, m.Value, m.Value))];
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
        catch (ArgumentException)
        {
            return []; // invalid regex
        }
    }
}
