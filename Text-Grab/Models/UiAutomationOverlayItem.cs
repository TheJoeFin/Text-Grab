using System.Windows;

namespace Text_Grab.Models;

public enum UiAutomationOverlaySource
{
    PointTextRange = 0,
    VisibleTextRange = 1,
    ElementBounds = 2,
}

public record UiAutomationOverlayItem(
    string Text,
    Rect ScreenBounds,
    UiAutomationOverlaySource Source,
    string ControlTypeProgrammaticName = "",
    string AutomationId = "",
    string RuntimeId = "");
