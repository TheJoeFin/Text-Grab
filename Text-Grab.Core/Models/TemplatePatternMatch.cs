using System;

namespace Text_Grab.Models;

/// <summary>
/// Represents a reference to a saved regex pattern within a GrabTemplate.
/// During execution the pattern is applied to the full-area OCR text and
/// matches are extracted according to <see cref="MatchMode"/>.
///
/// Placeholder syntax in the output template:
///   {p:PatternName:first}       — first match
///   {p:PatternName:last}        — last match
///   {p:PatternName:all:, }      — all matches joined by separator
///   {p:PatternName:2}           — 2nd match (1-based)
///   {p:PatternName:1,3}         — 1st and 3rd matches joined by separator
/// </summary>
public class TemplatePatternMatch
{
    /// <summary>
    /// The <see cref="StoredRegex.Id"/> of the saved pattern.
    /// Used for durable resolution even if the pattern is renamed.
    /// </summary>
    public string PatternId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the pattern (mirrors <see cref="StoredRegex.Name"/> at creation time).
    /// Also used in the <c>{p:PatternName:...}</c> placeholder syntax.
    /// </summary>
    public string PatternName { get; set; } = string.Empty;

    /// <summary>
    /// How to select from the regex matches.
    /// Values: "first", "last", "all", a single 1-based index like "2",
    /// or comma-separated indices like "1,3,5".
    /// </summary>
    public string MatchMode { get; set; } = "first";

    /// <summary>
    /// Separator string used when <see cref="MatchMode"/> is "all" or specifies
    /// multiple indices. Defaults to ", ".
    /// </summary>
    public string Separator { get; set; } = ", ";

    public TemplatePatternMatch() { }

    public TemplatePatternMatch(string patternId, string patternName, string matchMode = "first", string separator = ", ")
    {
        PatternId = patternId;
        PatternName = patternName;
        MatchMode = matchMode;
        Separator = separator;
    }
}
