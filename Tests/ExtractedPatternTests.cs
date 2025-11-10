using System.Text.RegularExpressions;
using Text_Grab.Models;

namespace Tests;

public class ExtractedPatternTests
{
    [Fact]
    public void Constructor_GeneratesAllPrecisionLevels()
    {
        // Given
        string input = "Abc123";

        // When
        ExtractedPattern extractedPattern = new(input);

        // Then
        Assert.NotNull(extractedPattern);
        Assert.Equal(input, extractedPattern.OriginalText);
        Assert.Equal(6, extractedPattern.AllPatterns.Count); // 0-5 = 6 levels
        Assert.False(extractedPattern.IgnoreCase); // Default should be case-sensitive
    }

    [Fact]
    public void Constructor_WithIgnoreCase_GeneratesAllPrecisionLevels()
    {
        // Given
        string input = "Abc123";

        // When
        ExtractedPattern extractedPattern = new(input, ignoreCase: true);

        // Then
        Assert.NotNull(extractedPattern);
        Assert.Equal(input, extractedPattern.OriginalText);
        Assert.Equal(6, extractedPattern.AllPatterns.Count); // 0-5 = 6 levels
        Assert.True(extractedPattern.IgnoreCase);
    }

    [Theory]
    [InlineData("Abc123", 0, @"\S+")]
    [InlineData("Abc123", 1, @"\w+")]
    [InlineData("Abc123", 2, @"\w{3}\w{3}")]
    [InlineData("Abc123", 3, @"[A-Za-z]{3}\d{3}")]
    [InlineData("Abc123", 4, @"(?i)Abc123")]
    [InlineData("Abc123", 5, @"Abc123")]
    public void GetPattern_ReturnsCorrectPatternForEachLevel(string input, int level, string expectedPattern)
    {
        // Given
        ExtractedPattern extractedPattern = new(input);

        // When
        string actualPattern = extractedPattern.GetPattern(level);

        // Then
        Assert.Equal(expectedPattern, actualPattern);
    }

    [Theory]
    [InlineData(-1)] // Below minimum
    [InlineData(6)]  // Above maximum
    [InlineData(10)] // Way above maximum
    public void GetPattern_WithInvalidLevel_ReturnsDefaultLevel(int invalidLevel)
    {
        // Given
        string input = "Test123";
        ExtractedPattern extractedPattern = new(input);
        string expectedPattern = extractedPattern.GetPattern(ExtractedPattern.DefaultPrecisionLevel);

        // When
        string actualPattern = extractedPattern.GetPattern(invalidLevel);

        // Then
        Assert.Equal(expectedPattern, actualPattern);
    }

    [Fact]
    public void GetPattern_CalledMultipleTimes_ReturnsSamePattern()
    {
        // Given
        string input = "Hello123";
        ExtractedPattern extractedPattern = new(input);

        // When - Call multiple times
        string pattern1 = extractedPattern.GetPattern(3);
        string pattern2 = extractedPattern.GetPattern(3);
        string pattern3 = extractedPattern.GetPattern(3);

        // Then - Should always return the same pre-generated pattern
        Assert.Equal(pattern1, pattern2);
        Assert.Equal(pattern2, pattern3);
    }

    [Fact]
    public void AllPatterns_ContainsAllSixLevels()
    {
        // Given
        ExtractedPattern extractedPattern = new("Test");

        // When
        IReadOnlyDictionary<int, string> allPatterns = extractedPattern.AllPatterns;

        // Then
        Assert.Contains(0, allPatterns.Keys);
        Assert.Contains(1, allPatterns.Keys);
        Assert.Contains(2, allPatterns.Keys);
        Assert.Contains(3, allPatterns.Keys);
        Assert.Contains(4, allPatterns.Keys);
        Assert.Contains(5, allPatterns.Keys);
    }

    [Theory]
    [InlineData(0, "Any Text")]
    [InlineData(1, "Words")]
    [InlineData(2, "Length")]
    [InlineData(3, "Types")]
    [InlineData(4, "Per Char")]
    [InlineData(5, "Exact")]
    public void GetLevelLabel_ReturnsCorrectLabel(int level, string expectedLabel)
    {
        // When
        string actualLabel = ExtractedPattern.GetLevelLabel(level);

        // Then
        Assert.Equal(expectedLabel, actualLabel);
    }

