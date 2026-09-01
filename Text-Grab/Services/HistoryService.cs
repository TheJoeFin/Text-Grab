using Humanizer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Text_Grab.Services;

/// <summary>
/// The live, app-bound half of the grab history: the in-memory lists, the DispatcherTimer that
/// debounces writes and the one that releases the cache when it goes idle, the cached fullscreen
/// bitmap, the recent-grabs menu, and opening a history entry back into a GrabFrame.
///
/// Everything that only touches the disk - loading, writing, normalization, the word-border
/// sidecar files and the retention rules - moved to
/// <see cref="Text_Grab.Utilities.HistoryFileUtilities"/> in batch 6e of the Core split. What
/// holds the rest here is state plus WPF: DispatcherTimer and MenuItem are WindowsBase and
/// PresentationFramework, and SaveToHistory takes a GrabFrame and an EditTextWindow.
/// </summary>
public partial class HistoryService : IDisposable
{
    #region Fields

    private static readonly TimeSpan historyCacheCheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan historyCacheIdleLifetime = TimeSpan.FromMinutes(2);
    private List<HistoryInfo> HistoryTextOnly = [];
    private List<HistoryInfo> HistoryWithImage = [];
    private readonly DispatcherTimer saveTimer = new();
    private readonly DispatcherTimer historyCacheReleaseTimer = new();
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool _textHistoryLoaded;
    private bool _imageHistoryLoaded;
    private bool _hasPendingWrite;
    private bool _disposed;
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
        // Acquire the HBITMAP first so a failure here doesn't leave CachedBitmap
        // pointing at a bitmap whose handle we never recorded.
        nint newHandle = bmp.GetHbitmap();

