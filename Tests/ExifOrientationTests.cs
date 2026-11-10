using System.Drawing;
using System.Drawing.Imaging;
using Text_Grab;
using Text_Grab.Utilities;

namespace Tests;

/// <summary>
/// Pins how file OCR orients an image before it reaches an engine (v5 F3, step 1).
///
/// Every fixture under Images\Exif stores the same 64x32 pixels - red top-left, green top-right,
/// blue bottom-left, yellow bottom-right - and differs only in its EXIF orientation tag. The
/// quadrants sit on 16px boundaries so JPEG chroma subsampling cannot bleed into the sample points.
///
/// Orientations 2 and 5 pin a quirk rather than an ideal: the loader applies only the three pure
/// rotations (EXIF 3, 6, 8). Mirrored orientations are not flipped, and EXIF 5 and 7, which are
/// a rotation plus a flip, are not rotated either.
/// </summary>
public class ExifOrientationTests
{
    [WpfTheory]
    [InlineData(1, 64, 32, 'R', 'G', 'B', 'Y')]
    [InlineData(3, 64, 32, 'Y', 'B', 'G', 'R')]
    [InlineData(6, 32, 64, 'B', 'R', 'Y', 'G')]
    [InlineData(8, 32, 64, 'G', 'Y', 'R', 'B')]
    [InlineData(2, 64, 32, 'R', 'G', 'B', 'Y')]
    [InlineData(5, 64, 32, 'R', 'G', 'B', 'Y')]
    public void LoadBitmapFromFile_AppliesExifRotation(
        int orientation,
        int expectedWidth,
        int expectedHeight,
        char topLeft,
        char topRight,
        char bottomLeft,
        char bottomRight)
    {
        string path = FileUtilities.GetPathToLocalFile($@".\Images\Exif\exif-orientation-{orientation}.jpg");

        using Bitmap bitmap = OcrSourceUtilities.LoadBitmapFromFile(path);

        Assert.Equal(expectedWidth, bitmap.Width);
        Assert.Equal(expectedHeight, bitmap.Height);

        int left = bitmap.Width / 4;
        int right = bitmap.Width * 3 / 4;
        int top = bitmap.Height / 4;
        int bottom = bitmap.Height * 3 / 4;

        Assert.Equal(
            new[] { topLeft, topRight, bottomLeft, bottomRight },
            new[]
            {
                Classify(bitmap.GetPixel(left, top)),
                Classify(bitmap.GetPixel(right, top)),
                Classify(bitmap.GetPixel(left, bottom)),
                Classify(bitmap.GetPixel(right, bottom)),
            });
    }

    /// <summary>
    /// Downstream scaling copies pixels as 32 bits per pixel, so a 24bpp bitmap (what GDI+ decodes
    /// a JPEG to) would come out garbled. The loader must hand back 32bpp ARGB.
    /// </summary>
    [WpfTheory]
    [InlineData(1)]
    [InlineData(6)]
    public void LoadBitmapFromFile_ReturnsArgb32(int orientation)
    {
        string path = FileUtilities.GetPathToLocalFile($@".\Images\Exif\exif-orientation-{orientation}.jpg");

        using Bitmap bitmap = OcrSourceUtilities.LoadBitmapFromFile(path);

        Assert.Equal(PixelFormat.Format32bppArgb, bitmap.PixelFormat);
    }

    private static char Classify(Color pixel)
    {
        (char Name, Color Reference)[] references =
        [
            ('R', Color.FromArgb(255, 0, 0)),
            ('G', Color.FromArgb(0, 255, 0)),
            ('B', Color.FromArgb(0, 0, 255)),
            ('Y', Color.FromArgb(255, 255, 0)),
        ];

        (char name, int distance) = references
            .Select(r => (r.Name, Math.Abs(r.Reference.R - pixel.R) + Math.Abs(r.Reference.G - pixel.G) + Math.Abs(r.Reference.B - pixel.B)))
            .MinBy(r => r.Item2);

        // A garbled or empty pixel matches none of the quadrant colours.
        return distance <= 90 ? name : '?';
    }
}
