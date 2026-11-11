using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Text_Grab.Models;
using Text_Grab.Services;

namespace Tests;

/// <summary>
/// Which method each global hotkey runs (v5 F5). Written against the switch in
/// NotifyIconUtilities before it was replaced; the table is unchanged and now checks
/// <see cref="HotkeyActionRouter"/>'s registrations. None and Settings have always done nothing.
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
        HotkeyActionRouter router = new(static _ => { });
        HotkeyActionRouter.RegisterTrayActions(router);

        Action? handler = router.GetHandler(action);

        Assert.Equal(expected, Describe(handler));
    }

    [Theory]
    [MemberData(nameof(ExpectedHandlers))]
    public void TheContainersRouter_HasTheSameRegistrations(ShortcutKeyActions action, string? expected)
    {
        HotkeyActionRouter router = AppServices.Provider.GetRequiredService<HotkeyActionRouter>();

        Assert.Equal(expected, Describe(router.GetHandler(action)));
    }

    [Fact]
    public void TryInvoke_RunsTheHandlerThroughTheUiThreadRunner()
    {
        List<Action> marshalled = [];
        bool ran = false;
        HotkeyActionRouter router = new(handler =>
        {
            marshalled.Add(handler);
            handler();
        });
        router.Register(ShortcutKeyActions.Lookup, () => ran = true);

        Assert.True(router.TryInvoke(ShortcutKeyActions.Lookup));
        Assert.True(ran);
        Assert.Single(marshalled);
    }

    [Fact]
    public void TryInvoke_UnregisteredAction_ReturnsFalseAndRunsNothing()
    {
        int marshalled = 0;
        HotkeyActionRouter router = new(_ => marshalled++);

        Assert.False(router.TryInvoke(ShortcutKeyActions.Settings));
        Assert.Equal(0, marshalled);
    }

    [Fact]
    public void Register_ReplacesAnEarlierHandler()
    {
        string? ran = null;
        HotkeyActionRouter router = new(handler => handler());
        router.Register(ShortcutKeyActions.GrabFrame, () => ran = "first");
        router.Register(ShortcutKeyActions.GrabFrame, () => ran = "second");

        router.TryInvoke(ShortcutKeyActions.GrabFrame);

        Assert.Equal("second", ran);
    }

    [Fact]
    public void Register_RejectsNull()
    {
        HotkeyActionRouter router = new(_ => { });

        Assert.Throws<ArgumentNullException>(() => router.Register(ShortcutKeyActions.Lookup, null!));
    }

    internal static string? Describe(Action? handler)
        => handler?.Method is MethodInfo method ? $"{method.DeclaringType?.Name}.{method.Name}" : null;
}
