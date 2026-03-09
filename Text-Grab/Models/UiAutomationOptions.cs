using System.Windows;

namespace Text_Grab.Models;

public record UiAutomationOptions(
    UiAutomationTraversalMode TraversalMode,
    bool IncludeOffscreen,
    bool PreferFocusedElement,
    Rect? FilterBounds = null);
