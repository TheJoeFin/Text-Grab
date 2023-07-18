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
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab.Services;

public class HistoryService
{
    #region Private Fields

    private static readonly string? exePath = Path.GetDirectoryName(System.AppContext.BaseDirectory);
    private static readonly string historyDirectory = $"{exePath}\\history";

    private static readonly int maxHistoryTextOnly = 100;
    private static readonly int maxHistoryWithImages = 10;
    private List<HistoryInfo> HistoryTextOnly = new();
    private List<HistoryInfo> HistoryWithImage = new();
    private DispatcherTimer saveTimer = new();
    #endregion Private Fields

    #region Public Constructors

    public HistoryService()
    {
        saveTimer.Interval = new(0, 0, 0, 0, 500);
        saveTimer.Tick += SaveTimer_Tick;
    }

    #endregion Public Constructors

    #region Public Properties

    public Bitmap? CachedBitmap { get; set; }

    #endregion Public Properties

    #region Public Methods

    public void CacheLastBitmap(Bitmap bmp)
    {
        CachedBitmap = null;
        CachedBitmap = bmp;
    }

    public void DeleteHistory()
    {
        if (!Directory.Exists(historyDirectory))
            return;

        HistoryWithImage.Clear();
        HistoryTextOnly.Clear();
        Directory.Delete(historyDirectory, true);
    }

    public List<HistoryInfo> GetEditWindows()
    {
        return HistoryTextOnly;
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

    public void SaveToHistory(GrabFrame grabFrameToSave)
    {
        if (!Settings.Default.UseHistory)
            return;

        HistoryInfo historyInfo = grabFrameToSave.AsHistoryItem();
        string imgRandomName = Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(historyInfo.ID))
        {
            historyInfo.ID = Guid.NewGuid().ToString();

            if (!Directory.Exists(historyDirectory))
                Directory.CreateDirectory(historyDirectory);

            string imgPath = $"{historyDirectory}\\{imgRandomName}.bmp";

            if (historyInfo.ImageContent is not null)
                historyInfo.ImageContent.Save(imgPath);

            historyInfo.ImagePath = imgPath;
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
        if (!Settings.Default.UseHistory)
            return;

        string imgRandomName = Guid.NewGuid().ToString();

        if (!Directory.Exists(historyDirectory))
            Directory.CreateDirectory(historyDirectory);

        string imgPath = $"{historyDirectory}\\{imgRandomName}.bmp";

        if (infoFromFullscreenGrab.ImageContent is not null)
            infoFromFullscreenGrab.ImageContent.Save(imgPath);

        infoFromFullscreenGrab.ImagePath = imgPath;

        HistoryWithImage.Add(infoFromFullscreenGrab);

        saveTimer.Stop();
        saveTimer.Start();
    }

    public void SaveToHistory(EditTextWindow etwToSave)
    {
        if (!Settings.Default.UseHistory)
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

    public async Task WriteHistory()
    {
        if (HistoryTextOnly.Count > 0)
            await WriteHistoryFiles(HistoryTextOnly, nameof(HistoryTextOnly), maxHistoryTextOnly);

        if (HistoryWithImage.Count > 0)
        {
            ClearOldImages();
            await WriteHistoryFiles(HistoryWithImage, nameof(HistoryWithImage), maxHistoryWithImages);
        }
    }

    #endregion Public Methods

    #region Private Methods

    private static async Task<List<HistoryInfo>> LoadHistory(string fileName)
    {
        string historyFilePath = $"{historyDirectory}\\{fileName}.json";
        
        if (!File.Exists(historyFilePath))
            return new List<HistoryInfo>();

        string rawText = await File.ReadAllTextAsync(historyFilePath);

        if (string.IsNullOrWhiteSpace(rawText)) return new List<HistoryInfo>();

        var tempHistory = JsonSerializer.Deserialize<List<HistoryInfo>>(rawText);

        if (tempHistory is List<HistoryInfo> jsonList && jsonList.Count > 0)
            return tempHistory;

        return new List<HistoryInfo>();
    }

    private static async Task WriteHistoryFiles(List<HistoryInfo> history, string fileName, int maxNumberToSave)
    {
        string historyFilePath = $"{historyDirectory}\\{fileName}.json";

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
            if (!Directory.Exists(historyDirectory))
                Directory.CreateDirectory(historyDirectory);

            if (!File.Exists(historyFilePath))
                File.Create(historyFilePath);

            await File.WriteAllTextAsync(historyFilePath, historyAsJson);
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

    private async void SaveTimer_Tick(object? sender, EventArgs e)
    {
        saveTimer.Stop();
        await WriteHistory();
        CachedBitmap = null;
    }

    #endregion Private Methods
}
