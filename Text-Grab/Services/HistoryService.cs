using Humanizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab.Services;

public class HistoryService
{
    #region Fields

    private static readonly int maxHistoryTextOnly = 100;
    private static readonly int maxHistoryWithImages = 10;
    private const string WordBorderInfoFileSuffix = ".wordborders.json";
    private static readonly TimeSpan historyCacheCheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan historyCacheIdleLifetime = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions HistoryJsonOptions = new()
    {
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private List<HistoryInfo> HistoryTextOnly = [];
    private List<HistoryInfo> HistoryWithImage = [];
    private readonly DispatcherTimer saveTimer = new();
    private readonly DispatcherTimer historyCacheReleaseTimer = new();
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool _textHistoryLoaded;
    private bool _imageHistoryLoaded;
    private bool _hasPendingWrite;
    private DateTimeOffset _lastHistoryAccessUtc = DateTimeOffset.MinValue;
    #endregion Fields

    #region Constructors

    public HistoryService()
    {
        saveTimer.Interval = new(0, 0, 0, 0, 500);
        saveTimer.Tick += SaveTimer_Tick;

        historyCacheReleaseTimer.Interval = historyCacheCheckInterval;
        historyCacheReleaseTimer.Tick += HistoryCacheReleaseTimer_Tick;
    }

    #endregion Constructors

    #region Properties

    public Bitmap? CachedBitmap { get; set; }
    private nint? _cachedBitmapHandle;

    #endregion Properties

    #region Public Methods

    public void CacheLastBitmap(Bitmap bmp)
    {
        DisposeCachedBitmap();
        CachedBitmap = bmp;
        _cachedBitmapHandle = bmp.GetHbitmap();
    }

    public void DeleteHistory()
    {
        saveTimer.Stop();
        historyCacheReleaseTimer.Stop();
        _hasPendingWrite = false;
        ReleaseLoadedHistoriesCore();
        DisposeCachedBitmap();

        FileUtilities.TryDeleteHistoryDirectory();
    }

    public List<HistoryInfo> GetEditWindows()
    {
        EnsureTextHistoryLoaded();
        TouchHistoryCache();
        return [.. HistoryTextOnly];
    }

    public HistoryInfo? GetLastFullScreenGrabInfo()
    {
        EnsureImageHistoryLoaded();
        TouchHistoryCache();
        return HistoryWithImage.Where(h => h.SourceMode == TextGrabMode.Fullscreen).LastOrDefault();
    }

    public bool HasAnyFullscreenHistory()
    {
        EnsureImageHistoryLoaded();
        TouchHistoryCache();
        return HistoryWithImage.Any(h => h.SourceMode == TextGrabMode.Fullscreen);
    }

    public bool GetLastHistoryAsGrabFrame()
    {
        EnsureImageHistoryLoaded();
        TouchHistoryCache();
        HistoryInfo? lastHistoryItem = HistoryWithImage.LastOrDefault();

        if (lastHistoryItem is not HistoryInfo historyInfo)
            return false;

        GrabFrame grabFrame = new(historyInfo);

        try { grabFrame.Show(); }
        catch { return false; }
        return true;
    }

    public string GetLastTextHistory()
    {
        EnsureTextHistoryLoaded();
        TouchHistoryCache();
        HistoryInfo? lastHistoryItem = HistoryTextOnly.LastOrDefault();

        if (lastHistoryItem is not HistoryInfo historyInfo)
            return string.Empty;

        return historyInfo.TextContent;
    }

    public List<HistoryInfo> GetRecentGrabs()
    {
        EnsureImageHistoryLoaded();
        TouchHistoryCache();
        return [.. HistoryWithImage];
    }

    public bool HasAnyHistoryWithImages()
    {
        EnsureImageHistoryLoaded();
        TouchHistoryCache();
        return HistoryWithImage.Count > 0;
    }

    public async Task LoadHistories()
    {
        saveTimer.Stop();
        historyCacheReleaseTimer.Stop();
        _hasPendingWrite = false;
        ReleaseLoadedHistoriesCore();

        HistoryTextOnly = await LoadHistoryAsync(nameof(HistoryTextOnly));
        _textHistoryLoaded = true;
        NormalizeHistoryIds(HistoryTextOnly);

        HistoryWithImage = await LoadHistoryAsync(nameof(HistoryWithImage));
        _imageHistoryLoaded = true;
        NormalizeHistoryIds(HistoryWithImage);

        if (MigrateWordBorderDataToSidecarFiles(HistoryWithImage))
            MarkHistoryDirty();

        TouchHistoryCache();
    }

    public async Task PopulateMenuItemWithRecentGrabs(MenuItem recentGrabsMenuItem)
    {
        List<HistoryInfo> grabsHistory = GetRecentGrabs();
        grabsHistory = [.. grabsHistory.OrderByDescending(x => x.CaptureDateTime)];

        recentGrabsMenuItem.Items.Clear();

        if (grabsHistory.Count < 1)
        {
            recentGrabsMenuItem.IsEnabled = false;
            return;
        }

        string historyBasePath = await FileUtilities.GetPathToHistory();

        foreach (HistoryInfo history in grabsHistory)
        {
            string imageFullPath = Path.Combine(historyBasePath, history.ImagePath);
            if (string.IsNullOrWhiteSpace(history.ImagePath) || !File.Exists(imageFullPath))
                continue;

            MenuItem menuItem = new();
            string historyId = history.ID;
            menuItem.Click += (object sender, RoutedEventArgs args) =>
            {
                HistoryInfo? selectedHistory = GetImageHistoryById(historyId);

                if (selectedHistory is null)
                {
                    menuItem.IsEnabled = false;
                    return;
                }

                GrabFrame grabFrame = new(selectedHistory);
                try { grabFrame.Show(); }
                catch { menuItem.IsEnabled = false; }
            };

            menuItem.Header = $"{history.CaptureDateTime.Humanize()} | {history.TextContent.MakeStringSingleLine().Truncate(20)}";
            recentGrabsMenuItem.Items.Add(menuItem);
        }
    }

    public void SaveToHistory(GrabFrame grabFrameToSave)
    {
        if (!DefaultSettings.UseHistory)
            return;

        EnsureImageHistoryLoaded();
        TouchHistoryCache();
        HistoryInfo historyInfo = grabFrameToSave.AsHistoryItem();
        string imgRandomName = Guid.NewGuid().ToString();
        HistoryInfo? prevHistory = string.IsNullOrEmpty(historyInfo.ID)
            ? null
            : HistoryWithImage.FirstOrDefault(h => h.ID == historyInfo.ID);

        if (prevHistory is null)
        {
            if (historyInfo.ImageContent is null)
                return;

            historyInfo.ImagePath = $"{imgRandomName}.bmp";
        }
        else
        {
            historyInfo.ImagePath = string.IsNullOrWhiteSpace(prevHistory.ImagePath)
                ? $"{imgRandomName}.bmp"
                : prevHistory.ImagePath;
            HistoryWithImage.Remove(prevHistory);
            prevHistory.ClearTransientImage();
            prevHistory.ClearTransientWordBorderData();
        }

        if (string.IsNullOrEmpty(historyInfo.ID))
            historyInfo.ID = Guid.NewGuid().ToString();

        PersistWordBorderData(historyInfo);

        if (historyInfo.ImageContent is not null && !string.IsNullOrWhiteSpace(historyInfo.ImagePath))
            FileUtilities.SaveImageFile(historyInfo.ImageContent, historyInfo.ImagePath, FileStorageKind.WithHistory);

        historyInfo.ClearTransientImage();
        HistoryWithImage.Add(historyInfo);

        MarkHistoryDirty();
    }

    public void SaveToHistory(HistoryInfo infoFromFullscreenGrab)
    {
        if (!DefaultSettings.UseHistory || infoFromFullscreenGrab.ImageContent is null)
            return;

        EnsureImageHistoryLoaded();
        TouchHistoryCache();

        if (string.IsNullOrWhiteSpace(infoFromFullscreenGrab.ID))
            infoFromFullscreenGrab.ID = Guid.NewGuid().ToString();

        string imgRandomName = Guid.NewGuid().ToString();

        FileUtilities.SaveImageFile(infoFromFullscreenGrab.ImageContent, $"{imgRandomName}.bmp", FileStorageKind.WithHistory);

        infoFromFullscreenGrab.ImagePath = $"{imgRandomName}.bmp";

        PersistWordBorderData(infoFromFullscreenGrab);
        infoFromFullscreenGrab.ClearTransientImage();
        HistoryWithImage.Add(infoFromFullscreenGrab);

        DisposeCachedBitmap();

        MarkHistoryDirty();
    }

    public void SaveToHistory(EditTextWindow etwToSave)
    {
        if (!DefaultSettings.UseHistory)
            return;

        EnsureTextHistoryLoaded();
        TouchHistoryCache();
        HistoryInfo historyInfo = etwToSave.AsHistoryItem();

        foreach (HistoryInfo inHistoryItem in HistoryTextOnly)
        {
            if (inHistoryItem.SourceMode != TextGrabMode.EditText)
                continue;

            if (inHistoryItem.TextContent == historyInfo.TextContent)
            {
                inHistoryItem.CaptureDateTime = DateTimeOffset.Now;
                MarkHistoryDirty();
                return;
            }
        }

        HistoryTextOnly.Add(historyInfo);

        MarkHistoryDirty();
    }

    public void WriteHistory()
    {
        if (!_hasPendingWrite)
            return;

        if (_textHistoryLoaded)
            WriteHistoryFiles(HistoryTextOnly, nameof(HistoryTextOnly), maxHistoryTextOnly);

        if (_imageHistoryLoaded)
        {
            ClearOldImages();
            PersistWordBorderData(HistoryWithImage);
            WriteHistoryFiles(HistoryWithImage, nameof(HistoryWithImage), maxHistoryWithImages);
            DeleteUnusedWordBorderFiles(HistoryWithImage);
        }

        _hasPendingWrite = false;
    }

    public void RemoveTextHistoryItem(HistoryInfo historyItem)
    {
        EnsureTextHistoryLoaded();
        TouchHistoryCache();
        HistoryTextOnly.Remove(historyItem);

        MarkHistoryDirty();
    }

    public void RemoveImageHistoryItem(HistoryInfo historyItem)
    {
        EnsureImageHistoryLoaded();
        TouchHistoryCache();
        HistoryWithImage.Remove(historyItem);
        historyItem.ClearTransientImage();
        historyItem.ClearTransientWordBorderData();
        DeleteHistoryArtifacts(historyItem);

        MarkHistoryDirty();
    }

    public HistoryInfo? GetImageHistoryById(string historyId)
    {
        if (string.IsNullOrWhiteSpace(historyId))
            return null;

        EnsureImageHistoryLoaded();
        TouchHistoryCache();
        return HistoryWithImage.FirstOrDefault(history => history.ID == historyId);
    }

    public HistoryInfo? GetTextHistoryById(string historyId)
    {
        if (string.IsNullOrWhiteSpace(historyId))
            return null;

        EnsureTextHistoryLoaded();
        TouchHistoryCache();
        return HistoryTextOnly.FirstOrDefault(history => history.ID == historyId);
    }

    public async Task<List<WordBorderInfo>> GetWordBorderInfosAsync(HistoryInfo history)
    {
        TouchHistoryCache();

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

    public void ReleaseLoadedHistories()
    {
        if (_hasPendingWrite)
            WriteHistory();

        ReleaseLoadedHistoriesCore();
    }

    #endregion Public Methods

    #region Private Methods

    private static async Task<List<HistoryInfo>> LoadHistoryAsync(string fileName)
    {
        string rawText = await FileUtilities.GetTextFileAsync($"{fileName}.json", FileStorageKind.WithHistory);

        if (string.IsNullOrWhiteSpace(rawText)) return [];

        List<HistoryInfo>? tempHistory = JsonSerializer.Deserialize<List<HistoryInfo>>(rawText, HistoryJsonOptions);

        if (tempHistory is List<HistoryInfo> jsonList && jsonList.Count > 0)
            return tempHistory;

        return [];
    }

    private static void WriteHistoryFiles(List<HistoryInfo> history, string fileName, int maxNumberToSave)
    {
        string historyAsJson = JsonSerializer
            .Serialize(history
                .OrderBy(x => x.CaptureDateTime)
                .TakeLast(maxNumberToSave),
            HistoryJsonOptions);

        try
        {
            FileUtilities.SaveTextFile(historyAsJson, $"{fileName}.json", FileStorageKind.WithHistory);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save history json file. {ex.Message}");
        }
    }
    private void ClearOldImages()
    {
        int numberToRemove = HistoryWithImage.Count - maxHistoryWithImages;

        if (numberToRemove < 1)
            return;

        List<HistoryInfo> imagesToRemove = [.. HistoryWithImage.Take(numberToRemove)];

        for (int i = 0; i < numberToRemove; i++)
            HistoryWithImage.RemoveAt(0);

        foreach (HistoryInfo infoItem in imagesToRemove)
            DeleteHistoryArtifacts(infoItem);

        ClearTransientHistoryPayloads(imagesToRemove);
    }

    private void DisposeCachedBitmap()
    {
        if (_cachedBitmapHandle is nint bmpH)
        {
            NativeMethods.DeleteObject(bmpH);
            _cachedBitmapHandle = null;
        }

        CachedBitmap?.Dispose();
        CachedBitmap = null;
    }

    private static void ClearTransientHistoryPayloads(IEnumerable<HistoryInfo> historyItems)
    {
        foreach (HistoryInfo historyItem in historyItems)
        {
            historyItem.ClearTransientImage();
            historyItem.ClearTransientWordBorderData();
        }
    }

    private void EnsureImageHistoryLoaded()
    {
        if (_imageHistoryLoaded)
            return;

        HistoryWithImage = LoadHistoryBlocking(nameof(HistoryWithImage));
        _imageHistoryLoaded = true;
        NormalizeHistoryIds(HistoryWithImage);

        if (MigrateWordBorderDataToSidecarFiles(HistoryWithImage))
            MarkHistoryDirty();
    }

    private void EnsureTextHistoryLoaded()
    {
        if (_textHistoryLoaded)
            return;

        HistoryTextOnly = LoadHistoryBlocking(nameof(HistoryTextOnly));
        _textHistoryLoaded = true;
        NormalizeHistoryIds(HistoryTextOnly);
    }

    private void HistoryCacheReleaseTimer_Tick(object? sender, EventArgs e)
    {
        if (_hasPendingWrite)
            return;

        if (_lastHistoryAccessUtc == DateTimeOffset.MinValue)
            return;

        if (DateTimeOffset.UtcNow - _lastHistoryAccessUtc < historyCacheIdleLifetime)
            return;

        ReleaseLoadedHistoriesCore();
    }

    private static List<HistoryInfo> LoadHistoryBlocking(string fileName)
    {
        return Task.Run(() => LoadHistoryAsync(fileName)).GetAwaiter().GetResult();
    }

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

    private void DeleteHistoryArtifacts(HistoryInfo historyItem)
    {
        DeleteHistoryFile(historyItem.ImagePath);
        DeleteHistoryFile(historyItem.WordBorderInfoFileName);
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

    private void DeleteUnusedWordBorderFiles(IEnumerable<HistoryInfo> historyItems)
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

    private void MarkHistoryDirty()
    {
        _hasPendingWrite = true;
        TouchHistoryCache();
        saveTimer.Stop();
        saveTimer.Start();
    }

    private bool MigrateWordBorderDataToSidecarFiles(IEnumerable<HistoryInfo> historyItems)
    {
        bool migratedAnyWordBorderData = false;

        foreach (HistoryInfo historyItem in historyItems)
        {
            if (PersistWordBorderData(historyItem))
                migratedAnyWordBorderData = true;
        }

        return migratedAnyWordBorderData;
    }

    private static void PersistWordBorderData(IEnumerable<HistoryInfo> historyItems)
    {
        foreach (HistoryInfo historyItem in historyItems)
            PersistWordBorderData(historyItem);
    }

    private static bool PersistWordBorderData(HistoryInfo historyItem)
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
        historyItem.ClearTransientWordBorderData();
        return true;
    }

    private void NormalizeHistoryIds(List<HistoryInfo> historyItems)
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

        if (updatedAnyIds)
            MarkHistoryDirty();
    }

    private void ReleaseLoadedHistoriesCore()
    {
        ClearTransientHistoryPayloads(HistoryWithImage);
        HistoryWithImage.Clear();
        HistoryTextOnly.Clear();
        _imageHistoryLoaded = false;
        _textHistoryLoaded = false;
        _lastHistoryAccessUtc = DateTimeOffset.MinValue;
        historyCacheReleaseTimer.Stop();
    }

    private void SaveTimer_Tick(object? sender, EventArgs e)
    {
        saveTimer.Stop();
        WriteHistory();
        DisposeCachedBitmap();
    }

    private void TouchHistoryCache()
    {
        _lastHistoryAccessUtc = DateTimeOffset.UtcNow;

        if (_textHistoryLoaded || _imageHistoryLoaded)
            historyCacheReleaseTimer.Start();
    }

    #endregion Private Methods
}
