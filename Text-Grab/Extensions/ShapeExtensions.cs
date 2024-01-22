using System;
using System.Drawing;
using System.Windows;

namespace Text_Grab;

public static class ShapeExtensions
{
    public static Rect AsRect(this Rectangle rectangle)
    {
        return new Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    public static Rectangle AsRectangle(this Rect rect)
    {
        return new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
    }

    public static Rect GetScaledDownByDpi(this Rect rect, DpiScale dpi)
    {
        return new Rect(rect.X / dpi.DpiScaleX,
                    rect.Y / dpi.DpiScaleY,
                    rect.Width / dpi.DpiScaleX,
                    rect.Height / dpi.DpiScaleY);
    }

    public static Rect GetScaledUpByDpi(this Rect rect, DpiScale dpi)
    {
        return new Rect(rect.X * dpi.DpiScaleX,
                    rect.Y * dpi.DpiScaleY,
                    rect.Width * dpi.DpiScaleX,
                    rect.Height * dpi.DpiScaleY);
    }

    public static Rect GetScaledUpByFraction(this Rect rect, Double scaleFactor)
    {
        return new Rect(rect.X * scaleFactor,
                    rect.Y * scaleFactor,
                    rect.Width * scaleFactor,
                    rect.Height * scaleFactor);
    }

    public static Rect GetScaleSizeByFraction(this Rect rect, Double scaleFactor)
    {
        return new Rect(rect.X,
                    rect.Y,
                    rect.Width * scaleFactor,
                    rect.Height * scaleFactor);
    }

    public static bool IsGood(this Rect rect)
    {
        if (double.IsNaN(rect.X) 
            || double.IsNegativeInfinity(rect.X)
            || double.IsPositiveInfinity(rect.X))
            return false;
        
        if (double.IsNaN(rect.Y) 
            || double.IsNegativeInfinity(rect.Y)
            || double.IsPositiveInfinity(rect.Y))
            return false;

        if (double.IsNaN(rect.Height)
            || rect.Height == 0
            || double.IsNegativeInfinity(rect.Height)
            || double.IsPositiveInfinity(rect.Height))
            return false;

        if (double.IsNaN(rect.Width)
            || rect.Width == 0
            || double.IsNegativeInfinity(rect.Width)
            || double.IsPositiveInfinity(rect.Width))
            return false;

        return true;
    }

    public static System.Windows.Point CenterPoint(this Rect rect)
    {
        double x = rect.Left + (rect.Width / 2);
        double y = rect.Top + (rect.Height / 2);
        return new(x, y);
    }
}