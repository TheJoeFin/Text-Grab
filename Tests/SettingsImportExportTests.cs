using System.IO;
using System.Text.Json;
using Text_Grab.Models;
using Text_Grab.Services;
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

    [WpfFact]
    public async Task ManagedJsonSettingWithDataSurvivesRoundTrip()
    {
        SettingsService settingsService = AppUtilities.TextGrabSettingsService;
        StoredRegex[] originalRegexes = settingsService.LoadStoredRegexes();

        StoredRegex[] testRegexes =
        [
            new StoredRegex
            {
                Id = "export-roundtrip-1",
                Name = "Date Pattern",
                Pattern = @"\d{4}-\d{2}-\d{2}",
                Description = "ISO date for export round-trip test",
            }
        ];
        settingsService.SaveStoredRegexes(testRegexes);

        string zipPath = string.Empty;
        string verifyDir = string.Empty;

        try
        {
            // Export and confirm the managed setting's file content appears in settings.json
            zipPath = await SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory: false);

            verifyDir = Path.Combine(Path.GetTempPath(), $"TextGrab_Verify_{Guid.NewGuid()}");
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, verifyDir);
            string exportedJson = await File.ReadAllTextAsync(Path.Combine(verifyDir, "settings.json"));
            Assert.Contains("export-roundtrip-1", exportedJson);

            // Clear the managed setting to simulate import on a clean machine
            settingsService.SaveStoredRegexes([]);
            Assert.Empty(settingsService.LoadStoredRegexes());

            // Import from the previously exported ZIP
            await SettingsImportExportUtilities.ImportSettingsFromZipAsync(zipPath);

            // The regex must be restored from the imported data
            StoredRegex[] restoredRegexes = settingsService.LoadStoredRegexes();
            StoredRegex restored = Assert.Single(restoredRegexes);
            Assert.Equal("export-roundtrip-1", restored.Id);
            Assert.Equal(@"\d{4}-\d{2}-\d{2}", restored.Pattern);
        }
        finally
        {
            settingsService.SaveStoredRegexes(originalRegexes);

            if (File.Exists(zipPath))
                File.Delete(zipPath);
            if (Directory.Exists(verifyDir))
                Directory.Delete(verifyDir, true);
        }
    }

    [WpfFact]
    public async Task ExportedSettingsJsonIncludesManagedSettingKeys()
    {
        string zipPath = await SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory: false);
        string tempDir = Path.Combine(Path.GetTempPath(), $"TextGrab_Test_{Guid.NewGuid()}");

        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);
            string jsonContent = await File.ReadAllTextAsync(Path.Combine(tempDir, "settings.json"));

            // All six managed-JSON setting names must appear as keys in the export
            Assert.True(jsonContent.Contains("regexList", StringComparison.OrdinalIgnoreCase));
            Assert.True(jsonContent.Contains("shortcutKeySets", StringComparison.OrdinalIgnoreCase));
            Assert.True(jsonContent.Contains("bottomButtonsJson", StringComparison.OrdinalIgnoreCase));
            Assert.True(jsonContent.Contains("webSearchItemsJson", StringComparison.OrdinalIgnoreCase));
            Assert.True(jsonContent.Contains("postGrabJSON", StringComparison.OrdinalIgnoreCase));
            Assert.True(jsonContent.Contains("postGrabCheckStates", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Simulates importing a ZIP that was produced by the old (memory-inefficient) app,
    /// where managed JSON settings were stored as inline strings inside Properties.Settings
    /// rather than in sidecar files. The new import pipeline must route those inline blobs
    /// to the correct sidecar files so the SettingsService can load them normally.
    /// </summary>
    [WpfFact]
    public async Task LegacyExportWithInlineManagedSettingsIsImportedToSidecarFiles()
    {
        SettingsService settingsService = AppUtilities.TextGrabSettingsService;

        StoredRegex[] originalRegexes = settingsService.LoadStoredRegexes();
        Dictionary<string, bool> originalCheckStates = settingsService.LoadPostGrabCheckStates();

        // Build a legacy-style settings.json: managed JSON blobs stored directly as
        // string values under camelCase keys, exactly as the old export produced them.
        StoredRegex legacyRegex = new()
        {
            Id = "legacy-regex-001",
            Name = "Legacy Invoice",
            Pattern = @"INV-\d{5}",
            Description = "Imported from legacy export",
        };
        string regexArrayJson = JsonSerializer.Serialize(new[] { legacyRegex });

        Dictionary<string, bool> legacyCheckStates = new() { ["Legacy Action"] = true };
        string checkStatesJson = JsonSerializer.Serialize(legacyCheckStates);

        // The old export wrote settings with camelCase keys and plain string values
        // for what are now managed-JSON settings.
        Dictionary<string, object?> legacySettings = new()
        {
            // managed settings stored inline (old behaviour)
            ["regexList"] = regexArrayJson,
            ["postGrabCheckStates"] = checkStatesJson,
            // a normal boolean setting to confirm regular settings still import
            ["correctErrors"] = false,
        };

        string legacyJson = JsonSerializer.Serialize(legacySettings, new JsonSerializerOptions { WriteIndented = true });

        string legacyDir = Path.Combine(Path.GetTempPath(), $"TextGrab_LegacyDir_{Guid.NewGuid()}");
        string legacyZipPath = Path.Combine(Path.GetTempPath(), $"TextGrab_Legacy_{Guid.NewGuid()}.zip");
        Directory.CreateDirectory(legacyDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(legacyDir, "settings.json"), legacyJson);
            System.IO.Compression.ZipFile.CreateFromDirectory(legacyDir, legacyZipPath);

            // Start from a clean state so the assertion is unambiguous
            settingsService.SaveStoredRegexes([]);
            settingsService.SavePostGrabCheckStates(new Dictionary<string, bool>());
            Assert.Empty(settingsService.LoadStoredRegexes());
            Assert.Empty(settingsService.LoadPostGrabCheckStates());

            // Act: import the legacy ZIP
            await SettingsImportExportUtilities.ImportSettingsFromZipAsync(legacyZipPath);

            // Assert – array-type managed setting
            StoredRegex[] importedRegexes = settingsService.LoadStoredRegexes();
            StoredRegex importedRegex = Assert.Single(importedRegexes);
            Assert.Equal("legacy-regex-001", importedRegex.Id);
            Assert.Equal(@"INV-\d{5}", importedRegex.Pattern);

            // Assert – dictionary-type managed setting
            Dictionary<string, bool> importedCheckStates = settingsService.LoadPostGrabCheckStates();
            Assert.True(importedCheckStates.ContainsKey("Legacy Action"));
            Assert.True(importedCheckStates["Legacy Action"]);

            // Assert – a plain (non-managed) setting came through too
            Assert.False(AppUtilities.TextGrabSettings.CorrectErrors);
        }
        finally
        {
            // Restore originals regardless of pass/fail
            settingsService.SaveStoredRegexes(originalRegexes);
            settingsService.SavePostGrabCheckStates(originalCheckStates);
            AppUtilities.TextGrabSettings.CorrectErrors = true;

            if (File.Exists(legacyZipPath))
                File.Delete(legacyZipPath);
            if (Directory.Exists(legacyDir))
                Directory.Delete(legacyDir, true);
        }
    }
}
