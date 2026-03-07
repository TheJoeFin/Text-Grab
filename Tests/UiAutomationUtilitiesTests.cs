using System.Linq;
using System.Windows;
using System.Windows.Automation;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

public class UiAutomationUtilitiesTests
{
    [Fact]
    public void NormalizeText_TrimsWhitespaceAndCollapsesEmptyLines()
    {
        string normalized = UIAutomationUtilities.NormalizeText("  Hello   world \r\n\r\n Second\tline ");

        Assert.Equal($"Hello world{Environment.NewLine}Second line", normalized);
    }

    [Fact]
    public void TryAddUniqueText_DeduplicatesNormalizedValues()
    {
        HashSet<string> seen = [];
        List<string> output = [];

        bool addedFirst = UIAutomationUtilities.TryAddUniqueText(" Hello   world ", seen, output);
        bool addedSecond = UIAutomationUtilities.TryAddUniqueText("Hello world", seen, output);

        Assert.True(addedFirst);
        Assert.False(addedSecond);
        Assert.Single(output);
    }

    [Fact]
    public void FindTargetWindowCandidate_PrefersCenterPointHit()
    {
        WindowSelectionCandidate first = new((nint)1, new Rect(0, 0, 80, 80), "First", 1);
        WindowSelectionCandidate second = new((nint)2, new Rect(90, 0, 80, 80), "Second", 2);

        WindowSelectionCandidate? candidate = UIAutomationUtilities.FindTargetWindowCandidate(
            new Rect(100, 10, 20, 20),
            [first, second]);

        Assert.Same(second, candidate);
    }

    [Fact]
    public void FindTargetWindowCandidate_FallsBackToLargestIntersection()
    {
        WindowSelectionCandidate first = new((nint)1, new Rect(0, 0, 50, 50), "First", 1);
        WindowSelectionCandidate second = new((nint)2, new Rect(60, 0, 80, 80), "Second", 2);

        WindowSelectionCandidate? candidate = UIAutomationUtilities.FindTargetWindowCandidate(
            new Rect(40, 40, 30, 30),
            [first, second]);

        Assert.Same(second, candidate);
    }

    [Fact]
    public void ShouldUseNameFallback_SkipsStructuralControls()
    {
        Assert.False(UIAutomationUtilities.ShouldUseNameFallback(ControlType.Window));
        Assert.False(UIAutomationUtilities.ShouldUseNameFallback(ControlType.Group));
        Assert.False(UIAutomationUtilities.ShouldUseNameFallback(ControlType.Pane));
        Assert.False(UIAutomationUtilities.ShouldUseNameFallback(ControlType.Custom));
        Assert.False(UIAutomationUtilities.ShouldUseNameFallback(ControlType.Button));
        Assert.False(UIAutomationUtilities.ShouldUseNameFallback(ControlType.SplitButton));
        Assert.False(UIAutomationUtilities.ShouldUseNameFallback(ControlType.ComboBox));
    }

    [Fact]
    public void ShouldUseNameFallback_AllowsVisibleTextContainers()
    {
        Assert.True(UIAutomationUtilities.ShouldUseNameFallback(ControlType.Text));
        Assert.True(UIAutomationUtilities.ShouldUseNameFallback(ControlType.ListItem));
        Assert.True(UIAutomationUtilities.ShouldUseNameFallback(ControlType.MenuItem));
        Assert.True(UIAutomationUtilities.ShouldUseNameFallback(ControlType.TabItem));
    }

    [Fact]
    public void GetSamplePoints_UsesCenterPointForSmallSelections()
    {
        IReadOnlyList<Point> samplePoints = UIAutomationUtilities.GetSamplePoints(new Rect(10, 20, 40, 30));

        Point samplePoint = Assert.Single(samplePoints);
        Assert.Equal(new Point(30, 35), samplePoint);
    }

    [Fact]
    public void GetSamplePoints_UsesGridForLargerSelections()
    {
        IReadOnlyList<Point> samplePoints = UIAutomationUtilities.GetSamplePoints(new Rect(0, 0, 100, 100));

        Assert.Equal(9, samplePoints.Count);
        Assert.Contains(new Point(50, 50), samplePoints);
        Assert.Contains(new Point(20, 20), samplePoints);
        Assert.Contains(new Point(80, 80), samplePoints);
    }

    [Fact]
    public void GetPointProbePoints_ReturnsCenterThenCrosshairNeighbors()
    {
        IReadOnlyList<Point> probePoints = UIAutomationUtilities.GetPointProbePoints(new Point(25, 40));

        Assert.Equal(5, probePoints.Count);
        Assert.Equal(new Point(25, 40), probePoints[0]);
        Assert.Contains(new Point(23, 40), probePoints);
        Assert.Contains(new Point(27, 40), probePoints);
        Assert.Contains(new Point(25, 38), probePoints);
        Assert.Contains(new Point(25, 42), probePoints);
    }

    [Fact]
    public void TryClipBounds_ReturnsIntersectionForOverlappingRects()
    {
        bool clipped = UIAutomationUtilities.TryClipBounds(
            new Rect(10, 10, 50, 50),
            new Rect(30, 25, 50, 50),
            out Rect result);

        Assert.True(clipped);
        Assert.Equal(new Rect(30, 25, 30, 35), result);
    }

    [Fact]
    public void TryClipBounds_ReturnsFalseWhenBoundsDoNotIntersect()
    {
        bool clipped = UIAutomationUtilities.TryClipBounds(
            new Rect(10, 10, 20, 20),
            new Rect(100, 100, 20, 20),
            out Rect result);

        Assert.False(clipped);
        Assert.Equal(Rect.Empty, result);
    }

    [Fact]
    public void TryAddUniqueOverlayItem_DeduplicatesNormalizedTextAndBounds()
    {
        HashSet<string> seen = [];
        List<UiAutomationOverlayItem> output = [];
        UiAutomationOverlayItem first = new(" Hello   world ", new Rect(10.01, 20.01, 30.01, 40.01), UiAutomationOverlaySource.ElementBounds);
        UiAutomationOverlayItem second = new("Hello world", new Rect(10.04, 20.04, 30.04, 40.04), UiAutomationOverlaySource.VisibleTextRange);

        bool addedFirst = UIAutomationUtilities.TryAddUniqueOverlayItem(first, seen, output);
        bool addedSecond = UIAutomationUtilities.TryAddUniqueOverlayItem(second, seen, output);

        Assert.True(addedFirst);
        Assert.False(addedSecond);
        Assert.Single(output);
    }

    [Fact]
    public void SortOverlayItems_OrdersTopThenLeft()
    {
        IReadOnlyList<UiAutomationOverlayItem> sorted = UIAutomationUtilities.SortOverlayItems(
        [
            new UiAutomationOverlayItem("Bottom", new Rect(40, 30, 10, 10), UiAutomationOverlaySource.ElementBounds),
            new UiAutomationOverlayItem("Right", new Rect(25, 10, 10, 10), UiAutomationOverlaySource.ElementBounds),
            new UiAutomationOverlayItem("Left", new Rect(10, 10, 10, 10), UiAutomationOverlaySource.ElementBounds),
        ]);

        Assert.Equal(["Left", "Right", "Bottom"], sorted.Select(item => item.Text));
    }
}
