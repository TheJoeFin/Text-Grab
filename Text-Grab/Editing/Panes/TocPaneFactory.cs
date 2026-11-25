namespace Text_Grab.Editing.Panes;

/// <summary>
/// Makes <see cref="TocPane"/>s. It holds nothing: every window asks every factory for its descriptor, so
/// the pane is made only in <see cref="Create"/>, which only a layout that has the pane calls.
/// </summary>
internal sealed class TocPaneFactory : IEditorPaneFactory
{
    public PaneDescriptor Descriptor => TocPane.Definition;

    public IEditorPane Create() => new TocPane();
}
