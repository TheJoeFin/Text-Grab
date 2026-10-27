using Dapplo.Windows.Common.Structs;
using Dapplo.Windows.User32;
using System.Windows;

namespace Text_Grab.Extensions;
public static class DapploExtensions
{
    public static Point ScaledCenterPoint(this DisplayInfo displayInfo)
    {
        NativeRect displayRect = displayInfo.Bounds;
        NativeMethods.GetScaleFactorForMonitor(displayInfo.MonitorHandle, out uint scaleFactor);
        double scaleFraction = scaleFactor / 100.0;
        Point rawCenter = displayRect.CenterPoint();
        Point displayScaledCenterPoint = new(rawCenter.X / scaleFraction, rawCenter.Y / scaleFraction);
        return displayScaledCenterPoint;
    }

    public static Rect ScaledBounds(this DisplayInfo displayInfo)
    {
        NativeRect displayRect = displayInfo.Bounds;
        NativeMethods.GetScaleFactorForMonitor(displayInfo.MonitorHandle, out uint scaleFactor);
        double scaleFraction = scaleFactor / 100.0;

        // Scale size and position
        Rect scaledBounds = new(
            displayRect.X / scaleFraction,
            displayRect.Y / scaleFraction,
            displayRect.Width / scaleFraction,
            displayRect.Height / scaleFraction);
        return scaledBounds;
    }

    public static Point CenterPoint(this NativeRect rect)
    {
        return new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0);
    }

    public static Rect ToRect(this NativeRect rect)
    {
        return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
