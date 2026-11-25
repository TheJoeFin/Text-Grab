using Text_Grab.Core.Editing;
using Text_Grab.Core.Layouts;
using Text_Grab.Models;

namespace Text_Grab;

/// <summary>
/// The contents button on the Markdown bar, which shows and hides the table of contents pane
/// (<see cref="Editing.Panes.TocPane"/>). The pane itself knows nothing of the button.
/// </summary>
public partial class EditTextWindow
{
    /// <summary>
    /// Shows the contents button when the window can show the pane and there is a reason to: the document
    /// has a heading, or the pane is already open (so that deleting the last heading never leaves an open
    /// pane with no button to close it). Checks it when the pane is shown. Called when the text, the mode
    /// or the layout changes, never for a selection change, which would read the whole document each time.
    /// </summary>
    internal void UpdateTocToggle()
    {
        // The editor session is made while the window's XAML is still being read.
        if (MarkdownToolbar is null)
            return;

        // Without the pane layout, SetPaneShown has nothing to show the pane in.
        if (paneLayout is null || editorMode != EtwEditorMode.Markdown)
        {
            if (MarkdownToolbar.IsTocToggleVisible)
                MarkdownToolbar.SetTocToggle(false, false);

            return;
        }

        bool paneShown = paneLayout.IsPaneVisible(PaneIds.Toc);
        MarkdownToolbar.SetTocToggle(paneShown || MarkdownHeadings.HasAny(Session.Text), paneShown);
    }

    private void MarkdownToolbar_TocToggleRequested(bool show)
    {
        SetPaneShown(PaneIds.Toc, show);
        UpdateTocToggle();
    }
}
