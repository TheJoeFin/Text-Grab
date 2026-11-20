using System.Globalization;
using Text_Grab.Models;

namespace Text_Grab.Core.Editing;

/// <summary>
/// The document in an Edit Text Window and the editor behind it, without naming the control.
/// </summary>
/// <remarks>
/// <see cref="Text"/> is the document in every <see cref="EtwEditorMode"/>. The Markdown and
/// spreadsheet editors are views of it that sync to and from it, so while one of them is showing,
/// <see cref="Text"/> and <see cref="Selection"/> can lag behind what is on screen until it syncs.
/// </remarks>
public interface IEditorSession
{
    /// <summary>
    /// The whole document as plain text. Setting it replaces all of it as one edit that undo can revert.
    /// </summary>
    string Text { get; set; }

    /// <summary>Which editor is showing the document.</summary>
    EtwEditorMode Mode { get; }

    /// <summary>
    /// The selection in <see cref="Text"/>. With nothing selected, its length is 0 and its start is the caret.
    /// </summary>
    /// <remarks>
    /// Setting it moves the selection without focusing or scrolling anything. To show a range to
    /// the user in whichever editor is visible, use <see cref="SelectInEditor"/>.
    /// </remarks>
    TextSpan Selection { get; set; }

    /// <summary>The text <see cref="Selection"/> covers; empty when nothing is selected.</summary>
    string SelectedText { get; }

    /// <summary>
    /// Replaces the selected text with <paramref name="replacement"/> as an edit undo can revert,
    /// and leaves <paramref name="replacement"/> selected.
    /// </summary>
    void ReplaceSelection(string replacement);

    /// <summary>
    /// Selects <paramref name="range"/>, then replaces it as <see cref="ReplaceSelection"/> does.
    /// </summary>
    void ReplaceRange(TextSpan range, string replacement);

    /// <summary>
    /// Adds <paramref name="text"/> to the end of the document as an edit undo can revert. The selection does not move.
    /// </summary>
    void AppendText(string text);

    /// <summary>The selected text, or the whole document when nothing is selected.</summary>
    string GetSelectedTextOrAllText();

    /// <summary>
    /// Replaces the selected text with <paramref name="replacement"/>, or the whole document when
    /// nothing is selected.
    /// </summary>
    void ReplaceSelectedTextOrAllText(string replacement);

    /// <summary>
    /// Shows <paramref name="range"/> to the user: selects it in whichever editor is visible,
    /// focuses that editor and brings the range into view.
    /// </summary>
    void SelectInEditor(TextSpan range);

    /// <summary>Scrolls the text editor to the end of the document.</summary>
    void ScrollToEnd();

    /// <summary>
    /// The culture the document is read in: the language picked in the window, or the current culture
    /// when none was picked. For anything that parses or changes text by culture, such as numbers and case.
    /// </summary>
    CultureInfo Culture { get; }

    /// <summary>
    /// The numbers in the selected spreadsheet cells, in selection order. Null when the spreadsheet
    /// editor is not showing or has no cells selected; empty when cells are selected but none of them
    /// holds a number.
    /// </summary>
    IReadOnlyList<double>? SelectedCellNumbers { get; }

    /// <summary>Raised when the spreadsheet editor's selected cells change.</summary>
    event EventHandler? SelectedCellsChanged;

    /// <summary>
    /// The line at the top of the editor's viewport, counted from 0, plus the fraction of it scrolled up
    /// out of view: 2.25 means the top quarter of the third line is hidden.
    /// </summary>
    /// <remarks>
    /// For views that scroll along with the text line for line. It follows the code editor in code mode
    /// and the text box in every other mode, since the text box holds the document in all of them. The
    /// text box counts the lines on screen, so while it wraps text, a wrapped line counts once per row.
    /// </remarks>
    double FirstVisibleLine { get; }

    /// <summary>
    /// Raised when the editor <see cref="FirstVisibleLine"/> follows scrolls or its viewport changes size.
    /// Not raised when the mode changes which editor that is; listen to <see cref="ModeChanged"/> for that.
    /// </summary>
    event EventHandler? ViewportChanged;

    /// <summary>
    /// Scrolls the editor <see cref="FirstVisibleLine"/> follows as a mouse wheel turn of
    /// <paramref name="wheelDelta"/> would (120 per notch, positive away from the user). Returns false
    /// when it could not scroll, so the caller can let the wheel event go on.
    /// </summary>
    bool ScrollByWheel(int wheelDelta);

    /// <summary>
    /// Raised after <see cref="Text"/> changes, whatever changed it: typing, an edit through this
    /// session, or a sync from the Markdown or spreadsheet editor.
    /// </summary>
    event EventHandler? TextChanged;

    /// <summary>Raised after <see cref="Mode"/> changes.</summary>
    event EventHandler? ModeChanged;
}
