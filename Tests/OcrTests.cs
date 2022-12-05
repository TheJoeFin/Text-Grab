using System.Drawing;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;
using Xunit;

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
        string ocrTextResult = await OcrExtensions.OcrAbsoluteFilePath(getPathToImages(testImagePath));

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
        string ocrTextResult = await OcrExtensions.OcrAbsoluteFilePath(getPathToImages(testImagePath));

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
December 	 12 	 Winter";


        string testImagePath = @".\Images\Table-Test.png";
        Uri uri = new Uri(testImagePath, UriKind.Relative);
        Language englishLanguage = new("en-US");
        Bitmap testBitmap = new(getPathToImages(testImagePath));
        // When
        OcrResult ocrResult = await OcrExtensions.GetOcrResultFromBitmap(testBitmap, englishLanguage);

        DpiScale dpi = new(1,1);
        System.Drawing.Rectangle rectCanvasSize = new System.Drawing.Rectangle
        {
            Width = 1132,
            Height = 1158,
            X = 0,
            Y = 0
        };

        List<WordBorder> wordBorders = new();
        int lineNumber = 0;

        foreach (OcrLine ocrLine in ocrResult.Lines)
        {
            double top = ocrLine.Words.Select(x => x.BoundingRect.Top).Min();
            double bottom = ocrLine.Words.Select(x => x.BoundingRect.Bottom).Max();
            double left = ocrLine.Words.Select(x => x.BoundingRect.Left).Min();
            double right = ocrLine.Words.Select(x => x.BoundingRect.Right).Max();

            Rect lineRect = new()
            {
                X = left,
                Y = top,
                Width = Math.Abs(right - left),
                Height = Math.Abs(bottom - top)
            };

            StringBuilder lineText = new();
            ocrLine.GetTextFromOcrLine(true, lineText);

            WordBorder wordBorderBox = new()
            {
                Width = lineRect.Width / (dpi.DpiScaleX),
                Height = lineRect.Height / (dpi.DpiScaleY),
                Top = lineRect.Y,
                Left = lineRect.X,
                Word = lineText.ToString().Trim(),
                ToolTip = ocrLine.Text,
                LineNumber = lineNumber,
            };
            wordBorders.Add(wordBorderBox);

            lineNumber++;
        }

        ResultTable resultTable = new();
        resultTable.AnalyzeAsTable(wordBorders, rectCanvasSize);

        StringBuilder stringBuilder = new();

        ResultTable.GetTextFromTabledWordBorders(stringBuilder, wordBorders, true);

        // Then
        Assert.Equal(expectedResult, stringBuilder.ToString());

    }

    private string getPathToImages(string imageRelativePath)
    {
        Uri codeBaseUrl = new(System.AppDomain.CurrentDomain.BaseDirectory);
        string codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
        string? dirPath = Path.GetDirectoryName(codeBasePath);

        if (dirPath is null)
            dirPath = "";

        return Path.Combine(dirPath, imageRelativePath);
    }
}