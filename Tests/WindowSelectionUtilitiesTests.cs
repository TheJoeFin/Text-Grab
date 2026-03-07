using System.Windows;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

public class WindowSelectionUtilitiesTests
{
    [Fact]
    public void FindWindowAtPoint_ReturnsFirstMatchingCandidate()
    {
        WindowSelectionCandidate topCandidate = new((nint)1, new Rect(0, 0, 40, 40), "Top", 100);
        WindowSelectionCandidate lowerCandidate = new((nint)2, new Rect(0, 0, 60, 60), "Lower", 101);

        WindowSelectionCandidate? found = WindowSelectionUtilities.FindWindowAtPoint(
            [topCandidate, lowerCandidate],
            new Point(20, 20));

        Assert.Same(topCandidate, found);
    }

    [Fact]
    public void FindWindowAtPoint_ReturnsNullWhenPointIsOutsideEveryCandidate()
    {
        WindowSelectionCandidate candidate = new((nint)1, new Rect(0, 0, 40, 40), "Only", 100);

        WindowSelectionCandidate? found = WindowSelectionUtilities.FindWindowAtPoint(
            [candidate],
            new Point(80, 80));

        Assert.Null(found);
    }
}
