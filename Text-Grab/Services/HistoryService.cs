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
    private static readonly string? exePath = Path.GetDirectoryName(System.AppContext.BaseDirectory);
    private static readonly string historyDirectory = $"{exePath}\\history";
    private static readonly string historyFilename = "History.json";
    private static readonly string historyFilePath = $"{historyDirectory}\\{historyFilename}";

    private DispatcherTimer saveTimer = new();

    public HistoryService()
    {
        saveTimer.Interval = new(0, 0, 0, 0, 500);
        saveTimer.Tick += SaveTimer_Tick;
    }

    private List<HistoryInfo> History { get; set; } = new();

    public Bitmap? CachedBitmap { get; set; }

    public bool GetLastHistoryAsEditTextWindow()
    {
        (bool hasHistory, HistoryInfo? lastHistoryItem) = GetLastHistory();

        if (!hasHistory || lastHistoryItem is not HistoryInfo historyInfo)
            return false;

        EditTextWindow etw = new(historyInfo);
        etw.Show();
        return true;
    }

    public bool GetLastHistoryAsGrabFrame()
    {
        HistoryInfo? lastHistoryItem = History.Where(h => h.SourceMode != TextGrabMode.EditText).LastOrDefault();

        if (lastHistoryItem is not HistoryInfo historyInfo)
            return false;

        GrabFrame grabFrame = new(historyInfo);
        grabFrame.Show();
        return true;
    }

    public async Task LoadHistory()
    {
        if (!File.Exists(historyFilePath))
            return;

        string rawText = await File.ReadAllTextAsync(historyFilePath);

        if (string.IsNullOrWhiteSpace(rawText)) return;

        var tempHistory = JsonSerializer.Deserialize<List<HistoryInfo>>(rawText);

        History.Clear();
        if (tempHistory is List<HistoryInfo> jsonList && jsonList.Count > 0)
            History = new(tempHistory);
    }

    public void SaveToHistory(GrabFrame grabFrameToSave)
    {
        if (!Settings.Default.UseHistory)
            return;

        HistoryInfo historyInfo = grabFrameToSave.AsHistoryItem();
        string imgRandomName = Guid.NewGuid().ToString();

        if (!Directory.Exists(historyDirectory))
            Directory.CreateDirectory(historyDirectory);

        string imgPath = $"{historyDirectory}\\{imgRandomName}.bmp";

        if (historyInfo.ImageContent is not null)
            historyInfo.ImageContent.Save(imgPath);

        historyInfo.ImagePath = imgPath;

        History.Add(historyInfo);

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

        History.Add(infoFromFullscreenGrab);

        saveTimer.Stop();
        saveTimer.Start();
    }

    public void SaveToHistory(EditTextWindow etwToSave)
    {
        if (!Settings.Default.UseHistory)
            return;

        HistoryInfo historyInfo = etwToSave.AsHistoryItem();

        foreach (HistoryInfo inHistoryItem in History)
        {
            if (inHistoryItem.SourceMode != TextGrabMode.EditText)
                continue;

            if (inHistoryItem.TextContent == historyInfo.TextContent)
            {
                inHistoryItem.CaptureDateTime = DateTimeOffset.Now;
                return;
            }
        }

        History.Add(historyInfo);

        saveTimer.Stop();
        saveTimer.Start();
    }

    public void WriteHistory()
    {
        if (History.Count == 0)
            return;

        JsonSerializerOptions options = new()
        {
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        string historyAsJson = JsonSerializer
            .Serialize(History
                .OrderBy(x => x.CaptureDateTime)
                .TakeLast(50),
            options);

        try
        {
            if (!Directory.Exists(historyDirectory))
                Directory.CreateDirectory(historyDirectory);

            if (!File.Exists(historyFilePath))
                File.Create(historyFilePath);

            File.WriteAllText(historyFilePath, historyAsJson);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save history json file. {ex.Message}");
        }
    }

    internal void CacheLastBitmap(Bitmap bmp)
    {
        CachedBitmap = null;
        CachedBitmap = bmp;
    }

    internal void DeleteHistory()
    {
        if (!Directory.Exists(historyDirectory))
            return;

        History.Clear();
        Directory.Delete(historyDirectory, true);
    }

    internal List<HistoryInfo> GetEditWindows()
    {
        return History.Where(h => h.SourceMode == TextGrabMode.EditText).ToList();
    }

    private (bool hasHistory, HistoryInfo? lastHistoryItem) GetLastHistory()
    {
        if (History is null || History.Count == 0)
            return (false, null);

        return (true, History.LastOrDefault());
    }

    private void SaveTimer_Tick(object? sender, EventArgs e)
    {
        saveTimer.Stop();
        WriteHistory();
        CachedBitmap = null;
    }
}
