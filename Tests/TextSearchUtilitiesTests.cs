using System.Text.RegularExpressions;
using Text_Grab.Utilities;

namespace Tests;

public class TextSearchUtilitiesTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", true)]
    [InlineData("  ", true)]
    [InlineData("text", true)]
    [InlineData("\t", true)]
    [InlineData("\n", true)]
    public void HasSearchText_TreatsWhitespaceAsSearchableInput(string? searchText, bool expected)
    {
        Assert.Equal(expected, TextSearchUtilities.HasSearchText(searchText));
    }

    [Fact]
    public void CreateFindAndReplaceSearchRegex_MatchesLiteralDoubleSpaces()
    {
        Regex regex = TextSearchUtilities.CreateFindAndReplaceSearchRegex(
            "  ".EscapeSpecialRegexChars(matchExactly: false),
            usePatternMode: false,
            exactMatch: false);

        Match match = regex.Match("alpha  beta");

        Assert.True(match.Success);
        Assert.Equal("  ", match.Value);
    }

    [Fact]
    public void CreateReplacementRegex_CollapsesDoubleSpaces()
    {
        Regex regex = TextSearchUtilities.CreateReplacementRegex(
            "  ".EscapeSpecialRegexChars(matchExactly: false),
            exactMatch: false);

        string replaced = regex.Replace("alpha  beta  gamma", " ");

        Assert.Equal("alpha beta gamma", replaced);
    }

    [Fact]
    public void CreateGrabFrameSearchRegex_TreatsSpacesLiterally()
    {
        Regex regex = TextSearchUtilities.CreateGrabFrameSearchRegex("a b", exactMatch: true);

        Assert.Matches(regex, "a b");
        Assert.DoesNotMatch(regex, "ab");
    }

    [Theory]
    [InlineData(" ", "·")]
    [InlineData("  ", "··")]
    [InlineData("\t", "⇥")]
    [InlineData("\n", "⏎")]
    [InlineData("\r", "␍")]
    [InlineData("\r\n", "⏎")]
    public void FormatMatchTextForDisplay_MakesWhitespaceMatchesVisible(string input, string expected)
    {
        Assert.Equal(expected, TextSearchUtilities.FormatMatchTextForDisplay(input));
    }
}
