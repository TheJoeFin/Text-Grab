using System;
using System.Collections.Generic;
using System.Linq;
using Text_Grab.Utilities;

namespace Text_Grab.Models;

/// <summary>Whether a <see cref="PatternItem"/> is backed by a user regex or a built-in recognizer.</summary>
public enum PatternKind
{
    /// <summary>A user-managed regular expression (<see cref="StoredRegex"/>).</summary>
    SavedRegex,

    /// <summary>A built-in, culture-aware recognizer (<see cref="BuiltInRecognizer"/>).</summary>
    Recognizer
}

/// <summary>
/// A single entry in the unified, user-facing "Patterns" list. To the user, saved regexes
/// and built-in recognizers are both just "patterns" — ways to find/extract a kind of value
/// from text. This type wraps either kind so the UI surfaces (template picker, Edit Text
/// Window apply menu, and the three search features) can list and act on them uniformly.
///
/// The two implementations stay separate underneath: <see cref="SavedRegex"/> patterns run
/// through <see cref="System.Text.RegularExpressions.Regex"/>, recognizers through
/// <see cref="RecognizerExecutor"/>. <see cref="PatternExecutor"/> dispatches over the two.
/// </summary>
public class PatternItem
{
    /// <summary>Subsection header for user regexes in the combined list.</summary>
    public const string SavedGroup = "Saved Patterns";

    /// <summary>Subsection header for built-in recognizers in the combined list.</summary>
    public const string SmartGroup = "Smart Patterns";

    /// <summary>Which implementation backs this item.</summary>
    public PatternKind Kind { get; }

    /// <summary>Stable identifier — <see cref="StoredRegex.Id"/> or <see cref="BuiltInRecognizer.Id"/>.</summary>
    public string Id { get; }

    /// <summary>Display name shown in menus and pickers.</summary>
    public string Name { get; }

    /// <summary>Short description of what the pattern matches.</summary>
    public string Description { get; }

    /// <summary>Subsection label for grouped rendering — <see cref="SavedGroup"/> or <see cref="SmartGroup"/>.</summary>
    public string GroupLabel { get; }

    /// <summary>The backing regex when <see cref="Kind"/> is <see cref="PatternKind.SavedRegex"/>; otherwise null.</summary>
    public StoredRegex? SavedRegex { get; }

    /// <summary>The backing recognizer when <see cref="Kind"/> is <see cref="PatternKind.Recognizer"/>; otherwise null.</summary>
    public BuiltInRecognizer? Recognizer { get; }

    internal PatternItem(StoredRegex savedRegex)
    {
        Kind = PatternKind.SavedRegex;
        Id = savedRegex.Id;
        Name = savedRegex.Name;
        Description = savedRegex.Description;
        GroupLabel = SavedGroup;
        SavedRegex = savedRegex;
    }

    internal PatternItem(BuiltInRecognizer recognizer)
    {
        Kind = PatternKind.Recognizer;
        Id = recognizer.Id;
        Name = recognizer.Name;
        Description = recognizer.Description;
        GroupLabel = SmartGroup;
        Recognizer = recognizer;
    }

    /// <summary>
    /// Returns the full combined catalog: the user's saved regexes first (falling back to the
    /// built-in defaults when none are saved), then the built-in recognizers.
    /// </summary>
    public static IReadOnlyList<PatternItem> GetAll()
    {
        StoredRegex[] saved = AppUtilities.TextGrabSettingsService.LoadStoredRegexes();
        if (saved.Length == 0)
            saved = StoredRegex.GetDefaultPatterns();

        return
        [
            .. saved.Select(s => new PatternItem(s)),
            .. BuiltInRecognizer.GetAll().Select(r => new PatternItem(r)),
        ];
    }

    /// <summary>
    /// Finds a pattern by display name (case-insensitive), preferring a saved regex over a
    /// recognizer when both share a name. Null when no pattern matches.
    /// </summary>
    public static PatternItem? GetByName(string name)
        => GetAll().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds the entries for a pattern combo box: a leading "none" sentinel, then the
    /// combined catalog with a non-selectable subsection header before each group. Items
    /// carry the <see cref="PatternItem"/> directly (names can collide across the two
    /// catalogs, e.g. both have "URL"), so selection resolves by identity, not by name.
    /// </summary>
    public static List<PatternChoice> BuildComboChoices(string noneLabel)
    {
        List<PatternChoice> choices = [new PatternChoice { Display = noneLabel }];

        string? currentGroup = null;
        foreach (PatternItem pattern in GetAll())
        {
            if (pattern.GroupLabel != currentGroup)
            {
                currentGroup = pattern.GroupLabel;
                choices.Add(new PatternChoice { Display = currentGroup, IsHeader = true });
            }

            choices.Add(new PatternChoice
            {
                Display = pattern.Name,
                Description = pattern.Description,
                Pattern = pattern,
            });
        }

        return choices;
    }
}

/// <summary>
/// One row in a pattern combo box — a selectable pattern, a non-selectable subsection
/// header, or the leading "none" sentinel (header = false, <see cref="Pattern"/> = null).
/// </summary>
public sealed class PatternChoice
{
    /// <summary>Text shown for the row.</summary>
    public string Display { get; init; } = string.Empty;

    /// <summary>Tooltip for selectable rows; empty for headers and the sentinel.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>True for a non-selectable subsection header row.</summary>
    public bool IsHeader { get; init; }

    /// <summary>The backing pattern for selectable rows; null for headers and the sentinel.</summary>
    public PatternItem? Pattern { get; init; }

    /// <summary>False for header rows — bound to the combo item's IsEnabled to block selection.</summary>
    public bool IsSelectable => !IsHeader;
}
