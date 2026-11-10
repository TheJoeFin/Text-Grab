using System.Reflection;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;

/// <summary>
/// Which method each global hotkey runs (v5 F5). Written against the switch in
/// NotifyIconUtilities before it was replaced, so the replacement has to reproduce exactly this.
/// None and Settings have always done nothing.
/// </summary>
public class HotkeyDispatchTests
{
    public static TheoryData<ShortcutKeyActions, string?> ExpectedHandlers => new()
    {
        { ShortcutKeyActions.None, null },
        { ShortcutKeyActions.Settings, null },
        { ShortcutKeyActions.Fullscreen, "TrayActions.OpenFullscreenGrab" },
        { ShortcutKeyActions.GrabFrame, "TrayActions.OpenGrabFrame" },
        { ShortcutKeyActions.Lookup, "TrayActions.OpenLookup" },
        { ShortcutKeyActions.EditWindow, "TrayActions.OpenEditTextWindow" },
        { ShortcutKeyActions.PreviousRegionGrab, "TrayActions.CopyPreviousRegion" },
        { ShortcutKeyActions.PreviousEditWindow, "TrayActions.OpenLastEditTextWindow" },
        { ShortcutKeyActions.PreviousGrabFrame, "TrayActions.OpenLastGrabFrame" },
        { ShortcutKeyActions.OpenClipboardContent, "TrayActions.OpenClipboardContent" },
        { ShortcutKeyActions.Flyout, "NotifyIconUtilities.ToggleFlyoutFromHotkey" },
    };

    [Fact]
    public void EveryShortcutAction_IsListed()
    {
        Assert.Equal(
            Enum.GetValues<ShortcutKeyActions>().Order(),
            ExpectedHandlers.Select(row => row.Data.Item1).Order());
    }

    [Theory]
    [MemberData(nameof(ExpectedHandlers))]
    public void Hotkey_RunsTheSameMethodAsBefore(ShortcutKeyActions action, string? expected)
    {
        Action? handler = NotifyIconUtilities.GetHotkeyHandler(action);

        Assert.Equal(expected, Describe(handler));
    }

    internal static string? Describe(Action? handler)
        => handler?.Method is MethodInfo method ? $"{method.DeclaringType?.Name}.{method.Name}" : null;
}
