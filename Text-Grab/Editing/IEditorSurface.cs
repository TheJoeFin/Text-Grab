using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Editing;

/// <summary>
/// One of the Edit Text Window's editors, as <see cref="EditorSession"/> sees it. There is one per
/// <see cref="EtwEditorMode"/>, and only the surface knows which control it wraps.
/// </summary>
internal interface IEditorSurface
{
    /// <summary>The editor mode this surface shows.</summary>
    EtwEditorMode Mode { get; }

    /// <summary>
    /// What this editor shows, as session text, or <c>null</c> when it has nothing to give (no
    /// document has been built yet). The session copies it into its text when it syncs from this editor.
    /// </summary>
    string? GetText();

    /// <summary>Rebuilds what this editor shows from session text.</summary>
    void SetText(string text);

    /// <summary>
    /// Selects <paramref name="range"/> of the session's text in this editor, focuses the editor and
    /// brings the range into view.
    /// </summary>
    void SelectAndShow(TextSpan range);
}
