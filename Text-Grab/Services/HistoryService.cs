using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Text_Grab.Models;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab.Services;

public class HistoryService
{
    private List<HistoryInfo> History { get; set; } = new();
    private string historyFilename = "History.json";
    private readonly string? exePath = Path.GetDirectoryName(System.AppContext.BaseDirectory);

    private (bool hasHistory, HistoryInfo? lastHistoryItem) GetLastHistory()
    {
        if (History is null)
            return (false, null);

        return (true, History.LastOrDefault());
    }

    private void GetHistoryAsGrabFrame(HistoryInfo historyInfo)
    {

    }

    private void GetHistoryAsEditTextWindow(HistoryInfo historyInfo)
    {

    }

    public HistoryService()
    {
        LoadHistory();
    }

    public async Task LoadHistory()
    {
        History.Clear();
        
        string historyFilePath = $"{exePath}\\{historyFilename}";

        string historyAsJson = JsonSerializer.Serialize(History);

        await File.WriteAllTextAsync(historyFilePath, historyAsJson);
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
        string imgPath = $"{exePath}\\{historyFilename}.bmp";

        if (historyInfo.ImageContent is not null)
            historyInfo.ImageContent.Save(imgPath);

        historyInfo.ImagePath = imgPath;

        History.Add(historyInfo);

        // Need to break WordBorders out as WordBorder Info to serialize
    }

    public void SaveToHistory(EditTextWindow etwToSave)
    {

    }
}
