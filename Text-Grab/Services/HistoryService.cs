using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Text_Grab.Models;
using Text_Grab.Views;

namespace Text_Grab.Services;

public class HistoryService
{
    private List<HistoryInfo>? History { get; set; } = null;


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

    public async Task LoadHistory()
    {
        if (History is not null)
            return;


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

    }

    public void SaveToHistory(EditTextWindow etwToSave)
    {

    }
}
