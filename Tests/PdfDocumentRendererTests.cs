using Text_Grab.Utilities;
using UglyToad.PdfPig.Core;
using Windows.Media.Ocr;

namespace Tests;

public class PdfDocumentRendererTests
{
    [Fact]
    public void GetRenderDimensions_DoublesTypicalPdfPageSize()
    {
        (uint width, uint height) = PdfDocumentRenderer.GetRenderDimensions(612, 792);

        Assert.Equal(1224u, width);
        Assert.Equal(1584u, height);
    }

    [Fact]
    public void GetRenderDimensions_ClampsToOcrEngineLimit()
    {
        (uint width, uint height) = PdfDocumentRenderer.GetRenderDimensions(5000, 2500);

        Assert.True(Math.Max(width, height) <= OcrEngine.MaxImageDimension);
        Assert.True(width > height);
    }

    [Fact]
    public void GetRenderDimensions_InvalidSize_ReturnsSinglePixel()
    {
        (uint width, uint height) = PdfDocumentRenderer.GetRenderDimensions(0, -1);

        Assert.Equal(1u, width);
        Assert.Equal(1u, height);
    }

    [Fact]
    public void ConvertPdfRectToImageRect_MapsPdfCoordinatesToRenderedBitmapSpace()
    {
        PdfRectangle pdfRect = new(10, 20, 60, 80);

        Windows.Foundation.Rect imageRect = PdfDocumentRenderer.ConvertPdfRectToImageRect(pdfRect, 100, 100, 200, 200);

        Assert.Equal(20, imageRect.X);
        Assert.Equal(40, imageRect.Y);
        Assert.Equal(100, imageRect.Width);
        Assert.Equal(120, imageRect.Height);
    }

    [Fact]
    public void GroupWordsIntoLines_GroupsNearbyWordsIntoSingleLine()
    {
        IReadOnlyList<PdfPageTextLine> lines = PdfDocumentRenderer.GroupWordsIntoLines(
        [
            (new Windows.Foundation.Rect(10, 10, 20, 12), "Hello"),
            (new Windows.Foundation.Rect(35, 11, 25, 12), "world"),
            (new Windows.Foundation.Rect(12, 40, 30, 12), "Again")
        ]);

        Assert.Collection(
            lines,
            firstLine =>
            {
                Assert.Equal("Hello world", firstLine.Text);
                Assert.True(firstLine.IsNativeText);
                Assert.Equal(10, firstLine.SourceRect.X);
                Assert.Equal(10, firstLine.SourceRect.Y);
                Assert.Equal(50, firstLine.SourceRect.Width);
                Assert.Equal(13, firstLine.SourceRect.Height);
            },
            secondLine => Assert.Equal("Again", secondLine.Text));
    }

    [Fact]
    public void ShouldIncludeOcrLine_OnlyReturnsTrueWhenImageOverlapIsMeaningful()
    {
        Windows.Foundation.Rect sourceRect = new(0, 0, 10, 10);

        bool shouldIncludeFromLargeOverlap = PdfDocumentRenderer.ShouldIncludeOcrLine(
            sourceRect,
            [new Windows.Foundation.Rect(5, 5, 10, 10)]);

        bool shouldIgnoreFromSmallOverlap = PdfDocumentRenderer.ShouldIncludeOcrLine(
            sourceRect,
            [new Windows.Foundation.Rect(8, 8, 10, 10)]);

        Assert.True(shouldIncludeFromLargeOverlap);
        Assert.False(shouldIgnoreFromSmallOverlap);
    }
}
