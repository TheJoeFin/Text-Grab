using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab;

namespace Tests;

public class ImageMethodsTests
{
    [WpfFact]
    public void ImageSourceToBitmap_ConvertsBitmapSourceDerivedImages()
    {
        byte[] pixels =
        [
            0, 0, 255, 255,
            0, 255, 0, 255,
            255, 0, 0, 255,
            255, 255, 255, 255
        ];

        BitmapSource source = BitmapSource.Create(
            2,
            2,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            8);
        CroppedBitmap cropped = new(source, new Int32Rect(1, 0, 1, 2));

        using Bitmap? bitmap = ImageMethods.ImageSourceToBitmap(cropped);

        Assert.NotNull(bitmap);
        Assert.Equal(1, bitmap!.Width);
        Assert.Equal(2, bitmap.Height);
    }

    [WpfFact]
    public void ImageSourceToBitmap_ReturnsNullForNonBitmapImageSources()
    {
        DrawingImage drawingImage = new();

        Bitmap? bitmap = ImageMethods.ImageSourceToBitmap(drawingImage);

        Assert.Null(bitmap);
    }
}
