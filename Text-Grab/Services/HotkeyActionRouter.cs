using System;
using System.Collections.Concurrent;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Services;

/// <summary>
/// Maps a global hotkey's <see cref="ShortcutKeyActions"/> to the handler it runs (v5 F5).
/// Replaces the switch that lived in <c>NotifyIconUtilities.HotKeyManager_HotKeyPressed</c>, so
/// something other than a hard-coded window launch - a workflow, later - can be registered
/// against a hotkey.
///
/// Handlers run on the UI thread, synchronously, exactly as that switch ran them: the hotkey
/// arrives on <c>HotKeyManager</c>'s background message-loop thread. One instance, from the
/// container.
/// </summary>
internal sealed class HotkeyActionRouter
{
    private readonly ConcurrentDictionary<ShortcutKeyActions, Action> _handlers = new();
    private readonly Action<Action> _runOnUiThread;

    public HotkeyActionRouter()
        : this(static handler => System.Windows.Application.Current.Dispatcher.Invoke(handler))
    {
    }

    /// <summary>For tests: substitute how a handler reaches the UI thread.</summary>
    internal HotkeyActionRouter(Action<Action> runOnUiThread)
        => _runOnUiThread = runOnUiThread ?? throw new ArgumentNullException(nameof(runOnUiThread));

    /// <summary>The router the app registers: every hotkey the old switch handled, on the same methods.</summary>
    public static HotkeyActionRouter CreateWithTrayActions()
    {
        HotkeyActionRouter router = new();
        RegisterTrayActions(router);
        return router;
    }

    /// <summary>
    /// The old switch, case for case. None and Settings stay unregistered: neither has ever done
    /// anything from a hotkey.
    /// </summary>
    internal static void RegisterTrayActions(HotkeyActionRouter router)
    {
        router.Register(ShortcutKeyActions.Fullscreen, TrayActions.OpenFullscreenGrab);
        router.Register(ShortcutKeyActions.GrabFrame, TrayActions.OpenGrabFrame);
        router.Register(ShortcutKeyActions.Lookup, TrayActions.OpenLookup);
        router.Register(ShortcutKeyActions.EditWindow, TrayActions.OpenEditTextWindow);
        router.Register(ShortcutKeyActions.PreviousRegionGrab, TrayActions.CopyPreviousRegion);
        router.Register(ShortcutKeyActions.PreviousEditWindow, TrayActions.OpenLastEditTextWindow);
        router.Register(ShortcutKeyActions.PreviousGrabFrame, TrayActions.OpenLastGrabFrame);
        router.Register(ShortcutKeyActions.OpenClipboardContent, TrayActions.OpenClipboardContent);
        router.Register(ShortcutKeyActions.Flyout, NotifyIconUtilities.ToggleFlyoutFromHotkey);
    }

    /// <summary>Points <paramref name="action"/> at <paramref name="handler"/>, replacing any earlier handler.</summary>
    public void Register(ShortcutKeyActions action, Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[action] = handler;
    }

    /// <summary>
    /// Runs the handler for <paramref name="action"/> on the UI thread and returns true, or returns
    /// false when nothing is registered for it.
    /// </summary>
    public bool TryInvoke(ShortcutKeyActions action)
    {
        if (!_handlers.TryGetValue(action, out Action? handler))
            return false;

        _runOnUiThread(handler);
        return true;
    }

    /// <summary>The registered handler, or null. For tests.</summary>
    internal Action? GetHandler(ShortcutKeyActions action)
        => _handlers.TryGetValue(action, out Action? handler) ? handler : null;
}
