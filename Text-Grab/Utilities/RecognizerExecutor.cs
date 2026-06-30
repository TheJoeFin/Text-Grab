using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Recognizers.Text;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

/// <summary>
/// What a recognizer emits for each match when applied to text.
/// </summary>
public enum RecognizerOutputKind
{
    /// <summary>The normalized/resolved value (e.g. "next tuesday" → 2026-07-07, "$5" → "5 Dollar").</summary>
    ResolvedValue,

    /// <summary>The original matched span exactly as it appeared in the source text.</summary>
    MatchedText
}

/// <summary>
/// A single recognized entity found in text.
/// </summary>
/// <param name="Start">Zero-based index of the first character of the match.</param>
/// <param name="Length">Number of characters in the matched span.</param>
/// <param name="Text">The matched span as it appeared in the source text.</param>
/// <param name="ResolvedValue">The normalized value for the match, or the matched text when no resolution exists.</param>
public readonly record struct RecognizerMatch(int Start, int Length, string Text, string ResolvedValue);

/// <summary>
/// Runs the built-in Microsoft Recognizers-Text recognizers against text.
/// Parallels <see cref="GrabTemplateExecutor"/>'s pattern logic, but recognizer-based.
/// The library calls are synchronous, so this is too.
///
/// Used by the Edit Text Window (apply), Grab Templates (<c>{r:Name:mode}</c> placeholders),
/// and the search features (Grab Frame, Quick Simple Lookup, Find &amp; Replace).
/// </summary>
public static class RecognizerExecutor
{
    /// <summary>
    /// Culture passed to the recognizers. English for now; centralized here so it is
    /// easy to make configurable later.
    /// </summary>
    public const string DefaultCulture = Culture.English;

    // ── Low-level matching (shared with search features) ────────────────────────

    /// <summary>
    /// Returns every entity the recognizer finds in <paramref name="text"/>, ordered by position.
    /// Never throws — recognizer failures yield an empty list.
    /// </summary>
    public static IReadOnlyList<RecognizerMatch> GetMatches(BuiltInRecognizer recognizer, string text, string? culture = null)
    {
        if (recognizer is null || string.IsNullOrEmpty(text))
            return [];

        List<ModelResult> results;
        try
        {
            results = recognizer.Recognize(text, culture ?? DefaultCulture);
        }
        catch (Exception)
        {
            return [];
        }

        if (results is null || results.Count == 0)
            return [];

        return [.. results
            .OrderBy(r => r.Start)
            .Select(r => new RecognizerMatch(
                Start: r.Start,
                Length: (r.End - r.Start) + 1, // Recognizers-Text End is the inclusive last index
                Text: r.Text,
                ResolvedValue: FormatResolvedValue(r)))];
    }

    /// <summary>True when the recognizer finds at least one entity in <paramref name="text"/>.</summary>
    public static bool HasMatch(BuiltInRecognizer recognizer, string text, string? culture = null)
        => GetMatches(recognizer, text, culture).Count > 0;

    // ── Application (Edit Text Window / templates) ──────────────────────────────

    /// <summary>
    /// Runs the recognizer over <paramref name="text"/>, selects matches per
    /// <paramref name="matchMode"/> ("first"/"last"/"all"/nth/"1,3"), and joins the chosen
    /// matches (resolved value or matched text) using <paramref name="separator"/>.
    /// Returns an empty string when there are no matches.
    /// </summary>
    public static string ApplyRecognizer(
        BuiltInRecognizer recognizer,
        string text,
        string matchMode = "all",
        string separator = ", ",
        RecognizerOutputKind output = RecognizerOutputKind.ResolvedValue,
        string? culture = null)
    {
        IReadOnlyList<RecognizerMatch> matches = GetMatches(recognizer, text, culture);
        if (matches.Count == 0)
            return string.Empty;

        List<string> values = [.. matches.Select(m =>
            output == RecognizerOutputKind.MatchedText ? m.Text : m.ResolvedValue)];

        return GrabTemplateExecutor.ExtractMatchesByMode(values, matchMode, separator);
    }

    // ── Resolution formatting ───────────────────────────────────────────────────

    /// <summary>
    /// Produces a normalized string for a recognizer result from its
    /// <see cref="ModelResult.Resolution"/> dictionary, falling back to the matched text.
    /// </summary>
    public static string FormatResolvedValue(ModelResult result)
    {
        IReadOnlyDictionary<string, object>? resolution = result.Resolution;
        if (resolution is null || resolution.Count == 0)
            return result.Text;

        // DateTime family stores a "values" list of dictionaries (datetime, daterange, set, …)
        if (resolution.TryGetValue("values", out object? valuesObj)
            && valuesObj is IEnumerable<Dictionary<string, string>> values)
        {
            Dictionary<string, string>? first = values.FirstOrDefault();
            if (first is not null)
            {
                if (first.TryGetValue("value", out string? v) && IsResolvedValue(v))
                    return v!;

                if (first.TryGetValue("start", out string? start) && first.TryGetValue("end", out string? end))
                    return $"{start} → {end}";

                if (first.TryGetValue("timex", out string? timex) && !string.IsNullOrEmpty(timex))
                    return timex;
            }

            return result.Text;
        }

        // Most other recognizers expose a "value", optionally with a "unit".
        if (resolution.TryGetValue("value", out object? valueObj) && valueObj is not null)
        {
            string value = valueObj.ToString() ?? string.Empty;
            if (resolution.TryGetValue("unit", out object? unitObj)
                && unitObj?.ToString() is { Length: > 0 } unit)
                return $"{value} {unit}";

            return value;
        }

        return result.Text;
    }

    private static bool IsResolvedValue(string? value)
        => !string.IsNullOrEmpty(value)
           && !value.Equals("not resolved", StringComparison.OrdinalIgnoreCase);
}
