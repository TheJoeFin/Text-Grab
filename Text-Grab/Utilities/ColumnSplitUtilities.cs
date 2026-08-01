using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

/// <summary>
/// How a spreadsheet cell's text should be broken into multiple columns.
/// </summary>
public enum SplitMode
{
    Delimiter,
    Regex,
    FixedLength,
}

/// <summary>
/// What to do with the splitter (delimiter / matched text) itself when splitting.
/// For "20.30" split on ".": <see cref="Remove"/> → ["20","30"];
/// <see cref="KeepLeft"/> → ["20.","30"]; <see cref="KeepRight"/> → ["20",".30"].
/// </summary>
public enum SplitterHandling
{
    Remove,
    KeepLeft,
    KeepRight,
}

/// <summary>
/// Configuration describing how to split a cell into parts. Shared by the
/// <c>SplitColumnWindow</c> preview and the actual apply in <c>EditTextWindow</c>.
/// </summary>
public record SplitColumnOptions
{
    public SplitMode Mode { get; init; } = SplitMode.Delimiter;

    /// <summary>Literal delimiter string used when <see cref="Mode"/> is <see cref="SplitMode.Delimiter"/>.</summary>
    public string DelimiterText { get; init; } = string.Empty;

    /// <summary>Raw regular expression used when <see cref="Mode"/> is <see cref="SplitMode.Regex"/>
    /// and no <see cref="PatternItem"/> is chosen.</summary>
    public string Pattern { get; init; } = string.Empty;

    /// <summary>A chosen saved regex or built-in smart pattern to split on. When set (and
    /// <see cref="Mode"/> is <see cref="SplitMode.Regex"/>), it takes precedence over
    /// <see cref="Pattern"/>.</summary>
    public PatternItem? PatternItem { get; init; }

    /// <summary>Whether the raw-regex split is case-insensitive.</summary>
    public bool IgnoreCase { get; init; }

    /// <summary>What to do with the splitter itself. Ignored for <see cref="SplitMode.FixedLength"/>.</summary>
    public SplitterHandling SplitterHandling { get; init; } = SplitterHandling.Remove;

    /// <summary>Character position used when <see cref="Mode"/> is <see cref="SplitMode.FixedLength"/>.</summary>
    public int Length { get; init; }

    /// <summary>When true, <see cref="Length"/> is measured from the end of the text.</summary>
    public bool SplitFromEnd { get; init; }
}

/// <summary>
/// Splits a single cell's text into parts according to a <see cref="SplitColumnOptions"/>.
/// Never throws: invalid input simply yields the original value as a single part.
/// </summary>
public static class ColumnSplitUtilities
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    public static IReadOnlyList<string> SplitCell(string value, SplitColumnOptions options)
    {
        value ??= string.Empty;
        ArgumentNullException.ThrowIfNull(options);

        return options.Mode switch
        {
            SplitMode.Delimiter => SplitOnDelimiter(value, options.DelimiterText, options.SplitterHandling),
            SplitMode.Regex => SplitOnPattern(value, options),
            SplitMode.FixedLength => SplitOnLength(value, options.Length, options.SplitFromEnd),
            _ => [value],
        };
    }

    private static IReadOnlyList<string> SplitOnPattern(string value, SplitColumnOptions options)
    {
        // A chosen saved/smart pattern wins over the raw regex box.
        return options.PatternItem is not null
            ? SplitByPatternItem(value, options.PatternItem, options.SplitterHandling)
            : SplitOnRegex(value, options.Pattern, options.IgnoreCase, options.SplitterHandling);
    }

    private static IReadOnlyList<string> SplitByPatternItem(string value, PatternItem patternItem, SplitterHandling handling)
    {
        IReadOnlyList<RecognizerMatch> matches = PatternExecutor.GetMatches(patternItem, value);
        List<(int Start, int Length)> spans = [.. matches.Select(m => (m.Start, m.Length))];
        return BuildPartsFromSpans(value, spans, handling);
    }

    private static IReadOnlyList<string> SplitOnDelimiter(string value, string delimiter, SplitterHandling handling)
    {
        if (string.IsNullOrEmpty(delimiter))
            return [value];

        List<(int Start, int Length)> spans = [];
        int searchFrom = 0;
        int foundAt;
        while ((foundAt = value.IndexOf(delimiter, searchFrom, StringComparison.Ordinal)) >= 0)
        {
            spans.Add((foundAt, delimiter.Length));
            searchFrom = foundAt + delimiter.Length;
        }

        return BuildPartsFromSpans(value, spans, handling);
    }

    private static IReadOnlyList<string> SplitOnRegex(string value, string pattern, bool ignoreCase, SplitterHandling handling)
    {
        if (string.IsNullOrEmpty(pattern))
            return [value];

        try
        {
            RegexOptions regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            MatchCollection matches = Regex.Matches(value, pattern, regexOptions, RegexTimeout);
            List<(int Start, int Length)> spans = [.. matches
                .Cast<Match>()
                .Where(m => m.Success)
                .Select(m => (m.Index, m.Length))];

            return BuildPartsFromSpans(value, spans, handling);
        }
        catch (Exception)
        {
            // Invalid pattern or timeout - leave the value unsplit rather than surfacing an error.
            return [value];
        }
    }

    private static IReadOnlyList<string> SplitOnLength(string value, int length, bool fromEnd)
    {
        int splitAt = fromEnd ? value.Length - length : length;
        splitAt = Math.Clamp(splitAt, 0, value.Length);

        return [value[..splitAt], value[splitAt..]];
    }

    /// <summary>
    /// Builds the resulting parts by splitting <paramref name="value"/> at the given splitter
    /// spans. <paramref name="handling"/> decides whether each splitter is dropped, kept on the
    /// left part, or kept on the right part. Overlapping and zero-width spans are skipped.
    /// </summary>
    private static IReadOnlyList<string> BuildPartsFromSpans(
        string value,
        List<(int Start, int Length)> spans,
        SplitterHandling handling)
    {
        List<(int Start, int Length)> orderedSpans = [.. spans
            .Where(s => s.Length > 0)
            .OrderBy(s => s.Start)];

        List<string> parts = [];
        int cursor = 0;

        foreach ((int start, int length) in orderedSpans)
        {
            if (start < cursor)
                continue; // skip overlapping spans

            int end = start + length;
            switch (handling)
            {
                case SplitterHandling.KeepLeft:
                    parts.Add(value[cursor..end]);
                    cursor = end;
                    break;

                case SplitterHandling.KeepRight:
                    parts.Add(value[cursor..start]);
                    cursor = start;
                    break;

                default: // Remove
                    parts.Add(value[cursor..start]);
                    cursor = end;
                    break;
            }
        }

        parts.Add(value[cursor..]);
        return parts;
    }
}
