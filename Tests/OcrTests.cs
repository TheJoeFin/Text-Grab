using System;
using System.Drawing;
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

        string testImagePath = @"C:\Users\jfinney\Documents\Text Grab\Text-Grab\Text-Grab\Images\font_sample.png";

        // When
        string ocrTextResult = await ImageMethods.OcrAbsoluteFilePath(testImagePath);

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

        string testImagePath = @"C:\Users\jfinney\Documents\Text Grab\Text-Grab\Text-Grab\Images\FontTest.png";

        // When
        string ocrTextResult = await ImageMethods.OcrAbsoluteFilePath(testImagePath);

        // Then
        Assert.Equal(expectedResult, ocrTextResult);
    }
}