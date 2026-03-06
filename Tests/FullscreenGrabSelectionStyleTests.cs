using System.Windows;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Views;

namespace Tests;

public class FullscreenGrabSelectionStyleTests
{
    [Theory]
    [InlineData(FsgSelectionStyle.Window, false, true)]
    [InlineData(FsgSelectionStyle.Window, true, true)]
    [InlineData(FsgSelectionStyle.Region, true, true)]
    [InlineData(FsgSelectionStyle.Region, false, false)]
    [InlineData(FsgSelectionStyle.Freeform, false, false)]
    [InlineData(FsgSelectionStyle.AdjustAfter, false, false)]
    public void ShouldKeepTopToolbarVisible_MatchesSelectionState(
        FsgSelectionStyle selectionStyle,
        bool isAwaitingAdjustAfterCommit,
        bool expected)
    {
        bool shouldKeepVisible = FullscreenGrab.ShouldKeepTopToolbarVisible(
            selectionStyle,
            isAwaitingAdjustAfterCommit);

        Assert.Equal(expected, shouldKeepVisible);
    }

    [Theory]
    [InlineData(FsgSelectionStyle.Region, true)]
    [InlineData(FsgSelectionStyle.Window, false)]
    [InlineData(FsgSelectionStyle.Freeform, false)]
    [InlineData(FsgSelectionStyle.AdjustAfter, true)]
    public void ShouldUseOverlayCutout_MatchesSelectionStyle(FsgSelectionStyle selectionStyle, bool expected)
    {
        bool shouldUseCutout = FullscreenGrab.ShouldUseOverlayCutout(selectionStyle);

        Assert.Equal(expected, shouldUseCutout);
    }

    [Theory]
    [InlineData(FsgSelectionStyle.Region, true)]
    [InlineData(FsgSelectionStyle.Window, false)]
    [InlineData(FsgSelectionStyle.Freeform, false)]
    [InlineData(FsgSelectionStyle.AdjustAfter, true)]
    public void ShouldDrawSelectionOutline_MatchesSelectionStyle(FsgSelectionStyle selectionStyle, bool expected)
    {
        bool shouldDrawOutline = FullscreenGrab.ShouldDrawSelectionOutline(selectionStyle);

        Assert.Equal(expected, shouldDrawOutline);
    }

    [Fact]
    public void ShouldCommitWindowSelection_RequiresSameWindowHandleOnMouseUp()
    {
        WindowSelectionCandidate pressedCandidate = new((nint)1, new Rect(0, 0, 40, 40), "Target", 100);
        WindowSelectionCandidate releasedSameCandidate = new((nint)1, new Rect(0, 0, 40, 40), "Target", 100);
        WindowSelectionCandidate releasedDifferentCandidate = new((nint)2, new Rect(0, 0, 40, 40), "Other", 200);

        Assert.True(FullscreenGrab.ShouldCommitWindowSelection(pressedCandidate, releasedSameCandidate));
        Assert.False(FullscreenGrab.ShouldCommitWindowSelection(pressedCandidate, releasedDifferentCandidate));
        Assert.False(FullscreenGrab.ShouldCommitWindowSelection(pressedCandidate, null));
        Assert.False(FullscreenGrab.ShouldCommitWindowSelection(null, releasedSameCandidate));
    }

    [Fact]
    public void WindowSelectionCandidate_DisplayText_UsesFallbacksWhenMetadataMissing()
    {
        WindowSelectionCandidate candidate = new((nint)1, new Rect(0, 0, 40, 40), string.Empty, 100);

        Assert.Equal("Application", candidate.DisplayAppName);
        Assert.Equal("Untitled window", candidate.DisplayTitle);
    }
}
