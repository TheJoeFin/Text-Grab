using System;
using System.Windows;
using System.Windows.Controls;

namespace Text_Grab.Utilities
{
    /// <summary>
    /// Helper for drawing a selection rectangle during mouse drag operations.
    /// </summary>
    public static class SelectionRectangleHelper
    {
        /// <summary>
        /// Resizes the specified border according to the supplied points.
        /// </summary>
        /// <param name="border">The target border.</param>
        /// <param name="anchorPoint">The drag operation starting point.</param>
        /// <param name="movablePoint">The current mouse position.</param>
        public static void DrawSelectionRectangle(Border border, Point anchorPoint, Point movablePoint)
        {
            var left = Math.Min(anchorPoint.X, movablePoint.X);
            var top = Math.Min(anchorPoint.Y, movablePoint.Y);

            border.Height = Math.Max(anchorPoint.Y, movablePoint.Y) - top;
            border.Width = Math.Max(anchorPoint.X, movablePoint.X) - left;

            Canvas.SetLeft(border, left);
            Canvas.SetTop(border, top);
        }
    }
}
