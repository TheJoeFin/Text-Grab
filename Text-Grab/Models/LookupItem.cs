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
    public string ShortValue { get; set; } = string.Empty;
    public string LongValue { get; set; } = string.Empty;

    public Wpf.Ui.Controls.SymbolRegular UiSymbol
    {
        get
        {
            return Kind switch
            {
                LookupItemKind.Simple => Wpf.Ui.Controls.SymbolRegular.Copy20,
                LookupItemKind.EditWindow => Wpf.Ui.Controls.SymbolRegular.Window24,
                LookupItemKind.GrabFrame => Wpf.Ui.Controls.SymbolRegular.PanelBottom20,
                LookupItemKind.Link => Wpf.Ui.Controls.SymbolRegular.Link24,
                _ => Wpf.Ui.Controls.SymbolRegular.Copy20,
            };
        }
    }

    public LookupItemKind Kind { get; set; } = LookupItemKind.Simple;

    public LookupItem()
    {

    }

    public LookupItem(string sv, string lv)
    {
        ShortValue = sv;
        LongValue = lv;
    }

    public LookupItem(HistoryInfo historyInfo)
    {
        ShortValue = historyInfo.CaptureDateTime.Humanize() + Environment.NewLine + historyInfo.CaptureDateTime.ToString("F");
        LongValue = historyInfo.TextContent.Length > 100 ? historyInfo.TextContent[..100].Trim() + "…" : historyInfo.TextContent.Trim();

        HistoryItem = historyInfo;

        if (string.IsNullOrEmpty(historyInfo.ImagePath))
            Kind = LookupItemKind.EditWindow;
        else
            Kind = LookupItemKind.GrabFrame;
    }

    public HistoryInfo? HistoryItem { get; set; }

    public override string ToString()
    {
        if (HistoryItem is not null)
            return $"{HistoryItem.CaptureDateTime:F} {HistoryItem.TextContent}";

        return $"{ShortValue} {LongValue}";
    }

    public string ToCSVString() => $"{ShortValue},{LongValue}";

    public bool Equals(LookupItem? other)
    {
        if (other is null)
            return false;

        if (other.ToString() == ToString())
            return true;

        return false;
    }
}
