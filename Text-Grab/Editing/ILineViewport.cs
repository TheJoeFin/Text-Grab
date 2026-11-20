using System;

namespace Text_Grab.Editing;

/// <summary>
/// An editor surface that shows the document line for line, so another view can scroll along with
/// it: the text box and the code editor. <see cref="EditorSession"/> exposes the one in use as
/// <see cref="Core.Editing.IEditorSession.FirstVisibleLine"/>.
/// </summary>
/// <remarks>
/// Lines, not pixels, because the editors' lines are not the same height as a follower's: the code
/// editor has its own font, and the text box's font is a setting.
/// </remarks>
internal interface ILineViewport
{
    /// <summary>
    /// The line at the top of the viewport, counted from 0, plus the fraction of it scrolled up out of view.
    /// </summary>
    double FirstVisibleLine { get; }

    /// <summary>Raised when the editor scrolls or its viewport changes size.</summary>
    event EventHandler? ViewportChanged;

    /// <summary>
    /// Scrolls as a mouse wheel turn of <paramref name="wheelDelta"/> would. Returns false when there
    /// was nothing to scroll that way.
    /// </summary>
    bool ScrollByWheel(int wheelDelta);
}
