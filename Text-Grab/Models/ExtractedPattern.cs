using System.Collections.Generic;
using System.Linq;
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
    /// Whether patterns should use case-insensitive inline flags.
    /// </summary>
    private bool _ignoreCase;

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
    /// <param name="ignoreCase">Whether to generate case-insensitive patterns with (?i) flag</param>
    public ExtractedPattern(string text, bool ignoreCase = false)
    {
        OriginalText = text;
        _ignoreCase = ignoreCase;
        GenerateAllPrecisionLevels();
    }

    /// <summary>
    /// Gets or sets whether patterns use case-insensitive inline flags.
    /// When changed, all patterns are regenerated.
    /// </summary>
    public bool IgnoreCase
    {
        get => _ignoreCase;
        set
        {
            if (_ignoreCase != value)
            {
                _ignoreCase = value;
                GenerateAllPrecisionLevels();
            }
        }
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
            string pattern = OriginalText.ExtractSimplePattern(level, _ignoreCase);
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

    /// <summary>
    /// Determines the optimal starting precision level based on text characteristics.
    /// Analyzes length, content type, and structure to suggest the most useful level.
    /// </summary>
    /// <param name="selection">The text to analyze</param>
    /// <returns>Recommended precision level (0-5)</returns>
    public static int DetermineStartingLevel(string selection)
    {
        if (string.IsNullOrWhiteSpace(selection))
            return DefaultPrecisionLevel;

        string trimmed = selection.Trim();
        int length = trimmed.Length;

        // Very short text - prefer exact or near-exact matching
        if (length == 1)
            return 5; // Exact match for single character

        // Very long text - prefer structure-only to avoid over-specification
        if (length > 25)
            return 2; // Length-based pattern for long strings

        // Content-based analysis (check in priority order)
        
        // Pure numbers (123, 4567) - likely want similar number sequences
        if (IsAllDigits(trimmed))
            return 2; // Length-flexible for number sequences

        // Has 3+ words with spaces ("the quick brown fox") - capturing phrase structure
        int wordCnt = WordCount(trimmed);
        if (wordCnt >= 3)
            return 1; // Structure-only for multi-word phrases

        // Alphanumeric ID pattern (ABC-123, user_456, ID:789) - structural delimiters are noise
        if (HasMultipleDelimiters(trimmed))
            return 3; // Character-class pattern, ignoring specific delimiters

        // Mixed letters+numbers, no spaces (user123, AB12CD) - IDs, codes, usernames
        if (IsAlphanumericMixed(trimmed))
            return 3; // Character-class for mixed content

        // Short text (2-4 chars) - prefer per-character for small variations
        if (length >= 2 && length <= 4)
            return 4; // Per-character for 2-4 chars

        // Single word, all letters ("Hello") - names, simple words
        if (IsSimpleWord(trimmed))
            return 4; // Case-insensitive per-character

        // Has special chars but short (#42, @joe, v1.2) - symbols are separators
        if (HasSpecialChars(trimmed) && length <= 10)
            return 3; // Separator-agnostic pattern

        // Default middle level for everything else
        return DefaultPrecisionLevel;
    }

    /// <summary>
    /// Checks if the string contains only digits.
    /// </summary>
    private static bool IsAllDigits(string text)
    {
        string trimmed = text.Trim();
        return trimmed.Length > 0 && trimmed.All(char.IsDigit);
    }

    /// <summary>
    /// Checks if the string has multiple delimiter/separator characters.
    /// </summary>
    private static bool HasMultipleDelimiters(string text)
    {
        char[] delimiters = ['-', '_', ':', '.', '/', '\\', '|'];
        int delimiterCount = text.Count(c => delimiters.Contains(c));
        return delimiterCount >= 2;
    }

    /// <summary>
    /// Counts the number of words (whitespace-separated sequences).
    /// </summary>
    private static int WordCount(string text)
    {
        return text.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Checks if the string contains both letters and digits (mixed alphanumeric).
    /// </summary>
    private static bool IsAlphanumericMixed(string text)
    {
        bool hasLetters = text.Any(char.IsLetter);
        bool hasDigits = text.Any(char.IsDigit);
        bool hasSpaces = text.Any(char.IsWhiteSpace);
        return hasLetters && hasDigits && !hasSpaces;
    }

    /// <summary>
    /// Checks if the string is a simple word (only letters, no spaces or digits).
    /// </summary>
    private static bool IsSimpleWord(string text)
    {
        string trimmed = text.Trim();
        return trimmed.Length > 0 
            && trimmed.All(char.IsLetter) 
            && !trimmed.Any(char.IsWhiteSpace);
    }

    /// <summary>
    /// Checks if the string contains special characters from the regex special char list.
    /// </summary>
    private static bool HasSpecialChars(string text)
    {
        return text.Any(c => StringMethods.specialCharList.Contains(c) || !char.IsLetterOrDigit(c));
    }
}
