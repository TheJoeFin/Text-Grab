using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Text_Grab.Properties;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

public static class SettingsImportExportUtilities
{
    private const string SettingsFileName = "settings.json";
    private const string HistoryTextOnlyFileName = "HistoryTextOnly.json";
    private const string HistoryWithImageFileName = "HistoryWithImage.json";
    private const string HistoryFolderName = "history";

    public static async Task<string> ExportSettingsToZipAsync(bool includeHistory)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"TextGrab_Export_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Export settings to JSON
            await ExportSettingsToJsonAsync(Path.Combine(tempDir, SettingsFileName));

            // Export history if requested
            if (includeHistory)
            {
                await ExportHistoryAsync(tempDir);
            }

            // Create zip file
            string zipFileName = $"TextGrab_Settings_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string zipFilePath = Path.Combine(documentsPath, zipFileName);

            if (File.Exists(zipFilePath))
                File.Delete(zipFilePath);

            ZipFile.CreateFromDirectory(tempDir, zipFilePath);

            return zipFilePath;
        }
        finally
        {
            // Clean up temp directory
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    public static async Task ImportSettingsFromZipAsync(string zipFilePath)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"TextGrab_Import_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Extract zip file
            ZipFile.ExtractToDirectory(zipFilePath, tempDir);

            // Import settings
            string settingsPath = Path.Combine(tempDir, SettingsFileName);
            if (File.Exists(settingsPath))
            {
                await ImportSettingsFromJsonAsync(settingsPath);
            }

            // Import history if present
            string historyTextOnlyPath = Path.Combine(tempDir, HistoryTextOnlyFileName);
            string historyWithImagePath = Path.Combine(tempDir, HistoryWithImageFileName);
            string historyFolderPath = Path.Combine(tempDir, HistoryFolderName);

            if (File.Exists(historyTextOnlyPath) || File.Exists(historyWithImagePath) || Directory.Exists(historyFolderPath))
            {
                await ImportHistoryAsync(tempDir);
            }
        }
        finally
        {
            // Clean up temp directory
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private static async Task ExportSettingsToJsonAsync(string filePath)
    {
        Settings settings = AppUtilities.TextGrabSettings;
        Dictionary<string, object?> settingsDict = new();

        // Iterate through all settings properties using reflection
        foreach (SettingsProperty property in settings.Properties)
        {
            string propertyName = property.Name;
            object? value = settings[propertyName];
            settingsDict[propertyName] = value;
        }

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string json = JsonSerializer.Serialize(settingsDict, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static async Task ImportSettingsFromJsonAsync(string filePath)
    {
        string json = await File.ReadAllTextAsync(filePath);
        
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        Dictionary<string, JsonElement>? settingsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options);

        if (settingsDict is null)
            return;

        Settings settings = AppUtilities.TextGrabSettings;

        // Apply each setting
        foreach (var kvp in settingsDict)
        {
            // Convert from camelCase back to PascalCase
            string propertyName = ConvertToPascalCase(kvp.Key);

            try
            {
                SettingsProperty? property = settings.Properties[propertyName];
                if (property is null)
                    continue;

                object? value = ConvertJsonElementToSettingValue(kvp.Value, property);
                if (value is not null)
                {
                    settings[propertyName] = value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to import setting {propertyName}: {ex.Message}");
            }
        }

        settings.Save();
    }

    private static async Task ExportHistoryAsync(string tempDir)
    {
        // Get history file paths
        string historyBasePath = await FileUtilities.GetPathToHistory();

        // Copy history JSON files
        string textOnlySource = Path.Combine(historyBasePath, HistoryTextOnlyFileName);
        string withImageSource = Path.Combine(historyBasePath, HistoryWithImageFileName);

        if (File.Exists(textOnlySource))
        {
            File.Copy(textOnlySource, Path.Combine(tempDir, HistoryTextOnlyFileName), true);
        }

        if (File.Exists(withImageSource))
        {
            File.Copy(withImageSource, Path.Combine(tempDir, HistoryWithImageFileName), true);
        }

        // Copy history images directory
        if (Directory.Exists(historyBasePath))
        {
            string historyDestDir = Path.Combine(tempDir, HistoryFolderName);
            Directory.CreateDirectory(historyDestDir);

            // Copy all .bmp files from history directory
            string[] imageFiles = Directory.GetFiles(historyBasePath, "*.bmp");
            foreach (string imageFile in imageFiles)
            {
                string fileName = Path.GetFileName(imageFile);
                string destPath = Path.Combine(historyDestDir, fileName);
                File.Copy(imageFile, destPath, true);
            }
        }
    }

    private static async Task ImportHistoryAsync(string tempDir)
    {
        string historyBasePath = await FileUtilities.GetPathToHistory();

        // Ensure history directory exists
        if (!Directory.Exists(historyBasePath))
            Directory.CreateDirectory(historyBasePath);

        // Copy history JSON files
        string textOnlySource = Path.Combine(tempDir, HistoryTextOnlyFileName);
        string withImageSource = Path.Combine(tempDir, HistoryWithImageFileName);

        if (File.Exists(textOnlySource))
        {
            File.Copy(textOnlySource, Path.Combine(historyBasePath, HistoryTextOnlyFileName), true);
        }

        if (File.Exists(withImageSource))
        {
            File.Copy(withImageSource, Path.Combine(historyBasePath, HistoryWithImageFileName), true);
        }

        // Copy history images
        string historySourceDir = Path.Combine(tempDir, HistoryFolderName);
        if (Directory.Exists(historySourceDir))
        {
            string[] imageFiles = Directory.GetFiles(historySourceDir, "*.bmp");
            foreach (string imageFile in imageFiles)
            {
                string fileName = Path.GetFileName(imageFile);
                string destPath = Path.Combine(historyBasePath, fileName);
                File.Copy(imageFile, destPath, true);
            }
        }

        // Reload history in the service
        await Singleton<HistoryService>.Instance.LoadHistories();
    }

    private static string ConvertToPascalCase(string camelCase)
    {
        if (string.IsNullOrEmpty(camelCase))
            return camelCase;

        return char.ToUpper(camelCase[0]) + camelCase.Substring(1);
    }

    private static object? ConvertJsonElementToSettingValue(JsonElement jsonElement, SettingsProperty property)
    {
        Type propertyType = property.PropertyType;

        try
        {
            if (propertyType == typeof(string))
            {
                return jsonElement.GetString();
            }
            else if (propertyType == typeof(bool))
            {
                return jsonElement.GetBoolean();
            }
            else if (propertyType == typeof(int))
            {
                return jsonElement.GetInt32();
            }
            else if (propertyType == typeof(double))
            {
                return jsonElement.GetDouble();
            }
            else if (propertyType == typeof(long))
            {
                return jsonElement.GetInt64();
            }
            else
            {
                // For other types, try to deserialize as string
                return jsonElement.GetString();
            }
        }
        catch
        {
            return null;
        }
    }
}
