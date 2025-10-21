using System.IO;
using Text_Grab.Utilities;

namespace Tests;

public class SettingsImportExportTests
{
    [WpfFact]
    public async Task CanExportSettingsWithoutHistory()
    {
        // Act
        string zipPath = await SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory: false);

        // Assert
        Assert.False(string.IsNullOrEmpty(zipPath));
        Assert.True(File.Exists(zipPath));
        Assert.EndsWith(".zip", zipPath);

        // Clean up
        if (File.Exists(zipPath))
            File.Delete(zipPath);
    }

    [WpfFact]
    public async Task ExportedZipContainsSettingsJson()
    {
        // Arrange
        string zipPath = await SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory: false);

        // Act
        string tempDir = Path.Combine(Path.GetTempPath(), $"TextGrab_Test_{Guid.NewGuid()}");
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);

        // Assert
        string settingsJsonPath = Path.Combine(tempDir, "settings.json");
        Assert.True(File.Exists(settingsJsonPath));

        string jsonContent = await File.ReadAllTextAsync(settingsJsonPath);
        Assert.False(string.IsNullOrEmpty(jsonContent));
        // Check that JSON contains some setting keys (any of the common settings)
        bool containsSettings = jsonContent.Contains("showtoast") || 
                                jsonContent.Contains("firstrun") || 
                                jsonContent.Contains("correcterrors");
        Assert.True(containsSettings, "Exported JSON should contain at least one settings property");

        // Clean up
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }
}
