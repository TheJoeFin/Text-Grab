using Text_Grab.Models;
using Text_Grab.Utilities;
using UglyToad.PdfPig.Core;
using Windows.Media.Ocr;

namespace Tests;

public class PdfDocumentRendererTests
{

    const string expectedPDFText = """
        Text Grab Demo PDF

        Milwaukee, September 4th 2026

        Text Grab Corp.
        123 N. Main St.
        Anywhere, USA 12345

        Dear Testing framework reading this,

        A hungry Fox saw some fine bunches of Grapes hanging from a vine that was trained along a high trellis, and did his best to reach them by jumping as high as he could into the air. But it was all in vain, for they were just out of reach: so he gave up trying, and walked away with an air of dignity and unconcern, remarking, "I thought those Grapes were ripe, but I see now they are quite sour."

        A Man and his Wife had the good fortune to possess a Goose which laid a Golden Egg every day. Lucky though they were, they soon began to think they were not getting rich fast enough, and, imagining the bird must be made of gold inside, they decided to kill it in order to secure the whole store of precious metal at once. But when they cut it open they found it was just like any other goose. Thus, they neither got rich all at once, as they had hoped, nor enjoyed any longer the daily addition to their wealth.
        Much wants more and loses all.

        There was once a house that was overrun with Mice. A Cat heard of this, and said to herself, "That's the place for me," and off she went and took up her quarters in the house, and caught the Mice one by one and ate them. At last the Mice could stand it no longer, and they determined to take to their holes and stay there. "That's awkward," said the Cat to herself: "the only thing to do is to coax them out by a trick." So she considered a while, and then climbed up the wall and let herself hang down by her hind legs from a peg, and pretended to be dead. By and by a Mouse peeped out and saw the Cat hanging there. "Aha!" it cried, "you're very clever, madam, no doubt: but you may turn yourself into a bag of meal hanging there, if you like, yet you won't catch us coming anywhere near you."
        If you are wise you won't be deceived by the innocent airs of those whom you have once found to be dangerous.

        There was once a Dog who used to snap at people and bite them without any provocation, and who was a great nuisance to every one who came to his master's house. So his master fastened a bell round his neck to warn people of his presence. The Dog was very proud of the bell, and strutted about tinkling it with immense satisfaction. But an old dog came up to him and said, "The fewer airs you give yourself the better, my friend. You don't think, do you, that your bell was given you as a reward of merit? On the contrary, it is a badge of disgrace."
        Notoriety is often mistaken for fame.

        Thank you,
        AESOP

        Contact
        joe@joefinapps.com
        123-555-1234
        Milwaukee, WI
        """;

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
        Assert.True((ulong)width * height <= PdfDocumentRenderer.MaxRenderPixelCount);
        Assert.True(width > height);
    }

    [Fact]
    public void GetRenderDimensions_ClampsTotalPixelCount()
    {
        (uint width, uint height) = PdfDocumentRenderer.GetRenderDimensions(10_000, 10_000);

        Assert.True((ulong)width * height <= PdfDocumentRenderer.MaxRenderPixelCount);
        Assert.Equal(width, height);
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

    [WpfFact]
    public async Task BuildTextFromLines_DetectsParagraphsInSamplePdf()
    {
        string pdfPath = FileUtilities.GetPathToLocalFile(@"TextFiles\Text-Grab-Test-PDF.pdf");

        using PdfDocumentRenderer pdfDocument = await PdfDocumentRenderer.LoadAsync(pdfPath);
        PdfPageContent pageContent = await pdfDocument.GetPageContentAsync(pageIndex: 0);
        string text = PdfDocumentRenderer.BuildTextFromLines(pageContent.NativeLines, useParagraphDetection: true);

        Assert.Contains(
            "A hungry Fox saw some fine bunches of Grapes hanging from a vine that was trained along a high trellis, and did his best to reach them by jumping as high as he could into the air. But it was all in vain, for they were just out of reach: so he gave up trying, and walked away with an air of dignity and unconcern, remarking, \"I thought those Grapes were ripe, but I see now they are quite sour.\"",
            text);
    }

    [WpfFact]
    public async Task GetSelectableWordsAsync_ReturnsMoreWordsThanLines()
    {
        string pdfPath = FileUtilities.GetPathToLocalFile(@"TextFiles\Text-Grab-Test-PDF.pdf");

        using PdfDocumentRenderer pdfDocument = await PdfDocumentRenderer.LoadAsync(pdfPath);
        PdfPageContent pageContent = await pdfDocument.GetPageContentAsync(pageIndex: 0);
        IReadOnlyList<PdfPageTextLine> words = await pdfDocument.GetSelectableWordsAsync(pageIndex: 0);

        Assert.True(pageContent.HasNativeText);
        Assert.True(words.Count > pageContent.NativeLines.Count);
        Assert.All(pageContent.NativeWords, word => Assert.Contains(word, words));
        Assert.Contains(words, word => word.Text == "Milwaukee,");
    }

    [Fact]
    public void CombineNativeAndOcrWords_PreservesScannedTableWithNativePageNumber()
    {
        PdfPageTextLine pageNumber = new(new Windows.Foundation.Rect(150, 280, 10, 10), "1", isNativeText: true);
        GeneratedOcrLinesWords ocrResult = new()
        {
            Lines =
            [
                new GeneratedOcrLine
                {
                    Words =
                    [
                        new GeneratedOcrWord { Text = "Quantity", BoundingBox = new(200, 40, 60, 12) },
                        new GeneratedOcrWord { Text = "Item", BoundingBox = new(40, 40, 40, 12) }
                    ]
                },
                new GeneratedOcrLine
                {
                    Words =
                    [
                        new GeneratedOcrWord { Text = "Apple", BoundingBox = new(40, 80, 40, 12) },
                        new GeneratedOcrWord { Text = "2", BoundingBox = new(200, 80, 10, 12) }
                    ]
                },
                GeneratedOcrLine.FromText("1", pageNumber.SourceRect)
            ]
        };

        IReadOnlyList<PdfPageTextLine> words = PdfDocumentRenderer.CombineNativeAndOcrWords(
            [pageNumber], [new(0, 0, 300, 300)], ocrResult, scale: 1);

        Assert.Equal(["Item", "Quantity", "Apple", "2", "1"], words.Select(word => word.Text).ToArray());
        Assert.All(words.Take(4), word => Assert.False(word.IsNativeText));
        Assert.Same(pageNumber, words[^1]);
        Assert.Equal(160, words[1].SourceRect.Left - words[0].SourceRect.Left);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2.5)]
    public void CombineNativeAndOcrWords_MapsScaledOcrToPageBeforeFiltering(double scale)
    {
        Windows.Foundation.Rect imageRegion = PdfDocumentRenderer.ConvertPdfRectToImageRect(
            new PdfRectangle(40, 90, 160, 170), 200, 200, 400, 400);
        Windows.Foundation.Rect imageWordRect = new(110, 90, 40, 12);
        PdfPageTextLine nativeWord = new(new Windows.Foundation.Rect(180, 90, 50, 12), "Native", isNativeText: true);
        GeneratedOcrLinesWords ocrResult = new()
        {
            Lines =
            [
                GeneratedOcrLine.FromText("Image", new(
                    imageWordRect.X * scale, imageWordRect.Y * scale,
                    imageWordRect.Width * scale, imageWordRect.Height * scale)),
                GeneratedOcrLine.FromText("Duplicate", new(180 * scale, 90 * scale, 50 * scale, 12 * scale)),
                GeneratedOcrLine.FromText("Outside image", new(10 * scale, 10 * scale, 40 * scale, 12 * scale))
            ]
        };

        IReadOnlyList<PdfPageTextLine> words = PdfDocumentRenderer.CombineNativeAndOcrWords(
            [nativeWord], [imageRegion], ocrResult, scale);

        Assert.Collection(
            words,
            imageWord =>
            {
                Assert.Equal("Image", imageWord.Text);
                Assert.Equal(imageWordRect, imageWord.SourceRect);
                Assert.False(imageWord.IsNativeText);
            },
            word => Assert.Same(nativeWord, word));
    }

    [Fact]
    public void CombineNativeAndOcrWords_SuppressesNativeOverlapsWithoutDroppingColumnGaps()
    {
        PdfPageTextLine leftWord = new(new Windows.Foundation.Rect(10, 40, 20, 12), "10", isNativeText: true);
        PdfPageTextLine rightWord = new(new Windows.Foundation.Rect(190, 40, 20, 12), "30", isNativeText: true);
        GeneratedOcrLinesWords ocrResult = new()
        {
            Lines =
            [
                new GeneratedOcrLine
                {
                    Words =
                    [
                        new GeneratedOcrWord { Text = "1O", BoundingBox = new(12, 41, 20, 12) },
                        new GeneratedOcrWord { Text = "10", BoundingBox = new(100, 40, 20, 12) },
                        new GeneratedOcrWord { Text = "3O", BoundingBox = new(188, 39, 20, 12) }
                    ]
                }
            ]
        };

        IReadOnlyList<PdfPageTextLine> words = PdfDocumentRenderer.CombineNativeAndOcrWords(
            [leftWord, rightWord], [new(0, 0, 240, 100)], ocrResult, scale: 1);

        Assert.Collection(
            words,
            word => Assert.Same(leftWord, word),
            imageWord =>
            {
                Assert.Equal("10", imageWord.Text);
                Assert.Equal(100, imageWord.SourceRect.X);
                Assert.False(imageWord.IsNativeText);
            },
            word => Assert.Same(rightWord, word));
    }

    [Fact]
    public void CombineNativeAndOcrWords_OverlappingImagesDoNotDuplicateWords()
    {
        PdfPageTextLine nativeWord = new(new Windows.Foundation.Rect(0, 0, 20, 10), "Native", isNativeText: true);
        GeneratedOcrLinesWords ocrResult = GeneratedOcrLinesWords.FromParagraph("Image", new(100, 100, 20, 10));

        IReadOnlyList<PdfPageTextLine> words = PdfDocumentRenderer.CombineNativeAndOcrWords(
            [nativeWord], [new(90, 90, 50, 40), new(100, 100, 60, 50)], ocrResult, scale: 1);

        Assert.Equal(2, words.Count);
        Assert.Single(words, word => word.Text == "Image");
    }

    [Fact]
    public void CombineNativeAndOcrWords_FiltersEmptyWordsAndInsignificantImageOverlap()
    {
        PdfPageTextLine nativeWord = new(new Windows.Foundation.Rect(0, 0, 20, 10), "Native", isNativeText: true);
        GeneratedOcrLinesWords ocrResult = new()
        {
            Lines =
            [
                GeneratedOcrLine.FromText("Keep", new(105, 105, 10, 10)),
                GeneratedOcrLine.FromText("Outside", new(108, 108, 10, 10)),
                GeneratedOcrLine.FromText(" ", new(100, 100, 10, 10)),
                GeneratedOcrLine.FromText("Empty bounds", new(100, 100, 0, 10))
            ]
        };

        IReadOnlyList<PdfPageTextLine> words = PdfDocumentRenderer.CombineNativeAndOcrWords(
            [nativeWord], [new(100, 100, 10, 10)], ocrResult, scale: 1);

        Assert.Equal(["Native", "Keep"], words.Select(word => word.Text).ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CombineNativeAndOcrWords_MissingOcrPreservesNativeWords(bool nullResult)
    {
        IReadOnlyList<PdfPageTextLine> nativeWords =
            [new(new Windows.Foundation.Rect(10, 10, 40, 12), "Native", isNativeText: true)];

        IReadOnlyList<PdfPageTextLine> words = PdfDocumentRenderer.CombineNativeAndOcrWords(
            nativeWords, [new(0, 0, 100, 100)], nullResult ? null : new GeneratedOcrLinesWords(), scale: 1);

        Assert.Same(nativeWords, words);
    }

    [Fact]
    public void CombineNativeAndOcrWords_WithoutImagesPreservesNativeWords()
    {
        IReadOnlyList<PdfPageTextLine> nativeWords =
            [new(new Windows.Foundation.Rect(10, 10, 40, 12), "Native", isNativeText: true)];
        GeneratedOcrLinesWords ocrResult = GeneratedOcrLinesWords.FromParagraph("Ignore", new(100, 100, 20, 10));

        IReadOnlyList<PdfPageTextLine> words = PdfDocumentRenderer.CombineNativeAndOcrWords(
            nativeWords, [], ocrResult, scale: 1);

        Assert.Same(nativeWords, words);
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
