using System.Windows;
using Text_Grab;
using Text_Grab.Models;

namespace Tests;

public class FullscreenCaptureResultTests
{
    [Theory]
    [InlineData(FsgSelectionStyle.Region, true)]
    [InlineData(FsgSelectionStyle.Window, true)]
    [InlineData(FsgSelectionStyle.Freeform, false)]
    [InlineData(FsgSelectionStyle.AdjustAfter, true)]
    public void SupportsTemplateActions_MatchesSelectionStyle(FsgSelectionStyle selectionStyle, bool expected)
    {
        FullscreenCaptureResult result = new(selectionStyle, Rect.Empty);

        Assert.Equal(expected, result.SupportsTemplateActions);
    }

    [Theory]
    [InlineData(FsgSelectionStyle.Region, true)]
    [InlineData(FsgSelectionStyle.Window, false)]
    [InlineData(FsgSelectionStyle.Freeform, false)]
    [InlineData(FsgSelectionStyle.AdjustAfter, true)]
    public void SupportsPreviousRegionReplay_MatchesSelectionStyle(FsgSelectionStyle selectionStyle, bool expected)
    {
        FullscreenCaptureResult result = new(selectionStyle, Rect.Empty);

        Assert.Equal(expected, result.SupportsPreviousRegionReplay);
    }
}
