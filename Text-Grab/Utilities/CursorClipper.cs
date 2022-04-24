using System;
using System.Windows;

namespace Text_Grab.Utilities;

/// <summary>
/// Functions to constrain the mouse cursor (typically used when dragging)
/// </summary>
public static class CursorClipper
{
    /// <summary>
    /// Constrain mouse cursor to the area of the specified UI element.
    /// </summary>
    /// <param name="element">Target UI element.</param>
    /// <returns>True on success.</returns>
    public static bool ClipCursor(FrameworkElement element)
    {
        const double dpi96 = 96.0;

        var topLeft = element.PointToScreen(new Point(0, 0));

        PresentationSource source = PresentationSource.FromVisual(element);
        if (source?.CompositionTarget == null)
        {
            return false;
        }

        double dpiX = dpi96 * source.CompositionTarget.TransformToDevice.M11;
        double dpiY = dpi96 * source.CompositionTarget.TransformToDevice.M22;

        var width = (int)((element.ActualWidth + 1) * dpiX / dpi96);
        var height = (int)((element.ActualHeight + 1) * dpiY / dpi96);

        OSInterop.RECT rect = new OSInterop.RECT
        {
            left = (int)topLeft.X,
            top = (int)topLeft.Y,
            right = (int)topLeft.X + width,
            bottom = (int)topLeft.Y + height
        };

        return OSInterop.ClipCursor(ref rect);
    }

    /// <summary>
    /// Remove any mouse cursor constraint.
    /// </summary>
    /// <returns>True on success.</returns>
    public static bool UnClipCursor()
    {
        return OSInterop.ClipCursor(IntPtr.Zero);
    }
}
