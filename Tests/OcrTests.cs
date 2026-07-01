using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Text_Grab;
using Text_Grab.Interfaces;
using Text_Grab.Models;
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

    [WpfFact]
    public async Task OcrFontSampleImage()
    {
        // Given
        string testImagePath = fontSamplePath;

        // When
        string ocrTextResult = await OcrUtilities.OcrAbsoluteFilePathAsync(FileUtilities.GetPathToLocalFile(testImagePath));

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
        string ocrTextResult = await OcrUtilities.OcrAbsoluteFilePathAsync(FileUtilities.GetPathToLocalFile(testImagePath));

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
        IOcrLinesWords ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(testBitmap, globalLang);

        DpiScale dpi = new(1, 1);
        Rectangle rectCanvasSize = new()
        {
            Width = 1132,
            Height = 1158,
            X = 0,
            Y = 0
        };

        List<WordBorderInfo> wordBorders = ResultTable.ParseOcrResultIntoWordBorderInfos(ocrResult, dpi);

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
            string ocrTextResult = await OcrUtilities.OcrAbsoluteFilePathAsync(FileUtilities.GetPathToLocalFile(testImagePath));

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
    [InlineData(10, 10, 10, 10, true)]   // overlapping bounding boxes
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

    [WpfFact]
    public async Task ReadQrCode()
    {
        string expectedResult = "This is a test of the QR Code system";

        string testImagePath = @".\Images\QrCodeTestImage.png";
        Uri uri = new(testImagePath, UriKind.Relative);
        // When
        string ocrTextResult = await OcrUtilities.OcrAbsoluteFilePathAsync(FileUtilities.GetPathToLocalFile(testImagePath));

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
        IOcrLinesWords ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(testBitmap, globalLang);

        DpiScale dpi = new(1, 1);
        Rectangle rectCanvasSize = new()
        {
            Width = 1152,
            Height = 1132,
            X = 0,
            Y = 0
        };

        List<WordBorderInfo> wordBorders = ResultTable.ParseOcrResultIntoWordBorderInfos(ocrResult, dpi);

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
        string wordBordersJson = await File.ReadAllTextAsync(FileUtilities.GetPathToLocalFile(resultWordBorders));

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
        double idealScaleFactor = await OcrUtilities.GetIdealScaleFactorForOcrAsync(bmp, language);
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
        double idealScaleFactor = await OcrUtilities.GetIdealScaleFactorForOcrAsync(bmp, language);
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
}
