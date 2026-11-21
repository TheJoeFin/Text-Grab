namespace Text_Grab.Editing.Panes;

/// <summary>
/// Makes <see cref="MarkdownPreviewPane"/>s. It holds nothing: every window asks every factory for its
/// descriptor, so the pane and its view model are made only in <see cref="Create"/>, which only a layout
/// that has the pane calls. Classic never does.
/// </summary>
internal sealed class MarkdownPreviewPaneFactory : IEditorPaneFactory
{
    public PaneDescriptor Descriptor => MarkdownPreviewPane.Definition;

    public IEditorPane Create() => new MarkdownPreviewPane();
}
