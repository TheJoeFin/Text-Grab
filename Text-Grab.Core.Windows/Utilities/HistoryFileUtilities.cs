using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Models;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

/// <summary>
/// The on-disk half of the grab history: reading and writing the two history JSON files, the
/// word-border sidecar files beside them, the normalization passes that keep older files
/// loadable, and the retention rules that decide what gets dropped.
///
/// The headless half of what used to be HistoryService (batch 6e of the Core split). Everything
/// here is static and owns no state past the serializer options - the in-memory history lists,
/// the DispatcherTimer-driven write debounce and cache-release cycle, the cached fullscreen
/// bitmap, the recent-grabs MenuItem building and the GrabFrame / EditTextWindow construction all
/// stayed behind in <c>Text_Grab.Services.HistoryService</c>, which still owns the state these
/// functions are handed.
///
/// Retention lives here rather than with the service because the caps and the selection rule are
/// one idea: <see cref="GetExcessVisualHistoryItems"/> picks what to drop and
/// <see cref="MaxHistoryTextOnly"/> and its siblings cap what gets written.
/// </summary>
public static class HistoryFileUtilities
{
    #region Fields

    /// <summary>How many text-only history entries survive a write.</summary>
    internal const int MaxHistoryTextOnly = 100;

    /// <summary>How many image-backed (non-PDF) history entries survive a write.</summary>
    internal const int MaxHistoryWithImages = 10;

    /// <summary>How many PDF-sourced history entries survive a write.</summary>
    internal const int MaxHistoryPdfDocuments = 10;

    private const string WordBorderInfoFileSuffix = ".wordborders.json";

    private static readonly AsyncLocal<bool> HistoryLanguageKindFallbackUsed = new();

    private static readonly JsonSerializerOptions HistoryJsonOptions = new()
    {
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters =
        {
            new HistoryLanguageKindJsonConverter(),
            new JsonStringEnumConverter()
        }
    };

    #endregion Fields

    #region Loading and writing

    internal static async Task<(List<HistoryInfo> HistoryItems, bool NeedsRewrite)> LoadHistoryAsync(string fileName)
    {
        string rawText = await FileUtilities.GetTextFileAsync($"{fileName}.json", FileStorageKind.WithHistory);

        if (string.IsNullOrWhiteSpace(rawText))
            return ([], false);

        try
        {
            HistoryLanguageKindFallbackUsed.Value = false;
            List<HistoryInfo>? tempHistory = JsonSerializer.Deserialize<List<HistoryInfo>>(rawText, HistoryJsonOptions);

            if (tempHistory is List<HistoryInfo> jsonList && jsonList.Count > 0)
                return (tempHistory, HistoryLanguageKindFallbackUsed.Value);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Failed to deserialize history file '{fileName}.json' as a list. Attempting item-by-item recovery. {ex}");
            return LoadHistoryWithRecovery(rawText, fileName);
        }
        finally
        {
            HistoryLanguageKindFallbackUsed.Value = false;
        }

        return ([], false);
    }

    internal static (List<HistoryInfo> HistoryItems, bool NeedsRewrite) LoadHistoryBlocking(string fileName)
    {
        return Task.Run(() => LoadHistoryAsync(fileName)).GetAwaiter().GetResult();
    }

    private static (List<HistoryInfo> HistoryItems, bool NeedsRewrite) LoadHistoryWithRecovery(string rawText, string fileName)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(rawText);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return ([], true);

