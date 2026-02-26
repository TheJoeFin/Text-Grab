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

        // Act
        string result = ContextMenuUtilities.GetShellKeyPath(extension);

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetShellKeyPath_ReturnsCorrectPath_ForJpgExtension()
    {
        // Arrange
        string extension = ".jpg";
        string expectedPath = @"Software\Classes\SystemFileAssociations\.jpg\shell\Text-Grab.GrabText";

        // Act
        string result = ContextMenuUtilities.GetShellKeyPath(extension);

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetShellKeyPath_ReturnsCorrectPath_ForTiffExtension()
    {
        // Arrange
        string extension = ".tiff";
        string expectedPath = @"Software\Classes\SystemFileAssociations\.tiff\shell\Text-Grab.GrabText";

        // Act
        string result = ContextMenuUtilities.GetShellKeyPath(extension);

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetShellKeyPath_ReturnsConsistentFormat()
    {
        // Act
        string pngPath = ContextMenuUtilities.GetShellKeyPath(".png");
        string jpgPath = ContextMenuUtilities.GetShellKeyPath(".jpg");

        // Assert - Both should follow the same pattern
        Assert.StartsWith(@"Software\Classes\SystemFileAssociations\", pngPath);
        Assert.StartsWith(@"Software\Classes\SystemFileAssociations\", jpgPath);
        Assert.EndsWith(@"\shell\Text-Grab.GrabText", pngPath);
        Assert.EndsWith(@"\shell\Text-Grab.GrabText", jpgPath);
    }
}
