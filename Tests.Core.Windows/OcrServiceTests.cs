using System.Drawing;
using Text_Grab.Core.Abstractions;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Text_Grab.Tests.Core.Windows;

/// <summary>
/// Headless OCR through <see cref="IOcrService"/> (v5 F3), in a host with no WPF and no app
/// assembly. The expected strings are the ones the app-hosted Tests\OcrSourceTests.cs asserts for
/// the same fixtures, so passing here means the headless path produces the app's text.
/// </summary>
public class OcrServiceTests
{
    private const string FontSampleResult =
        "Times-Roman\r\nHelvetica\r\nCourier\r\nPalatino-Roman\r\nHelvetica-Narrow\r\nBookman-Demi";

    private const string FontTestResult =
        "Arial\r\nTimes New Roman\r\nGeorgia\r\nSegoe\r\nRockwell Condensed\r\nCouier New";

    private const string ParagraphResult =
        "Static cling\r\nStatic cling is the tendency for light objects to stick (cling) to other objects owing to static electricity. Common everyday examples include dust and pet fur clinging to clothing, socks sticking together after being removed from a clothes dryer, or a rubber balloon attracting water after being rubbed against hair.\r\nWhile often considered a minor household annoyance, static cling represents a fundamental demonstration of electrostatics and has significant implications in manufacturing, electronics cooling, and material handling.\r\nhttps://en.wikipedia.org/wiki/Static_cling";

    private static readonly OcrOptions English = OcrOptions.FromSettings(new FakeTextGrabSettings(), "en-US");

    private static string Fixture(string name) => FileUtilities.GetPathToLocalFile($@".\Images\{name}");

    [Theory]
    [InlineData("font_sample.png", FontSampleResult)]
    [InlineData("FontTest.png", FontTestResult)]
    [InlineData("paragraph-test-image.png", ParagraphResult)]
    public async Task OcrFileAsync_ProducesTheAppsText(string fixture, string expected)
    {
        IOcrService ocr = new OcrService();

        string text = await ocr.OcrFileAsync(Fixture(fixture), English, TestContext.Current.CancellationToken);

        Assert.Equal(expected, text);
    }

    [Fact]
    public async Task OcrImageAsync_FromAFileStream_ProducesTheAppsText()
    {
        IOcrService ocr = new OcrService();
        await using FileStream stream = File.OpenRead(Fixture("font_sample.png"));

        string text = await ocr.OcrImageAsync(stream, English, TestContext.Current.CancellationToken);

        Assert.Equal(FontSampleResult, text);
    }

    [Fact]
    public async Task OcrImageAsync_AcceptsANonSeekableStream()
    {
        IOcrService ocr = new OcrService();
        await using NonSeekableStream stream = new(File.ReadAllBytes(Fixture("font_sample.png")));

        string text = await ocr.OcrImageAsync(stream, English, TestContext.Current.CancellationToken);

        Assert.Equal(FontSampleResult, text);
    }

    [Fact]
    public async Task OcrFileAsync_ExplicitOptionsOverrideTheSettings()
    {
        // The fake settings have ParagraphDetection on; switching it off per call must keep the
        // engine's line breaks instead of joining the wrapped paragraph.
        IOcrService ocr = new OcrService();
        OcrOptions noParagraphs = English with { ParagraphDetection = false };

        string text = await ocr.OcrFileAsync(Fixture("paragraph-test-image.png"), noParagraphs, TestContext.Current.CancellationToken);

        Assert.NotEqual(ParagraphResult, text);
        Assert.True(
            text.Split("\r\n").Length > ParagraphResult.Split("\r\n").Length,
            $"Expected more lines without paragraph detection, got:{Environment.NewLine}{text}");
    }

    [Fact]
    public async Task OcrFileAsync_RejectsPdf()
    {
        IOcrService ocr = new OcrService();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => ocr.OcrFileAsync(@"C:\does-not-matter\document.PDF", English, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OcrFileAsync_MissingFile_Throws()
    {
        IOcrService ocr = new OcrService();
        string missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => ocr.OcrFileAsync(missing, English, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OcrFileAsync_HonoursAnAlreadyCancelledToken()
    {
        IOcrService ocr = new OcrService();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ocr.OcrFileAsync(Fixture("font_sample.png"), English, new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task OcrImageAsync_HonoursAnAlreadyCancelledToken()
    {
        IOcrService ocr = new OcrService();
        await using FileStream stream = File.OpenRead(Fixture("font_sample.png"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ocr.OcrImageAsync(stream, English, new CancellationToken(canceled: true)));
    }

    [Theory]
    [InlineData(0, 0, 0, 10)]
    [InlineData(0, 0, 10, 0)]
    [InlineData(5, 5, 0.9f, 20)]
    public async Task OcrScreenRegionAsync_RejectsAnEmptyRegion(float x, float y, float width, float height)
    {
        IOcrService ocr = new OcrService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => ocr.OcrScreenRegionAsync(new RectangleF(x, y, width, height), English, TestContext.Current.CancellationToken));
    }

    private sealed class NonSeekableStream(byte[] contents) : MemoryStream(contents)
    {
        public override bool CanSeek => false;
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
        public override long Position
        {
            get => base.Position;
            set => throw new NotSupportedException();
        }
    }
}
