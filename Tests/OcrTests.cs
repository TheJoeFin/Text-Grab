using Text_Grab.Utilities;

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