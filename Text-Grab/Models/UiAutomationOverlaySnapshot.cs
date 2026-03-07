using System.Collections.Generic;
using System.Windows;

namespace Text_Grab.Models;

public record UiAutomationOverlaySnapshot(
    Rect CaptureBounds,
    WindowSelectionCandidate TargetWindow,
    IReadOnlyList<UiAutomationOverlayItem> Items)
{
    public bool HasItems => Items.Count > 0;
}