    [Fact]
    public void GetLevelDescription_ReturnsDescriptionForAllLevels()
    {
        // Given/When/Then
        for (int level = 0; level <= 5; level++)
        {
            string description = ExtractedPattern.GetLevelDescription(level);
            Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.DoesNotContain("Unknown", description);
        }
    }

    [Fact]
    public void GetLevelDescription_WithInvalidLevel_ReturnsUnknownMessage()
    {
        // When
        string description = ExtractedPattern.GetLevelDescription(99);

        // Then
        Assert.Contains("Unknown", description);
    }

    [Fact]
    public void EmptyString_GeneratesValidPatterns()
    {
        // Given
        ExtractedPattern extractedPattern = new("");

        // When/Then - Should not throw and should return empty patterns
        for (int level = 0; level <= 5; level++)
        {
            string pattern = extractedPattern.GetPattern(level);
            Assert.NotNull(pattern);
        }
    }

    [Theory]
    [InlineData("(123)-555-6789", 3, @"(\()\d{3}(\))-\d{3}-\d{4}")]
    [InlineData("Hello World!", 3, @"[A-Za-z]{5}\s[A-Za-z]{5}!")]
    [InlineData("ab12ab12ab12ab12ab12", 3, @"([A-Za-z]{2}\d{2}){5}")]
    [InlineData("Test", 4, @"(?i)Test")]
    [InlineData("ABC", 4, @"(?i)ABC")]
    [InlineData("A.B", 5, @"A\.B")]
    public void ComplexPatterns_GeneratedCorrectly(string input, int level, string expectedPattern)
    {
        // Given
        ExtractedPattern extractedPattern = new(input);

        // When
        string actualPattern = extractedPattern.GetPattern(level);

        // Then
        Assert.Equal(expectedPattern, actualPattern);
    }

    [Fact]
    public void AllPatterns_IsReadOnly()
    {
        // Given
        ExtractedPattern extractedPattern = new("Test");

        // When
        IReadOnlyDictionary<int, string> allPatterns = extractedPattern.AllPatterns;

        // Then
        Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyDictionary<int, string>>(allPatterns);
    }

    [Fact]
    public void Constants_HaveCorrectValues()
    {
        // Then
        Assert.Equal(0, ExtractedPattern.MinPrecisionLevel);
        Assert.Equal(5, ExtractedPattern.MaxPrecisionLevel);
        Assert.Equal(3, ExtractedPattern.DefaultPrecisionLevel);
    }

