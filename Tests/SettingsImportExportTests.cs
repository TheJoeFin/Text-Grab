using System.IO;
using Text_Grab.Properties;
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
        Assert.True(zipPath.EndsWith(".zip"));

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
        Assert.Contains("firstRun", jsonContent.ToLower()); // Check for at least one setting

        // Clean up
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }

    [WpfFact]
    public async Task CanRoundTripSettings()
    {
        // Arrange - Save original setting value
        Settings settings = AppUtilities.TextGrabSettings;
        bool originalShowToast = settings.ShowToast;
        
        // Change a setting
        settings.ShowToast = !originalShowToast;
        settings.Save();

        // Export settings
        string zipPath = await SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory: false);

        // Change setting again
        settings.ShowToast = originalShowToast;
        settings.Save();

        // Act - Import settings
        await SettingsImportExportUtilities.ImportSettingsFromZipAsync(zipPath);

        // Assert - Verify setting was restored to exported value
        settings.Reload();
        Assert.Equal(!originalShowToast, settings.ShowToast);

        // Clean up
        settings.ShowToast = originalShowToast;
        settings.Save();
        if (File.Exists(zipPath))
            File.Delete(zipPath);
    }
}
