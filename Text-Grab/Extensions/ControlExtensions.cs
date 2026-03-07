using System;
using System.Windows;
using System.Windows.Controls;

namespace Text_Grab;

public static class ControlExtensions
{
    public static double GetHorizontalScaleFactor(this Viewbox viewbox)
    {
        if (viewbox.Child is not FrameworkElement childElement)
            return 1.0;

        double outsideWidth = viewbox.ActualWidth;
        double outsideHeight = viewbox.ActualHeight;
        double insideWidth = childElement.ActualWidth;
        double insideHeight = childElement.ActualHeight;

        if (!double.IsFinite(outsideWidth) || !double.IsFinite(insideWidth)
            || outsideWidth <= 0 || insideWidth <= 4)
        {
            return 1.0;
        }

        // A Viewbox with Stretch="Uniform" applies min(width_ratio, height_ratio) so that
        // the content fits in both dimensions. Using only the width ratio produces the wrong
        // scale when the image is height-limited (taller relative to the window than it is
        // wide), which causes OCR word borders to be placed at incorrect canvas positions.
        double scale = outsideWidth / insideWidth;

        if (double.IsFinite(outsideHeight) && double.IsFinite(insideHeight)
            && outsideHeight > 0 && insideHeight > 4)
        {
            double scaleY = outsideHeight / insideHeight;
            scale = Math.Min(scale, scaleY);
        }

        if (!double.IsFinite(scale) || scale <= 0)
            return 1.0;

        return scale;
    }

    public static Rect GetAbsolutePlacement(this FrameworkElement element, bool relativeToScreen = false)
    {
        Point absolutePos = default;

        try
        {
            absolutePos = element.PointToScreen(new System.Windows.Point(0, 0));
        }
        catch (System.Exception)
        {
            return Rect.Empty;
#if DEBUG
            throw;
#endif
        }
        if (relativeToScreen)
        {
            return new Rect(absolutePos.X, absolutePos.Y, element.ActualWidth, element.ActualHeight);
        }
        Point posMW = Application.Current.MainWindow.PointToScreen(new System.Windows.Point(0, 0));
        absolutePos = new System.Windows.Point(absolutePos.X - posMW.X, absolutePos.Y - posMW.Y);
        return new Rect(absolutePos.X, absolutePos.Y, element.ActualWidth, element.ActualHeight);
    }
}
