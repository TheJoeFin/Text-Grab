using System.Drawing;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using Text_Grab;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Tests;

public class OcrTests
{
    [Fact]
    public async Task OcrFontSampleImage()
    {
        // Given
        string expectedResult = @"Times-Roman
Helvetica
Courier
Palatino-Roman
Helvetica-Narrow
Bookman-Demi
";

        string testImagePath = @".\Images\font_sample.png";

        // When
        string ocrTextResult = await OcrExtensions.OcrAbsoluteFilePath(getPathToLocalFile(testImagePath));

        // Then
        Assert.Equal(expectedResult, ocrTextResult);
    }

    [Fact]
    public async Task OcrFontTestImage()
    {
        // Given
        string expectedResult = @"Arial
Times New Roman
Georgia
Segoe
Rockwell Condensed
Couier New
";

        string testImagePath = @".\Images\FontTest.png";
        Uri uri = new Uri(testImagePath, UriKind.Relative);
        // When
        string ocrTextResult = await OcrExtensions.OcrAbsoluteFilePath(getPathToLocalFile(testImagePath));

        // Then
        Assert.Equal(expectedResult, ocrTextResult);
    }

    [WpfFact]
    public async Task AnalyzeTable()
    {
        string expectedResult = @"Month	Int	Season
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


        string testImagePath = @".\Images\Table-Test.png";
        Uri uri = new Uri(testImagePath, UriKind.Relative);
        Language englishLanguage = new("en-US");
        Bitmap testBitmap = new(getPathToLocalFile(testImagePath));
        // When
        OcrResult ocrResult = await OcrExtensions.GetOcrResultFromBitmap(testBitmap, englishLanguage);

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
        Uri uri = new Uri(testImagePath, UriKind.Relative);
        Language englishLanguage = new("en-US");
        Bitmap testBitmap = new(getPathToLocalFile(testImagePath));
        // When
        OcrResult ocrResult = await OcrExtensions.GetOcrResultFromBitmap(testBitmap, englishLanguage);

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

    [WpfFact]
    public async Task TesseractHocr()
    {
        int intialLinesToSkip = 12;

        // Given
        string hocrFilePath = getPathToLocalFile(@"TextFiles\font_sample.hocr");
        string[] hocrFileContentsArray = await File.ReadAllLinesAsync(hocrFilePath);

        // combine string array into one string
        StringBuilder sb = new();
        foreach (string line in hocrFileContentsArray.Skip(intialLinesToSkip).ToArray())
            sb.AppendLine(line);

        string hocrFileContents = sb.ToString();


        string testImagePath = @"Images\font_sample.png";
        // need to scale to get the test to match the output
        // Bitmap scaledBMP = ImageMethods
        Uri fileURI = new(getPathToLocalFile(testImagePath), UriKind.Absolute);
        BitmapImage bmpImg = new(fileURI);
        bmpImg.Freeze();
        Bitmap bmp = ImageMethods.BitmapImageToBitmap(bmpImg);
        Language language = LanguageUtilities.GetOCRLanguage();
        double idealScaleFactor = await OcrExtensions.GetIdealScaleFactorForOCR(bmp, language);
        Bitmap scaledBMP = ImageMethods.ScaleBitmapUniform(bmp, idealScaleFactor);

        // When
        string tessoutput = await TesseractHelper.GetTextFromBitmap(scaledBMP, true);

        string[] tessoutputArray = tessoutput.Split(Environment.NewLine);
        StringBuilder sb2 = new();
        foreach (string line in tessoutputArray.Skip(intialLinesToSkip).ToArray())
            sb2.AppendLine(line);

        tessoutput = sb2.ToString();

        // Then
        Assert.Equal(hocrFileContents, tessoutput);
    }

    private string getPathToLocalFile(string imageRelativePath)
    {
        Uri codeBaseUrl = new(System.AppDomain.CurrentDomain.BaseDirectory);
        string codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
        string? dirPath = Path.GetDirectoryName(codeBasePath);

        if (dirPath is null)
            dirPath = "";

        return Path.Combine(dirPath, imageRelativePath);
    }
}