using System;
using System.Collections.Generic;

namespace Text_Grab.Models;

/// <summary>
/// Defines a reusable capture template: a set of numbered named regions on a fixed-layout
/// document (e.g. a business card or invoice) and an output format string that assembles
/// the OCR results from those regions into final text.
///
/// Output template syntax — region placeholders:
///   {N}          — replaced by the OCR text from region N  (1-based)
///   {N:trim}     — trimmed OCR text from region N
///   {N:upper}    — uppercased OCR text from region N
///   {N:lower}    — lowercased OCR text from region N
///
/// Output template syntax — pattern placeholders (regex):
///   {p:Name:first}     — first regex match of the named pattern
///   {p:Name:last}      — last regex match
///   {p:Name:all:, }    — all matches joined by separator
///   {p:Name:2}         — 2nd match (1-based)
///   {p:Name:1,3}       — 1st and 3rd matches joined by separator
///
/// Escape sequences:
///   \n           — newline
///   \t           — tab
///   \\           — literal backslash
///   \{           — literal opening brace
/// </summary>
public class GrabTemplate
{
    /// <summary>Unique persistent identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Human-readable name shown in menus and list boxes.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description shown as a tooltip.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Date this template was created.</summary>
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;

    /// <summary>Date this template was last used for capture.</summary>
    public DateTimeOffset? LastUsedDate { get; set; }

    /// <summary>
    /// Optional path to a reference image the designer shows as the canvas background.
    /// May be empty if no reference image was loaded.
    /// </summary>
    public string SourceImagePath { get; set; } = string.Empty;

    /// <summary>
    /// Width of the reference image (pixels). Used to convert ratio ↔ absolute coordinates.
    /// </summary>
    public double ReferenceImageWidth { get; set; } = 800;

    /// <summary>
    /// Height of the reference image (pixels). Used to convert ratio ↔ absolute coordinates.
    /// </summary>
    public double ReferenceImageHeight { get; set; } = 600;

    /// <summary>
    /// The capture regions, each with a 1-based <see cref="TemplateRegion.RegionNumber"/>.
    /// </summary>
    public List<TemplateRegion> Regions { get; set; } = [];

    /// <summary>
    /// Output format string. Use {N}, {N:trim}, {N:upper}, {N:lower} for regions
    /// and {p:Name:mode} or {p:Name:mode:separator} for pattern matches.
    /// Example: "Name: {1}\nEmail: {p:Email Address:first}\nPhone: {3}"
    /// </summary>
    public string OutputTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Pattern references used in the output template.
    /// Each maps a saved <see cref="StoredRegex"/> to a match-selection mode.
    /// </summary>
    public List<TemplatePatternMatch> PatternMatches { get; set; } = [];

    public GrabTemplate() { }

    public GrabTemplate(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Returns whether this template has the minimum required data to be executed.
    /// A template is valid if it has a name, an output template, and at least one
    /// region or pattern reference.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name)
        && (Regions.Count > 0 || PatternMatches.Count > 0)
        && !string.IsNullOrWhiteSpace(OutputTemplate);

    /// <summary>
    /// Returns all region numbers referenced in the output template.
    /// </summary>
    public IEnumerable<int> GetReferencedRegionNumbers()
    {
        System.Text.RegularExpressions.MatchCollection matches =
            System.Text.RegularExpressions.Regex.Matches(
                OutputTemplate,
                @"\{(\d+)(?::[a-z]+)?\}");

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int number))
                yield return number;
        }
    }

    /// <summary>
    /// Returns all pattern names referenced in the output template via {p:Name:mode} syntax.
    /// </summary>
    public IEnumerable<string> GetReferencedPatternNames()
    {
        System.Text.RegularExpressions.MatchCollection matches =
            System.Text.RegularExpressions.Regex.Matches(
                OutputTemplate,
                @"\{p:([^:}]+):[^}]+\}");

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            yield return match.Groups[1].Value;
        }
    }
}
