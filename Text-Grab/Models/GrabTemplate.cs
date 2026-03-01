using System;
using System.Collections.Generic;

namespace Text_Grab.Models;

/// <summary>
/// Defines a reusable capture template: a set of numbered named regions on a fixed-layout
/// document (e.g. a business card or invoice) and an output format string that assembles
/// the OCR results from those regions into final text.
///
/// Output template syntax:
///   {N}          — replaced by the OCR text from region N  (1-based)
///   {N:trim}     — trimmed OCR text from region N
///   {N:upper}    — uppercased OCR text from region N
///   {N:lower}    — lowercased OCR text from region N
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
    /// Output format string. Use {N}, {N:trim}, {N:upper}, {N:lower}, \n, \t.
    /// Example: "Name: {1}\nEmail: {2}\nPhone: {3}"
    /// </summary>
    public string OutputTemplate { get; set; } = string.Empty;

    public GrabTemplate() { }

    public GrabTemplate(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Returns whether this template has the minimum required data to be executed.
    /// </summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name)
        && Regions.Count > 0
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
}
