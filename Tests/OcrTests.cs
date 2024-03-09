using System.Drawing;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using Text_Grab;
using Text_Grab.Controls;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;

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
        Bitmap testBitmap = new(FileUtilities.GetPathToLocalFile(testImagePath));
        // When
        OcrResult ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(testBitmap, EnglishLanguage);

        DpiScale dpi = new(1, 1);
        Rectangle rectCanvasSize = new()
        {
            Width = 1132,
            Height = 1158,
            X = 0,
            Y = 0
        };

        List<WordBorder> wordBorders = ResultTable.ParseOcrResultIntoWordBorders(ocrResult, dpi);

        ResultTable resultTable = new();
        resultTable.AnalyzeAsTable(wordBorders, rectCanvasSize);

        StringBuilder stringBuilder = new();

        ResultTable.GetTextFromTabledWordBorders(stringBuilder, wordBorders, true);

        // Then
        Assert.Equal(expectedResult, stringBuilder.ToString());

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
        Bitmap testBitmap = new(FileUtilities.GetPathToLocalFile(testImagePath));
        // When
        OcrResult ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(testBitmap, EnglishLanguage);

        DpiScale dpi = new(1, 1);
        Rectangle rectCanvasSize = new()
        {
            Width = 1152,
            Height = 1132,
            X = 0,
            Y = 0
        };

        List<WordBorder> wordBorders = ResultTable.ParseOcrResultIntoWordBorders(ocrResult, dpi);

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
        int intialLinesToSkip = 12;

        // Given
        string hocrFilePath = FileUtilities.GetPathToLocalFile(@"TextFiles\font_sample.hocr");
        string[] hocrFileContentsArray = await File.ReadAllLinesAsync(hocrFilePath);

        // combine string array into one string
        StringBuilder sb = new();
        foreach (string line in hocrFileContentsArray.Skip(intialLinesToSkip).ToArray())
            sb.AppendLine(line);

        string hocrFileContents = sb.ToString();

        string testImagePath = fontSamplePath;
        // need to scale to get the test to match the output
        // Bitmap scaledBMP = ImageMethods
        Uri fileURI = new(FileUtilities.GetPathToLocalFile(testImagePath), UriKind.Absolute);
        BitmapImage bmpImg = new(fileURI);
        bmpImg.Freeze();
        Bitmap bmp = ImageMethods.BitmapImageToBitmap(bmpImg);
        Language language = LanguageUtilities.GetOCRLanguage();
        double idealScaleFactor = await OcrUtilities.GetIdealScaleFactorForOcrAsync(bmp, language);
        Bitmap scaledBMP = ImageMethods.ScaleBitmapUniform(bmp, idealScaleFactor);

        // When
        Language englishLanguage = new("en-US");
        OcrOutput tessoutput = await TesseractHelper.GetOcrOutputFromBitmap(scaledBMP, englishLanguage);

        string[] tessoutputArray = tessoutput.RawOutput.Split(Environment.NewLine);
        StringBuilder sb2 = new();
        foreach (string line in tessoutputArray.Skip(intialLinesToSkip).ToArray())
            sb2.AppendLine(line);

        tessoutput.RawOutput = sb2.ToString();

        // Then
        Assert.Equal(hocrFileContents, tessoutput.RawOutput);
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
        Language language = LanguageUtilities.GetOCRLanguage();
        double idealScaleFactor = await OcrUtilities.GetIdealScaleFactorForOcrAsync(bmp, language);
        Bitmap scaledBMP = ImageMethods.ScaleBitmapUniform(bmp, idealScaleFactor);

        // When
        Language englishLanguage = new("eng");
        OcrOutput tessoutput = await TesseractHelper.GetOcrOutputFromBitmap(scaledBMP, englishLanguage, englishLanguage.LanguageTag);

        if (tessoutput.RawOutput == "Cannot find tesseract.exe")
            return;

        // Then
        Assert.Equal(fontSampleResultForTesseract, tessoutput.RawOutput);
    }

    [WpfFact(Skip = "fails GitHub actions")]
    public async Task GetTessLanguages()
    {
        List<string> expected = new() { "eng", "spa" };
        List<string> actualStrings = await TesseractHelper.TesseractLanguagesAsStrings();

        if (actualStrings.Count == 0)
            return;

        foreach (string tag in expected)
        {
            Assert.Contains(tag, actualStrings);
        }
    }
    
    [WpfFact(Skip ="fails GitHub actions")]
    public async Task GetTesseractStrongLanguages()
    {
        List<ILanguage> expectedList = new()
        {
            new TessLang("eng"),
            new TessLang("spa"),
        };

        List<ILanguage> actualList = await TesseractHelper.TesseractLanguages();

        if (actualList.Count == 0)
            return;

        foreach (ILanguage tag in expectedList)
        {
            Assert.Contains(tag.AbbreviatedName, actualList.Select(x => x.AbbreviatedName).ToList());
        }
    }

    [WpfFact]
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
}