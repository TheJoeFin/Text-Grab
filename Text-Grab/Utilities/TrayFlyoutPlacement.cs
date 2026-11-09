using System;
using System.Drawing;

namespace Text_Grab.Utilities;

/// <summary>
/// Pure placement math for the tray flyout. Everything is in physical pixels.
/// </summary>
public static class TrayFlyoutPlacement
{
    /// <summary>
    /// Returns the top-left of a flyout of <paramref name="size"/> for a click at <paramref name="cursor"/>.
    /// When <paramref name="snapToCorner"/> is set (tray icon / overflow clicks) the flyout sits in the
    /// work-area corner nearest the cursor, so it is always adjacent to the taskbar and never off screen.
    /// Otherwise (hotkey) it centers on the cursor. The result is always clamped into the work area.
    /// </summary>
    public static Point Compute(Point cursor, Rectangle workArea, Size size, int gap, bool snapToCorner = true)
    {
        int minX = workArea.Left + gap;
        int minY = workArea.Top + gap;
        int maxX = Math.Max(minX, workArea.Right - size.Width - gap);
        int maxY = Math.Max(minY, workArea.Bottom - size.Height - gap);

        int x;
        int y;

        if (snapToCorner)
        {
            int centerX = workArea.Left + (workArea.Width / 2);
            int centerY = workArea.Top + (workArea.Height / 2);
            x = cursor.X >= centerX ? maxX : minX;
            y = cursor.Y >= centerY ? maxY : minY;
        }
        else
        {
            x = cursor.X - (size.Width / 2);
            y = cursor.Y - (size.Height / 2);
        }

        return new Point(Math.Clamp(x, minX, maxX), Math.Clamp(y, minY, maxY));
    }
}
