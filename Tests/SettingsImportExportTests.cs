using System.IO;
using System.Text.Json;
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
        bool containsSettings = jsonContent.Contains("ShowToast") ||
                                jsonContent.Contains("FirstRun") ||
                                jsonContent.Contains("CorrectErrors");
        Assert.True(containsSettings, "Exported JSON should contain at least one settings property");

        // Clean up
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }

    [WpfFact]
    public async Task RoundTripSettingsExportImportPreservesAllValues()
    {
        // Step 1: Export current settings to get baseline
        string originalZipPath = await SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory: false);

        // Step 2: Extract and read the original JSON
        string originalTempDir = Path.Combine(Path.GetTempPath(), $"TextGrab_Original_{Guid.NewGuid()}");
        System.IO.Compression.ZipFile.ExtractToDirectory(originalZipPath, originalTempDir);
        string originalJsonPath = Path.Combine(originalTempDir, "settings.json");
        string originalJson = await File.ReadAllTextAsync(originalJsonPath);

        // Step 3: Deserialize to dictionary to get all key-value pairs
        Dictionary<string, JsonElement>? originalSettings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(originalJson);
        Assert.NotNull(originalSettings);
        Assert.NotEmpty(originalSettings);

        // Step 4: Modify one value in the JSON to simulate a change
        Dictionary<string, JsonElement> modifiedSettings = new(originalSettings);

        // Find a boolean setting to flip (e.g., ShowToast or FirstRun)
        string keyToModify = originalSettings.Keys.FirstOrDefault(k =>
            k.Equals("ShowToast", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("FirstRun", StringComparison.OrdinalIgnoreCase)) ?? originalSettings.Keys.First();

        // If it's a boolean, flip it; otherwise just ensure it has a different value
        if (originalSettings[keyToModify].ValueKind == JsonValueKind.True)
        {
            modifiedSettings[keyToModify] = JsonSerializer.SerializeToElement(false);
        }
        else if (originalSettings[keyToModify].ValueKind == JsonValueKind.False)
        {
            modifiedSettings[keyToModify] = JsonSerializer.SerializeToElement(true);
        }
        else if (originalSettings[keyToModify].ValueKind == JsonValueKind.String)
        {
            modifiedSettings[keyToModify] = JsonSerializer.SerializeToElement("TestModifiedValue");
        }

        // Step 5: Serialize modified settings and save to a new zip
        string modifiedJson = JsonSerializer.Serialize(modifiedSettings, new JsonSerializerOptions { WriteIndented = true });
        string modifiedTempDir = Path.Combine(Path.GetTempPath(), $"TextGrab_Modified_{Guid.NewGuid()}");
        Directory.CreateDirectory(modifiedTempDir);
        string modifiedJsonPath = Path.Combine(modifiedTempDir, "settings.json");
        await File.WriteAllTextAsync(modifiedJsonPath, modifiedJson);

        string modifiedZipPath = Path.Combine(Path.GetTempPath(), $"TextGrab_Modified_{Guid.NewGuid()}.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(modifiedTempDir, modifiedZipPath);

        // Step 6: Import the modified settings
        await SettingsImportExportUtilities.ImportSettingsFromZipAsync(modifiedZipPath);

        // Step 7: Export again to get the imported settings
        string reimportedZipPath = await SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory: false);

        // Step 8: Extract and compare
        string reimportedTempDir = Path.Combine(Path.GetTempPath(), $"TextGrab_Reimported_{Guid.NewGuid()}");
        System.IO.Compression.ZipFile.ExtractToDirectory(reimportedZipPath, reimportedTempDir);
        string reimportedJsonPath = Path.Combine(reimportedTempDir, "settings.json");
        string reimportedJson = await File.ReadAllTextAsync(reimportedJsonPath);

        Dictionary<string, JsonElement>? reimportedSettings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(reimportedJson);
        Assert.NotNull(reimportedSettings);

        // Step 9: Compare element by element - the modified value should match what we set
        foreach (KeyValuePair<string, JsonElement> kvp in modifiedSettings)
        {
            Assert.True(reimportedSettings.ContainsKey(kvp.Key),
                $"Reimported settings missing key: {kvp.Key}");

            // Compare the JSON elements
            string expectedValue = kvp.Value.ToString();
            string actualValue = reimportedSettings[kvp.Key].ToString();

            Assert.Equal(expectedValue,
                         actualValue);
        }

        // Verify the modified key specifically has the new value
        Assert.True(reimportedSettings.ContainsKey(keyToModify),
            $"Modified key '{keyToModify}' should exist in reimported settings");
        Assert.Equal(modifiedSettings[keyToModify].ToString(), reimportedSettings[keyToModify].ToString());

        // Step 10: Restore original settings
        await SettingsImportExportUtilities.ImportSettingsFromZipAsync(originalZipPath);

        // Clean up
        if (File.Exists(originalZipPath)) File.Delete(originalZipPath);
        if (File.Exists(modifiedZipPath)) File.Delete(modifiedZipPath);
        if (File.Exists(reimportedZipPath)) File.Delete(reimportedZipPath);
        if (Directory.Exists(originalTempDir)) Directory.Delete(originalTempDir, true);
        if (Directory.Exists(modifiedTempDir)) Directory.Delete(modifiedTempDir, true);
        if (Directory.Exists(reimportedTempDir)) Directory.Delete(reimportedTempDir, true);
    }
}