        DisposeCachedBitmap();
        CachedBitmap = bmp;
        _cachedBitmapHandle = newHandle;
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
        HistoryInfo? lastHistoryItem = HistoryFileUtilities.GetMostRecentGrab(HistoryWithImage);

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
        return [.. HistoryWithImage.Where(history => !history.IsPdfDocument)];
    }

    public List<HistoryInfo> GetRecentPdfDocuments()
    {
        EnsureImageHistoryLoaded();
        TouchHistoryCache();
        return [.. HistoryWithImage.Where(history => history.IsPdfDocument)];
    }

    public bool HasAnyRecentGrabs()
    {
        EnsureImageHistoryLoaded();
        TouchHistoryCache();
        return HistoryWithImage.Any(history => !history.IsPdfDocument);
    }

    public async Task LoadHistories()
    {
        saveTimer.Stop();
        historyCacheReleaseTimer.Stop();
        _hasPendingWrite = false;
        ReleaseLoadedHistoriesCore();

        (HistoryTextOnly, bool textHistoryNeedsRewrite) =
            await HistoryFileUtilities.LoadHistoryAsync(nameof(HistoryTextOnly));
        _textHistoryLoaded = true;
        // Both normalizers mutate, so neither may be short-circuited away by the other.
        bool normalizedTextIds = HistoryFileUtilities.NormalizeHistoryIds(HistoryTextOnly);
        bool normalizedTextCompatibilityData = HistoryFileUtilities.NormalizeHistoryCompatibilityData(HistoryTextOnly);

        if (normalizedTextIds || textHistoryNeedsRewrite || normalizedTextCompatibilityData)
            MarkHistoryDirty();

        (HistoryWithImage, bool imageHistoryNeedsRewrite) =
            await HistoryFileUtilities.LoadHistoryAsync(nameof(HistoryWithImage));
        _imageHistoryLoaded = true;
        // Both normalizers mutate, so neither may be short-circuited away by the other.
        bool normalizedImageIds = HistoryFileUtilities.NormalizeHistoryIds(HistoryWithImage);
        bool normalizedImageCompatibilityData = HistoryFileUtilities.NormalizeHistoryCompatibilityData(HistoryWithImage);

        if (normalizedImageIds || imageHistoryNeedsRewrite || normalizedImageCompatibilityData)
            MarkHistoryDirty();

        if (HistoryFileUtilities.EnsureWordBorderSidecarFiles(HistoryWithImage))
            MarkHistoryDirty();

        TouchHistoryCache();
    }

    public async Task PopulateMenuItemWithRecentGrabs(MenuItem recentGrabsMenuItem)
    {
        await PopulateMenuItemWithImageHistory(recentGrabsMenuItem, GetRecentGrabs());
    }

    public async Task PopulateMenuItemWithRecentPdfs(MenuItem recentPdfsMenuItem)
    {
        await PopulateMenuItemWithImageHistory(recentPdfsMenuItem, GetRecentPdfDocuments());
    }

    private async Task PopulateMenuItemWithImageHistory(MenuItem historyMenuItem, List<HistoryInfo> historyItems)
    {
        historyItems = [.. historyItems.OrderByDescending(x => x.CaptureDateTime)];

        ClearRecentGrabsMenuItems(historyMenuItem);

        if (historyItems.Count < 1)
        {
            historyMenuItem.IsEnabled = false;
            return;
        }

        historyMenuItem.IsEnabled = true;

        string historyBasePath = await FileUtilities.GetPathToHistory();

        foreach (HistoryInfo history in historyItems)
        {
            string imageFullPath = Path.Combine(historyBasePath, history.ImagePath);
            if (string.IsNullOrWhiteSpace(history.ImagePath) || !File.Exists(imageFullPath))
                continue;

            MenuItem menuItem = new() { Tag = history.ID };
            menuItem.Click += RecentGrabMenuItem_Click;

            string snippet = history.TextContent.Trim().Replace("\t", " ").MakeStringSingleLine().Truncate(40);
            string sourceName = history.IsPdfDocument && !string.IsNullOrWhiteSpace(history.SourcePath)
                ? $"{Path.GetFileName(history.SourcePath)} | "
                : string.Empty;
            menuItem.Header = $"{history.CaptureDateTime.Humanize().Trim()} | {sourceName}{snippet}";
            menuItem.Icon = new SymbolIcon
            {
                Symbol = history.IsPdfDocument
                    ? SymbolRegular.DocumentSearch24
                    : history.EditorMode switch
                {
                    EtwEditorMode.Spreadsheet => SymbolRegular.Table24,
                    EtwEditorMode.Markdown => SymbolRegular.Markdown20,
                    _ => SymbolRegular.TextT24,
                },
            };
            historyMenuItem.Items.Add(menuItem);
        }
    }

    public void ClearRecentGrabsMenuItems(MenuItem recentGrabsMenuItem)
    {
        foreach (object item in recentGrabsMenuItem.Items)
        {
            if (item is MenuItem oldItem)
                oldItem.Click -= RecentGrabMenuItem_Click;
        }
        recentGrabsMenuItem.Items.Clear();
    }

    private void RecentGrabMenuItem_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string historyId)
            return;

        HistoryInfo? selectedHistory = GetImageHistoryById(historyId);
        if (selectedHistory is null)
        {
            menuItem.IsEnabled = false;
            return;
        }

        GrabFrame grabFrame = selectedHistory.IsPdfDocument
            && !string.IsNullOrWhiteSpace(selectedHistory.SourcePath)
            && File.Exists(selectedHistory.SourcePath)
                ? new GrabFrame(selectedHistory, selectedHistory.SourcePath)
                : new GrabFrame(selectedHistory);
        try { grabFrame.Show(); }
        catch { menuItem.IsEnabled = false; }
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

        HistoryFileUtilities.NormalizeHistoryCompatibilityData(historyInfo);
        HistoryFileUtilities.PersistWordBorderData(historyInfo);

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

        HistoryFileUtilities.NormalizeHistoryCompatibilityData(infoFromFullscreenGrab);
        HistoryFileUtilities.PersistWordBorderData(infoFromFullscreenGrab);
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
        HistoryFileUtilities.NormalizeHistoryCompatibilityData(historyInfo);

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
        {
            HistoryFileUtilities.NormalizeHistoryCompatibilityData(HistoryTextOnly);
            HistoryFileUtilities.WriteHistoryFiles(
                HistoryTextOnly,
                nameof(HistoryTextOnly),
                HistoryFileUtilities.MaxHistoryTextOnly);
        }

        if (_imageHistoryLoaded)
        {
            ClearOldImages();
            HistoryFileUtilities.NormalizeHistoryCompatibilityData(HistoryWithImage);
            HistoryFileUtilities.PersistWordBorderData(HistoryWithImage);
            HistoryFileUtilities.WriteHistoryFiles(
                HistoryWithImage,
                nameof(HistoryWithImage),
                HistoryFileUtilities.MaxHistoryWithImages + HistoryFileUtilities.MaxHistoryPdfDocuments);
            HistoryFileUtilities.DeleteUnusedWordBorderFiles(HistoryWithImage);
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
        HistoryFileUtilities.DeleteHistoryArtifacts(historyItem);

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

    public Task<List<WordBorderInfo>> GetWordBorderInfosAsync(HistoryInfo history)
    {
        TouchHistoryCache();
        return HistoryFileUtilities.GetWordBorderInfosAsync(history);
    }

    public void ReleaseLoadedHistories()
    {
        if (_hasPendingWrite)
            WriteHistory();

        ReleaseLoadedHistoriesCore();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        saveTimer.Stop();
        saveTimer.Tick -= SaveTimer_Tick;

        historyCacheReleaseTimer.Stop();
        historyCacheReleaseTimer.Tick -= HistoryCacheReleaseTimer_Tick;

        if (_hasPendingWrite)
            WriteHistory();

        DisposeCachedBitmap();
        ReleaseLoadedHistoriesCore();

        GC.SuppressFinalize(this);
    }

    #endregion Public Methods

    #region Private Methods

    private void ClearOldImages()
    {
        List<HistoryInfo> imagesToRemove = HistoryFileUtilities.GetExcessVisualHistoryItems(HistoryWithImage);

        if (imagesToRemove.Count == 0)
            return;

        foreach (HistoryInfo historyItem in imagesToRemove)
            HistoryWithImage.Remove(historyItem);

        foreach (HistoryInfo infoItem in imagesToRemove)
            HistoryFileUtilities.DeleteHistoryArtifacts(infoItem);

        HistoryFileUtilities.ClearTransientHistoryPayloads(imagesToRemove);
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

    private void EnsureImageHistoryLoaded()
    {
        if (_imageHistoryLoaded)
            return;

        (HistoryWithImage, bool imageHistoryNeedsRewrite) =
            HistoryFileUtilities.LoadHistoryBlocking(nameof(HistoryWithImage));
        _imageHistoryLoaded = true;
        // Both normalizers mutate, so neither may be short-circuited away by the other.
        bool normalizedIds = HistoryFileUtilities.NormalizeHistoryIds(HistoryWithImage);
        bool normalizedCompatibilityData = HistoryFileUtilities.NormalizeHistoryCompatibilityData(HistoryWithImage);

        if (normalizedIds || imageHistoryNeedsRewrite || normalizedCompatibilityData)
            MarkHistoryDirty();

        if (HistoryFileUtilities.EnsureWordBorderSidecarFiles(HistoryWithImage))
            MarkHistoryDirty();
    }

    private void EnsureTextHistoryLoaded()
    {
        if (_textHistoryLoaded)
            return;

        (HistoryTextOnly, bool textHistoryNeedsRewrite) =
            HistoryFileUtilities.LoadHistoryBlocking(nameof(HistoryTextOnly));
        _textHistoryLoaded = true;
        // Both normalizers mutate, so neither may be short-circuited away by the other.
        bool normalizedIds = HistoryFileUtilities.NormalizeHistoryIds(HistoryTextOnly);
        bool normalizedCompatibilityData = HistoryFileUtilities.NormalizeHistoryCompatibilityData(HistoryTextOnly);

        if (normalizedIds || textHistoryNeedsRewrite || normalizedCompatibilityData)
            MarkHistoryDirty();
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

    private void MarkHistoryDirty()
    {
        _hasPendingWrite = true;
        TouchHistoryCache();
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void ReleaseLoadedHistoriesCore()
    {
        HistoryFileUtilities.ClearTransientHistoryPayloads(HistoryWithImage);
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
