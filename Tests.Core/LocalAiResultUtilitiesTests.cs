using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core;

public class LocalAiResultUtilitiesTests
{
    [Fact]
    public void IsUnchanged_IdenticalText_ReturnsTrue()
    {
        Assert.True(LocalAiResultUtilities.IsUnchanged("Hello world", "Hello world"));
    }

    [Fact]
    public void IsUnchanged_DifferentText_ReturnsFalse()
    {
        Assert.False(LocalAiResultUtilities.IsUnchanged("Hello world", "Hello, world!"));
    }

    [Fact]
    public void IsUnchanged_IgnoresLineEndingsAndSurroundingWhitespace()
    {
        Assert.True(LocalAiResultUtilities.IsUnchanged("line one\r\nline two", "line one\nline two\n"));
        Assert.True(LocalAiResultUtilities.IsUnchanged("  padded  ", "padded"));
    }

    [Fact]
    public void IsUnchanged_InteriorWhitespaceStillCounts()
    {
        Assert.False(LocalAiResultUtilities.IsUnchanged("one two", "one  two"));
    }

    [Fact]
    public void IsUnchanged_CaseSensitive()
    {
        Assert.False(LocalAiResultUtilities.IsUnchanged("Hello", "hello"));
    }
}
