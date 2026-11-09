using System;
using System.IO;
using Humanizer;
using Wpf.Ui.Controls;

namespace Text_Grab.Models;

public enum TrayFlyoutItemKind
{
    EditText = 0,
    GrabFrame = 1,
    File = 2,
}

/// <summary>
/// One row in the tray flyout list. A flat, UI-friendly projection of a <see cref="HistoryInfo"/>.
/// </summary>
public sealed class TrayFlyoutItem
{
    private const int MaxTitleLength = 70;

    public TrayFlyoutItem(TrayFlyoutItemKind kind, HistoryInfo history)
    {
        Kind = kind;
        History = history;
        Timestamp = history.CaptureDateTime;

        Title = kind == TrayFlyoutItemKind.File
            ? Path.GetFileName(history.SourcePath)
            : BuildSnippet(history.TextContent);

        Subtitle = kind == TrayFlyoutItemKind.File
            ? $"{DescribeTime(Timestamp)} · {Path.GetDirectoryName(history.SourcePath)}"
            : $"{DescribeTime(Timestamp)} · {KindLabel(kind)}";

        SearchText = kind == TrayFlyoutItemKind.File
            ? $"{Path.GetFileName(history.SourcePath)} {history.SourcePath}"
            : history.TextContent;

        Symbol = kind switch
        {
            TrayFlyoutItemKind.File => history.IsPdfDocument ? SymbolRegular.DocumentSearch24 : SymbolRegular.Document24,
            TrayFlyoutItemKind.GrabFrame => SymbolRegular.ImageEdit24,
            _ => history.EditorMode switch
            {
                EtwEditorMode.Spreadsheet => SymbolRegular.Table24,
                EtwEditorMode.Markdown => SymbolRegular.Markdown20,
                _ => SymbolRegular.TextT24,
            },
        };

        AutomationName = $"{KindLabel(kind)}: {Title}";
    }

    public TrayFlyoutItemKind Kind { get; }

    public HistoryInfo History { get; }

    public DateTimeOffset Timestamp { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string SearchText { get; }

    public SymbolRegular Symbol { get; }

    public string AutomationName { get; }

    public static string BuildSnippet(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "(empty)";

        string singleLine = text.Trim().Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        while (singleLine.Contains("  "))
            singleLine = singleLine.Replace("  ", " ");

        return singleLine.Length <= MaxTitleLength
            ? singleLine
            : string.Concat(singleLine.AsSpan(0, MaxTitleLength - 1), "…");
    }

    private static string KindLabel(TrayFlyoutItemKind kind) => kind switch
    {
        TrayFlyoutItemKind.GrabFrame => "Grab Frame",
        TrayFlyoutItemKind.File => "File",
        _ => "Edit Text",
    };

    private static string DescribeTime(DateTimeOffset timestamp)
        => timestamp == default ? string.Empty : timestamp.Humanize().Trim();
}
