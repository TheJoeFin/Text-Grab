using System;
using Text_Grab.Core.Actions;

namespace Text_Grab.Editing.Panes;

/// <summary>
/// The Markdown preview pane's actions as descriptors, so that their IDs are pinned and E2 can wire them
/// into menus. Until then the one action is the pane's own key (Enter in the preview) and a command
/// palette entry; nothing in the registry runs it.
/// </summary>
internal static class MarkdownPreviewPaneActions
{
    public const string GoToSource = "textgrab.markdownPreview.goToSource";

    /// <summary>Registers the pane's actions. Call once per registry; a second call throws on the first duplicate ID.</summary>
    public static void RegisterAll(IActionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        // The gesture is the pane's own key while the preview has focus, not a window shortcut.
        registry.Register(new ActionDescriptor(
            GoToSource, "Go to Preview Position in Source", "ArrowRight24", ActionCategory.File, ActionKind.AppCommand, DefaultGesture: "Enter"));
    }
}
