namespace Text_Grab.Editing.Panes;

/// <summary>
/// Makes panes of one kind. Each kind of pane registers one in the app's container
/// (<c>AppServices</c>), and hosts find a pane's factory by <see cref="PaneDescriptor.Id"/>.
/// </summary>
public interface IEditorPaneFactory
{
    PaneDescriptor Descriptor { get; }

    /// <summary>A new, unattached pane.</summary>
    IEditorPane Create();
}
