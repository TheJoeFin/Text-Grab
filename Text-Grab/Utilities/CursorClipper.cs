using System;
using System.Windows;
using System.Windows.Controls;

namespace Text_Grab.Utilities
{
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
            var topLeft = element.PointToScreen(new Point(0, 0));

            OSInterop.RECT rect = new OSInterop.RECT
            {
                left = (int)topLeft.X,
                top = (int)topLeft.Y,
                right = (int)topLeft.X + (int)element.ActualWidth + 1,
                bottom = (int)topLeft.Y + (int)element.ActualHeight + 1
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
}
