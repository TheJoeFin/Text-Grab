using System;
using System.Collections.Generic;
using System.Windows;
using Text_Grab.Core.Layouts;
using Text_Grab.Editing.Panes;

namespace Text_Grab.Editing.Layout;

/// <summary>
/// What the Edit Text Window asks of its pane host (<see cref="LayoutRenderer"/>), with no AvalonDock type
/// anywhere in it.
/// </summary>
/// <remarks>
/// The window holds the host through this interface so that, with the pane layout off, nothing the
/// window runs makes the runtime load <see cref="LayoutRenderer"/>, whose fields would load AvalonDock.
/// Only the method that creates the host names the concrete class.
/// </remarks>
internal interface IPaneHost
{
    /// <summary>A pane was shown or hidden, or a resize ended.</summary>
    event EventHandler? PaneLayoutChanged;

    /// <summary>What the window puts in its editor-area row.</summary>
    FrameworkElement View { get; }

    /// <summary>The layout as it is now, sizes and shown state included.</summary>
    EtwLayoutDefinition Layout { get; }

    IReadOnlyList<IEditorPane> Panes { get; }

    void Render(EtwLayoutDefinition layout);

    bool IsPaneVisible(string paneId);

    void SetPaneVisible(string paneId, bool visible);

    GridLength? GetPaneSize(string paneId);

    void SetPaneSize(string paneId, GridLength size);

    double GetPaneActualSize(string paneId);

    void Close();
}