            List<HistoryInfo> recoveredHistory = [];
            bool needsRewrite = true;
            int index = 0;

            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                try
                {
                    HistoryLanguageKindFallbackUsed.Value = false;
                    HistoryInfo? historyItem = element.Deserialize<HistoryInfo>(HistoryJsonOptions);
                    if (historyItem is not null)
                    {
                        recoveredHistory.Add(historyItem);
                        if (HistoryLanguageKindFallbackUsed.Value)
                            needsRewrite = true;
                    }
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"Skipped invalid history item at index {index} from '{fileName}.json'. {ex}");
                }
                finally
                {
                    HistoryLanguageKindFallbackUsed.Value = false;
                }

                index++;
            }

            return (recoveredHistory, needsRewrite);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Failed to parse history file '{fileName}.json' during recovery. {ex}");
            return ([], true);
        }
    }

    internal static void WriteHistoryFiles(List<HistoryInfo> history, string fileName, int maxNumberToSave)
    {
        string historyAsJson = JsonSerializer
            .Serialize(history
                .OrderBy(x => x.CaptureDateTime)
                .TakeLast(maxNumberToSave),
            HistoryJsonOptions);

        try
        {
            SaveHistoryTextFileBlocking(historyAsJson, $"{fileName}.json");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save history json file. {ex.Message}");
        }
    }

    #endregion Loading and writing

    #region Normalization

    internal static bool NormalizeHistoryIds(List<HistoryInfo> historyItems)
    {
        HashSet<string> seenIds = [];
        bool updatedAnyIds = false;

        foreach (HistoryInfo historyItem in historyItems)
        {
            if (!string.IsNullOrWhiteSpace(historyItem.ID) && seenIds.Add(historyItem.ID))
                continue;

            string nextId;
            do
            {
                nextId = Guid.NewGuid().ToString();
            }
            while (!seenIds.Add(nextId));

            historyItem.ID = nextId;
            updatedAnyIds = true;
        }

        return updatedAnyIds;
    }

    internal static bool NormalizeHistoryCompatibilityData(IEnumerable<HistoryInfo> historyItems)
    {
        bool normalizedAnyHistoryItems = false;

        foreach (HistoryInfo historyItem in historyItems)
        {
            if (NormalizeHistoryCompatibilityData(historyItem))
                normalizedAnyHistoryItems = true;
        }

        return normalizedAnyHistoryItems;
    }

    internal static bool NormalizeHistoryCompatibilityData(HistoryInfo historyItem)
    {
        (string normalizedLanguageTag, LanguageKind normalizedLanguageKind, bool usedUiAutomation) =
            LanguageUtilities.NormalizePersistedLanguageIdentity(
                historyItem.LanguageKind,
                historyItem.LanguageTag,
                historyItem.UsedUiAutomation);

        if (string.Equals(historyItem.LanguageTag, normalizedLanguageTag, StringComparison.Ordinal)
            && historyItem.LanguageKind == normalizedLanguageKind
            && historyItem.UsedUiAutomation == usedUiAutomation)
        {
            return false;
        }

        historyItem.LanguageTag = normalizedLanguageTag;
        historyItem.LanguageKind = normalizedLanguageKind;
        historyItem.UsedUiAutomation = usedUiAutomation;
        return true;
    }

    #endregion Normalization

    #region Word border sidecar files

    internal static bool EnsureWordBorderSidecarFiles(IEnumerable<HistoryInfo> historyItems)
    {
        bool migratedAnyWordBorderData = false;

        foreach (HistoryInfo historyItem in historyItems)
        {
            if (PersistWordBorderData(historyItem))
                migratedAnyWordBorderData = true;
        }

        return migratedAnyWordBorderData;
    }

    internal static void PersistWordBorderData(IEnumerable<HistoryInfo> historyItems)
    {
        foreach (HistoryInfo historyItem in historyItems)
            PersistWordBorderData(historyItem);
    }

    internal static bool PersistWordBorderData(HistoryInfo historyItem)
    {
        if (string.IsNullOrWhiteSpace(historyItem.WordBorderInfoJson))
            return false;

        if (string.IsNullOrWhiteSpace(historyItem.ID))
            historyItem.ID = Guid.NewGuid().ToString();

        string wordBorderInfoFileName = GetWordBorderInfoFileName(historyItem.ID);
        bool couldSaveWordBorderInfo = SaveHistoryTextFileBlocking(historyItem.WordBorderInfoJson, wordBorderInfoFileName);

        if (!couldSaveWordBorderInfo)
        {
            historyItem.WordBorderInfoFileName = null;
            return false;
        }

        historyItem.WordBorderInfoFileName = wordBorderInfoFileName;

        // When file-backed settings are enabled, the sidecar file is the authority
        // for word border data, so drop the inline JSON to reduce memory/disk usage.
        if (SettingsAccess.Current.EnableFileBackedManagedSettings)
            historyItem.ClearTransientWordBorderData();

        return true;
    }

    internal static async Task<List<WordBorderInfo>> GetWordBorderInfosAsync(HistoryInfo history)
    {
        if (!string.IsNullOrWhiteSpace(history.WordBorderInfoFileName))
        {
            // Sanitize the persisted file name to prevent path traversal outside the history directory
            string sanitizedFileName = Path.GetFileName(history.WordBorderInfoFileName);

            if (!string.IsNullOrWhiteSpace(sanitizedFileName)
                && string.Equals(Path.GetExtension(sanitizedFileName), ".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string historyBasePath = await FileUtilities.GetPathToHistory();
                    string wordBorderInfoPath = Path.Combine(historyBasePath, sanitizedFileName);

                    if (File.Exists(wordBorderInfoPath))
                    {
                        await using FileStream wordBorderInfoStream = File.OpenRead(wordBorderInfoPath);
                        List<WordBorderInfo>? wordBorderInfos =
                            await JsonSerializer.DeserializeAsync<List<WordBorderInfo>>(wordBorderInfoStream, HistoryJsonOptions);

                        if (wordBorderInfos is not null)
                            return wordBorderInfos;
                    }
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"Failed to read word border info file for history item '{history.ID}': {ex}");
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine($"Failed to deserialize word border info file for history item '{history.ID}': {ex}");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(history.WordBorderInfoJson))
            return [];

        try
        {
            List<WordBorderInfo>? inlineWordBorderInfos =
                JsonSerializer.Deserialize<List<WordBorderInfo>>(history.WordBorderInfoJson, HistoryJsonOptions);

            return inlineWordBorderInfos ?? [];
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Failed to deserialize inline word border info for history item '{history.ID}': {ex}");
            return [];
        }
    }

    #endregion Word border sidecar files

    #region Retention

    internal static HistoryInfo? GetMostRecentGrab(IEnumerable<HistoryInfo> historyItems)
    {
        return historyItems
            .Where(history => !history.IsPdfDocument)
            .MaxBy(history => history.CaptureDateTime);
    }

    internal static List<HistoryInfo> GetExcessVisualHistoryItems(IEnumerable<HistoryInfo> historyItems)
    {
        return
        [
            .. historyItems
                .Where(history => !history.IsPdfDocument)
                .OrderBy(history => history.CaptureDateTime)
                .SkipLast(MaxHistoryWithImages),
            .. historyItems
                .Where(history => history.IsPdfDocument)
                .OrderBy(history => history.CaptureDateTime)
                .SkipLast(MaxHistoryPdfDocuments),
        ];
    }

    internal static void ClearTransientHistoryPayloads(IEnumerable<HistoryInfo> historyItems)
    {
        foreach (HistoryInfo historyItem in historyItems)
        {
            historyItem.ClearTransientImage();
            historyItem.ClearTransientWordBorderData();
        }
    }

    #endregion Retention

    #region Deleting artifacts

    internal static void DeleteHistoryArtifacts(HistoryInfo historyItem)
    {
        DeleteHistoryFile(historyItem.ImagePath);
        DeleteHistoryFile(historyItem.WordBorderInfoFileName);
    }

    internal static void DeleteUnusedWordBorderFiles(IEnumerable<HistoryInfo> historyItems)
    {
        string historyBasePath = GetHistoryPathBlocking();

        if (!Directory.Exists(historyBasePath))
            return;

        HashSet<string> expectedFileNames = [.. historyItems
            .Select(historyItem => historyItem.WordBorderInfoFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => Path.GetFileName(fileName!))];

        string[] wordBorderInfoFiles = Directory.GetFiles(historyBasePath, $"*{WordBorderInfoFileSuffix}");

        foreach (string wordBorderInfoFile in wordBorderInfoFiles)
        {
            string fileName = Path.GetFileName(wordBorderInfoFile);

            if (!expectedFileNames.Contains(fileName))
            {
                try
                {
                    File.Delete(wordBorderInfoFile);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"Failed to delete word border info file '{wordBorderInfoFile}': {ex}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.WriteLine($"Access denied when deleting word border info file '{wordBorderInfoFile}': {ex}");
                }
            }
        }
    }

    private static void DeleteHistoryFile(string? historyFileName)
    {
        if (string.IsNullOrWhiteSpace(historyFileName))
            return;

        string historyBasePath = GetHistoryPathBlocking();
        string filePath = Path.Combine(historyBasePath, Path.GetFileName(historyFileName));

        if (!File.Exists(filePath))
            return;

        try
        {
            File.Delete(filePath);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to delete history file '{filePath}': {ex}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Access denied when deleting history file '{filePath}': {ex}");
        }
    }

    #endregion Deleting artifacts

    #region Path and file helpers

    private static string GetHistoryPathBlocking()
    {
        return Task.Run(async () => await FileUtilities.GetPathToHistory()).GetAwaiter().GetResult();
    }

    private static string GetWordBorderInfoFileName(string historyId)
    {
        return $"{historyId}{WordBorderInfoFileSuffix}";
    }

    private static bool SaveHistoryTextFileBlocking(string textContent, string fileName)
    {
        return Task.Run(async () => await FileUtilities.SaveTextFile(textContent, fileName, FileStorageKind.WithHistory))
            .GetAwaiter()
            .GetResult();
    }

    #endregion Path and file helpers

    #region Json converter

    private sealed class HistoryLanguageKindJsonConverter : JsonConverter<LanguageKind>
    {
        public override LanguageKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? value = reader.GetString();

                if (!string.IsNullOrWhiteSpace(value)
                    && Enum.TryParse(value, true, out LanguageKind parsedValue)
                    && Enum.IsDefined(typeof(LanguageKind), parsedValue))
                {
                    return parsedValue;
                }

                HistoryLanguageKindFallbackUsed.Value = true;
                Debug.WriteLine($"Unknown history LanguageKind '{value}'. Falling back to {LanguageKind.Global}.");
                return LanguageKind.Global;
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int numericValue))
            {
                if (Enum.IsDefined(typeof(LanguageKind), numericValue))
                    return (LanguageKind)numericValue;

                HistoryLanguageKindFallbackUsed.Value = true;
                Debug.WriteLine($"Unknown history LanguageKind numeric value '{numericValue}'. Falling back to {LanguageKind.Global}.");
                return LanguageKind.Global;
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                HistoryLanguageKindFallbackUsed.Value = true;
                return LanguageKind.Global;
            }

            HistoryLanguageKindFallbackUsed.Value = true;
            Debug.WriteLine($"Unexpected token '{reader.TokenType}' for history LanguageKind. Falling back to {LanguageKind.Global}.");
            return LanguageKind.Global;
        }

        public override void Write(Utf8JsonWriter writer, LanguageKind value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }

    #endregion Json converter
}
