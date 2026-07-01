using ImageMagick;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab;
using Text_Grab.Utilities;

namespace Tests;

public class ImageMethodsTests
{
    private const string fontTestPath = @".\Images\FontTest.png";
    private const string fontSamplePath = @".\Images\font_sample.png";

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

    [WpfFact]
    public void BitmapCompare_ReturnsZeroDiff()
    {
        string path1 = FileUtilities.GetPathToLocalFile(fontTestPath);
        MagickImage img1 = new(path1);

        IMagickErrorInfo compare = img1.Compare(img1);

        Assert.NotNull(compare);
        Assert.Equal(0, compare.NormalizedMeanError);
    }

    [WpfFact]
    public void BitmapCompare_ReturnsNonZeroDiff()
    {
        string path1 = FileUtilities.GetPathToLocalFile(fontTestPath);
        string path2 = FileUtilities.GetPathToLocalFile(fontSamplePath);
        MagickImage img1 = new(path1);
        MagickImage img2 = new(path2);

        IMagickErrorInfo compare = img1.Compare(img2);

        Assert.NotNull(compare);
        Assert.NotEqual(0, compare.NormalizedMeanError);
    }
}
