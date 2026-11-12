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
    /// Raised after <see cref="Text"/> changes, whatever changed it: typing, an edit through this
    /// session, or a sync from the Markdown or spreadsheet editor.
    /// </summary>
    event EventHandler? TextChanged;

    /// <summary>Raised after <see cref="Mode"/> changes.</summary>
    event EventHandler? ModeChanged;
}
