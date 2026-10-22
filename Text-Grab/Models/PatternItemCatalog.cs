using System;
using System.Collections.Generic;
using System.Linq;
using Text_Grab.Utilities;

namespace Text_Grab.Models;

/// <summary>
/// Loads the combined, user-facing "Patterns" catalog (saved regexes + built-in recognizers)
/// from settings. Split out from <see cref="PatternItem"/> because it depends on
/// <see cref="AppUtilities.TextGrabSettingsService"/>, which only exists in the app.
/// </summary>
public static class PatternItemCatalog
{
    /// <summary>
    /// Returns the combined catalog: the user's saved regexes first (falling back to the
    /// built-in defaults when none are saved), then the built-in recognizers. Recognizers the
    /// user has hidden are excluded unless <paramref name="includeHidden"/> is true — the
    /// Patterns Manager passes true so it can offer an "unhide" action.
    /// </summary>
    public static IReadOnlyList<PatternItem> GetAll(bool includeHidden = false)
    {
        StoredRegex[] saved = AppUtilities.TextGrabSettingsService.LoadStoredRegexes();
        if (saved.Length == 0)
            saved = StoredRegex.GetDefaultPatterns();

        HashSet<string> hiddenIds = [.. AppUtilities.TextGrabSettingsService.LoadHiddenSmartPatternIds()];

        IEnumerable<PatternItem> recognizers = BuiltInRecognizer.GetAll()
            .Select(r => new PatternItem(r, isHidden: hiddenIds.Contains(r.Id)));

        if (!includeHidden)
            recognizers = recognizers.Where(r => !r.IsHidden);

        return
        [
            .. saved.Select(s => new PatternItem(s)),
            .. recognizers,
        ];
    }

    /// <summary>
    /// Finds a pattern by display name (case-insensitive), preferring a saved regex over a
    /// recognizer when both share a name. Null when no pattern matches.
    /// </summary>
    public static PatternItem? GetByName(string name)
        => GetAll().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
