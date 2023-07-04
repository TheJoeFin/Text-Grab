using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab.Services;

public class HistoryService
{
    private List<HistoryInfo> History { get; set; } = new();
    private static readonly string? exePath = Path.GetDirectoryName(System.AppContext.BaseDirectory);
    private static readonly string historyFilename = "History.json";
    private static readonly string historyDirectory = $"{exePath}\\history";
    private static readonly string historyFilePath = $"{historyDirectory}\\{historyFilename}";

    private (bool hasHistory, HistoryInfo? lastHistoryItem) GetLastHistory()
    {
        if (History is null || History.Count == 0)
            return (false, null);

        return (true, History.LastOrDefault());
    }

    private void GetHistoryAsGrabFrame(HistoryInfo historyInfo)
    {
        GrabFrame grabFrame = new(historyInfo);
        grabFrame.Show();
    }

    private void GetHistoryAsEditTextWindow(HistoryInfo historyInfo)
    {

    }

    public void WriteHistory()
    {
        if (History.Count == 0) return;

        string historyAsJson = JsonSerializer.Serialize(History);

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

    public bool GetLastHistoryAsGrabFrame()
    {
        (bool hasHistory, HistoryInfo? lastHistoryItem) = GetLastHistory();

        if (!hasHistory || lastHistoryItem is not HistoryInfo historyInfo)
            return false;

        GetHistoryAsGrabFrame(historyInfo);
        return true;
    }

    public bool GetLastHistoryAsEditTextWindow()
    {
        (bool hasHistory, HistoryInfo? lastHistoryItem) = GetLastHistory();

        if (!hasHistory || lastHistoryItem is not HistoryInfo historyInfo)
            return false;

        GetHistoryAsEditTextWindow(historyInfo);
        return true;
    }

    public void SaveToHistory(GrabFrame grabFrameToSave)
    {
        HistoryInfo historyInfo = grabFrameToSave.AsHistoryItem();
        string imgRandomName = Guid.NewGuid().ToString();
        string imgPath = $"{exePath}\\history\\{imgRandomName}.bmp";

        if (historyInfo.ImageContent is not null)
            historyInfo.ImageContent.Save(imgPath);

        historyInfo.ImagePath = imgPath;

        History.Add(historyInfo);
    }

    public void SaveToHistory(FullscreenGrab fsgToSave)
    {

    }

    public void SaveToHistory(EditTextWindow etwToSave)
    {

    }
}
