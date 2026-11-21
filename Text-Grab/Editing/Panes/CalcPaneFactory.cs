namespace Text_Grab.Editing.Panes;

/// <summary>Makes <see cref="CalcPane"/>s.</summary>
internal sealed class CalcPaneFactory : IEditorPaneFactory
{
    public PaneDescriptor Descriptor => CalcPane.Definition;

    public IEditorPane Create() => new CalcPane();
}
