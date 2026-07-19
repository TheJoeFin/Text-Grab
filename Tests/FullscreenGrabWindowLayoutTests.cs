using System.Windows;
using Text_Grab.Views;

namespace Tests;

public class FullscreenGrabWindowLayoutTests
{
    [Theory]
    [InlineData(40, 40)]
    [InlineData(1920, 1080)]
    public void GetFullscreenClipBounds_UsesRenderedWindowSize(double width, double height)
    {
        Rect expected = new(0, 0, width, height);

        Rect actual = FullscreenGrab.GetFullscreenClipBounds(new Size(width, height));

        Assert.Equal(expected, actual);
    }
}
