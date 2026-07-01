using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Tests;

public class GrabFrameEtwTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void ShouldOpenNewEtwInSpreadsheetMode_OnlyReturnsTrueForNewTableEtw(
        bool isTableModeSelected,
        bool hasExistingEditTextWindow,
        bool expected)
    {
        bool shouldUseSpreadsheetMode = WindowUtilities.ShouldOpenNewEtwInSpreadsheetMode(
            isTableModeSelected,
            hasExistingEditTextWindow);

        Assert.Equal(expected, shouldUseSpreadsheetMode);
    }

    [Theory]
    [InlineData(true, true, true, true, false, false, false, true)]
    [InlineData(true, true, true, true, false, true, false, true)]
    [InlineData(true, true, true, true, false, true, true, false)]
    [InlineData(true, true, true, true, true, false, false, false)]
    [InlineData(true, true, false, true, false, false, false, false)]
    public void ShouldUpdateLinkedDestinationText_PreservesSpreadsheetSelectionWhenClosing(
        bool isFromEditWindow,
        bool hasDestinationTextBox,
        bool shouldAlwaysUpdateEtw,
        bool isEditTextToggleEnabled,
        bool hasActiveGrabTemplate,
        bool preserveLinkedSpreadsheetSelection,
        bool isDestinationSpreadsheetMode,
        bool expected)
    {
        bool shouldUpdate = GrabFrame.ShouldUpdateLinkedDestinationText(
            isFromEditWindow,
            hasDestinationTextBox,
            shouldAlwaysUpdateEtw,
            isEditTextToggleEnabled,
            hasActiveGrabTemplate,
            preserveLinkedSpreadsheetSelection,
            isDestinationSpreadsheetMode);

        Assert.Equal(expected, shouldUpdate);
    }
}
