using System.Drawing;
using System.Windows;
using System.Windows.Media;
using Text_Grab.Utilities;
using Point = System.Windows.Point;

namespace Tests;

public class FreeformCaptureUtilitiesTests
{
    [WpfFact]
    public void GetBounds_RoundsOutwardToIncludeAllPoints()
    {
        List<Point> points =
        [
            new(1.2, 2.8),
            new(10.1, 4.2),
            new(4.6, 9.9)
        ];

        Rect bounds = FreeformCaptureUtilities.GetBounds(points);

        Assert.Equal(new Rect(new Point(1, 2), new Point(11, 10)), bounds);
    }

    [WpfFact]
    public void BuildGeometry_CreatesClosedFigure()
    {
        List<Point> points =
        [
            new(0, 0),
            new(4, 0),
            new(4, 4)
        ];

        PathGeometry geometry = FreeformCaptureUtilities.BuildGeometry(points);

        Assert.Single(geometry.Figures);
        Assert.Equal(points[0], geometry.Figures[0].StartPoint);
        Assert.True(geometry.Figures[0].IsClosed);
        Assert.Equal(2, geometry.Figures[0].Segments.Count);
    }

    [WpfFact]
    public void CreateMaskedBitmap_WhitensPixelsOutsideThePolygon()
    {
        using Bitmap sourceBitmap = new(10, 10);
        using Graphics graphics = Graphics.FromImage(sourceBitmap);
        graphics.Clear(System.Drawing.Color.Black);

        using Bitmap maskedBitmap = FreeformCaptureUtilities.CreateMaskedBitmap(
            sourceBitmap,
            [
                new Point(2, 2),
                new Point(7, 2),
                new Point(7, 7),
                new Point(2, 7)
            ]);

        Assert.Equal(System.Drawing.Color.Gray.ToArgb(), maskedBitmap.GetPixel(0, 0).ToArgb());
        Assert.Equal(System.Drawing.Color.Black.ToArgb(), maskedBitmap.GetPixel(4, 4).ToArgb());
    }
}
