using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using Text_Grab.Models;
using Text_Grab.Properties;
using Wpf.Ui.Controls;

namespace Text_Grab.Utilities;

/// <summary>
/// Provides CRUD operations for <see cref="GrabTemplate"/> objects, persisted as
/// a JSON file on disk. Previously stored in application settings, but moved to
/// file-based storage because ApplicationDataContainer has an 8 KB per-value limit.
/// Pattern follows <see cref="PostGrabActionManager"/>.
/// </summary>
public static class GrabTemplateManager
{
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private const string TemplatesFileName = "GrabTemplates.json";
    private static bool _migrated;

    // Allow tests to override the file path
    internal static string? TestFilePath { get; set; }

    // ── File path ─────────────────────────────────────────────────────────────

    private static string GetTemplatesFilePath()
    {
        if (TestFilePath is not null)
            return TestFilePath;

        if (AppUtilities.IsPackaged())
        {
            string localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            return Path.Combine(localFolder, TemplatesFileName);
        }

        string? exeDir = Path.GetDirectoryName(FileUtilities.GetExePath());
        return Path.Combine(exeDir ?? "c:\\Text-Grab", TemplatesFileName);
    }

    /// <summary>
    /// Saves <paramref name="imageSource"/> as a PNG in the template-images folder, named
    /// after <paramref name="templateName"/> and the first 8 characters of <paramref name="templateId"/>.
    /// Returns the full file path on success, or <c>null</c> if the source is null or the write fails.
    /// </summary>
    public static string? SaveTemplateReferenceImage(BitmapSource? imageSource, string templateName, string templateId)
    {
        if (imageSource is null)
            return null;

        try
        {
            string folder = GetTemplateImagesFolder();
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string safeName = Regex.Replace(templateName.ToLowerInvariant(), @"[^\w]+", "-").Trim('-');
            string shortId = templateId.Length >= 8 ? templateId[..8] : templateId;
            string filePath = Path.Combine(folder, $"{safeName}_{shortId}.png");

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(imageSource));
            using FileStream fs = new(filePath, FileMode.Create, FileAccess.Write);
            encoder.Save(fs);

            return filePath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save template reference image: {ex.Message}");
            return null;
        }
    }

    /// <summary>Returns the folder where template reference images are stored alongside the templates JSON.</summary>
    public static string GetTemplateImagesFolder()
    {
        if (AppUtilities.IsPackaged())
        {
            string localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            return Path.Combine(localFolder, "template-images");
        }

        string? exeDir = Path.GetDirectoryName(FileUtilities.GetExePath());
        return Path.Combine(exeDir ?? "c:\\Text-Grab", "template-images");
    }

    // ── Migration from settings ───────────────────────────────────────────────

    private static void MigrateFromSettingsIfNeeded()
    {
        if (_migrated)
            return;

        _migrated = true;

        string filePath = GetTemplatesFilePath();
        if (File.Exists(filePath))
            return;

        try
        {
            string settingsJson = DefaultSettings.GrabTemplatesJSON;
            if (string.IsNullOrWhiteSpace(settingsJson))
                return;

            // Validate the JSON before migrating
            List<GrabTemplate>? templates = JsonSerializer.Deserialize<List<GrabTemplate>>(settingsJson, JsonOptions);
            if (templates is null || templates.Count == 0)
                return;

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, settingsJson);

            // Clear the setting so it no longer overflows the container
            DefaultSettings.GrabTemplatesJSON = string.Empty;
            DefaultSettings.Save();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to migrate GrabTemplates from settings to file: {ex.Message}");
        }
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>Returns all saved templates, or an empty list if none exist.</summary>
    public static List<GrabTemplate> GetAllTemplates()
    {
        MigrateFromSettingsIfNeeded();

        string filePath = GetTemplatesFilePath();

        if (!File.Exists(filePath))
            return [];

        try
        {
            string json = File.ReadAllText(filePath);

            if (string.IsNullOrWhiteSpace(json))
                return [];

            List<GrabTemplate>? templates = JsonSerializer.Deserialize<List<GrabTemplate>>(json, JsonOptions);
            if (templates is not null)
                return templates;
        }
        catch (JsonException)
        {
            // Return empty list if deserialization fails — never crash
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to read GrabTemplates file: {ex.Message}");
        }

        return [];
    }

    /// <summary>Returns the template with the given ID, or null.</summary>
    public static GrabTemplate? GetTemplateById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return GetAllTemplates().FirstOrDefault(t => t.Id == id);
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>Replaces the entire saved template list.</summary>
    public static void SaveTemplates(List<GrabTemplate> templates)
    {
        string json = JsonSerializer.Serialize(templates, JsonOptions);
        string filePath = GetTemplatesFilePath();

        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(filePath, json);
    }

    /// <summary>Adds a new template (or updates an existing one with the same ID).</summary>
    public static void AddOrUpdateTemplate(GrabTemplate template)
    {
        List<GrabTemplate> templates = GetAllTemplates();
        int existing = templates.FindIndex(t => t.Id == template.Id);
        if (existing >= 0)
            templates[existing] = template;
        else
            templates.Add(template);

        SaveTemplates(templates);
    }

    /// <summary>Removes the template with the given ID. No-op if not found.</summary>
    public static void DeleteTemplate(string id)
    {
        List<GrabTemplate> templates = GetAllTemplates();
        int removed = templates.RemoveAll(t => t.Id == id);
        if (removed > 0)
            SaveTemplates(templates);
    }

    /// <summary>Creates and saves a shallow copy of an existing template with a new ID and name.</summary>
    public static GrabTemplate? DuplicateTemplate(string id)
    {
        GrabTemplate? original = GetTemplateById(id);
        if (original is null)
            return null;

        string json = JsonSerializer.Serialize(original, JsonOptions);
        GrabTemplate? copy = JsonSerializer.Deserialize<GrabTemplate>(json, JsonOptions);
        if (copy is null)
            return null;

        copy.Id = Guid.NewGuid().ToString();
        copy.Name = $"{original.Name} (copy)";
        copy.CreatedDate = DateTimeOffset.Now;
        copy.LastUsedDate = null;

        AddOrUpdateTemplate(copy);
        return copy;
    }

    // ── ButtonInfo bridge ─────────────────────────────────────────────────────

    /// <summary>
    /// Generates a <see cref="ButtonInfo"/> post-grab action that executes the given template.
    /// </summary>
    public static ButtonInfo CreateButtonInfoForTemplate(GrabTemplate template)
    {
        return new ButtonInfo(
            buttonText: template.Name,
            clickEvent: "ApplyTemplate_Click",
            symbolIcon: SymbolRegular.DocumentTableSearch24,
            defaultCheckState: DefaultCheckState.Off)
        {
            TemplateId = template.Id,
            IsRelevantForFullscreenGrab = true,
            IsRelevantForEditWindow = false,
            OrderNumber = 7.0,
        };
    }

    /// <summary>
    /// Updates a <see cref="GrabTemplate"/>'s LastUsedDate and persists it.
    /// </summary>
    public static void RecordUsage(string templateId)
    {
        List<GrabTemplate> templates = GetAllTemplates();
        GrabTemplate? template = templates.FirstOrDefault(t => t.Id == templateId);
        if (template is null)
            return;

        template.LastUsedDate = DateTimeOffset.Now;
        SaveTemplates(templates);
    }
}
