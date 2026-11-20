using System;
using Text_Grab.Core.Actions;

namespace Text_Grab.Editing.Panes;

/// <summary>
/// The Mermaid pane's actions as descriptors, so that their IDs are pinned and E2 can wire them into
/// menus. Until then each is an ordinary command on the pane's own buttons, its context menu and the
/// command palette; nothing in the registry runs them.
/// </summary>
internal static class MermaidPaneActions
{
    public const string ExportSvg = "textgrab.mermaid.exportSvg";
    public const string ExportPng = "textgrab.mermaid.exportPng";
    public const string CopyImage = "textgrab.mermaid.copyImage";
    public const string OpenFullScreen = "textgrab.mermaid.openFullScreen";

    /// <summary>Registers the pane's actions. Call once per registry; a second call throws on the first duplicate ID.</summary>
    public static void RegisterAll(IActionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(Command(ExportSvg, "Export Diagram as SVG", "ArrowDownload24"));
        registry.Register(Command(ExportPng, "Export Diagram as PNG", "Image24"));
        registry.Register(Command(CopyImage, "Copy Diagram Image", "ImageCopy24"));
        registry.Register(Command(OpenFullScreen, "Open Diagram Full Screen", "FullScreenMaximize24"));
    }

    private static ActionDescriptor Command(string id, string displayName, string iconKey)
        => new(id, displayName, iconKey, ActionCategory.File, ActionKind.AppCommand);
}
