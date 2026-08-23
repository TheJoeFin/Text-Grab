using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Text_Grab.Models;
using Text_Grab.Properties;
using Wpf.Ui.Controls;

namespace Text_Grab.Utilities;

/// <summary>
/// Provides CRUD operations for <see cref="GrabTemplate"/> objects, keeping the
/// legacy settings string and the file-backed JSON representation in sync during
/// the transition release. Pattern follows <see cref="PostGrabActionManager"/>.
/// </summary>
/// <remarks>
/// TODO: This class has no thread-safety guards. All current callers are UI-thread
/// methods so this is safe today, but if templates are ever read/written from
/// background threads a lock (like SettingsService._managedJsonLock) should be added.
/// </remarks>
public static class GrabTemplateManager
{
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private const string TemplatesFileName = "GrabTemplates.json";

    // In-memory cache of the resolved template list. GetAllTemplates() used to hit disk
    // (and potentially Settings.Save(), which is slow) on every call — including every time
    // a menu that lists templates (e.g. the EditTextWindow "Capture" menu) was opened. Cached
    // here instead and only refreshed by writes that go through this class.
    private static List<GrabTemplate>? _cachedTemplates;

    // Allow tests to override the file path.
    // TODO: If more test seams are needed, consider consolidating these into a small
    // options/config object instead of individual static properties.
    private static string? _testFilePath;
    internal static string? TestFilePath
    {
        get => _testFilePath;
        set { _testFilePath = value; InvalidateCache(); }
    }

    internal static string? TestImagesFolderPath { get; set; }

    private static bool? _testPreferFileBackedMode;
    internal static bool? TestPreferFileBackedMode
    {
        get => _testPreferFileBackedMode;
        set { _testPreferFileBackedMode = value; InvalidateCache(); }
    }

    /// <summary>Drops the in-memory template cache so the next read re-resolves from disk/settings.</summary>
    internal static void InvalidateCache() => _cachedTemplates = null;

    private static bool PreferFileBackedTemplates =>
        TestPreferFileBackedMode ?? AppUtilities.TextGrabSettingsService.IsFileBackedManagedSettingsEnabled;

    // ── File path ─────────────────────────────────────────────────────────────

    private static string GetTemplatesFilePath()
    {
        if (TestFilePath is not null)
            return TestFilePath;

        if (AutomationProfile.Current is AutomationProfile profile)
            return profile.TemplatesFilePath;

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

            string safeName = templateName.ReplaceReservedCharacters();
            string shortId = templateId.Length >= 8 ? templateId[..8] : templateId;
            string filePath = Path.Combine(folder, $"{safeName}_{shortId}.png");

            // Write to a temp file first so the encoder never contends with WPF's
            // read lock on filePath (held when BitmapImage was loaded without OnLoad).
            string tempPath = Path.Combine(folder, $"{Guid.NewGuid():N}.tmp");

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(imageSource));
            using (FileStream fs = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                encoder.Save(fs);

            // Atomically replace the destination; succeeds even when the target file
            // is open for reading by another process (e.g. WPF's BitmapImage).
            File.Move(tempPath, filePath, overwrite: true);
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
        if (TestImagesFolderPath is not null)
            return TestImagesFolderPath;

        if (TestFilePath is not null)
        {
            string? testDir = Path.GetDirectoryName(TestFilePath);
            return Path.Combine(testDir ?? Path.GetTempPath(), "template-images");
        }

        if (AutomationProfile.Current is AutomationProfile profile)
            return profile.TemplateImagesDirectory;

        if (AppUtilities.IsPackaged())
        {
            string localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            return Path.Combine(localFolder, "template-images");
        }

        string? exeDir = Path.GetDirectoryName(FileUtilities.GetExePath());
        return Path.Combine(exeDir ?? "c:\\Text-Grab", "template-images");
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all saved templates, or an empty list if none exist. Resolved once per process
    /// (or since the last write/<see cref="InvalidateCache"/>) and cached; callers get a fresh
    /// list instance each time so structural edits (add/remove) by one caller can't leak into
    /// another caller's list before being persisted.
    /// </summary>
    public static List<GrabTemplate> GetAllTemplates()
    {
        _cachedTemplates ??= LoadTemplatesFromStorage();
        return [.. _cachedTemplates];
    }

    private static List<GrabTemplate> LoadTemplatesFromStorage()
    {
        try
        {
            string json = ResolveTemplatesJson();

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
        SaveTemplatesJson(json);
        _cachedTemplates = [.. templates];
    }

    internal static string GetTemplatesJsonForExport()
    {
        List<GrabTemplate> templates = GetAllTemplates();
        return JsonSerializer.Serialize(templates, JsonOptions);
    }

    internal static void ImportTemplatesFromJson(string templatesJson)
    {
        List<GrabTemplate> templates = string.IsNullOrWhiteSpace(templatesJson)
            ? []
            : JsonSerializer.Deserialize<List<GrabTemplate>>(templatesJson, JsonOptions) ?? [];

        SaveTemplates(templates);
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

    private static string ResolveTemplatesJson()
    {
        string settingsJson = DefaultSettings.GrabTemplatesJSON;
        string fileJson = TryReadTemplatesFileText();
        string preferredJson = PreferFileBackedTemplates ? fileJson : settingsJson;
        string secondaryJson = PreferFileBackedTemplates ? settingsJson : fileJson;
        string selectedJson = string.IsNullOrWhiteSpace(preferredJson)
            ? secondaryJson
            : preferredJson;

        if (string.IsNullOrWhiteSpace(selectedJson))
            return string.Empty;

        if (!string.Equals(settingsJson, selectedJson, StringComparison.Ordinal))
            SetLegacyTemplatesJson(selectedJson);

        if (!string.Equals(fileJson, selectedJson, StringComparison.Ordinal))
            TryWriteTemplatesFile(selectedJson);

        return selectedJson;
    }

    private static string TryReadTemplatesFileText()
    {
        string filePath = GetTemplatesFilePath();
        if (!File.Exists(filePath))
            return string.Empty;

        try
        {
            return File.ReadAllText(filePath);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to read GrabTemplates file: {ex.Message}");
            return string.Empty;
        }
    }

    private static void SaveTemplatesJson(string json)
    {
        SetLegacyTemplatesJson(json);
        TryWriteTemplatesFile(json);
    }

    private static void SetLegacyTemplatesJson(string json)
    {
        if (string.Equals(DefaultSettings.GrabTemplatesJSON, json, StringComparison.Ordinal))
            return;

        DefaultSettings.GrabTemplatesJSON = json;
        DefaultSettings.Save();
    }

    private static bool TryWriteTemplatesFile(string json)
    {
        string filePath = GetTemplatesFilePath();

        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to persist GrabTemplates file: {ex.Message}");
            return false;
        }
    }
}
