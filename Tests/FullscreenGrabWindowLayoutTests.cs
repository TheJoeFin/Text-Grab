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

    [Theory]
    [InlineData(WindowState.Normal, 1920, 1080)]   // not maximized -> force
    [InlineData(WindowState.Minimized, 1920, 1080)] // not maximized -> force
    [InlineData(WindowState.Maximized, 40, 40)]     // tiny despite maximized -> force
    [InlineData(WindowState.Maximized, 1920, 100)]  // too short -> force
    [InlineData(WindowState.Maximized, 100, 1080)]  // too narrow -> force
    public void ShouldForceMaximize_ReturnsTrue_WhenOverlayIsNotFullScreen(WindowState state, double width, double height)
    {
        Assert.True(FullscreenGrab.ShouldForceMaximize(state, width, height));
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1366, 768)]
    [InlineData(200, 200)]
    public void ShouldForceMaximize_ReturnsFalse_WhenMaximizedAndLargeEnough(double width, double height)
    {
        Assert.False(FullscreenGrab.ShouldForceMaximize(WindowState.Maximized, width, height));
    }
}
