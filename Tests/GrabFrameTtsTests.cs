using Text_Grab.Views;

namespace Tests;

public class GrabFrameTtsTests
{
    [Theory]
    [InlineData(false, true, "Current frame text", true)]
    [InlineData(true, true, "Current frame text", false)]
    [InlineData(true, false, "Current frame text", false)]
    [InlineData(false, true, "", false)]
    [InlineData(false, true, "   ", false)]
    public void ShouldSpeakCurrentFrameWhenEnabled_RequiresUncheckedToCheckedTransition(
        bool wasSpeakEnabled,
        bool isSpeakEnabled,
        string frameText,
        bool expected)
    {
        Assert.Equal(
            expected,
            GrabFrame.ShouldSpeakCurrentFrameWhenEnabled(
                wasSpeakEnabled,
                isSpeakEnabled,
                frameText));
    }
}
