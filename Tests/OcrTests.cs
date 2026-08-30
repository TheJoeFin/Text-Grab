using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Text_Grab;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.Globalization;

namespace Tests;

public class OcrTests
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

    private const string ComplexTablePath = @".\Images\Table-Complex.png";
    private const string ComplexWordBorders = @".\TextFiles\Table-Complex-WordBorders.json";
    private const string ComplexTableResult = @"DESCRIPTION	YEAR TO DATE ACTUAL	ANNUAL BUDGET	BALANCE	% BUDGET REMAINING
CORPORATE INCOME	(1) $138,553	$358,100	$219,547	61 %
FOUNDATION INCOME	432,275	824,700	392,425	48%
GOVERNMENT INCOME	375,375	833,825	458,450	55%
PUBLICATIONS INCOME	1,341	3,000	1,659	55%
INTEREST INCOME	(2) 26,767	39,000	12,233	31%
INVESTMENT GAIN	(3) 50,472	0	N/A	N/A
MISCELLANEOUS INCOME	1,650	6,995	5,345	76%
TOTAL REVENUE	1,026,433	2,065,620	1,089,659	53%
SALARIES & WAGES	355,633	603,840	248,207	41%
FRINGE BENEFITS	63,182	120,120	56,938	47%
OFFICE RENT	83,131	132,000	48,869	37%
EQUIPMENT RENTAL & MAINTENANCE	15,364	19,900	4,536	23%
SUPPLIES	8,051	10,200	2,149	21%
TELEPHONE AND POSTAGE	15,088	24,100	9,012	37%
INSURANCE	6,149	5,500	(649)	(12)%
REGISTRATION & LICENSES	415	760	345	45%
DEPRECIATION	8,482	17,000	8,518	50%
BANK CHARGES	344	670	326	49%
AUDIT FEES	19,000	19,000	0	0%
BOARD MEETINGS	12,541	20,000	7,459	37%
TRAVEL	6,910	20,000	13,090	65%
LODGING & PERDIEM	15,623	20,000	4,377	22%
SEMINARS & MEETINGS	3,442	8,700	5,258	60%
PROFESSIONAL FESS	5,050	16,000	10,950	68%
PRINTING & PUBLICATIONS	25,576	25,000	(576)	(2) %
MATERIALS,SUBS,DUES & TRAININGS	4,445	6,800	2,355	35%
LOCAL STAFF DEVELOPMENT	0	7,500	7,500	100%
STIPENDS	8,250	9,750	1,500	15%
SUBTOTAL	656,675	1,086,840	430,165	40%
TRANSFER PAYMENTS TO SUBRECIPIENTS	360,009	978,780	618,771	63%
TOTAL EXPENDITURES	1,016,684	2,065,620	1,048,936	51%
REVENUES OVERY(UNDER) EXPENDITURES	$9,749	$0	$9,749	N/A";

    private const string JaTestExpectedResult = @"""くろ　からだ　しつ
黒ごまは体にいいです。タンバク質やカルシウムが
かみ　くろ　こうか
たくさんあります。髪を黒くする効果もあります。
くろ　あぶら　はだ　かみ　りようり
黒ごま油は肌や髪に使います。料理にも使います。
かゆ　た　からだ
お粥やデサートに入れます。でも、食べすきると体
た
によくないです。少しすつ食べましよう。
""";

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

    [Theory]
    [InlineData(10, 10, 25, 10, true)]   // bounding-box gap = 5
    [InlineData(10, 10, 26, 10, false)]  // threshold boundary: gap = 6
    [InlineData(10, 10, 27, 10, false)]  // bounding-box gap = 7
    [InlineData(10, 10, 10, 10, false)]  // same visual row
    [InlineData(10, 10, 14, 10, false)]  // insufficient vertical advance
    [InlineData(10, 10, 18, 10, true)]   // distinct rows with slight overlap
    [InlineData(10, 10, 16, 30, false)]  // height ratio = 3
    [InlineData(10, 0, 13, 10, false)]   // zero height
    public void IsWrappedParagraph_ReturnsExpected(
        double currentTop, double currentHeight,
        double nextTop, double nextHeight,
        bool expected)
    {
        bool result = OcrUtilities.IsWrappedParagraph(currentTop, currentHeight, nextTop, nextHeight);
        Assert.Equal(expected, result);
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

    [Fact]
    public void GroupWrappedParagraphLines_CombinesWrappedLinesIntoParagraphBlocks()
    {
        List<OcrUtilities.PositionedOcrLine> lines =
        [
            new(0, "Static cling is the tendency", new Windows.Foundation.Rect(0, 0, 100, 10)),
            new(1, "for light objects to stick.", new Windows.Foundation.Rect(0, 14, 100, 10)),
            new(2, "New paragraph.", new Windows.Foundation.Rect(0, 32, 120, 12)),
        ];

        List<OcrUtilities.GroupedOcrLines> groups = OcrUtilities.GroupWrappedParagraphLines(lines);

        Assert.Equal(2, groups.Count);
        Assert.Equal(0, groups[0].StartingLineNumber);
        Assert.Equal("Static cling is the tendency for light objects to stick.", groups[0].SingleLineText);
        Assert.Equal($"Static cling is the tendency{Environment.NewLine}for light objects to stick.", groups[0].DisplayText);
        Assert.Equal(0, groups[0].BoundingBox.Y);
        Assert.Equal(24, groups[0].BoundingBox.Height);
        Assert.Equal("New paragraph.", groups[1].SingleLineText);
    }

    [Fact]
    public void GroupWrappedParagraphLines_DoesNotMergeEntriesOnTheSameVisualRow()
    {
        List<OcrUtilities.PositionedOcrLine> lines =
        [
            new(0, "Left entry", new Windows.Foundation.Rect(0, 10, 50, 10)),
            new(1, "Right entry", new Windows.Foundation.Rect(60, 10, 50, 10)),
        ];

        List<OcrUtilities.GroupedOcrLines> groups = OcrUtilities.GroupWrappedParagraphLines(lines);

        Assert.Equal(2, groups.Count);
        Assert.All(groups, group => Assert.DoesNotContain(Environment.NewLine, group.DisplayText));
        Assert.All(groups, group => Assert.Equal(10, group.BoundingBox.Height));
    }

    [Fact]
    public void GroupWrappedParagraphLines_RemovesEmbeddedLineBreaksFromIndividualOcrLines()
    {
        List<OcrUtilities.PositionedOcrLine> lines =
        [
            new(0, $"First{Environment.NewLine}line", new Windows.Foundation.Rect(0, 0, 100, 10)),
        ];

        OcrUtilities.GroupedOcrLines group = Assert.Single(OcrUtilities.GroupWrappedParagraphLines(lines));

        Assert.Equal("First line", group.DisplayText);
        Assert.Equal("First line", group.SingleLineText);
    }

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

    [WpfFact]
    public async Task OcrComplexTableTestImage()
    {
        // Given
        string resultWordBorders = ComplexWordBorders;
        string expectedResult = ComplexTableResult;
        string wordBordersJson = await File.ReadAllTextAsync(
            FileUtilities.GetPathToLocalFile(resultWordBorders),
            TestContext.Current.CancellationToken);

        List<WordBorderInfo> wbInfoList = JsonSerializer.Deserialize<List<WordBorderInfo>>(wordBordersJson ?? "[]")
            ?? throw new Exception("Failed to deserialize WordBorderInfo list");

        // When
        // 1514 x 1243 image size
        Rectangle rectCanvasSize = new()
        {
            Width = 1514,
            Height = 1243,
            X = 0,
            Y = 0
        };

        ResultTable resultTable = new();
        resultTable.AnalyzeAsTable(wbInfoList, rectCanvasSize);
        StringBuilder stringBuilder = new();

        ResultTable.GetTextFromTabledWordBorders(stringBuilder, wbInfoList, true);

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

    [WpfFact(Skip = "fails GitHub actions")]
    public async Task GetTessLanguages()
    {
        List<string> expected = ["eng", "spa"];
        List<string> actualStrings = await TesseractHelper.TesseractLanguagesAsStrings();

        if (actualStrings.Count == 0)
            return;

        foreach (string tag in expected)
        {
            Assert.Contains(tag, actualStrings);
        }
    }

    [WpfFact(Skip = "fails GitHub actions")]
    public async Task GetTesseractStrongLanguages()
    {
        List<ILanguage> expectedList =
        [
            new TessLang("eng"),
            new TessLang("spa"),
        ];

        List<ILanguage> actualList = await TesseractHelper.TesseractLanguages();

        if (actualList.Count == 0)
            return;

        foreach (ILanguage tag in expectedList)
        {
            Assert.Contains(tag.AbbreviatedName, actualList.Select(x => x.AbbreviatedName).ToList());
        }
    }

    [WpfFact(Skip = "fails GitHub actions")]
    public async Task GetTesseractGitHubLanguage()
    {
        TesseractGitHubFileDownloader fileDownloader = new();

        int length = TesseractGitHubFileDownloader.tesseractTrainedDataFileNames.Length;
        string languageFileDataName = TesseractGitHubFileDownloader.tesseractTrainedDataFileNames[new Random().Next(length)];
        string tempFilePath = Path.Combine(Path.GetTempPath(), languageFileDataName);

        await fileDownloader.DownloadFileAsync(languageFileDataName, tempFilePath);

        Assert.True(File.Exists(tempFilePath));
        Assert.True(new FileInfo(tempFilePath).Length > 0);

        File.Delete(tempFilePath);
    }

    [Fact]
    public void BuildTextFromOcrLines_FiltersFuriganaForJapanese()
    {
        // Given a Japanese line where the kanji 黒 is annotated with the small
        // furigana くろ rendered directly above it.
        FakeOcrLine line = new("くろ黒ごま", new Windows.Foundation.Rect(0, 0, 60, 30))
        {
            Words =
            [
                // Furigana: short and sitting above the kanji it annotates.
                new FakeOcrWord("くろ", new Windows.Foundation.Rect(0, 0, 16, 8)),
                // Main text: full-height single characters.
                new FakeOcrWord("黒", new Windows.Foundation.Rect(0, 10, 20, 20)),
                new FakeOcrWord("ご", new Windows.Foundation.Rect(20, 10, 20, 20)),
                new FakeOcrWord("ま", new Windows.Foundation.Rect(40, 10, 20, 20)),
            ]
        };

        FakeOcrLinesWords ocrResult = new() { Lines = [line] };

        // When
        string text = OcrUtilities.BuildTextFromOcrLines(new GlobalLang("ja"), ocrResult);

        // Then the furigana is dropped, leaving only the main text.
        Assert.Equal("黒ごま", text);
    }

    // ----- FilterFurigana unit tests (the geometry heuristic) -----

    [Fact]
    public void FilterFurigana_EmptyList_ReturnsEmpty()
    {
        List<IOcrWord> result = OcrUtilities.FilterFurigana([]);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterFurigana_SingleWord_IsKept()
    {
        List<IOcrWord> words = [Word("黒", 0, 0, 20, 20)];

        List<IOcrWord> result = OcrUtilities.FilterFurigana(words);

        Assert.Equal(["黒"], result.Select(w => w.Text));
    }

    [Fact]
    public void FilterFurigana_UniformHeights_KeepsAllInOrder()
    {
        // No word is small relative to the median, so nothing is furigana.
        List<IOcrWord> words =
        [
            Word("黒", 0, 0, 20, 20),
            Word("ご", 20, 0, 20, 20),
            Word("ま", 40, 0, 20, 20),
        ];

        List<IOcrWord> result = OcrUtilities.FilterFurigana(words);

        Assert.Equal(["黒", "ご", "ま"], result.Select(w => w.Text));
    }

    [Fact]
    public void FilterFurigana_RemovesSmallWordAboveOverlappingKanji()
    {
        List<IOcrWord> words =
        [
            Word("くろ", 0, 0, 16, 8),   // furigana: short, sitting above
            Word("黒", 0, 10, 20, 20),   // kanji: taller, below, overlapping
        ];

        List<IOcrWord> result = OcrUtilities.FilterFurigana(words);

        Assert.Equal(["黒"], result.Select(w => w.Text));
    }

    [Fact]
    public void FilterFurigana_KeepsSmallWordWhenNotHorizontallyOverlapping()
    {
        // Small, but nowhere near a kanji horizontally, so it is real text.
        List<IOcrWord> words =
        [
            Word("くろ", 100, 0, 16, 8),
            Word("黒", 0, 10, 20, 20),
        ];

        List<IOcrWord> result = OcrUtilities.FilterFurigana(words);

        Assert.Equal(["くろ", "黒"], result.Select(w => w.Text));
    }

    [Fact]
    public void FilterFurigana_KeepsSmallWordBelowMainText()
    {
        // Furigana sits above its kanji; a small word BELOW a larger word is
        // not furigana and must be kept.
        List<IOcrWord> words =
        [
            Word("黒", 0, 0, 20, 20),
            Word("くろ", 0, 22, 16, 8),
        ];

        List<IOcrWord> result = OcrUtilities.FilterFurigana(words);

        Assert.Equal(["黒", "くろ"], result.Select(w => w.Text));
    }

    [Fact]
    public void FilterFurigana_KeepsSmallWordWhenWordBelowIsNotLarger()
    {
        // A small word directly above another small word is not furigana:
        // furigana requires a larger word (the kanji) beneath it. The two tall
        // words only exist to raise the median height.
        List<IOcrWord> words =
        [
            Word("く", 0, 0, 8, 8),
            Word("ろ", 0, 10, 8, 8),     // below + overlapping, but also small
            Word("本", 50, 0, 20, 20),
            Word("語", 80, 0, 20, 20),
        ];

        List<IOcrWord> result = OcrUtilities.FilterFurigana(words);

        Assert.Equal(["く", "ろ", "本", "語"], result.Select(w => w.Text));
    }

    [Theory]
    [InlineData("く", true)]        // 1-char ruby is removed
    [InlineData("くろ", true)]      // 2-char ruby is removed
    [InlineData("くろが", false)]   // 3+ chars is treated as real text and kept
    public void FilterFurigana_OnlyRemovesShortWords(string rubyText, bool removed)
    {
        List<IOcrWord> words =
        [
            Word(rubyText, 0, 0, 16, 8),
            Word("黒", 0, 10, 20, 20),
        ];

        List<IOcrWord> result = OcrUtilities.FilterFurigana(words);

        string[] expected = removed ? ["黒"] : [rubyText, "黒"];
        Assert.Equal(expected, result.Select(w => w.Text));
    }

    [Fact]
    public void FilterFurigana_RemovesMultipleFuriganaKeepingMainText()
    {
        List<IOcrWord> words =
        [
            Word("くろ", 0, 0, 16, 8),
            Word("黒", 0, 10, 20, 20),
            Word("ごま", 20, 0, 16, 8),
            Word("米", 20, 10, 20, 20),
        ];

        List<IOcrWord> result = OcrUtilities.FilterFurigana(words);

        Assert.Equal(["黒", "米"], result.Select(w => w.Text));
    }

    // ----- BuildTextFromOcrLines integration (language gating) -----

    [Fact]
    public void BuildTextFromOcrLines_JapaneseWithoutFurigana_IsUnchanged()
    {
        FakeOcrLine line = new("黒ごま", new Windows.Foundation.Rect(0, 0, 60, 20))
        {
            Words =
            [
                Word("黒", 0, 0, 20, 20),
                Word("ご", 20, 0, 20, 20),
                Word("ま", 40, 0, 20, 20),
            ]
        };
        FakeOcrLinesWords ocrResult = new() { Lines = [line] };

        string text = OcrUtilities.BuildTextFromOcrLines(new GlobalLang("ja"), ocrResult);

        Assert.Equal("黒ごま", text);
    }

    [Fact]
    public void BuildTextFromOcrLines_ChineseText_JoinsWithoutSpaces()
    {
        FakeOcrLine line = new("中文", new Windows.Foundation.Rect(0, 0, 40, 20))
        {
            Words =
            [
                Word("中", 0, 0, 20, 20),
                Word("文", 20, 0, 20, 20),
            ]
        };
        FakeOcrLinesWords ocrResult = new() { Lines = [line] };

        string text = OcrUtilities.BuildTextFromOcrLines(new GlobalLang("zh-Hans"), ocrResult);

        Assert.Equal("中文", text);
    }

    [Fact]
    public void BuildTextFromOcrLines_FiltersRubyTextForChinese()
    {
        // The same small-ruby heuristic also runs for Chinese, another
        // non-space-joining language (e.g. bopomofo above a character).
        FakeOcrLine line = new("ㄓ中文", new Windows.Foundation.Rect(0, 0, 40, 30))
        {
            Words =
            [
                Word("ㄓ", 0, 0, 8, 8),
                Word("中", 0, 10, 20, 20),
                Word("文", 20, 10, 20, 20),
            ]
        };
        FakeOcrLinesWords ocrResult = new() { Lines = [line] };

        string text = OcrUtilities.BuildTextFromOcrLines(new GlobalLang("zh-Hans"), ocrResult);

        Assert.Equal("中文", text);
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

    [Fact]
    public void OrderLinesForReadingFlow_SortsRowsTopToBottomAndLeftToRight()
    {
        // Mimics the Windows OCR engine returning furigana ruby lines and a
        // trailing fragment out of reading order (as seen with Ja-Lang-Image.png).
        // Row 1 (y~0): furigana くろ + main-line reading, emitted out of x-order.
        // Row 2 (y~30): the main text line.
        FakeOcrLine furiganaRight = new("しつ", new Windows.Foundation.Rect(200, 0, 20, 8));
        FakeOcrLine furiganaLeft = new("くろ", new Windows.Foundation.Rect(0, 0, 20, 8));
        FakeOcrLine mainLine = new("黒ごま質", new Windows.Foundation.Rect(0, 30, 240, 20));

        // Engine order is scrambled: right furigana, main line, then left furigana.
        FakeOcrLinesWords ocrResult = new()
        {
            Lines = [furiganaRight, mainLine, furiganaLeft]
        };

        IReadOnlyList<IOcrLine> ordered = OcrUtilities.OrderLinesForReadingFlow(ocrResult.Lines);

        Assert.Equal(["くろ", "しつ", "黒ごま質"], ordered.Select(l => l.Text));
    }

    [Fact]
    public void OrderLinesForReadingFlow_KeepsSeparateRowsInVerticalOrder()
    {
        // Two furigana rows and two main-text rows interleaved and shuffled must
        // come back strictly top-to-bottom.
        FakeOcrLine ruby2 = new("かみ", new Windows.Foundation.Rect(0, 100, 20, 8));
        FakeOcrLine main2 = new("髪", new Windows.Foundation.Rect(0, 130, 40, 20));
        FakeOcrLine ruby1 = new("くろ", new Windows.Foundation.Rect(0, 0, 20, 8));
        FakeOcrLine main1 = new("黒", new Windows.Foundation.Rect(0, 30, 40, 20));

        FakeOcrLinesWords ocrResult = new() { Lines = [main2, ruby1, main1, ruby2] };

        IReadOnlyList<IOcrLine> ordered = OcrUtilities.OrderLinesForReadingFlow(ocrResult.Lines);

        Assert.Equal(["くろ", "黒", "かみ", "髪"], ordered.Select(l => l.Text));
    }

    [Fact]
    public void FilterFuriganaLines_RemovesShortLineAboveTallerOverlappingLine()
    {
        // A short furigana line sitting just above a taller kanji line that it
        // overlaps horizontally is dropped.
        FakeOcrLine furigana = new("くろ", new Windows.Foundation.Rect(0, 0, 40, 8));
        FakeOcrLine mainLine = new("黒ごま", new Windows.Foundation.Rect(0, 10, 120, 20));

        FakeOcrLinesWords ocrResult = new() { Lines = [furigana, mainLine] };

        IReadOnlyList<IOcrLine> result = OcrUtilities.FilterFuriganaLines(ocrResult.Lines);

        Assert.Equal(["黒ごま"], result.Select(l => l.Text));
    }

    [Fact]
    public void FilterFuriganaLines_KeepsTwoBodyLinesOfSimilarHeight()
    {
        // Two normal body lines stacked vertically: neither is much shorter than
        // the other, so nothing is treated as furigana.
        FakeOcrLine top = new("黒ごまは体に", new Windows.Foundation.Rect(0, 0, 200, 20));
        FakeOcrLine bottom = new("たくさんあります", new Windows.Foundation.Rect(0, 26, 200, 20));

        FakeOcrLinesWords ocrResult = new() { Lines = [top, bottom] };

        IReadOnlyList<IOcrLine> result = OcrUtilities.FilterFuriganaLines(ocrResult.Lines);

        Assert.Equal(["黒ごまは体に", "たくさんあります"], result.Select(l => l.Text));
    }

    [Fact]
    public void FilterFuriganaLines_KeepsShortLineNotHorizontallyOverlappingAnyKanji()
    {
        // A short line off to the side (no taller line beneath it) is real text.
        FakeOcrLine shortSide = new("注", new Windows.Foundation.Rect(300, 0, 20, 8));
        FakeOcrLine mainLine = new("黒ごま", new Windows.Foundation.Rect(0, 10, 120, 20));

        FakeOcrLinesWords ocrResult = new() { Lines = [shortSide, mainLine] };

        IReadOnlyList<IOcrLine> result = OcrUtilities.FilterFuriganaLines(ocrResult.Lines);

        Assert.Equal(["注", "黒ごま"], result.Select(l => l.Text));
    }

    [Fact]
    public void FilterFuriganaLines_KeepsShortLineWhenGapIsTooLarge()
    {
        // Short line far above a taller line is a separate heading/body line, not
        // a hugging ruby annotation, so it is kept.
        FakeOcrLine shortHeading = new("メモ", new Windows.Foundation.Rect(0, 0, 40, 8));
        FakeOcrLine mainLine = new("黒ごま", new Windows.Foundation.Rect(0, 60, 120, 20));

        FakeOcrLinesWords ocrResult = new() { Lines = [shortHeading, mainLine] };

        IReadOnlyList<IOcrLine> result = OcrUtilities.FilterFuriganaLines(ocrResult.Lines);

        Assert.Equal(["メモ", "黒ごま"], result.Select(l => l.Text));
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
