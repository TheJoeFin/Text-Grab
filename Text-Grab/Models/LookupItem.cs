using Humanizer;
using System;

namespace Text_Grab.Models;

public enum LookupItemKind
{
    Simple = 0,
    EditWindow = 1,
    GrabFrame = 2,
    Link = 3,
}

public class LookupItem : IEquatable<LookupItem>
{
    public string shortValue { get; set; } = string.Empty;
    public string longValue { get; set; } = string.Empty;

    public LookupItemKind Kind { get; set; } = LookupItemKind.Simple;

    public LookupItem()
    {

    }

    public LookupItem(string sv, string lv)
    {
        shortValue = sv;
        longValue = lv;
    }

    public LookupItem(HistoryInfo historyInfo)
    {
        shortValue = historyInfo.CaptureDateTime.Humanize() + Environment.NewLine + historyInfo.CaptureDateTime.ToString("F");
        longValue = historyInfo.TextContent;
        HistoryItem = historyInfo;

        if (string.IsNullOrEmpty(historyInfo.ImagePath))
            Kind = LookupItemKind.EditWindow;
        else
            Kind = LookupItemKind.GrabFrame;
    }

    public HistoryInfo? HistoryItem { get; set; }

    public override string ToString() => $"{shortValue} {longValue}";

    public string ToCSVString() => $"{shortValue},{longValue}";

    public bool Equals(LookupItem? other)
    {
        if (other is null)
            return false;

        if (other.ToString() == ToString())
            return true;

        return false;
    }
}
