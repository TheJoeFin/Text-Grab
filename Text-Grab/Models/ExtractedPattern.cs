using System.Collections.Generic;
using Text_Grab.Utilities;

namespace Text_Grab.Models;

/// <summary>
/// Manages a set of regex patterns extracted from text at different precision levels.
/// Pre-generates all 6 precision levels (0-5) for instant switching.
/// </summary>
public class ExtractedPattern
{
    /// <summary>
    /// The original text that was used to extract the patterns.
    /// </summary>
    public string OriginalText { get; }

    /// <summary>
    /// Dictionary storing patterns at each precision level (0-5).
    /// </summary>
    private readonly Dictionary<int, string> _patternsByLevel = [];

    /// <summary>
    /// Total number of precision levels supported.
    /// </summary>
    public const int MaxPrecisionLevel = 5;
    public const int MinPrecisionLevel = 0;
    public const int DefaultPrecisionLevel = 3;

    /// <summary>
    /// Creates a new ExtractedPattern by generating all precision levels from the input text.
    /// </summary>
    /// <param name="text">The text to extract patterns from</param>
    public ExtractedPattern(string text)
    {
        OriginalText = text;
        GenerateAllPrecisionLevels();
    }

    /// <summary>
    /// Gets the pattern at the specified precision level.
    /// </summary>
    /// <param name="precisionLevel">Precision level (0-5)</param>
    /// <returns>The regex pattern at that precision level</returns>
    public string GetPattern(int precisionLevel)
    {
        if (precisionLevel is < MinPrecisionLevel or > MaxPrecisionLevel)
            precisionLevel = DefaultPrecisionLevel;

        return _patternsByLevel.TryGetValue(precisionLevel, out string? pattern)
                    ? pattern
                    : string.Empty;
    }

    /// <summary>
    /// Gets all patterns indexed by their precision level.
    /// </summary>
    public IReadOnlyDictionary<int, string> AllPatterns => _patternsByLevel;

    /// <summary>
    /// Pre-generates patterns for all precision levels (0-5).
    /// This is done once during construction so switching levels is instant.
    /// </summary>
    private void GenerateAllPrecisionLevels()
    {
        for (int level = MinPrecisionLevel; level <= MaxPrecisionLevel; level++)
        {
            string pattern = OriginalText.ExtractSimplePattern(level);
            _patternsByLevel[level] = pattern;
        }
    }

    /// <summary>
    /// Gets a human-readable description of what a precision level means.
    /// </summary>
    /// <param name="level">The precision level (0-5)</param>
    /// <returns>Description of the precision level</returns>
    public static string GetLevelDescription(int level)
    {
        return level switch
        {
            0 => "Least Precise - Matches any non-whitespace (\\S+)",
            1 => "Word Characters - Matches letters, digits, underscore (\\w+)",
            2 => "Word Characters with Count - Preserves length but not character types",
            3 => "Character Types with Counts - Distinguishes letters from digits (Default)",
            4 => "Individual Character Match - Each position with case-insensitive Latin letters",
            5 => "Most Precise - Exact escaped string match",
            _ => "Unknown precision level"
        };
    }

    /// <summary>
    /// Gets a short label for a precision level.
    /// </summary>
    /// <param name="level">The precision level (0-5)</param>
    /// <returns>Short label for the level</returns>
    public static string GetLevelLabel(int level)
    {
        return level switch
        {
            0 => "Any Text",
            1 => "Words",
            2 => "Length",
            3 => "Types",
            4 => "Per Char",
            5 => "Exact",
            _ => $"Level {level}"
        };
    }
}
