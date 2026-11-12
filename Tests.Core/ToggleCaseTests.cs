using System;
using System.Globalization;
using Text_Grab;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

public class ToggleCaseTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData("the case", CurrentCase.Lower, "the case", CurrentCase.Camel)]
    [InlineData("the case", CurrentCase.Camel, "The Case", CurrentCase.Upper)]
    [InlineData("the case", CurrentCase.Upper, "THE CASE", CurrentCase.Lower)]
    [InlineData("THE CASE", CurrentCase.Lower, "the case", CurrentCase.Camel)]
    [InlineData("THE CASE", CurrentCase.Camel, "THE CASE", CurrentCase.Upper)]
    [InlineData("tHe CaSe", CurrentCase.Upper, "THE CASE", CurrentCase.Lower)]
    public void KnownState_AppliesThatCaseAndAdvances(string text, CurrentCase state, string expected, CurrentCase next)
    {
        Assert.Equal((expected, next), StringMethods.ToggleCase(text, state, English));
    }

    [Theory]
    [InlineData("the case", "the case", CurrentCase.Camel)]
    [InlineData("THE CASE", "THE CASE", CurrentCase.Lower)]
    [InlineData("tHe CaSe", "The Case", CurrentCase.Upper)]
    [InlineData("123", "123", CurrentCase.Upper)]
    public void UnknownState_DetectsTheCaseFirst(string text, string expected, CurrentCase next)
    {
        Assert.Equal((expected, next), StringMethods.ToggleCase(text, CurrentCase.Unknown, English));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void WhitespaceOnly_IsUnchangedAndStaysUnknown(string text)
    {
        Assert.Equal((text, CurrentCase.Unknown), StringMethods.ToggleCase(text, CurrentCase.Unknown, English));
    }

    [Fact]
    public void CyclesThroughEveryCase()
    {
        string text = "the quick fox";
        CurrentCase state = CurrentCase.Unknown;
        string[] seen = new string[4];

        for (int i = 0; i < seen.Length; i++)
        {
            (text, state) = StringMethods.ToggleCase(text, state, English);
            seen[i] = text;
        }

        Assert.Equal(["the quick fox", "The Quick Fox", "THE QUICK FOX", "the quick fox"], seen);
        Assert.Equal(CurrentCase.Camel, state);
    }

    [Fact]
    public void UsesTheGivenCulture()
    {
        CultureInfo turkish = CultureInfo.GetCultureInfo("tr-TR");

        Assert.Equal("İSTANBUL", StringMethods.ToggleCase("istanbul", CurrentCase.Upper, turkish).Result);
        Assert.Equal("ISTANBUL", StringMethods.ToggleCase("istanbul", CurrentCase.Upper, CultureInfo.InvariantCulture).Result);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => StringMethods.ToggleCase(null!, CurrentCase.Unknown, English));
        Assert.Throws<ArgumentNullException>(() => StringMethods.ToggleCase("text", CurrentCase.Unknown, null!));
    }
}
