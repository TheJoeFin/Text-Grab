using Humanizer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    private List<HistoryInfo> HistoryTextOnly = new();
    private List<HistoryInfo> HistoryWithImage = new();
    private DispatcherTimer saveTimer = new();
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    #endregion Fields

    #region Constructors

    public HistoryService()
    {
        saveTimer.Interval = new(0, 0, 0, 0, 500);
        saveTimer.Tick += SaveTimer_Tick;
    }

    #endregion Constructors

    #region Properties

    public Bitmap? CachedBitmap { get; set; }

    #endregion Properties

    #region Public Methods

    public void CacheLastBitmap(Bitmap bmp)
    {
        CachedBitmap = null;
        CachedBitmap = bmp;
    }

    public void DeleteHistory()
    {
        HistoryWithImage.Clear();
        HistoryTextOnly.Clear();

        FileUtilities.TryDeleteHistoryDirectory();
    }

    public List<HistoryInfo> GetEditWindows()
    {
        return HistoryTextOnly;
    }

    public HistoryInfo? GetLastFullScreenGrabInfo()
    {
        return HistoryWithImage.Where(h => h.SourceMode == TextGrabMode.Fullscreen).LastOrDefault();
    }

    public bool HasAnyFullscreenHistory()
    {
        return HistoryWithImage.Any(h => h.SourceMode == TextGrabMode.Fullscreen);
    }

    public bool GetLastHistoryAsGrabFrame()
    {
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
        HistoryInfo? lastHistoryItem = HistoryTextOnly.LastOrDefault();

        if (lastHistoryItem is not HistoryInfo historyInfo)
            return string.Empty;

        return historyInfo.TextContent;
    }

    public List<HistoryInfo> GetRecentGrabs()
    {
        return HistoryWithImage;
    }

    public bool HasAnyHistoryWithImages()
    {
        return HistoryWithImage.Count > 0;
    }

    public async Task LoadHistories()
    {
        HistoryTextOnly = await LoadHistory(nameof(HistoryTextOnly));
        HistoryWithImage = await LoadHistory(nameof(HistoryWithImage));
    }

    public async Task PopulateMenuItemWithRecentGrabs(MenuItem recentGrabsMenuItem)
    {
        List<HistoryInfo> grabsHistory = GetRecentGrabs();
        grabsHistory = grabsHistory.OrderByDescending(x => x.CaptureDateTime).ToList();

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
            menuItem.Click += (object sender, RoutedEventArgs args) =>
            {
                GrabFrame grabFrame = new(history);
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

        HistoryInfo historyInfo = grabFrameToSave.AsHistoryItem();
        string imgRandomName = Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(historyInfo.ID))
        {
            if (historyInfo.ImageContent is null)
                return;

            historyInfo.ID = Guid.NewGuid().ToString();

            FileUtilities.SaveImageFile(historyInfo.ImageContent, $"{imgRandomName}.bmp", FileStorageKind.WithHistory);
            historyInfo.ImagePath = $"{imgRandomName}.bmp";
        }
        else
        {
            HistoryInfo? prevHistory = HistoryWithImage.Where(h => h.ID == historyInfo.ID).FirstOrDefault();

            if (prevHistory is not null)
            {
                historyInfo.ImagePath = prevHistory.ImagePath;
                HistoryWithImage.Remove(prevHistory);
            }
        }

        HistoryWithImage.Add(historyInfo);

        saveTimer.Stop();
        saveTimer.Start();
    }

    public void SaveToHistory(HistoryInfo infoFromFullscreenGrab)
    {
        if (!DefaultSettings.UseHistory || infoFromFullscreenGrab.ImageContent is null)
            return;

        string imgRandomName = Guid.NewGuid().ToString();

        FileUtilities.SaveImageFile(infoFromFullscreenGrab.ImageContent, $"{imgRandomName}.bmp", FileStorageKind.WithHistory);

        infoFromFullscreenGrab.ImagePath = $"{imgRandomName}.bmp";

        HistoryWithImage.Add(infoFromFullscreenGrab);

        if (CachedBitmap is not null)
        {
            NativeMethods.DeleteObject(CachedBitmap.GetHbitmap());
            CachedBitmap = null;
        }

        saveTimer.Stop();
        saveTimer.Start();
    }

    public void SaveToHistory(EditTextWindow etwToSave)
    {
        if (!DefaultSettings.UseHistory)
            return;

        HistoryInfo historyInfo = etwToSave.AsHistoryItem();

        foreach (HistoryInfo inHistoryItem in HistoryTextOnly)
        {
            if (inHistoryItem.SourceMode != TextGrabMode.EditText)
                continue;

            if (inHistoryItem.TextContent == historyInfo.TextContent)
            {
                inHistoryItem.CaptureDateTime = DateTimeOffset.Now;
                return;
            }
        }

        HistoryTextOnly.Add(historyInfo);

        saveTimer.Stop();
        saveTimer.Start();
    }

    public void WriteHistory()
    {
        if (HistoryTextOnly.Count > 0)
            WriteHistoryFiles(HistoryTextOnly, nameof(HistoryTextOnly), maxHistoryTextOnly);

        if (HistoryWithImage.Count > 0)
        {
            ClearOldImages();
            WriteHistoryFiles(HistoryWithImage, nameof(HistoryWithImage), maxHistoryWithImages);
        }
    }
    #endregion Public Methods

    #region Private Methods

    private static async Task<List<HistoryInfo>> LoadHistory(string fileName)
    {
        string rawText = await FileUtilities.GetTextFileAsync($"{fileName}.json",FileStorageKind.WithHistory);

        if (string.IsNullOrWhiteSpace(rawText)) return new List<HistoryInfo>();

        var tempHistory = JsonSerializer.Deserialize<List<HistoryInfo>>(rawText);

        if (tempHistory is List<HistoryInfo> jsonList && jsonList.Count > 0)
            return tempHistory;

        return new List<HistoryInfo>();
    }

    private static void WriteHistoryFiles(List<HistoryInfo> history, string fileName, int maxNumberToSave)
    {
        JsonSerializerOptions options = new()
        {
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        string historyAsJson = JsonSerializer
            .Serialize(history
                .OrderBy(x => x.CaptureDateTime)
                .TakeLast(maxNumberToSave),
            options);

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

        List<HistoryInfo> imagesToRemove = HistoryWithImage.Take(numberToRemove).ToList();

        for (int i = 0; i < numberToRemove; i++)
            HistoryWithImage.RemoveAt(0);

        foreach (HistoryInfo infoItem in imagesToRemove)
        {
            if (File.Exists(infoItem.ImagePath))
                File.Delete(infoItem.ImagePath);
        }
    }

    private void SaveTimer_Tick(object? sender, EventArgs e)
    {
        saveTimer.Stop();
        WriteHistory();
        CachedBitmap = null;
    }
    #endregion Private Methods
}
