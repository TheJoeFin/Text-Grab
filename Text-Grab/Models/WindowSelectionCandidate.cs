using System;
using System.Windows;

namespace Text_Grab.Models;

public record WindowSelectionCandidate(IntPtr Handle, Rect Bounds, string Title, int ProcessId, string AppName = "")
{
    public bool Contains(Point point) => Bounds.Contains(point);

    public string DisplayAppName => string.IsNullOrWhiteSpace(AppName) ? "Application" : AppName;

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Untitled window" : Title;
}
