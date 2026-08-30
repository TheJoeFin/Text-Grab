using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Text_Grab;
using Text_Grab.Models;
using Text_Grab.Utilities;
using UnitsNet;
using Windows.Storage.Streams;
using static System.Net.Mime.MediaTypeNames;

namespace Tests;

public class BarcodeUtilitiesTests
{
    [Fact]
    public void TryToReadBarcodes_WithDisposedBitmap_ReturnsEmptyList()
    {
        Bitmap disposedBitmap = new(8, 8);
        disposedBitmap.Dispose();

        List<OcrOutput> results = BarcodeUtilities.TryToReadBarcodes(disposedBitmap);

        Assert.Empty(results);
    }

    [Fact]
    public void TryToReadBarcodes_WithTwoQrCodes_ReturnsTwoResults()
    {
        // Build a side-by-side bitmap containing two different QR codes
        using Bitmap qr1 = BarcodeUtilities.GetQrCodeForText("https://example.com", ZXing.QrCode.Internal.ErrorCorrectionLevel.M);
        using Bitmap qr2 = BarcodeUtilities.GetQrCodeForText("https://example.org", ZXing.QrCode.Internal.ErrorCorrectionLevel.M);

        using Bitmap combined = new(qr1.Width + qr2.Width, Math.Max(qr1.Height, qr2.Height));
        using (Graphics g = Graphics.FromImage(combined))
        {
            g.Clear(Color.White);
            g.DrawImage(qr1, 0, 0);
            g.DrawImage(qr2, qr1.Width, 0);
        }

        List<OcrOutput> results = BarcodeUtilities.TryToReadBarcodes(combined);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(OcrOutputKind.Barcode, r.Kind));
        Assert.Contains(results, r => r.RawOutput == "https://example.com");
        Assert.Contains(results, r => r.RawOutput == "https://example.org");
    }

    [WpfFact]
    public void ReadTestSingleQRCode()
    {
        string expectedOutput = "This is a test of the QR Code system";
        string testFilePath = FileUtilities.GetPathToLocalFile(@".\Images\QrCodeTestImage.png");

        Bitmap testBmp = new(testFilePath);

        List<OcrOutput> result = BarcodeUtilities.TryToReadBarcodes(testBmp);

        Assert.Single(result);
        Assert.Equal(expectedOutput, result[0].RawOutput);
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

        Bitmap clonedBitmap = BitmapUtilities.GetBitmapFromIRandomAccessStream(randomAccessStream);

        Assert.Equal(8, clonedBitmap.Width);
        Assert.Equal(8, clonedBitmap.Height);
        Assert.Equal(Color.Red.ToArgb(), clonedBitmap.GetPixel(0, 0).ToArgb());
    }
}
