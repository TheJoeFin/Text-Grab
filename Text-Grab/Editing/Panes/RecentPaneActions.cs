using System;
using Text_Grab.Core.Actions;

namespace Text_Grab.Editing.Panes;

/// <summary>
/// The recent items pane's actions as descriptors, so that their IDs are pinned and E2 can wire them
/// into menus. Until then each is an ordinary command on the pane's own list, context menu and the
/// command palette; nothing in the registry runs them.
/// </summary>
internal static class RecentPaneActions
{
    public const string Open = "textgrab.recent.open";
    public const string OpenInNewWindow = "textgrab.recent.openInNewWindow";
    public const string Search = "textgrab.recent.search";
    public const string Refresh = "textgrab.recent.refresh";

    /// <summary>Registers the pane's actions. Call once per registry; a second call throws on the first duplicate ID.</summary>
    public static void RegisterAll(IActionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(Command(Open, "Open Recent Item", "Open24", "Enter"));
        registry.Register(Command(OpenInNewWindow, "Open Recent Item in New Window", "WindowEdit20", "Ctrl+Enter"));
        registry.Register(Command(Search, "Search Recent Items", "Search24"));
        registry.Register(Command(Refresh, "Refresh Recent Items", "ArrowClockwise24"));
    }

    // The gestures are the pane's own keys while its list or search box has focus, not window shortcuts.
    private static ActionDescriptor Command(string id, string displayName, string iconKey, string? gesture = null)
        => new(id, displayName, iconKey, ActionCategory.File, ActionKind.AppCommand, DefaultGesture: gesture);
}
