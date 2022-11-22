using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Media.Imaging;
using Text_Grab;

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
        string ocrTextResult = await ImageMethods.OcrAbsoluteFilePath(getPathToImages(testImagePath));

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
        string ocrTextResult = await ImageMethods.OcrAbsoluteFilePath(getPathToImages(testImagePath));

        // Then
        Assert.Equal(expectedResult, ocrTextResult);
    }

    private string getPathToImages(string imageRelativePath)
    {
        var codeBaseUrl = new Uri(System.AppDomain.CurrentDomain.BaseDirectory);
        var codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
        var dirPath = Path.GetDirectoryName(codeBasePath);
        return Path.Combine(dirPath, imageRelativePath);
    }
}