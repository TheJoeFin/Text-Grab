using System;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

public class SplitOnSelectionTests
{
    [Theory]
    [InlineData("one, two, three", ", ", "one\r\n, two\r\n, three")]
    [InlineData("A a A", "A", "\r\nA a \r\nA")]
    [InlineData("aaaaa", "aa", "\r\naa\r\naaa")]
    [InlineData("no match", "x", "no match")]
    [InlineData("", "x", "")]
    public void SplitOnSelection_BreaksBeforeEveryOccurrence(string fullText, string selection, string expected)
    {
        Assert.Equal(expected.Replace("\r\n", Environment.NewLine), StringMethods.SplitOnSelection(fullText, selection));
    }

    [Theory]
    [InlineData("one, two, three", ", ", "one, \r\ntwo, \r\nthree")]
    [InlineData("A a A", "A", "A\r\n a A\r\n")]
    [InlineData("aaaaa", "aa", "aa\r\naa\r\na")]
    [InlineData("no match", "x", "no match")]
    public void SplitAfterSelection_BreaksAfterEveryOccurrence(string fullText, string selection, string expected)
    {
        Assert.Equal(expected.Replace("\r\n", Environment.NewLine), StringMethods.SplitAfterSelection(fullText, selection));
    }

    [Fact]
    public void EmptySelection_Throws()
    {
        Assert.Throws<ArgumentException>(() => StringMethods.SplitOnSelection("text", ""));
        Assert.Throws<ArgumentException>(() => StringMethods.SplitAfterSelection("text", ""));
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => StringMethods.SplitOnSelection(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => StringMethods.SplitOnSelection("text", null!));
        Assert.Throws<ArgumentNullException>(() => StringMethods.SplitAfterSelection(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => StringMethods.SplitAfterSelection("text", null!));
    }
}
