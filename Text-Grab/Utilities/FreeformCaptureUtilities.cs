using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Text_Grab.Utilities;

public static class FreeformCaptureUtilities
{
    public static Rect GetBounds(IReadOnlyList<Point> points)
    {
        if (points is null || points.Count == 0)
            return Rect.Empty;

        double left = points.Min(static point => point.X);
        double top = points.Min(static point => point.Y);
        double right = points.Max(static point => point.X);
        double bottom = points.Max(static point => point.Y);

        return new Rect(
            new Point(Math.Floor(left), Math.Floor(top)),
            new Point(Math.Ceiling(right), Math.Ceiling(bottom)));
    }

    public static PathGeometry BuildGeometry(IReadOnlyList<Point> points)
    {
        PathGeometry geometry = new();
        if (points is null || points.Count < 2)
            return geometry;

        PathFigure figure = new()
        {
            StartPoint = points[0],
            IsClosed = true,
            IsFilled = true
        };

        foreach (Point point in points.Skip(1))
            figure.Segments.Add(new LineSegment(point, true));

        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    public static Bitmap CreateMaskedBitmap(Bitmap sourceBitmap, IReadOnlyList<Point> pointsRelativeToBounds)
    {
        ArgumentNullException.ThrowIfNull(sourceBitmap);

        if (pointsRelativeToBounds is null || pointsRelativeToBounds.Count < 3)
            return new Bitmap(sourceBitmap);

        Bitmap maskedBitmap = new(sourceBitmap.Width, sourceBitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(maskedBitmap);
        using GraphicsPath graphicsPath = new();

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(System.Drawing.Color.Gray);

        graphicsPath.AddPolygon([.. pointsRelativeToBounds.Select(static point => new PointF((float)point.X, (float)point.Y))]);
        graphics.SetClip(graphicsPath);
        graphics.DrawImage(sourceBitmap, new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height));

        return maskedBitmap;
    }
}
