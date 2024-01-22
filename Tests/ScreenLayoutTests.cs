using System.Windows;
using Text_Grab;

namespace Tests;
public class ScreenLayoutTests
{
    /*
    DISPLAY1	X=0,Y=0,Width=3440,Height=1400 
    DISPLAY2	X=3440,Y=-1163,Width=2400,Height=3760 
    DISPLAY3	X=-1920,Y=387,Width=1920,Height=1030 
    */
    private static Rect display1 = new(0, 0, 3440, 1400);
    private static Rect display2 = new(3440, -1163, 2400, 3760);
    private static Rect display3 = new(-1920, 387, 1920, 1030);

    [Fact]
    public void ShouldFindCenterOfEachRect()
    {
        Point center1 = display1.CenterPoint();
        Point center2 = display2.CenterPoint();
        Point center3 = display3.CenterPoint();

        Assert.True(display1.Contains(center1));
        Assert.True(display2.Contains(center2));
        Assert.True(display3.Contains(center3));

        Assert.False(display1.Contains(center3));
        Assert.False(display1.Contains(center2));

        Assert.False(display2.Contains(center1));
        Assert.False(display2.Contains(center3));

        Assert.False(display3.Contains(center1));
        Assert.False(display3.Contains(center2));
    }

    [Fact]
    public void SmallRectanglesContained()
    {
        /*
        FullscreenGrab fullScreenGrab = allFullscreenGrab[count];
        fullScreenGrab.WindowStartupLocation = WindowStartupLocation.Manual;
        fullScreenGrab.Width = 40;
        fullScreenGrab.Height = 40;
        fullScreenGrab.DestinationTextBox = destinationTextBox;
        fullScreenGrab.WindowState = WindowState.Normal;

        Point screenCenterPoint = screen.GetCenterPoint();
        Point windowCenterPoint = fullScreenGrab.GetWindowCenter();

        fullScreenGrab.Left = screenCenterPoint.X - windowCenterPoint.X;
        fullScreenGrab.Top = screenCenterPoint.Y - windowCenterPoint.Y;
        */

        int sideLength = 40;

        double smallLeft1 = display1.CenterPoint().X - (sideLength / 2);
        double smallTop1 = display1.CenterPoint().Y - (sideLength / 2);
        Rect smallRect1 = new(smallLeft1, smallTop1, sideLength, sideLength);
        Assert.True(display1.Contains(smallRect1));
        Assert.False(display2.Contains(smallRect1));
        Assert.False(display3.Contains(smallRect1));

        double smallLeft2 = display2.CenterPoint().X - (sideLength / 2);
        double smallTop2 = display2.CenterPoint().Y - (sideLength / 2);
        Rect smallRect2 = new(smallLeft2, smallTop2, sideLength, sideLength);
        
        Assert.True(display2.Contains(smallRect2));
        Assert.False(display1.Contains(smallRect2));
        Assert.False(display3.Contains(smallRect2));

        double smallLeft3 = display3.CenterPoint().X - (sideLength / 2);
        double smallTop3 = display3.CenterPoint().Y - (sideLength / 2);
        Rect smallRect3 = new(smallLeft3, smallTop3, sideLength, sideLength);

        Assert.True(display3.Contains(smallRect3));
        Assert.False(display1.Contains(smallRect3));
        Assert.False(display2.Contains(smallRect3));
    }
}
