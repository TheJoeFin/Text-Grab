using System;
using System.Collections.Generic;
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
/// Output template syntax:
///   {N}        — OCR text from region N  (1-based)
///   {N:trim}   — trimmed OCR text
///   {N:upper}  — uppercased OCR text
///   {N:lower}  — lowercased OCR text
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

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes the given template using <paramref name="captureRegion"/> as the
    /// coordinate space. Each template region is mapped to a sub-rectangle of
    /// <paramref name="captureRegion"/>, OCR'd, then assembled via the output template.
    /// </summary>
    /// <param name="template">The template to execute.</param>
    /// <param name="captureRegion">
    ///     The screen rectangle (in WPF units, pre-DPI-scaling applied by caller)
    ///     that bounds the user's selection. Template region ratios are applied to
    ///     this rectangle's width/height.
    /// </param>
    /// <param name="language">The OCR language to use. Pass null to use the app default.</param>
    public static async Task<string> ExecuteTemplateAsync(
        GrabTemplate template,
        Rect captureRegion,
        ILanguage? language = null)
    {
        if (!template.IsValid)
            return string.Empty;

        // 1. OCR each region
        ILanguage resolvedLanguage = language ?? LanguageUtilities.GetOCRLanguage();
        Dictionary<int, string> regionResults = await OcrAllRegionsAsync(
            template, captureRegion, resolvedLanguage);

        // 2. Apply output template
        return ApplyOutputTemplate(template.OutputTemplate, regionResults);
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
    public static List<string> ValidateOutputTemplate(string outputTemplate, IEnumerable<int> availableRegionNumbers)
    {
        List<string> issues = [];
        HashSet<int> available = [.. availableRegionNumbers];

        MatchCollection matches = PlaceholderRegex.Matches(outputTemplate);
        HashSet<int> referenced = [];

        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups[1].Value, out int num))
            {
                issues.Add($"Invalid placeholder: {match.Value}");
                continue;
            }

            if (!available.Contains(num))
                issues.Add($"Placeholder {{{{num}}}} references region {num} which does not exist.");

            referenced.Add(num);
        }

        foreach (int availableNum in available)
        {
            if (!referenced.Contains(availableNum))
                issues.Add($"Region {availableNum} is defined but not used in the output template.");
        }

        return issues;
    }
}
