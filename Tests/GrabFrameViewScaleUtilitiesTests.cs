using System.Windows;
using Text_Grab.Utilities;

namespace Tests;

public class GrabFrameViewScaleUtilitiesTests
{
    [Theory]
    [InlineData(1.0, 1, 1.25)]
    [InlineData(1.0, -1, 0.75)]
    [InlineData(0.5, -1, 0.5)]
    [InlineData(5.0, 1, 5.0)]
    public void StepScale_AdjustsAndClampsAsExpected(double currentScale, int direction, double expected)
    {
        double actual = GrabFrameViewScaleUtilities.StepScale(currentScale, direction);

        Assert.Equal(expected, actual, 3);
    }

    [Fact]
    public void GetMinimumWindowRect_LeavesLargeWindowUnchanged()
    {
        Rect currentWindowRect = new(300, 200, 900, 700);
        Size minimumWindowSize = new(
            GrabFrameViewScaleUtilities.MinimumLoadedDocumentWindowWidth,
            GrabFrameViewScaleUtilities.MinimumLoadedDocumentWindowHeight);
        Rect workArea = new(0, 0, 1920, 1080);

        Rect actual = GrabFrameViewScaleUtilities.GetMinimumWindowRect(currentWindowRect, minimumWindowSize, workArea);

        Assert.Equal(currentWindowRect, actual);
    }

    [Fact]
    public void GetMinimumWindowRect_ExpandsAndCentersWithinWorkArea()
    {
        Rect currentWindowRect = new(500, 250, 400, 300);
        Size minimumWindowSize = new(
            GrabFrameViewScaleUtilities.MinimumLoadedDocumentWindowWidth,
            GrabFrameViewScaleUtilities.MinimumLoadedDocumentWindowHeight);
        Rect workArea = new(0, 0, 1920, 1080);

        Rect actual = GrabFrameViewScaleUtilities.GetMinimumWindowRect(currentWindowRect, minimumWindowSize, workArea);

        Assert.Equal(new Rect(300, 175, 800, 450), actual);
    }

    [Fact]
    public void GetMinimumWindowRect_ClampsExpandedWindowInsideWorkArea()
    {
        Rect currentWindowRect = new(1500, 700, 400, 300);
        Size minimumWindowSize = new(
            GrabFrameViewScaleUtilities.MinimumLoadedDocumentWindowWidth,
            GrabFrameViewScaleUtilities.MinimumLoadedDocumentWindowHeight);
        Rect workArea = new(0, 0, 1920, 1080);

        Rect actual = GrabFrameViewScaleUtilities.GetMinimumWindowRect(currentWindowRect, minimumWindowSize, workArea);

        Assert.Equal(new Rect(1120, 625, 800, 450), actual);
    }
}
