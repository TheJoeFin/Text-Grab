using System;
using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Editing;

/// <summary>
/// The spreadsheet editor. Its cells hold the session's text split on delimiters, so a text range
/// has no direct place in the grid.
/// </summary>
/// <remarks>
/// The grid, its table document and their undo history belong to the window, so reading and
/// rebuilding the content are delegated to it.
/// </remarks>
internal sealed class SpreadsheetSurface(
    TextBoxSurface textSurface,
    Func<string?> getText,
    Action<string> loadFromText) : IEditorSurface
{
    public EtwEditorMode Mode => EtwEditorMode.Spreadsheet;

    /// <summary>
    /// Commits the grid into its table document and returns the document serialized as delimited text.
    /// </summary>
    public string? GetText() => getText();

    public void SetText(string text) => loadFromText(text);

    /// <summary>
    /// Selects the range in the hidden raw text editor only, as the window always has in spreadsheet
    /// mode; nothing in the grid is selected. Spreadsheet search goes through the window's cell methods instead.
    /// </summary>
    public void SelectAndShow(TextSpan range) => textSurface.SelectAndShow(range);
}
