using Text_Grab.Models;

namespace Tests;

public class GrabFrameTableEditStateTests
{
    [Fact]
    public void TryCommitPreview_AddsAndSortsManualSeparators()
    {
        GrabFrameTableEditState state = new();
        state.SetManualSeparators([40], [70]);

        state.BeginPlacement(GrabFrameTablePlacementMode.AddRow);

        Assert.True(state.TryUpdatePreview(20, 0, 100, state.ManualRowSeparators));
        Assert.True(state.TryCommitPreview());
        Assert.Equal([20d, 40d], state.ManualRowSeparators);
    }

    [Fact]
    public void TryUpdatePreview_RejectsSeparatorTooCloseToExistingDivider()
    {
        GrabFrameTableEditState state = new();
        state.BeginPlacement(GrabFrameTablePlacementMode.AddColumn);

        Assert.False(state.TryUpdatePreview(22, 0, 100, [20d]));
        Assert.Equal(22d, state.PreviewPosition);
        Assert.False(state.IsPreviewValid);
        Assert.False(state.TryCommitPreview());
    }
}
