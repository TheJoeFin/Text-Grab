using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Editing;

/// <summary>
/// The spreadsheet editor. Its cells hold the session's text split on delimiters, so a text range
/// has no direct place in the grid.
/// </summary>
internal sealed class SpreadsheetSurface(TextBoxSurface textSurface) : IEditorSurface
{
    public EtwEditorMode Mode => EtwEditorMode.Spreadsheet;

    /// <summary>
    /// Selects the range in the hidden raw text editor only, as the window always has in spreadsheet
    /// mode; nothing in the grid is selected. Spreadsheet search goes through the window's cell methods instead.
    /// </summary>
    public void SelectAndShow(TextSpan range) => textSurface.SelectAndShow(range);
}
