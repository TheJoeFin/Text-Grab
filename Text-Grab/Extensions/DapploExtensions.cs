using Dapplo.Windows.User32;
using System.Windows;

namespace Text_Grab.Extensions;
public static class DapploExtensions
{
    public static Point ScaledCenterPoint(this DisplayInfo displayInfo)
    {
        Rect displayRect = displayInfo.Bounds;
        NativeMethods.GetScaleFactorForMonitor(displayInfo.MonitorHandle, out uint scaleFactor);
        double scaleFraction = scaleFactor / 100.0;
        Point rawCenter = displayRect.CenterPoint();
        Point displayScaledCenterPoint = new(rawCenter.X / scaleFraction, rawCenter.Y / scaleFraction);
        return displayScaledCenterPoint;
    }

    public static Rect ScaledBounds(this DisplayInfo displayInfo)
    {
        Rect displayRect = displayInfo.Bounds;
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
}
