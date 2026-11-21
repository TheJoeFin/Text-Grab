namespace Text_Grab.Editing.Panes;

/// <summary>
/// Makes <see cref="RecentPane"/>s. It holds nothing: every window asks every factory for its
/// descriptor, so the history source and the pane's view model are made only in <see cref="Create"/>,
/// which only a layout that has the pane calls.
/// </summary>
internal sealed class RecentPaneFactory : IEditorPaneFactory
{
    public PaneDescriptor Descriptor => RecentPane.Definition;

    public IEditorPane Create() => new RecentPane();
}
