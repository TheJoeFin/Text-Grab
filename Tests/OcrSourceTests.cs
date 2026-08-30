// This is the app-coupled split of the original Tests/OcrTests.cs (batch 7a): live OCR-engine
// calls through OcrSourceUtilities, anything constructing a WPF BitmapImage, and anything
// reading AppUtilities.TextGrabSettings directly. The headless majority (27 methods against
// pure Text_Grab.Utilities.OcrUtilities logic) kept the original OcrTests name and moved to
// Tests.Core.Windows/OcrTests.cs; this file has the remaining 15.
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using Text_Grab;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.Globalization;

namespace Tests;

public class OcrSourceTests
{
    private const string fontSamplePath = @".\Images\font_sample.png";
    private const string fontSampleResult = @"Times-Roman
Helvetica
Courier
Palatino-Roman
Helvetica-Narrow
Bookman-Demi";

    private const string fontSampleResultForTesseract = @"Times-Roman
Helvetica
Courier
Palatino-Roman
Helvetica-Narrow

Bookman-Demi
";

    private const string fontTestPath = @".\Images\FontTest.png";

    private const string fontTestResult = @"Arial
Times New Roman
Georgia
Segoe
Rockwell Condensed
Couier New";

    private const string tableTestPath = @".\Images\Table-Test.png";
    private const string tableTestResult = @"Month	Int	Season
January	1	Winter
February	2	Winter
March	3	Spring
April	4	Spring
May	5	Spring
June	6	Summer
July	7	Summer
August	8	Summer
September	9	Fall
October	10	Fall
November	11	Fall
December	12	Winter";

    private const string jaTestPath = @".\Images\Ja-Lang-Image.png";

    // The reading-order-corrected OCR output for Ja-Lang-Image.png. Furigana ruby
    // lines are still present inline (they are kept per the current line-ordering
    // fix), but every line now appears in top-to-bottom / left-to-right reading
    // order instead of the scrambled order the Windows OCR engine returns.
    //
    // JaTestExpectedResult (above) is the aspirational, fully-corrected target:
    // furigana grouped per row with full-width spaces AND engine misreads fixed
    // (からだ vs からた, こうか vs カ, ...). Reaching it needs more than ordering:
    // furigana row grouping plus OCR error correction that recovers dakuten and
    // small-kana the engine drops. This constant captures what is achievable today.
    private const string JaReadingOrderResult =
        "くろからたしつ黒ごまは体にいいです。タンバク質やカルシウムがかみ彡ろカたくさんあります。髪を黒くする効果もあります。くろあぶらはだかみりようり黒ごま油は肌や髪に使います。料理にも使います。かゆたからだお粥やデサ ー トに入れます。でも、食べすき、ると体たによくないです。少しすっ食べましよう。";

    // With furigana removal enabled, the ruby-reading lines are dropped and only
    // the main body text remains (still subject to the engine's own misreads and
    // one stray mis-detected fragment "み彡" the geometry heuristic cannot catch).
    private const string JaFuriganaRemovedResult =
        "黒ごまは体にいいです。タンバク質やカルシウムがみ彡たくさんあります。髪を黒くする効果もあります。黒ごま油は肌や髪に使います。料理にも使います。お粥やデサ ー トに入れます。でも、食べすき、ると体によくないです。少しすっ食べましよう。";

