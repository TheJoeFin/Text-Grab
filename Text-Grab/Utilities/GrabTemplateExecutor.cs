using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Text_Grab.Interfaces;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

/// <summary>
/// Executes a <see cref="GrabTemplate"/> against a captured screen region:
/// OCRs each sub-region, then formats the results using the template's
/// <see cref="GrabTemplate.OutputTemplate"/> string.
///
/// Output template syntax — region placeholders:
///   {N}        — OCR text from region N  (1-based)
///   {N:trim}   — trimmed OCR text
///   {N:upper}  — uppercased OCR text
///   {N:lower}  — lowercased OCR text
///
/// Output template syntax — pattern placeholders:
///   {p:Name:first}     — first regex match
///   {p:Name:last}      — last regex match
///   {p:Name:all:, }    — all matches joined by separator
///   {p:Name:2}         — 2nd match (1-based)
///   {p:Name:1,3}       — 1st and 3rd matches joined by separator
///
/// Escape sequences:
///   \n         — newline
///   \t         — tab
///   \\         — literal backslash
///   \{         — literal opening brace
/// </summary>
public static class GrabTemplateExecutor
{
    // Matches {N} or {N:modifier} where N is one or more digits
    private static readonly Regex PlaceholderRegex =
        new(@"\{(\d+)(?::([a-z]+))?\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches {p:PatternName:mode} or {p:PatternName:mode:separator}
    // Group 1 = pattern name, Group 2 = match mode, Group 3 = optional separator
    private static readonly Regex PatternPlaceholderRegex =
        new(@"\{p:([^:}]+):([^:}]+)(?::([^}]*))?\}", RegexOptions.Compiled);

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes the given template using <paramref name="captureRegion"/> as the
    /// coordinate space. Each template region is mapped to a sub-rectangle of
    /// <paramref name="captureRegion"/>, OCR'd, then assembled via the output template.
    /// </summary>
    /// <param name="template">The template to execute.</param>
    /// <param name="captureRegion">
    ///     The screen rectangle in physical screen pixels that bounds the user's
    ///     selection. Template region ratios are applied to this rectangle's
    ///     width/height to derive each sub-region's capture bounds.
    /// </param>
    /// <param name="language">The OCR language to use. Pass null to use the app default.</param>
    public static async Task<string> ExecuteTemplateAsync(
        GrabTemplate template,
        Rect captureRegion,
        ILanguage? language = null)
    {
        if (!template.IsValid)
            return string.Empty;

        ILanguage resolvedLanguage = language ?? LanguageUtilities.GetOCRLanguage();

        // 1. OCR each region (if any)
        Dictionary<int, string> regionResults = template.Regions.Count > 0
            ? await OcrAllRegionsAsync(template, captureRegion, resolvedLanguage)
            : [];

        // 2. OCR full capture area for pattern matching (if any pattern references exist)
        string? fullAreaText = null;
        if (template.PatternMatches.Count > 0)
        {
            try
            {
                fullAreaText = await OcrUtilities.GetTextFromAbsoluteRectAsync(captureRegion, resolvedLanguage);
            }
            catch (Exception)
            {
                fullAreaText = string.Empty;
            }
        }

        // 3. Resolve pattern regexes from saved patterns
        Dictionary<string, string> patternRegexes = [];
        if (template.PatternMatches.Count > 0)
            patternRegexes = ResolvePatternRegexes(template.PatternMatches);

        // 4. Apply output template
        string output = ApplyOutputTemplate(template.OutputTemplate, regionResults);

        if (fullAreaText != null)
            output = ApplyPatternPlaceholders(output, fullAreaText, template.PatternMatches, patternRegexes);

        return output;
    }

    /// <summary>
    /// Applies the output template string with the provided region text values.
    /// Useful for unit testing the string processing independently of OCR.
    /// </summary>
    public static string ApplyOutputTemplate(
        string outputTemplate,
        IReadOnlyDictionary<int, string> regionResults)
    {
        if (string.IsNullOrEmpty(outputTemplate))
            return string.Empty;

        // Replace escape sequences first
        string processed = outputTemplate
            .Replace(@"\\", "\x00BACKSLASH\x00")  // protect real backslashes
            .Replace(@"\n", "\n")
            .Replace(@"\t", "\t")
            .Replace(@"\{", "\x00LBRACE\x00")     // protect literal braces
            .Replace("\x00BACKSLASH\x00", @"\");

        // Replace {N} / {N:modifier} placeholders
        string result = PlaceholderRegex.Replace(processed, match =>
        {
            if (!int.TryParse(match.Groups[1].Value, out int regionNumber))
                return match.Value; // leave unknown placeholders as-is

            regionResults.TryGetValue(regionNumber, out string? text);
            text ??= string.Empty;

            string modifier = match.Groups[2].Success
                ? match.Groups[2].Value.ToLowerInvariant()
                : string.Empty;

            return modifier switch
            {
                "trim" => text.Trim(),
                "upper" => text.ToUpper(),
                "lower" => text.ToLower(),
                _ => text
            };
        });

        // Restore protected literal characters
        result = result.Replace("\x00LBRACE\x00", "{");

        return result;
    }

    // ── Pattern placeholder processing ──────────────────────────────────────

    /// <summary>
    /// Replaces <c>{p:PatternName:mode}</c> and <c>{p:PatternName:mode:separator}</c>
    /// placeholders in the template with regex match results from the full-area OCR text.
    /// </summary>
    public static string ApplyPatternPlaceholders(
        string template,
        string fullText,
        IReadOnlyList<TemplatePatternMatch> patternMatches,
        IReadOnlyDictionary<string, string> patternRegexes)
    {
        if (string.IsNullOrEmpty(template) || patternMatches.Count == 0)
            return template;

        return PatternPlaceholderRegex.Replace(template, match =>
        {
            string patternName = match.Groups[1].Value;
            string mode = match.Groups[2].Value;
            string separatorOverride = match.Groups[3].Success ? match.Groups[3].Value : null!;

            // Find the matching pattern config
            TemplatePatternMatch? patternMatch = patternMatches
                .FirstOrDefault(p => p.PatternName.Equals(patternName, StringComparison.OrdinalIgnoreCase));

            if (patternMatch == null)
                return match.Value; // leave unresolved

            // Resolve the regex string
            if (!patternRegexes.TryGetValue(patternMatch.PatternId, out string? regexPattern)
                && !patternRegexes.TryGetValue(patternMatch.PatternName, out regexPattern))
                return string.Empty; // pattern not found

            string separator = separatorOverride ?? patternMatch.Separator;

            try
            {
                MatchCollection regexMatches = Regex.Matches(fullText, regexPattern, RegexOptions.Multiline, RegexTimeout);

                if (regexMatches.Count == 0)
                    return string.Empty;

                return ExtractMatchesByMode(regexMatches, mode, separator);
            }
            catch (RegexMatchTimeoutException)
            {
                return string.Empty;
            }
            catch (ArgumentException)
            {
                return string.Empty; // invalid regex
            }
        });
    }

    /// <summary>
    /// Extracts match values based on the mode string.
    /// </summary>
    internal static string ExtractMatchesByMode(MatchCollection matches, string mode, string separator)
    {
        List<string> allValues = matches.Select(m => m.Value).ToList();

        return mode.ToLowerInvariant() switch
        {
            "first" => allValues[0],
            "last" => allValues[^1],
            "all" => string.Join(separator, allValues),
            _ => ExtractByIndices(allValues, mode, separator)
        };
    }

    private static string ExtractByIndices(List<string> values, string mode, string separator)
    {
        // mode is either a single index like "2" or comma-separated like "1,3,5"
        string[] parts = mode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<string> selected = [];

        foreach (string part in parts)
        {
            if (int.TryParse(part, out int index) && index >= 1 && index <= values.Count)
                selected.Add(values[index - 1]); // convert 1-based to 0-based
        }

        return string.Join(separator, selected);
    }

    /// <summary>
    /// Resolves <see cref="TemplatePatternMatch"/> entries to their actual regex strings
    /// by loading saved <see cref="StoredRegex"/> patterns from settings.
    /// Returns a dictionary keyed by both PatternId and PatternName for flexible lookup.
    /// </summary>
    internal static Dictionary<string, string> ResolvePatternRegexes(
        IReadOnlyList<TemplatePatternMatch> patternMatches)
    {
        Dictionary<string, string> result = [];

        StoredRegex[] savedPatterns = LoadSavedPatterns();
        Dictionary<string, StoredRegex> byId = [];
        Dictionary<string, StoredRegex> byName = new(StringComparer.OrdinalIgnoreCase);

        foreach (StoredRegex sr in savedPatterns)
        {
            byId[sr.Id] = sr;
            byName[sr.Name] = sr;
        }

        foreach (TemplatePatternMatch pm in patternMatches)
        {
            StoredRegex? resolved = null;

            // Prefer lookup by ID (survives renames)
            if (!string.IsNullOrEmpty(pm.PatternId) && byId.TryGetValue(pm.PatternId, out resolved))
            {
                result[pm.PatternId] = resolved.Pattern;
                result[pm.PatternName] = resolved.Pattern;
                continue;
            }

            // Fallback to name
            if (byName.TryGetValue(pm.PatternName, out resolved))
            {
                result[pm.PatternId] = resolved.Pattern;
                result[pm.PatternName] = resolved.Pattern;
            }
        }

        return result;
    }

    private static StoredRegex[] LoadSavedPatterns()
    {
        StoredRegex[] patterns = AppUtilities.TextGrabSettingsService.LoadStoredRegexes();
        return patterns.Length == 0 ? StoredRegex.GetDefaultPatterns() : patterns;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<Dictionary<int, string>> OcrAllRegionsAsync(
        GrabTemplate template,
        Rect captureRegion,
        ILanguage language)
    {
        Dictionary<int, string> results = [];

        foreach (TemplateRegion region in template.Regions)
        {
            // Compute absolute screen rect from capture region + region ratios
            Rect absoluteRegionRect = new(
                x: captureRegion.X + region.RatioLeft * captureRegion.Width,
                y: captureRegion.Y + region.RatioTop * captureRegion.Height,
                width: region.RatioWidth * captureRegion.Width,
                height: region.RatioHeight * captureRegion.Height);

            if (absoluteRegionRect.Width <= 0 || absoluteRegionRect.Height <= 0)
            {
                results[region.RegionNumber] = region.DefaultValue;
                continue;
            }

            try
            {
                // GetTextFromAbsoluteRectAsync uses absolute screen coordinates
                string regionText = await OcrUtilities.GetTextFromAbsoluteRectAsync(absoluteRegionRect, language);
                // Use default value when OCR returns nothing
                results[region.RegionNumber] = string.IsNullOrWhiteSpace(regionText)
                    ? region.DefaultValue
                    : regionText.Trim();
            }
            catch (Exception)
            {
                results[region.RegionNumber] = region.DefaultValue;
            }
        }

        return results;
    }

    /// <summary>
    /// Validates the output template syntax and returns a list of issues.
    /// Returns an empty list when valid.
    /// </summary>
    public static List<string> ValidateOutputTemplate(
        string outputTemplate,
        IEnumerable<int> availableRegionNumbers,
        IEnumerable<string>? availablePatternNames = null)
    {
        List<string> issues = [];
        HashSet<int> available = [.. availableRegionNumbers];

        // Validate region placeholders
        MatchCollection regionMatches = PlaceholderRegex.Matches(outputTemplate);
        HashSet<int> referenced = [];

        foreach (Match match in regionMatches)
        {
            if (!int.TryParse(match.Groups[1].Value, out int num))
            {
                issues.Add($"Invalid placeholder: {match.Value}");
                continue;
            }

            if (!available.Contains(num))
                issues.Add($"Placeholder {{{num}}} references region {num} which does not exist.");

            referenced.Add(num);
        }

        foreach (int availableNum in available)
        {
            if (!referenced.Contains(availableNum))
                issues.Add($"Region {availableNum} is defined but not used in the output template.");
        }

        // Validate pattern placeholders
        if (availablePatternNames != null)
        {
            HashSet<string> availableNames = new(availablePatternNames, StringComparer.OrdinalIgnoreCase);
            MatchCollection patternMatches = PatternPlaceholderRegex.Matches(outputTemplate);

            foreach (Match match in patternMatches)
            {
                string patternName = match.Groups[1].Value;
                string mode = match.Groups[2].Value;

                if (!availableNames.Contains(patternName))
                    issues.Add($"Pattern placeholder references \"{patternName}\" which is not a saved pattern.");

                if (!IsValidMatchMode(mode))
                    issues.Add($"Invalid match mode \"{mode}\" for pattern \"{patternName}\". Use first, last, all, or numeric indices.");
            }
        }

        return issues;
    }

    private static bool IsValidMatchMode(string mode)
    {
        if (string.IsNullOrEmpty(mode))
            return false;

        return mode.ToLowerInvariant() switch
        {
            "first" or "last" or "all" => true,
            _ => mode.Split(',', StringSplitOptions.RemoveEmptyEntries)
                     .All(p => int.TryParse(p.Trim(), out int v) && v >= 1)
        };
    }
}
