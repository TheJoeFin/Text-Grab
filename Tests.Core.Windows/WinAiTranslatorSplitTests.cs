using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

public sealed class WinAiTranslatorSplitTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void SplitInHalf_TerminalNewlineWithoutWordBoundary_LeavesTextUnsplit(string newline)
    {
        string text = new string('a', 5000) + newline;

        string[] pieces = WinAiTranslator.SplitInHalf(text);

        Assert.Equal(text, Assert.Single(pieces));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void SplitInHalf_TerminalNewline_FallsBackToInteriorSpace(string newline)
    {
        string first = new string('a', 2500) + " ";
        string second = new string('b', 2500) + newline;

        string[] pieces = WinAiTranslator.SplitInHalf(first + second);

        Assert.Equal(new[] { first, second }, pieces);
        AssertValidSplit(first + second, pieces);
    }

    [Theory]
    [InlineData("\n", 300, 700)]
    [InlineData("\r\n", 300, 700)]
    [InlineData("\n", 700, 300)]
    [InlineData("\r\n", 700, 300)]
    public void SplitInHalf_InteriorNewline_PreservesSeparator(string newline, int firstLength, int secondLength)
    {
        string first = new string('a', firstLength) + newline;
        string second = new('b', secondLength);

        string[] pieces = WinAiTranslator.SplitInHalf(first + second);

        Assert.Equal(new[] { first, second }, pieces);
        AssertValidSplit(first + second, pieces);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("  \r\n\t")]
    public void SplitInHalf_LeadingWhitespace_PreservesTextAndMakesProgress(string prefix)
    {
        string text = prefix + new string('a', 2000) + " " + new string('b', 3000);

        string[] pieces = WinAiTranslator.SplitInHalf(text);

        AssertValidSplit(text, pieces);
    }

    [Fact]
    public void SplitInHalf_ParagraphWithoutNewline_SplitsAfterSpace()
    {
        string first = new string('a', 2500) + " ";
        string second = new('b', 2500);

        string[] pieces = WinAiTranslator.SplitInHalf(first + second);

        Assert.Equal(new[] { first, second }, pieces);
        AssertValidSplit(first + second, pieces);
    }

    [Fact]
    public void SplitInHalf_LongUnbrokenText_LeavesTextUnsplit()
    {
        string text = new('a', 10000);

        string[] pieces = WinAiTranslator.SplitInHalf(text);

        Assert.Equal(text, Assert.Single(pieces));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short text")]
    [InlineData("first\nsecond")]
    [InlineData(" \r\n\t ")]
    public void SplitInHalf_ShortText_LeavesTextUnsplit(string text)
    {
        string[] pieces = WinAiTranslator.SplitInHalf(text);

        Assert.Equal(text, Assert.Single(pieces));
    }

    [Theory]
    [InlineData(199, 1)]
    [InlineData(200, 2)]
    public void SplitInHalf_MinimumLength_PreservesExistingThreshold(int length, int expectedPieces)
    {
        string text = new string('a', 99) + " " + new string('b', length - 100);

        string[] pieces = WinAiTranslator.SplitInHalf(text);

        Assert.Equal(expectedPieces, pieces.Length);
        Assert.Equal(text, string.Concat(pieces));
        if (expectedPieces == 2)
            AssertValidSplit(text, pieces);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void SplitInHalf_RecursiveSplits_AlwaysMakeProgress(string newline)
    {
        string text = new string('a', 5000) + newline
            + new string('b', 3000) + " " + new string('c', 3000) + newline
            + new string('d', 5000) + newline;
        Stack<string> pending = new();
        pending.Push(text);

        while (pending.TryPop(out string? block))
        {
            string[] pieces = WinAiTranslator.SplitInHalf(block);
            if (pieces.Length == 1)
            {
                Assert.Equal(block, pieces[0]);
                continue;
            }

            AssertValidSplit(block, pieces);
            foreach (string piece in pieces)
                pending.Push(piece);
        }
    }

    private static void AssertValidSplit(string text, string[] pieces)
    {
        Assert.Equal(2, pieces.Length);
        Assert.Equal(text, string.Concat(pieces));
        Assert.All(pieces, piece => Assert.InRange(piece.Length, 1, text.Length - 1));
    }
}