    [Theory]
    [InlineData("en-US", "H3llO")]
    [InlineData("ru-RU", "HЭllΘ")]
    public void CleanOutput_CorrectsOnlyLatinCaptureLanguages(string languageTag, string expected)
    {
        Settings settings = AppUtilities.TextGrabSettings;
        bool originalCorrectToLatin = settings.CorrectToLatin;
        bool originalCorrectErrors = settings.CorrectErrors;
        settings.CorrectToLatin = true;
        settings.CorrectErrors = false;

        try
        {
            OcrOutput output = new()
            {
                Kind = OcrOutputKind.Paragraph,
                Language = new GlobalLang(languageTag),
                RawOutput = "HЭllΘ"
            };

            output.CleanOutput();

            Assert.Equal(expected, output.CleanedOutput);
        }
        finally
        {
            settings.CorrectToLatin = originalCorrectToLatin;
            settings.CorrectErrors = originalCorrectErrors;
        }
    }
    [WpfFact]
    public async Task OcrFontSampleImage()
    {
        // Given
        string testImagePath = fontSamplePath;

        // When
        string ocrTextResult = await OcrSourceUtilities.OcrAbsoluteFilePathAsync(FileUtilities.GetPathToLocalFile(testImagePath));

        // Then
        Assert.Equal(fontSampleResult, ocrTextResult);
    }
    [WpfFact]
    public async Task OcrFontTestImage()
    {
        // Given
        string testImagePath = fontTestPath;
        string expectedResult = fontTestResult;

        Uri uri = new(testImagePath, UriKind.Relative);
        // When
        string ocrTextResult = await OcrSourceUtilities.OcrAbsoluteFilePathAsync(FileUtilities.GetPathToLocalFile(testImagePath));

        // Then
        Assert.Equal(expectedResult, ocrTextResult);
    }
    [WpfFact]
    public async Task AnalyzeTable()
    {
        string testImagePath = tableTestPath;
        string expectedResult = tableTestResult;


        Uri uri = new(testImagePath, UriKind.Relative);
        Language EnglishLanguage = new("en-US");
        GlobalLang globalLang = new(EnglishLanguage);
        Bitmap testBitmap = new(FileUtilities.GetPathToLocalFile(testImagePath));
        // When
        IOcrLinesWords ocrResult = await OcrSourceUtilities.GetOcrResultFromImageAsync(testBitmap, globalLang);

        Rectangle rectCanvasSize = new()
        {
            Width = 1132,
            Height = 1158,
            X = 0,
            Y = 0
        };

        List<WordBorderInfo> wordBorders = OcrUtilities.ParseOcrResultIntoWordBorderInfos(ocrResult);

        ResultTable resultTable = new();
        resultTable.AnalyzeAsTable(wordBorders, rectCanvasSize);

        StringBuilder stringBuilder = new();

        ResultTable.GetTextFromTabledWordBorders(stringBuilder, wordBorders, true);

        // Then
        Assert.Equal(expectedResult, stringBuilder.ToString());

    }
    [WpfFact]
    public async Task ParagraphWrapDetection()
    {
        // Given
        string testImagePath = @".\Images\paragraph-test-image.png";
        bool originalParagraphDetection = AppUtilities.TextGrabSettings.ParagraphDetection;
        AppUtilities.TextGrabSettings.ParagraphDetection = true;
        string expectedResult = "Static cling\r\nStatic cling is the tendency for light objects to stick (cling) to other objects owing to static electricity. Common everyday examples include dust and pet fur clinging to clothing, socks sticking together after being removed from a clothes dryer, or a rubber balloon attracting water after being rubbed against hair.\r\nWhile often considered a minor household annoyance, static cling represents a fundamental demonstration of electrostatics and has significant implications in manufacturing, electronics cooling, and material handling.\r\nhttps://en.wikipedia.org/wiki/Static_cling";

        try
        {
            // When
            string ocrTextResult = await OcrSourceUtilities.OcrAbsoluteFilePathAsync(FileUtilities.GetPathToLocalFile(testImagePath));

            // Then
            Assert.Equal(expectedResult, ocrTextResult);
        }
        finally
        {
            AppUtilities.TextGrabSettings.ParagraphDetection = originalParagraphDetection;
        }
    }
    [Fact]
    public void BuildTextFromOcrLines_UsesParagraphDetectionForWinAi()
    {
        bool originalParagraphDetection = AppUtilities.TextGrabSettings.ParagraphDetection;
        AppUtilities.TextGrabSettings.ParagraphDetection = true;

        try
        {
            FakeOcrLinesWords ocrResult = new()
            {
                Lines =
                [
                    new FakeOcrLine("Static cling is the tendency", new Windows.Foundation.Rect(0, 0, 100, 10)),
                    new FakeOcrLine("for light objects to stick.", new Windows.Foundation.Rect(0, 14, 100, 10)),
                    new FakeOcrLine("New paragraph.", new Windows.Foundation.Rect(0, 32, 100, 10)),
                ]
            };

            string text = OcrUtilities.BuildTextFromOcrLines(new WindowsAiLang(), ocrResult);

            Assert.Equal("Static cling is the tendency for light objects to stick.\r\nNew paragraph.", text);
        }
        finally
        {
            AppUtilities.TextGrabSettings.ParagraphDetection = originalParagraphDetection;
        }
    }
    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    public void ShouldUseParagraphDetection_RespectsTableMode(
        bool paragraphDetectionEnabled,
        bool isSpaceJoiningLanguage,
        bool isTableMode,
        bool expected)
    {
        bool originalParagraphDetection = AppUtilities.TextGrabSettings.ParagraphDetection;
        AppUtilities.TextGrabSettings.ParagraphDetection = paragraphDetectionEnabled;

        try
        {
            bool result = OcrUtilities.ShouldUseParagraphDetection(isSpaceJoiningLanguage, isTableMode);
            Assert.Equal(expected, result);
        }
        finally
        {
            AppUtilities.TextGrabSettings.ParagraphDetection = originalParagraphDetection;
        }
    }
    [WpfFact]
    public async Task OcrJapaneseImage_ReadingOrder_KeepsFuriganaWhenDisabled()
    {
        // Given
        GlobalLang japanese = new("ja");

        // Skip if the Japanese OCR language pack is not installed on this machine.
        if (!Windows.Media.Ocr.OcrEngine.IsLanguageSupported(japanese.OriginalLanguage))
            return;

        Settings settings = AppUtilities.TextGrabSettings;
        bool originalRemoveFurigana = settings.RemoveFurigana;
        settings.RemoveFurigana = false;

        try
        {
            // When
            string ocrTextResult = await OcrSourceUtilities.OcrAbsoluteFilePathAsync(
                FileUtilities.GetPathToLocalFile(jaTestPath), japanese);

            // Then furigana are kept, but every line is in natural reading order
            // (top-to-bottom, left-to-right).
            Assert.Equal(JaReadingOrderResult, ocrTextResult);
        }
        finally
        {
            settings.RemoveFurigana = originalRemoveFurigana;
        }
    }
    [WpfFact]
    public async Task OcrJapaneseImage_RemovesFuriganaWhenEnabled()
    {
        // Given
        GlobalLang japanese = new("ja");

        if (!Windows.Media.Ocr.OcrEngine.IsLanguageSupported(japanese.OriginalLanguage))
            return;

        Settings settings = AppUtilities.TextGrabSettings;
        bool originalRemoveFurigana = settings.RemoveFurigana;
        settings.RemoveFurigana = true;

        try
        {
            // When
            string ocrTextResult = await OcrSourceUtilities.OcrAbsoluteFilePathAsync(
                FileUtilities.GetPathToLocalFile(jaTestPath), japanese);

            // Then the furigana ruby lines are dropped, leaving the main text.
            Assert.Equal(JaFuriganaRemovedResult, ocrTextResult);
        }
        finally
        {
            settings.RemoveFurigana = originalRemoveFurigana;
        }
    }
    [WpfFact]
    public async Task InspectJapaneseOcrOutput()
    {
        // Exploration harness: dumps the raw OCR lines/words with their bounding
        // boxes so we can see exactly what the Windows OCR engine returns for a
        // furigana-heavy Japanese image, and how the current pipeline processes it.
        GlobalLang japanese = new("ja");

        if (!Windows.Media.Ocr.OcrEngine.IsLanguageSupported(japanese.OriginalLanguage))
            return;

        Bitmap testBitmap = new(FileUtilities.GetPathToLocalFile(jaTestPath));
        double scale = await OcrSourceUtilities.GetIdealScaleFactorForOcrAsync(testBitmap, japanese);
        Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(testBitmap, scale);
        IOcrLinesWords ocrResult = await OcrSourceUtilities.GetOcrResultFromImageAsync(scaledBitmap, japanese);

        StringBuilder report = new();
        report.AppendLine($"scale factor: {scale:0.###}");
        report.AppendLine($"line count: {ocrResult.Lines.Length}");
        report.AppendLine();

        for (int i = 0; i < ocrResult.Lines.Length; i++)
        {
            IOcrLine line = ocrResult.Lines[i];
            Windows.Foundation.Rect lb = line.BoundingBox;
            report.AppendLine(
                $"LINE {i,2}  Y={lb.Y,7:0.0} H={lb.Height,6:0.0}  X={lb.X,7:0.0} W={lb.Width,7:0.0}  \"{line.Text}\"");
            foreach (IOcrWord w in line.Words)
            {
                Windows.Foundation.Rect wb = w.BoundingBox;
                report.AppendLine(
                    $"    word  Y={wb.Y,7:0.0} H={wb.Height,6:0.0}  X={wb.X,7:0.0} W={wb.Width,7:0.0}  \"{w.Text}\"");
            }
        }

        report.AppendLine();
        report.AppendLine("=== reading-flow ordered lines ===");
        foreach (IOcrLine line in OcrUtilities.OrderLinesForReadingFlow(ocrResult.Lines))
            report.AppendLine($"  Y={line.BoundingBox.Y,7:0.0} X={line.BoundingBox.X,7:0.0}  \"{line.Text}\"");

        report.AppendLine();
        report.AppendLine("=== BuildTextFromOcrLines (current pipeline output) ===");
        report.AppendLine(OcrUtilities.BuildTextFromOcrLines(japanese, ocrResult));

        string outPath = Path.Combine(Path.GetTempPath(), "ja-ocr-report.txt");
        await File.WriteAllTextAsync(outPath, report.ToString(), new UTF8Encoding(true), TestContext.Current.CancellationToken);
        System.Diagnostics.Debug.WriteLine(report.ToString());
        System.Diagnostics.Debug.WriteLine($"Report written to {outPath}");
    }
    [WpfFact]
    public async Task ReadQrCode()
    {
        string expectedResult = "This is a test of the QR Code system";

        string testImagePath = @".\Images\QrCodeTestImage.png";
        Uri uri = new(testImagePath, UriKind.Relative);
        // When
        string ocrTextResult = await OcrSourceUtilities.OcrAbsoluteFilePathAsync(FileUtilities.GetPathToLocalFile(testImagePath));

        // Then
        Assert.Equal(expectedResult, ocrTextResult);
    }
    [WpfFact]
    public async Task AnalyzeTable2()
    {
        string expectedResult = @"Test	Text
12	The Quick Brown Fox
13	Jumped over the
14	Lazy
15
20
200
300	Brown
400	Dog";

        string testImagePath = @".\Images\Table-Test-2.png";
        Uri uri = new(testImagePath, UriKind.Relative);
        Language EnglishLanguage = new("en-US");
        GlobalLang globalLang = new(EnglishLanguage);
        Bitmap testBitmap = new(FileUtilities.GetPathToLocalFile(testImagePath));
        // When
        IOcrLinesWords ocrResult = await OcrSourceUtilities.GetOcrResultFromImageAsync(testBitmap, globalLang);

        Rectangle rectCanvasSize = new()
        {
            Width = 1152,
            Height = 1132,
            X = 0,
            Y = 0
        };

        List<WordBorderInfo> wordBorders = OcrUtilities.ParseOcrResultIntoWordBorderInfos(ocrResult);

        ResultTable resultTable = new();
        resultTable.AnalyzeAsTable(wordBorders, rectCanvasSize);

        StringBuilder stringBuilder = new();

        ResultTable.GetTextFromTabledWordBorders(stringBuilder, wordBorders, true);

        // Then
        Assert.Equal(expectedResult, stringBuilder.ToString());
    }
    [WpfFact(Skip = "since the hocr is not being used from Tesseract it will not be tested for now")]
    public async Task TesseractHocr()
    {
        int initialLinesToSkip = 12;

        // Given
        string hocrFilePath = FileUtilities.GetPathToLocalFile(@"TextFiles\font_sample.hocr");
        string[] hocrFileContentsArray = await File.ReadAllLinesAsync(hocrFilePath);

        // combine string array into one string
        StringBuilder sb = new();
        foreach (string line in hocrFileContentsArray.Skip(initialLinesToSkip).ToArray())
            sb.AppendLine(line);

        string hocrFileContents = sb.ToString();

        string testImagePath = fontSamplePath;
        // need to scale to get the test to match the output
        // Bitmap scaledBMP = ImageMethods
        Uri fileURI = new(FileUtilities.GetPathToLocalFile(testImagePath), UriKind.Absolute);
        BitmapImage bmpImg = new(fileURI);
        bmpImg.Freeze();
        Bitmap bmp = ImageMethods.BitmapImageToBitmap(bmpImg);
        ILanguage language = LanguageUtilities.GetOCRLanguage();
        double idealScaleFactor = await OcrSourceUtilities.GetIdealScaleFactorForOcrAsync(bmp, language);
        Bitmap scaledBMP = ImageMethods.ScaleBitmapUniform(bmp, idealScaleFactor);

        // When
        TessLang EnglishLanguage = new("eng");
        OcrOutput tesseractOutput = await TesseractHelper.GetOcrOutputFromBitmap(scaledBMP, EnglishLanguage);

        string[] tesseractOutputArray = tesseractOutput.RawOutput.Split(Environment.NewLine);
        StringBuilder sb2 = new();
        foreach (string line in tesseractOutputArray.Skip(initialLinesToSkip).ToArray())
            sb2.AppendLine(line);

        tesseractOutput.RawOutput = sb2.ToString();

        // Then
        Assert.Equal(hocrFileContents, tesseractOutput.RawOutput);
    }
    [WpfFact]
    public async Task TesseractFontSample()
    {
        string testImagePath = fontSamplePath;
        // need to scale to get the test to match the output
        // Bitmap scaledBMP = ImageMethods
        Uri fileURI = new(FileUtilities.GetPathToLocalFile(testImagePath), UriKind.Absolute);
        BitmapImage bmpImg = new(fileURI);
        bmpImg.Freeze();
        Bitmap bmp = ImageMethods.BitmapImageToBitmap(bmpImg);
        ILanguage language = LanguageUtilities.GetOCRLanguage();
        double idealScaleFactor = await OcrSourceUtilities.GetIdealScaleFactorForOcrAsync(bmp, language);
        Bitmap scaledBMP = ImageMethods.ScaleBitmapUniform(bmp, idealScaleFactor);

        // When
        TessLang EnglishLanguage = new("eng");
        OcrOutput tesseractOutput = await TesseractHelper.GetOcrOutputFromBitmap(scaledBMP, EnglishLanguage);

        if (tesseractOutput.RawOutput == "Cannot find tesseract.exe")
            return;

        // Then
        Assert.Equal(fontSampleResultForTesseract, tesseractOutput.RawOutput);
    }
    [Fact]
    public void BuildTextFromOcrLines_SpaceJoiningLanguage_DoesNotFilterFurigana()
    {
        // For space-joining languages the whole line text is used verbatim, so
        // the furigana heuristic never runs, even with a tiny word present.
        Settings settings = AppUtilities.TextGrabSettings;
        bool originalParagraphDetection = settings.ParagraphDetection;
        bool originalCorrectErrors = settings.CorrectErrors;
        settings.ParagraphDetection = false;
        settings.CorrectErrors = false;

        try
        {
            FakeOcrLine line = new("Hello World", new Windows.Foundation.Rect(0, 0, 100, 30))
            {
                Words =
                [
                    Word("x", 0, 0, 4, 4),   // tiny word that would be furigana in CJK
                    Word("Hello", 0, 10, 50, 20),
                    Word("World", 55, 10, 50, 20),
                ]
            };
            FakeOcrLinesWords ocrResult = new() { Lines = [line] };

            string text = OcrUtilities.BuildTextFromOcrLines(new GlobalLang("en-US"), ocrResult);

            Assert.Equal("Hello World" + System.Environment.NewLine, text);
        }
        finally
        {
            settings.ParagraphDetection = originalParagraphDetection;
            settings.CorrectErrors = originalCorrectErrors;
        }
    }

    private static FakeOcrWord Word(string text, double x, double y, double width, double height)
        => new(text, new Windows.Foundation.Rect(x, y, width, height));

    private sealed class FakeOcrLinesWords : IOcrLinesWords
    {
        public string Text { get; set; } = string.Empty;

        public IOcrLine[] Lines { get; set; } = [];

        public float Angle { get; set; }
    }

    private sealed class FakeOcrLine : IOcrLine
    {
        public FakeOcrLine(string text, Windows.Foundation.Rect boundingBox)
        {
            Text = text;
            BoundingBox = boundingBox;
        }

        public string Text { get; set; }

        public IOcrWord[] Words { get; set; } = [];

        public Windows.Foundation.Rect BoundingBox { get; set; }
    }

    private sealed class FakeOcrWord : IOcrWord
    {
        public FakeOcrWord(string text, Windows.Foundation.Rect boundingBox)
        {
            Text = text;
            BoundingBox = boundingBox;
        }

        public string Text { get; set; }

        public Windows.Foundation.Rect BoundingBox { get; set; }
    }
}
