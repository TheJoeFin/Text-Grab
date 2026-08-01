using Text_Grab.Views;

namespace Tests;

public class GrabFrameSearchTests
{
    [Fact]
    public void BuildSearchText_MapsMultiWordTextBackToSourceItems()
    {
        (string text, IReadOnlyList<(int SourceIndex, int Start, int Length)> segments) =
            GrabFrame.BuildSearchText(
                [("555", 20), ("Call", 0), ("1234", 40)],
                isSpaceJoining: true,
                isRightToLeft: false);

        Assert.Equal("Call 555 1234", text);
        Assert.Collection(
            segments,
            segment => Assert.Equal((1, 0, 4), segment),
            segment => Assert.Equal((0, 5, 3), segment),
            segment => Assert.Equal((2, 9, 4), segment));

        List<int> matchedSourceIndexes =
        [
            .. segments
                .Where(segment => GrabFrame.SpansOverlap(segment.Start, segment.Length, 5, 8))
                .Select(segment => segment.SourceIndex)
        ];
        Assert.Equal([0, 2], matchedSourceIndexes);
    }

    [Fact]
    public void BuildSearchText_UsesRightToLeftVisualOrder()
    {
        (string text, _) = GrabFrame.BuildSearchText(
            [("right", 100), ("left", 0)],
            isSpaceJoining: true,
            isRightToLeft: true);

        Assert.Equal("right left", text);
    }

    [Theory]
    [InlineData(0, 10, 10, 0, 12, 10, true)]
    [InlineData(0, 10, 10, 0, 30, 10, false)]
    [InlineData(0, 10, 10, 1, 10, 10, false)]
    public void AreOnSameSearchLine_RequiresMatchingLineAndVerticalAlignment(
        int firstLineNumber,
        double firstTop,
        double firstHeight,
        int secondLineNumber,
        double secondTop,
        double secondHeight,
        bool expected)
    {
        Assert.Equal(
            expected,
            GrabFrame.AreOnSameSearchLine(
                firstLineNumber,
                firstTop,
                firstHeight,
                secondLineNumber,
                secondTop,
                secondHeight));
    }

    [Theory]
    [InlineData(0, 4, 2, 4, true)]
    [InlineData(0, 4, 4, 2, false)]
    [InlineData(5, 3, 0, 8, true)]
    [InlineData(5, 0, 5, 1, false)]
    public void SpansOverlap_DetectsOnlyNonEmptyIntersectingRanges(
        int firstStart,
        int firstLength,
        int secondStart,
        int secondLength,
        bool expected)
    {
        Assert.Equal(expected, GrabFrame.SpansOverlap(firstStart, firstLength, secondStart, secondLength));
    }
}
