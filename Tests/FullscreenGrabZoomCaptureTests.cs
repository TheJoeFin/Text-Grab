using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Views;

namespace Tests;

public class FullscreenGrabZoomCaptureTests
{
    [Fact]
    public void TryGetBitmapCropRectForSelection_UsesSelectionRectWithoutZoom()
    {
        bool didCreateCrop = FullscreenGrab.TryGetBitmapCropRectForSelection(
            new Rect(10, 20, 30, 40),
            Matrix.Identity,
            null,
            200,
            200,
            out Int32Rect cropRect);

        Assert.True(didCreateCrop);
        Assert.Equal(10, cropRect.X);
        Assert.Equal(20, cropRect.Y);
        Assert.Equal(30, cropRect.Width);
        Assert.Equal(40, cropRect.Height);
    }

    [Fact]
    public void TryGetBitmapCropRectForSelection_MapsZoomedSelectionBackToFrozenBitmap()
    {
        TransformGroup zoomTransform = new();
        zoomTransform.Children.Add(new ScaleTransform(2, 2, 50, 50));
        zoomTransform.Children.Add(new TranslateTransform(-10, 15));

        Rect sourceRect = new(40, 50, 20, 10);
        Rect displayedSelectionRect = TransformRect(sourceRect, zoomTransform.Value);

        bool didCreateCrop = FullscreenGrab.TryGetBitmapCropRectForSelection(
            displayedSelectionRect,
            Matrix.Identity,
            zoomTransform,
            200,
            200,
            out Int32Rect cropRect);

        Assert.True(didCreateCrop);
        Assert.Equal(40, cropRect.X);
        Assert.Equal(50, cropRect.Y);
        Assert.Equal(20, cropRect.Width);
        Assert.Equal(10, cropRect.Height);
    }

    [Fact]
    public void TryGetBitmapCropRectForSelection_AppliesDeviceScalingAfterUndoingZoom()
    {
        ScaleTransform zoomTransform = new(2, 2);

        bool didCreateCrop = FullscreenGrab.TryGetBitmapCropRectForSelection(
            new Rect(20, 30, 40, 50),
            new Matrix(1.5, 0, 0, 1.5, 0, 0),
            zoomTransform,
            200,
            200,
            out Int32Rect cropRect);

        Assert.True(didCreateCrop);
        Assert.Equal(15, cropRect.X);
        Assert.Equal(22, cropRect.Y);
        Assert.Equal(30, cropRect.Width);
        Assert.Equal(38, cropRect.Height);
    }

    private static Rect TransformRect(Rect rect, Matrix matrix)
    {
        Point[] points =
        [
            matrix.Transform(rect.TopLeft),
            matrix.Transform(new Point(rect.Right, rect.Top)),
            matrix.Transform(new Point(rect.Left, rect.Bottom)),
            matrix.Transform(rect.BottomRight)
        ];

        double left = points.Min(static point => point.X);
        double top = points.Min(static point => point.Y);
        double right = points.Max(static point => point.X);
        double bottom = points.Max(static point => point.Y);

        return new Rect(new Point(left, top), new Point(right, bottom));
    }
}