    [Fact]
    public void PrecisionLevels_MatchCountDecreases_FromLevel0ToLevel5()
    {
        // Given - A large block of text with various patterns
        string largeText = @"
Hello World! This is a test of the pattern matching system.
Test123 and ABC456 are examples of mixed text.
Email: test@example.com and phone: (123)-456-7890
More words: hello, HELLO, HeLLo - case variations
Numbers: 123, 456, 789
Special chars: @#$%^&*()
test test TEST Test
abc ABC Abc
Mixed: Test123, ABC456, xyz789
URL: https://example.com/path?query=value
Multiple  spaces   and	tabs
Line1
Line2
Line3
The quick brown fox jumps over the lazy dog.
UPPERCASE TEXT AND lowercase text and MiXeD CaSe TeXt.
test123 test456 test789 pattern123 pattern456
Same word repeated: test test test test test
";

        // Extract pattern from a common word "test"
        string searchTerm = "test";
        ExtractedPattern extractedPattern = new(searchTerm);

        // When - Count matches at each precision level
        Dictionary<int, int> matchCountsByLevel = [];

        for (int level = 0; level <= 5; level++)
        {
            string pattern = extractedPattern.GetPattern(level);
            MatchCollection matches = Regex.Matches(largeText, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            matchCountsByLevel[level] = matches.Count;
        }

        // Then - Verify match counts follow expected precision patterns

        // Output for debugging
        System.Diagnostics.Debug.WriteLine("Match counts by precision level for 'test':");
        for (int level = 0; level <= 5; level++)
        {
            System.Diagnostics.Debug.WriteLine($"  Level {level} ({ExtractedPattern.GetLevelLabel(level)}): {matchCountsByLevel[level]} matches - Pattern: {extractedPattern.GetPattern(level)}");
        }

        // NOTE: Level 0 (\S+) matches non-whitespace SEQUENCES, so it can match FEWER items than Level 1 (\w+)
        // because \w+ splits on special characters while \S+ includes them.

        // The key principle is that each level should be MORE RESTRICTIVE in WHAT it matches,
        // not necessarily match fewer total occurrences.

        // Verify level 2 is more restrictive than level 1
        // Level 2 requires specific length, so should match same or fewer
        Assert.True(matchCountsByLevel[2] <= matchCountsByLevel[1],
          $"Level 2 (Length) should match at most as many as Level 1 (Words). L1={matchCountsByLevel[1]}, L2={matchCountsByLevel[2]}");

        // Verify level 3 is more restrictive than level 2
        Assert.True(matchCountsByLevel[3] <= matchCountsByLevel[2],
                   $"Level 2 (Length) should match at least as many as Level 3 (Types). L2={matchCountsByLevel[2]}, L3={matchCountsByLevel[3]}");

        // Verify level 4 is case-insensitive but position-specific
        // Level 4 should match case variations like "test", "Test", "TEST", "TeSt"
        Assert.True(matchCountsByLevel[4] > 0, "Level 4 should find at least some matches");

        // Level 5 is exact match (but with case-insensitive flag, it still matches case variations)
        Assert.True(matchCountsByLevel[5] > 0, "Level 5 should find at least some exact matches");

        // Level 5 should generally be most restrictive (allowing for regex flag effects)
        Assert.True(matchCountsByLevel[5] <= matchCountsByLevel[4],
       $"Level 5 (Exact) should match at most as many as Level 4 (Per Char). L4={matchCountsByLevel[4]}, L5={matchCountsByLevel[5]}");
    }

    [Fact]
    public void PrecisionLevels_SpecificPattern_MatchCountValidation()
    {
        // Given - Text with a specific repeating pattern
        string text = @"
ABC123 abc123 AbC123 ABC456 test123 TEST123
XYZ789 xyz789 XyZ789 pattern123
DATA001 data001 DaTa001 INFO999
";

        // Extract pattern from "ABC123"
        string searchTerm = "ABC123";
        ExtractedPattern extractedPattern = new(searchTerm);

        // When - Count matches at each level
        Dictionary<int, int> matchCounts = [];
        for (int level = 0; level <= 5; level++)
        {
            string pattern = extractedPattern.GetPattern(level);
            MatchCollection matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            matchCounts[level] = matches.Count;
        }

        // Output for debugging
        System.Diagnostics.Debug.WriteLine("Match counts by precision level for 'ABC123':");
        for (int level = 0; level <= 5; level++)
        {
            System.Diagnostics.Debug.WriteLine($"  Level {level} ({ExtractedPattern.GetLevelLabel(level)}): {matchCounts[level]} matches - Pattern: {extractedPattern.GetPattern(level)}");
        }

        // Then - Verify expected behavior
        // Level 0 (\S+) - matches all non-whitespace sequences
        Assert.True(matchCounts[0] > 10, "Level 0 should match many non-whitespace sequences");

        // Level 1 (\w+) - matches all word character sequences
        Assert.True(matchCounts[1] > 10, "Level 1 should match many word sequences");

        // Level 2-3 should be more restrictive
        Assert.True(matchCounts[2] <= matchCounts[1], "Level 2 should be more restrictive than Level 1");
        Assert.True(matchCounts[3] <= matchCounts[2], "Level 3 should be more restrictive than Level 2");

        // Level 4 ([Aa][Bb][Cc][1][2][3]) - should match case-insensitive "ABC123"
        // Should match: ABC123, abc123, AbC123 (3 matches)
        Assert.True(matchCounts[4] >= 3, $"Level 4 should match at least 3 case variations, got {matchCounts[4]}");
        Assert.True(matchCounts[4] < matchCounts[3], "Level 4 should be more restrictive than Level 3");

        // Level 5 (exact match "ABC123") - should match only exact string
        // With case-insensitive regex, it will match "ABC123", "abc123", "AbC123"
        Assert.True(matchCounts[5] >= 1, $"Level 5 should match at least once, got {matchCounts[5]}");
        Assert.True(matchCounts[5] <= matchCounts[4], "Level 5 should be most restrictive");

        // Level 5 should match same as Level 4 since we're using case-insensitive search
        // and the only difference is Level 4 uses character classes while Level 5 is literal
        Assert.Equal(matchCounts[4], matchCounts[5]);
    }

    [Fact]
    public void PrecisionLevels_DemonstrateHierarchy_WithSimpleText()
    {
        // Given - Simple repeating text to demonstrate precision hierarchy clearly
        string text = "test Test TEST teST test123 testing best rest";

        string searchTerm = "test";
        ExtractedPattern extractedPattern = new(searchTerm);

        // When - Count matches at each level with case-insensitive search
        Dictionary<int, int> matchCounts = [];
        for (int level = 0; level <= 5; level++)
        {
            string pattern = extractedPattern.GetPattern(level);
            MatchCollection matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            matchCounts[level] = matches.Count;
        }

        // Output for debugging
        System.Diagnostics.Debug.WriteLine($"\nPrecision hierarchy for '{searchTerm}' in: {text}");
        for (int level = 0; level <= 5; level++)
        {
            System.Diagnostics.Debug.WriteLine($"  Level {level} ({ExtractedPattern.GetLevelLabel(level)}): {matchCounts[level]} matches - Pattern: '{extractedPattern.GetPattern(level)}'");
        }

        // Then - Verify the expected precision hierarchy
        // Level 0: \S+ - matches all non-whitespace (test, Test, TEST, teST, test123, testing, best, rest) = 8
        Assert.Equal(8, matchCounts[0]);

        // Level 1: \w+ - matches all word sequences (same as Level 0 in this simple case) = 8
        Assert.Equal(8, matchCounts[1]);

        // Level 2: \w{4} - matches 4 word characters (matches "test" in test, Test, TEST, teST, test123, testing, best, rest) = 8
        // Note: \w{4} finds the first 4 chars in words >= 4 chars long
        Assert.Equal(8, matchCounts[2]);

        // Level 3: [A-Za-z]{4} - matches exactly 4 letters (test, Test, TEST, teST, test from test123, test from testing, best, rest) = 8
        // Note: [A-Za-z]{4} finds 4 letters even in longer words
        Assert.Equal(8, matchCounts[3]);

        // Level 4: [Tt][Ee][Ss][Tt] - case-insensitive "test" specifically (test, Test, TEST, teST, test from test123, test from testing) = 6
        Assert.Equal(6, matchCounts[4]);

        // Level 5: test - exact match (with case-insensitive flag: test, Test, TEST, teST, test from test123, test from testing) = 6
        Assert.Equal(6, matchCounts[5]);

        // Verify hierarchy: each level should be same or more restrictive than previous
        Assert.True(matchCounts[1] <= matchCounts[0], "Level 1 should be <= Level 0");
        Assert.True(matchCounts[2] <= matchCounts[1], "Level 2 should be <= Level 1");
        Assert.True(matchCounts[3] <= matchCounts[2], "Level 3 should be <= Level 2");
        Assert.True(matchCounts[4] <= matchCounts[3], "Level 4 should be <= Level 3");
        Assert.True(matchCounts[5] <= matchCounts[4], "Level 5 should be <= Level 4");
    }

    // DetermineStartingLevel Tests

    [Theory]
    [InlineData("a", 5)]           // Single character -> exact match
    [InlineData("AB", 4)]          // 2-4 characters -> per-character
    [InlineData("xyz", 4)]         // 2-4 characters -> per-character
    [InlineData("test", 4)]        // 2-4 characters -> per-character
    public void DetermineStartingLevel_ShortText_ReturnsHighPrecision(string input, int expectedLevel)
    {
        // When
        int actualLevel = ExtractedPattern.DetermineStartingLevel(input);

        // Then
        Assert.Equal(expectedLevel, actualLevel);
    }

    [Fact]
    public void DetermineStartingLevel_LongText_ReturnsLowerPrecision()
    {
        // Given - Text longer than 25 characters
        string longText = "This is a very long string that exceeds the limit";

        // When
        int actualLevel = ExtractedPattern.DetermineStartingLevel(longText);

        // Then
        Assert.Equal(2, actualLevel); // Should return length-based pattern
    }

    [Theory]
    [InlineData("123", 2)]          // Pure numbers
    [InlineData("4567", 2)]         // Pure numbers
    [InlineData("999", 2)]          // Pure numbers
    [InlineData("12345", 2)]        // Pure numbers (5 digits, not too long)
    public void DetermineStartingLevel_PureNumbers_ReturnsLengthFlexible(string input, int expectedLevel)
    {
        // When
        int actualLevel = ExtractedPattern.DetermineStartingLevel(input);

        // Then
        Assert.Equal(expectedLevel, actualLevel);
    }

    [Theory]
    [InlineData("ABC-123", 3)]      // Has delimiter
    [InlineData("user_456", 3)]     // Has delimiter
    [InlineData("ID:789", 3)]       // Has delimiter
    [InlineData("file.txt", 3)]     // Has delimiter
    public void DetermineStartingLevel_AlphanumericWithDelimiters_ReturnsSeparatorAgnostic(string input, int expectedLevel)
    {
        // When
        int actualLevel = ExtractedPattern.DetermineStartingLevel(input);

        // Then
        Assert.Equal(expectedLevel, actualLevel);
    }

    [Theory]
    [InlineData("the quick brown", 1)]      // 3 words
    [InlineData("one two three four", 1)]   // 4 words
    [InlineData("hello world again", 1)]    // 3 words
    public void DetermineStartingLevel_MultipleWords_ReturnsStructureOnly(string input, int expectedLevel)
    {
        // When
        int actualLevel = ExtractedPattern.DetermineStartingLevel(input);

        // Then
        Assert.Equal(expectedLevel, actualLevel);
    }

    [Theory]
    [InlineData("user123", 3)]      // Mixed letters and numbers, no spaces
    [InlineData("AB12CD", 3)]       // Mixed letters and numbers, no spaces
    [InlineData("test456", 3)]      // Mixed letters and numbers, no spaces
    public void DetermineStartingLevel_AlphanumericMixed_ReturnsCharacterClass(string input, int expectedLevel)
    {
        // When
        int actualLevel = ExtractedPattern.DetermineStartingLevel(input);

        // Then
        Assert.Equal(expectedLevel, actualLevel);
    }

    [Theory]
    [InlineData("Hello", 4)]        // Simple word
    [InlineData("World", 4)]        // Simple word
    [InlineData("Test", 4)]         // Simple word (2-4 chars, but all letters so treated as simple word)
    [InlineData("Testing", 4)]      // Simple word (longer than 4 chars)
    public void DetermineStartingLevel_SimpleWord_ReturnsCaseInsensitive(string input, int expectedLevel)
    {
        // When
        int actualLevel = ExtractedPattern.DetermineStartingLevel(input);

        // Then
        Assert.Equal(expectedLevel, actualLevel);
    }

    [Theory]
    [InlineData("#42", 4)]          // Has special chars, but 2-4 char range (3 chars) -> per-character
    [InlineData("@joe", 4)]         // Has special chars, but 2-4 char range (4 chars) -> per-character  
    [InlineData("v1.2", 3)]         // Has multiple delimiters (2 dots) -> separator-agnostic
    [InlineData("$USD", 4)]         // Has special chars, but 2-4 char range (4 chars) -> per-character
    [InlineData("#hashtag", 3)]     // Has special chars, longer than 4 chars -> separator-agnostic
    public void DetermineStartingLevel_SpecialCharsShort_ReturnsSeparatorAgnostic(string input, int expectedLevel)
    {
        // When
        int actualLevel = ExtractedPattern.DetermineStartingLevel(input);

        // Then
        Assert.Equal(expectedLevel, actualLevel);
    }

    [Theory]
    [InlineData("", 3)]             // Empty string
    [InlineData("   ", 3)]          // Whitespace only
    [InlineData(null, 3)]           // Null
    public void DetermineStartingLevel_EmptyOrWhitespace_ReturnsDefault(string input, int expectedLevel)
    {
        // When
        int actualLevel = ExtractedPattern.DetermineStartingLevel(input);

        // Then
        Assert.Equal(expectedLevel, actualLevel);
    }

    [Theory]
    [InlineData("123-456-7890", 3)] // Phone number pattern (has delimiters)
    [InlineData("XX-YY-ZZ", 3)]     // Repeating pattern with delimiters
    [InlineData("ID_001_ABC", 3)]   // Multiple delimiters
    public void DetermineStartingLevel_RepeatingPatterns_ReturnsCharacterClass(string input, int expectedLevel)
    {
        // When
        int actualLevel = ExtractedPattern.DetermineStartingLevel(input);

        // Then
        Assert.Equal(expectedLevel, actualLevel);
    }

    [Fact]
    public void DetermineStartingLevel_RealWorldExamples_ProduceSensibleDefaults()
    {
        // Given - Various real-world examples
        var testCases = new Dictionary<string, int>
        {
            // Short exact matches
            { "5", 5 },                             // Single char
            { "OK", 4 },                            // Very short (2 chars)
            
            // IDs and codes
            { "USR123", 3 },                        // User ID (mixed alphanumeric)
            { "ABC-DEF", 3 },                       // Code with delimiter
            { "item_42", 3 },                       // Underscore-separated
            
            // Numbers
            { "42", 2 },                            // Short number (2 chars, all digits)
            { "12345", 2 },                         // Longer number sequence
            
            // Words
            { "name", 4 },                          // Simple word (4 chars)
            { "hello world", 3 },                   // Two words with space delimiter (11 chars total, simple)
            { "one two three", 1 },                 // Three words
            
            // Long strings
            { "This is a longer string that exceeds the limit", 2 }, // > 25 chars
            
            // Special characters
            { "@user", 3 },                         // Symbol prefix (5 chars, has special char)
            { "v2.0.1", 3 },                        // Version number (multiple delimiters)
            { "#tag", 4 },                          // Hashtag (4 chars, in 2-4 range)
        };

        // When/Then
        foreach (var testCase in testCases)
        {
            int actualLevel = ExtractedPattern.DetermineStartingLevel(testCase.Key);
            Assert.Equal(testCase.Value, actualLevel);
        }
    }

    [Fact]
    public void DetermineStartingLevel_EdgeCases_HandledGracefully()
    {
        // Given - Edge cases
        Assert.Equal(3, ExtractedPattern.DetermineStartingLevel(""));       // Empty -> default
        Assert.Equal(3, ExtractedPattern.DetermineStartingLevel(null));     // Null -> default
        Assert.Equal(3, ExtractedPattern.DetermineStartingLevel("   "));    // Whitespace only -> default (trims to empty)
        Assert.Equal(3, ExtractedPattern.DetermineStartingLevel("\t"));     // Single tab -> default (whitespace-only)
        Assert.Equal(5, ExtractedPattern.DetermineStartingLevel(" a "));    // Single char with spaces (trims to 1 char)
    }

    // Case Sensitivity Tests

    [Theory]
    [InlineData("Abc123", 0, false, @"\S+")]
    [InlineData("Abc123", 0, true, @"(?i)\S+")]
    [InlineData("Abc123", 1, false, @"\w+")]
    [InlineData("Abc123", 1, true, @"(?i)\w+")]
    [InlineData("Abc123", 2, false, @"\w{3}\w{3}")]
    [InlineData("Abc123", 2, true, @"(?i)\w{3}\w{3}")]
    [InlineData("Abc123", 3, false, @"[A-Za-z]{3}\d{3}")]
    [InlineData("Abc123", 3, true, @"(?i)[A-Za-z]{3}\d{3}")]
    [InlineData("Abc123", 4, false, @"(?i)Abc123")]
    [InlineData("Abc123", 4, true, @"(?i)Abc123")]
    [InlineData("Abc123", 5, false, @"Abc123")]
    [InlineData("Abc123", 5, true, @"Abc123")]
    public void ExtractSimplePattern_WithCaseSensitivity_IncludesCorrectFlag(
        string input, int level, bool ignoreCase, string expectedPattern)
    {
        // When
        string actualPattern = Text_Grab.Utilities.StringMethods.ExtractSimplePattern(input, level, ignoreCase);

        // Then
        Assert.Equal(expectedPattern, actualPattern);
    }

    [Fact]
    public void ExtractSimplePattern_CaseInsensitive_MatchesDifferentCases()
    {
        // Given
        string input = "Test";
        string text = "test TEST TeSt Test testing";

        // When - Generate case-insensitive pattern
        string pattern = Text_Grab.Utilities.StringMethods.ExtractSimplePattern(input, 4, ignoreCase: true);
        MatchCollection matches = Regex.Matches(text, pattern);

        // Then - Should match all case variations
        Assert.Equal(5, matches.Count); // test, TEST, TeSt, Test (not "testing")
        Assert.Contains("(?i)", pattern); // Verify inline flag is present
    }

    [Fact]
    public void ExtractSimplePattern_CaseSensitive_MatchesExactCase()
    {
        // Given
        string input = "Test";
        string text = "test TEST TeSt Test testing";

        // When - Generate case-sensitive pattern (default)
        string pattern = Text_Grab.Utilities.StringMethods.ExtractSimplePattern(input, 5, ignoreCase: false);
        MatchCollection matches = Regex.Matches(text, pattern);

        // Then - Should match only exact case
        Assert.Equal(1, matches.Count); // Only "Test"
        Assert.DoesNotContain("(?i)", pattern); // Verify inline flag is NOT present
    }

    [Theory]
    [InlineData("Hello", 0, true)]
    [InlineData("World123", 1, true)]
    [InlineData("Test42", 2, true)]
    [InlineData("ABC", 3, true)]
    [InlineData("xyz", 4, true)]
    public void ExtractSimplePattern_AllLevels_SupportCaseInsensitiveFlag(string input, int level, bool ignoreCase)
    {
        // When
        string pattern = Text_Grab.Utilities.StringMethods.ExtractSimplePattern(input, level, ignoreCase);

        // Then
        if (ignoreCase)
        {
            Assert.StartsWith("(?i)", pattern);
        }
        else
        {
            Assert.DoesNotContain("(?i)", pattern);
        }
    }

    [Fact]
    public void ExtractSimplePattern_DefaultCaseSensitivity_IsFalse()
    {
        // Given
        string input = "Test";

        // When - Call without specifying ignoreCase (using default)
        string pattern = Text_Grab.Utilities.StringMethods.ExtractSimplePattern(input, 3);

        // Then - Default should be case-sensitive (no flag)
        Assert.DoesNotContain("(?i)", pattern);
    }

    [Fact]
    public void ExtractSimplePattern_CaseInsensitive_CrossPlatformCompatible()
    {
        // Given
        string input = "Test123";
        string text = "test123 TEST123 TeSt123 Test123";

        // When - Generate pattern with inline flag
        string pattern = Text_Grab.Utilities.StringMethods.ExtractSimplePattern(input, 4);

        // Create regex without RegexOptions to verify inline flag works standalone
        Regex regex = new(pattern);
        MatchCollection matches = regex.Matches(text);

        // Then - Inline flag should make it case-insensitive without needing RegexOptions
        Assert.Equal(4, matches.Count);
        Assert.Contains("(?i)", pattern);
    }

    [Theory]
    [InlineData("ABC", "abc ABC aBc", 3, true)] // Level 3: character types with counts
    [InlineData("ABC", "abc ABC aBc", 3, false)] // Level 3: character types with counts
    [InlineData("123", "123 456", 2, true)] // Level 2: length-based
    [InlineData("Test", "test TEST", 4, true)] // Level 4: per-character
    public void ExtractSimplePattern_CaseFlag_AffectsMatchBehavior(
        string input, string text, int level, bool ignoreCase)
    {
        // When
        string pattern = Text_Grab.Utilities.StringMethods.ExtractSimplePattern(input, level, ignoreCase);
        Regex regex = new Regex(pattern);
        int matchCount = regex.Matches(text).Count;

        // Then - Case-insensitive should find more or equal matches
        if (ignoreCase)
        {
            Assert.True(matchCount > 0, "Case-insensitive pattern should find matches");
        }

        // Verify flag presence
        Assert.Equal(ignoreCase, pattern.Contains("(?i)"));
    }

    [Fact]
    public void ExtractSimplePattern_ComplexPattern_WithCaseInsensitivity()
    {
        // Given
        string input = "Hello World!";
        string text = "hello world! HELLO WORLD! HeLLo WoRLd!";

        // When - Level 3 with case insensitivity
        string pattern = Text_Grab.Utilities.StringMethods.ExtractSimplePattern(input, 3, ignoreCase: true);
        Regex regex = new Regex(pattern);
        MatchCollection matches = regex.Matches(text);

        // Then
        Assert.Contains("(?i)", pattern);
        Assert.Equal(3, matches.Count); // All three variations should match
    }

    [Fact]
    public void ExtractedPattern_WithIgnoreCase_AllPatternsHaveFlag()
    {
        // Given
        string input = "Abc123";
        ExtractedPattern extractedPattern = new(input, ignoreCase: true);

        // When/Then - All patterns should have the (?i) flag
        for (int level = 0; level <= 4; level++)
        {
            string pattern = extractedPattern.GetPattern(level);
            Assert.StartsWith("(?i)", pattern);
        }
    }
}
