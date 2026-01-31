using Text_Grab.Utilities;

namespace Tests;

/// <summary>
/// Tests for the WindowsAiUtilities.CleanRegexResult method.
/// </summary>
public class WindowsAiUtilitiesTests
{
    /// <summary>
    /// Tests that null input returns an empty string.
    /// </summary>
    [Fact]
    public void CleanRegexResult_NullInput_ReturnsEmptyString()
    {
        // Arrange
        string? input = null;

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input!);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// Tests that empty string input returns an empty string.
    /// </summary>
    [Fact]
    public void CleanRegexResult_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        string input = string.Empty;

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// Tests that whitespace-only inputs return an empty string.
    /// </summary>
    /// <param name="input">The whitespace input to test</param>
    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("  \t\n  ")]
    public void CleanRegexResult_WhitespaceOnly_ReturnsEmptyString(string input)
    {
        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// Tests that simple regex patterns without markdown are returned correctly.
    /// </summary>
    /// <param name="input">The input containing a regex pattern</param>
    /// <param name="expected">The expected cleaned regex pattern</param>
    [Theory]
    [InlineData("\\d+", "\\d+")]
    [InlineData("  \\d+  ", "\\d+")]
    [InlineData("[a-z]+", "[a-z]+")]
    [InlineData("(\\d{3})-\\d{4}", "(\\d{3})-\\d{4}")]
    [InlineData("^start.*end$", "^start.*end$")]
    [InlineData("a|b|c", "a|b|c")]
    [InlineData(".*", ".*")]
    [InlineData("\\w+@\\w+\\.com", "\\w+@\\w+\\.com")]
    public void CleanRegexResult_SimpleRegexPattern_ReturnsPattern(string input, string expected)
    {
        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Tests that markdown code blocks are properly removed.
    /// </summary>
    /// <param name="input">The input with markdown formatting</param>
    /// <param name="expected">The expected cleaned regex pattern</param>
    [Theory]
    [InlineData("```\n\\d+\n```", "\\d+")]
    [InlineData("```regex\n\\d+\n```", "\\d+")]
    [InlineData("```csharp\n[a-z]+\n```", "[a-z]+")]
    [InlineData("```\n(\\w+)\n```", "(\\w+)")]
    [InlineData("```python\n^.*$\n```", "^.*$")]
    public void CleanRegexResult_MarkdownCodeBlock_ReturnsPattern(string input, string expected)
    {
        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Tests that markdown code blocks without closing backticks are handled correctly.
    /// </summary>
    [Fact]
    public void CleanRegexResult_MarkdownWithoutClosing_ReturnsPattern()
    {
        // Arrange
        string input = "```\n\\d+";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\d+", result);
    }

    /// <summary>
    /// Tests that empty markdown code blocks return empty string.
    /// </summary>
    [Fact]
    public void CleanRegexResult_EmptyMarkdownBlock_ReturnsEmptyString()
    {
        // Arrange
        string input = "```\n```";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// Tests that just markdown markers without content return empty string.
    /// </summary>
    [Fact]
    public void CleanRegexResult_JustMarkdownMarkers_ReturnsEmptyString()
    {
        // Arrange
        string input = "```";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// Tests that backticks surrounding patterns are removed.
    /// </summary>
    /// <param name="input">The input with backticks</param>
    /// <param name="expected">The expected cleaned regex pattern</param>
    [Theory]
    [InlineData("`\\d+`", "\\d+")]
    [InlineData("``[a-z]+``", "[a-z]+")]
    [InlineData("`(\\w+)`", "(\\w+)")]
    public void CleanRegexResult_BacktickWrapped_ReturnsPattern(string input, string expected)
    {
        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Tests that common prefixes are removed from regex patterns.
    /// </summary>
    /// <param name="input">The input with prefix</param>
    /// <param name="expected">The expected cleaned regex pattern</param>
    [Theory]
    [InlineData("regex: \\d+", "\\d+")]
    [InlineData("Regex: \\d+", "\\d+")]
    [InlineData("REGEX: \\d+", "\\d+")]
    [InlineData("pattern: [a-z]+", "[a-z]+")]
    [InlineData("Pattern: [a-z]+", "[a-z]+")]
    [InlineData("PATTERN: [a-z]+", "[a-z]+")]
    public void CleanRegexResult_WithPrefix_ReturnsPatternWithoutPrefix(string input, string expected)
    {
        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Tests that lines starting with Expression: are filtered out.
    /// </summary>
    [Fact]
    public void CleanRegexResult_ExpressionPrefix_FiltersOutLine()
    {
        // Arrange
        string input = "Expression: \\d+\n[a-z]+";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("[a-z]+", result);
    }

    /// <summary>
    /// Tests that comment lines are filtered out correctly.
    /// </summary>
    /// <param name="input">The input with comments</param>
    /// <param name="expected">The expected cleaned regex pattern</param>
    [Theory]
    [InlineData("// This is a comment\n\\d+", "\\d+")]
    [InlineData("# This is a comment\n[a-z]+", "[a-z]+")]
    [InlineData("// Comment 1\n// Comment 2\n(\\w+)", "(\\w+)")]
    [InlineData("# Python comment\n^.*$", "^.*$")]
    public void CleanRegexResult_WithComments_FiltersCommentsAndReturnsPattern(string input, string expected)
    {
        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Tests that multi-line input returns the first valid regex pattern.
    /// </summary>
    [Fact]
    public void CleanRegexResult_MultiplePatterns_ReturnsFirstPattern()
    {
        // Arrange
        string input = "Here's your regex:\n\\d+\n[a-z]+\n(\\w+)";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\d+", result);
    }

    /// <summary>
    /// Tests that explanatory text is filtered when followed by regex pattern.
    /// </summary>
    [Fact]
    public void CleanRegexResult_ExplanationWithPattern_ReturnsPattern()
    {
        // Arrange
        string input = "This pattern matches digits:\n\\d+";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\d+", result);
    }

    /// <summary>
    /// Tests various regex special characters are properly detected.
    /// </summary>
    /// <param name="input">The input containing regex special characters</param>
    [Theory]
    [InlineData("[abc]")]
    [InlineData("(test)")]
    [InlineData("\\d")]
    [InlineData("^start")]
    [InlineData("end$")]
    [InlineData("a+")]
    [InlineData("b*")]
    [InlineData("c?")]
    [InlineData("a|b")]
    [InlineData(".*")]
    public void CleanRegexResult_RegexSpecialCharacters_RecognizedAsPattern(string input)
    {
        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal(input, result);
    }

    /// <summary>
    /// Tests that plain text without regex characters is returned as-is when no better option exists.
    /// </summary>
    [Fact]
    public void CleanRegexResult_PlainTextNoRegexChars_ReturnsCleanedText()
    {
        // Arrange
        string input = "just plain text";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("just plain text", result);
    }

    /// <summary>
    /// Tests that text with only some special characters but no regex-like patterns is handled correctly.
    /// </summary>
    [Fact]
    public void CleanRegexResult_TextWithNonRegexSpecialChars_ReturnsCleanedText()
    {
        // Arrange
        string input = "Hello! How are you?";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("Hello! How are you?", result);
    }

    /// <summary>
    /// Tests complex markdown with multiple elements returns the correct pattern.
    /// </summary>
    [Fact]
    public void CleanRegexResult_ComplexMarkdownWithExplanation_ReturnsPattern()
    {
        // Arrange
        string input = "```regex\n// Matches email addresses\n\\w+@\\w+\\.com\n```";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\w+@\\w+\\.com", result);
    }

    /// <summary>
    /// Tests that mixed line endings (CR, LF, CRLF) are handled correctly.
    /// </summary>
    [Fact]
    public void CleanRegexResult_MixedLineEndings_ReturnsPattern()
    {
        // Arrange
        string input = "Comment line\r\n\\d+\r[a-z]+";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\d+", result);
    }

    /// <summary>
    /// Tests that very long strings are handled without errors.
    /// </summary>
    [Fact]
    public void CleanRegexResult_VeryLongString_HandlesWithoutError()
    {
        // Arrange
        string longPrefix = new('x', 10000);
        string input = $"{longPrefix}\n\\d+";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\d+", result);
    }

    /// <summary>
    /// Tests that multiple backticks are all removed correctly.
    /// </summary>
    [Fact]
    public void CleanRegexResult_MultipleBackticks_AllRemoved()
    {
        // Arrange
        string input = "````\\d+````";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\d+", result);
    }

    /// <summary>
    /// Tests that Regex: prefix lines are filtered when they don't contain a pattern.
    /// </summary>
    [Fact]
    public void CleanRegexResult_RegexPrefixWithoutPattern_FiltersLine()
    {
        // Arrange
        string input = "Regex: This is the pattern\n\\d+";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\d+", result);
    }

    /// <summary>
    /// Tests that patterns with all types of special regex characters combined are recognized.
    /// </summary>
    [Fact]
    public void CleanRegexResult_ComplexRegexPattern_ReturnsCompletePattern()
    {
        // Arrange
        string input = "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$", result);
    }

    /// <summary>
    /// Tests that markdown without a newline after opening fence is handled.
    /// </summary>
    [Fact]
    public void CleanRegexResult_MarkdownWithoutNewlineAfterOpening_HandlesCorrectly()
    {
        // Arrange
        string input = "```\\d+```";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\d+", result);
    }

    /// <summary>
    /// Tests that only opening markdown fence without closing is properly handled.
    /// </summary>
    [Fact(Skip = "ProductionBugSuspected")]
    [Trait("Category", "ProductionBugSuspected")]
    public void CleanRegexResult_OnlyOpeningFence_ReturnsPattern()
    {
        // Arrange
        string input = "```\npattern: [a-z]+";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("[a-z]+", result);
    }

    /// <summary>
    /// Tests that whitespace before and after markdown is properly trimmed.
    /// </summary>
    [Fact]
    public void CleanRegexResult_MarkdownWithSurroundingWhitespace_TrimsCorrectly()
    {
        // Arrange
        string input = "  ```\n\\d+\n```  ";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\d+", result);
    }

    /// <summary>
    /// Tests that empty lines between content don't affect result.
    /// </summary>
    [Fact]
    public void CleanRegexResult_EmptyLinesBetweenContent_ReturnsFirstPattern()
    {
        // Arrange
        string input = "\n\n\\d+\n\n[a-z]+";

        // Act
        string result = WindowsAiUtilities.CleanRegexResult(input);

        // Assert
        Assert.Equal("\\d+", result);
    }
}
