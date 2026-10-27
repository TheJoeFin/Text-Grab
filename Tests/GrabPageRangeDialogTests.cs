using System.Collections.Generic;
using Text_Grab.Controls;

namespace Tests;

public class GrabPageRangeDialogTests
{
    [Fact]
    public void BuildPageIndices_All_ReturnsEveryPageInRange()
    {
        List<int> indices = GrabPageRangeDialog.BuildPageIndices(2, 5, GrabPageParity.All);

        Assert.Equal([2, 3, 4, 5], indices);
    }

    [Fact]
    public void BuildPageIndices_OddOnly_UsesPrintedPageNumbers()
    {
        // Indices 0..5 are pages 1..6; odd pages are 1, 3, 5 → indices 0, 2, 4.
        List<int> indices = GrabPageRangeDialog.BuildPageIndices(0, 5, GrabPageParity.OddOnly);

        Assert.Equal([0, 2, 4], indices);
    }

    [Fact]
    public void BuildPageIndices_EvenOnly_UsesPrintedPageNumbers()
    {
        List<int> indices = GrabPageRangeDialog.BuildPageIndices(0, 5, GrabPageParity.EvenOnly);

        Assert.Equal([1, 3, 5], indices);
    }

    [Fact]
    public void BuildPageIndices_EmptyRange_ReturnsNothing()
    {
        Assert.Empty(GrabPageRangeDialog.BuildPageIndices(4, 3, GrabPageParity.All));
    }
}
