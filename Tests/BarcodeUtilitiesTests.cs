using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Windows.Storage.Streams;

namespace Tests;

public class BarcodeUtilitiesTests
{
    [Fact]
    public void TryToReadBarcodes_WithDisposedBitmap_ReturnsEmptyBarcodeOutput()
    {
        Bitmap disposedBitmap = new(8, 8);
        disposedBitmap.Dispose();

        OcrOutput result = BarcodeUtilities.TryToReadBarcodes(disposedBitmap);

        Assert.Equal(OcrOutputKind.Barcode, result.Kind);
        Assert.Equal(string.Empty, result.RawOutput);
    }

    [Fact]
    public async Task GetBitmapFromIRandomAccessStream_ReturnsBitmapIndependentOfSourceStream()
    {
        using Bitmap sourceBitmap = new(8, 8);
        sourceBitmap.SetPixel(0, 0, Color.Red);

        using MemoryStream memoryStream = new();
        sourceBitmap.Save(memoryStream, ImageFormat.Png);

        using InMemoryRandomAccessStream randomAccessStream = new();
        _ = await randomAccessStream.WriteAsync(memoryStream.ToArray().AsBuffer());

        Bitmap clonedBitmap = ImageMethods.GetBitmapFromIRandomAccessStream(randomAccessStream);

        Assert.Equal(8, clonedBitmap.Width);
        Assert.Equal(8, clonedBitmap.Height);
        Assert.Equal(Color.Red.ToArgb(), clonedBitmap.GetPixel(0, 0).ToArgb());
    }
}
