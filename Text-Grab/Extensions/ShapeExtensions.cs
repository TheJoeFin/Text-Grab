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

    // Conversions across the Core boundary. Text-Grab.Core and Text-Grab.Core.Windows cannot use
    // System.Windows.Rect (WindowsBase, WPF-only), so they speak RectangleF/PointF/SizeF from
    // System.Drawing.Primitives instead. View code converts here, at the edge.

    public static Rect AsRect(this RectangleF rectangle)
    {
        return new Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    public static RectangleF AsRectangleF(this Rect rect)
    {
        return new RectangleF((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
    }

    public static System.Windows.Point AsPoint(this PointF point)
    {
        return new System.Windows.Point(point.X, point.Y);
    }

    public static PointF AsPointF(this System.Windows.Point point)
    {
        return new PointF((float)point.X, (float)point.Y);
    }

    public static System.Windows.Size AsSize(this SizeF size)
    {
        return new System.Windows.Size(size.Width, size.Height);
    }

    public static SizeF AsSizeF(this System.Windows.Size size)
    {
        return new SizeF((float)size.Width, (float)size.Height);
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