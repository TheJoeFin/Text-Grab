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
    }

    [Fact]
    public void ShouldUseNameFallback_AllowsLeafControls()
    {
        Assert.True(UIAutomationUtilities.ShouldUseNameFallback(ControlType.Text));
        Assert.True(UIAutomationUtilities.ShouldUseNameFallback(ControlType.Button));
        Assert.True(UIAutomationUtilities.ShouldUseNameFallback(ControlType.ListItem));
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
}
