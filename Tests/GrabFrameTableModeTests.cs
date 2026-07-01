using Text_Grab.Models;
using Text_Grab.Views;

namespace Tests;

public class GrabFrameTableModeTests
{
    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    [InlineData(0, false)]
    public void ShouldAllowWordBorderMerging_RequiresMultipleSelectedWordBorders(
        int selectedWordBorderCount,
        bool expected)
    {
        bool actual = GrabFrame.ShouldAllowWordBorderMerging(selectedWordBorderCount);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(false, true, false, true, false)]
    public void ShouldRefreshOcrBordersForTableModeActivation_OnlyRefreshesForParagraphGroupedOcrBorders(
        bool isTableModeSelected,
        bool paragraphDetectionEnabled,
        bool hasNativePdfText,
        bool hasMergedParagraphBorders,
        bool expected)
    {
        bool actual = GrabFrame.ShouldRefreshOcrBordersForTableModeActivation(
            isTableModeSelected,
            new GlobalLang("en-US"),
            paragraphDetectionEnabled,
            hasNativePdfText,
            hasMergedParagraphBorders);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ShouldRefreshOcrBordersForTableModeActivation_ReturnsFalseForUiAutomation()
    {
        bool actual = GrabFrame.ShouldRefreshOcrBordersForTableModeActivation(
            isTableModeSelected: true,
            language: new UiAutomationLang(),
            paragraphDetectionEnabled: true,
            hasNativePdfText: false,
            hasMergedParagraphBorders: true);

        Assert.False(actual);
    }
}
