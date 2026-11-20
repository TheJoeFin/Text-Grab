namespace Text_Grab.Editing.Panes;

/// <summary>
/// Makes <see cref="MermaidPane"/>s. It holds nothing: every window asks every factory for its
/// descriptor, so the pane, its view model and the renderer behind them are made only in
/// <see cref="Create"/>, which only a layout that has the pane calls. Classic never does.
/// </summary>
internal sealed class MermaidPaneFactory : IEditorPaneFactory
{
    public PaneDescriptor Descriptor => MermaidPane.Definition;

    public IEditorPane Create() => new MermaidPane();
}
