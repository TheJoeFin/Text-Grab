using Text_Grab.Views;

namespace Tests;

public class GrabFrameUnfreezeTests
{
    [Theory]
    [InlineData(3, 3, false, false, true)]
    [InlineData(3, 4, false, false, false)]
    [InlineData(3, 3, true, false, false)]
    [InlineData(3, 3, false, true, false)]
    public void ShouldApplyUnfreezeResult_RequiresCurrentLiveTransition(
        int transitionVersion,
        int currentTransitionVersion,
        bool isFreezeMode,
        bool isCleanedUp,
        bool expected)
    {
        Assert.Equal(
            expected,
            GrabFrame.ShouldApplyUnfreezeResult(
                transitionVersion,
                currentTransitionVersion,
                isFreezeMode,
                isCleanedUp));
    }
}
