using Text_Grab.Utilities;

namespace Tests;

public class ContextMenuTests
{
    [Fact]
    public void GetShellKeyPath_ReturnsCorrectPath_ForPngExtension()
    {
        // Arrange
        string extension = ".png";
        string expectedPath = @"Software\Classes\SystemFileAssociations\.png\shell\Text-Grab.GrabText";

        // Act - Use reflection to access private method for testing
        var method = typeof(ContextMenuUtilities).GetMethod(
            "GetShellKeyPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        string? result = method?.Invoke(null, [extension]) as string;

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetShellKeyPath_ReturnsCorrectPath_ForJpgExtension()
    {
        // Arrange
        string extension = ".jpg";
        string expectedPath = @"Software\Classes\SystemFileAssociations\.jpg\shell\Text-Grab.GrabText";

        // Act - Use reflection to access private method for testing
        var method = typeof(ContextMenuUtilities).GetMethod(
            "GetShellKeyPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        string? result = method?.Invoke(null, [extension]) as string;

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void ImageExtensions_ContainsExpectedFormats()
    {
        // Arrange - Use reflection to access private field
        var field = typeof(ContextMenuUtilities).GetField(
            "ImageExtensions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        string[]? extensions = field?.GetValue(null) as string[];

        // Assert
        Assert.NotNull(extensions);
        Assert.Contains(".png", extensions);
        Assert.Contains(".jpg", extensions);
        Assert.Contains(".jpeg", extensions);
        Assert.Contains(".bmp", extensions);
        Assert.Contains(".gif", extensions);
        Assert.Contains(".tiff", extensions);
        Assert.Contains(".tif", extensions);
    }
}
