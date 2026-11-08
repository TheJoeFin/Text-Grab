using System.Collections.Generic;
using Text_Grab.Controls;
using Text_Grab.Utilities;

namespace Tests;

public class GrabPageRangeDialogTests
{
    [Fact]
    public void FilterByParity_All_KeepsEverything()
    {
        Assert.Equal([2, 3, 4, 5], GrabPageRangeDialog.FilterByParity([2, 3, 4, 5], GrabPageParity.All));
    }

    [Fact]
    public void FilterByParity_OddOnly_UsesPrintedPageNumbers()
    {
        // Indices 0..5 are pages 1..6; odd pages are 1, 3, 5 → indices 0, 2, 4.
        Assert.Equal([0, 2, 4], GrabPageRangeDialog.FilterByParity([0, 1, 2, 3, 4, 5], GrabPageParity.OddOnly));
    }

    [Fact]
    public void FilterByParity_EvenOnly_UsesPrintedPageNumbers()
    {
        Assert.Equal([1, 3, 5], GrabPageRangeDialog.FilterByParity([0, 1, 2, 3, 4, 5], GrabPageParity.EvenOnly));
    }

    [Fact]
    public void PageRangeParser_RangesAndSinglePages()
    {
        Assert.True(PageRangeParser.TryParse("4-9,12", 20, out List<int> indices, out _));
        Assert.Equal([3, 4, 5, 6, 7, 8, 11], indices);
    }

    [Fact]
    public void PageRangeParser_BlankMeansAllPages()
    {
        Assert.True(PageRangeParser.TryParse("  ", 3, out List<int> indices, out _));
        Assert.Equal([0, 1, 2], indices);
    }

    [Fact]
    public void PageRangeParser_OpenEndedRanges()
    {
        Assert.True(PageRangeParser.TryParse("-2, 5-", 6, out List<int> indices, out _));
        Assert.Equal([0, 1, 4, 5], indices);
    }

    [Fact]
    public void PageRangeParser_SortsAndDeduplicates()
    {
        Assert.True(PageRangeParser.TryParse("8, 2-4, 3, 8", 10, out List<int> indices, out _));
        Assert.Equal([1, 2, 3, 7], indices);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("11")]
    [InlineData("9-4")]
    [InlineData("-")]
    [InlineData("1-2-3")]
    [InlineData("1,,x")]
    public void PageRangeParser_InvalidInput_ReturnsError(string text)
    {
        Assert.False(PageRangeParser.TryParse(text, 10, out _, out string error));
        Assert.NotEmpty(error);
    }
}
