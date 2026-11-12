using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using Text_Grab;
using Text_Grab.Models;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// Characterization tests for the Edit Text Window's cross-editor behavior, written against the code
/// before E1b introduced <c>IEditorSession</c>. They pin what the window does today, including quirks,
/// so the session refactor can be checked against it. A failure here means behavior changed.
/// </summary>
/// <remarks>
/// The tests drive the window only through what survives E1b: named elements via <c>FindName</c>,
/// public methods, the public routed commands and the mode menu items (see <see cref="EditTextWindowTestHost"/>).
/// The one exception is spreadsheet undo, whose command binding is added by <c>Window_Loaded</c>;
/// <see cref="EditTextWindowTestHost.AddLoadedCommandBindings"/> adds just those bindings.
/// </remarks>
public class EditorSessionCharacterizationTests
{
    private const string ThreeLines = "one\r\ntwo\r\nthree";
    private const string Table = "Name\tCity\r\nann\tparis\r\nbob\tlima";

    // ---- A transform applied to a selection, in each mode ----

    [WpfFact]
    public void Text_TransformAppliesToTheSelectionOnly()
    {
        EditTextWindow window = CreateWindow(ThreeLines);

        window.SelectInEditor(0, "one\r\ntwo".Length);
        window.JoinLinesInEditTextWindow(" ", trimLineBeforeJoining: false);

        Assert.Equal("one two\r\nthree", RawText(window));
    }

    [WpfFact]
    public void Text_TransformWithNoSelectionAppliesToAllText()
    {
        EditTextWindow window = CreateWindow(ThreeLines);

        window.JoinLinesInEditTextWindow(" ", trimLineBeforeJoining: false);

        Assert.Equal("one two three", RawText(window));
    }

    [WpfFact]
    public void Text_SingleLineCommandAppliesToTheSelectionOnly()
    {
        EditTextWindow window = CreateWindow(ThreeLines);

        window.SelectInEditor(0, "one\r\ntwo".Length);
        EditTextWindow.SingleLineCmd.Execute(null, RawTextBox(window));

        Assert.Equal("one two\r\nthree", RawText(window));
    }

    [WpfFact]
    public void Markdown_TransformIgnoresTheRenderedSelectionAndAppliesToAllRawText()
    {
        // Pins today's behavior: the transform reads the hidden raw text box's selection, not the
        // selection in the rendered Markdown editor. Selecting first also re-serializes the raw text
        // from the rendered document, which turns each plain line break into a hard break ("  ").
        EditTextWindow window = CreateWindow(ThreeLines);
        SwitchMode(window, EtwEditorMode.Markdown);

        window.SelectInEditor(0, "one\r\ntwo".Length);
        Assert.False(string.IsNullOrEmpty(MarkdownEditor(window).Selection.Text));

        window.JoinLinesInEditTextWindow(" ", trimLineBeforeJoining: false);

        Assert.Equal("one   two   three", RawText(window));
        Assert.Equal("one   two   three", RenderedMarkdown(window).TrimEnd());
    }

    [WpfFact]
    public void Spreadsheet_TransformAppliesToTheSelectedCellOnly()
    {
        EditTextWindow window = CreateWindow(Table);
        SwitchMode(window, EtwEditorMode.Spreadsheet);

        window.NavigateToSpreadsheetCell(1, 1);
        DoEvents();
        window.JoinLinesInEditTextWindow(" ", false, "[", "]");

        Assert.Equal("Name\tCity\r\nann\t[paris]\r\nbob\tlima", RawText(window));
    }

    [WpfFact]
    public void Spreadsheet_TransformWithNoSelectedCellAppliesToEveryPopulatedCell()
    {
        EditTextWindow window = CreateWindow(Table);
        SwitchMode(window, EtwEditorMode.Spreadsheet);

        window.JoinLinesInEditTextWindow(" ", false, "[", "]");

        Assert.Equal("[Name]\t[City]\r\n[ann]\t[paris]\r\n[bob]\t[lima]", RawText(window));
    }

    // ---- Undo after a transform, in each mode ----

    [WpfFact]
    public void Text_UndoAfterSelectionTransformRestoresTheText()
    {
        EditTextWindow window = CreateWindow(ThreeLines);
        window.SelectInEditor(0, "one\r\ntwo".Length);
        window.JoinLinesInEditTextWindow(" ", trimLineBeforeJoining: false);

        ApplicationCommands.Undo.Execute(null, RawTextBox(window));

        Assert.Equal(ThreeLines, RawText(window));
    }

    [WpfFact]
    public void Text_UndoAfterWholeTextTransformRestoresTheText()
    {
        EditTextWindow window = CreateWindow(ThreeLines);
        window.JoinLinesInEditTextWindow(" ", trimLineBeforeJoining: false);

        ApplicationCommands.Undo.Execute(null, RawTextBox(window));

        Assert.Equal(ThreeLines, RawText(window));
    }

    [WpfFact]
    public void Markdown_UndoAfterTransformDoesNotRestoreTheText()
    {
        // Pins today's behavior: the transform rebuilds the rendered document, which starts a new
        // undo history in the Markdown editor, so undo there has nothing to revert.
        EditTextWindow window = CreateWindow(ThreeLines);
        SwitchMode(window, EtwEditorMode.Markdown);
        window.JoinLinesInEditTextWindow(" ", trimLineBeforeJoining: false);

        ApplicationCommands.Undo.Execute(null, MarkdownEditor(window));

        Assert.Equal("one two three", RawText(window));
    }

    [WpfFact]
    public void Spreadsheet_UndoAfterTransformRestoresTheCell()
    {
        EditTextWindow window = CreateWindow(Table);
        AddLoadedCommandBindings(window);
        SwitchMode(window, EtwEditorMode.Spreadsheet);
        window.NavigateToSpreadsheetCell(1, 1);
        DoEvents();
        window.JoinLinesInEditTextWindow(" ", false, "[", "]");
        Assert.Equal("Name\tCity\r\nann\t[paris]\r\nbob\tlima", RawText(window));

        ApplicationCommands.Undo.Execute(null, Grid(window));

        Assert.Equal(Table, RawText(window));
    }

    // ---- Switching modes preserves text ----

    [WpfFact]
    public void TextToMarkdownAndBack_TurnsPlainLineBreaksIntoHardBreaks()
    {
        // Pins today's behavior: leaving Markdown mode serializes the rendered document, and a
        // single line break inside a paragraph comes back as a Markdown hard break ("  " + newline).
        EditTextWindow window = CreateWindow(ThreeLines);

        SwitchMode(window, EtwEditorMode.Markdown);
        SwitchMode(window, EtwEditorMode.Text);

        Assert.Equal("one  \r\ntwo  \r\nthree", RawText(window));
    }

    [WpfFact]
    public void TextToMarkdownAndBack_KeepsHeadingAndParagraphText()
    {
        EditTextWindow window = CreateWindow("# Title\r\n\r\nSome **bold** text");

        SwitchMode(window, EtwEditorMode.Markdown);
        SwitchMode(window, EtwEditorMode.Text);

        Assert.Equal("# Title\r\n\r\nSome **bold** text", RawText(window));
    }

    [WpfFact]
    public void TextToSpreadsheetAndBack_KeepsTabSeparatedText()
    {
        EditTextWindow window = CreateWindow(Table);

        SwitchMode(window, EtwEditorMode.Spreadsheet);
        SwitchMode(window, EtwEditorMode.Text);

        Assert.Equal(Table, RawText(window));
    }

    [WpfFact]
    public void TextToMarkdownToSpreadsheetAndBack_CarriesHardBreaksIntoTheCells()
    {
        // Pins today's behavior: the Markdown hard breaks added on leaving Markdown mode end up as
        // trailing spaces in the last cell of each row.
        EditTextWindow window = CreateWindow(Table);

        SwitchMode(window, EtwEditorMode.Markdown);
        SwitchMode(window, EtwEditorMode.Spreadsheet);
        SwitchMode(window, EtwEditorMode.Text);

        Assert.Equal("Name\tCity  \r\nann\tparis  \r\nbob\tlima", RawText(window));
    }

    [WpfFact]
    public void SwitchingModes_ReportsTheModeAndRaisesEditorModeChanged()
    {
        EditTextWindow window = CreateWindow(Table);
        int changes = 0;
        window.EditorModeChanged += (_, _) => changes++;

        SwitchMode(window, EtwEditorMode.Spreadsheet);
        Assert.True(window.IsSpreadsheetMode);
        SwitchMode(window, EtwEditorMode.Markdown);
        Assert.False(window.IsSpreadsheetMode);
        SwitchMode(window, EtwEditorMode.Markdown);
        SwitchMode(window, EtwEditorMode.Text);

        Assert.Equal(3, changes);
    }

    // ---- Find and select, in each mode ----

    [WpfFact]
    public void Text_SelectInEditorSelectsTheRawRange()
    {
        EditTextWindow window = CreateWindow(ThreeLines);

        window.SelectInEditor(ThreeLines.IndexOf("two"), 3);

        Assert.Equal("two", RawTextBox(window).SelectedText);
        Assert.Equal(ThreeLines.IndexOf("two"), RawTextBox(window).SelectionStart);
    }

    [WpfFact]
    public void Markdown_SelectInEditorMapsTheRawRangeOntoTheRenderedText()
    {
        const string markdown = "Some **bold** text";
        EditTextWindow window = CreateWindow(markdown);
        SwitchMode(window, EtwEditorMode.Markdown);

        window.SelectInEditor(markdown.IndexOf("**bold**"), "**bold**".Length);

        Assert.Equal("bold", MarkdownEditor(window).Selection.Text);
        Assert.Equal(0, RawTextBox(window).SelectionLength);
    }

    [WpfFact]
    public void Spreadsheet_SearchThenNavigateSelectsTheMatchingCell()
    {
        EditTextWindow window = CreateWindow(Table);
        SwitchMode(window, EtwEditorMode.Spreadsheet);

        List<FindResult> results = window.SearchSpreadsheetCells(new Regex("lima"));
        FindResult match = Assert.Single(results);
        window.NavigateToSpreadsheetCell(match.RowIndex!.Value, match.ColumnIndex!.Value);
        DoEvents();

        Assert.Equal((2, 1), (match.RowIndex.Value, match.ColumnIndex.Value));
        DataGridCellInfo selected = Assert.Single(Grid(window).SelectedCells);
        Assert.Equal(2, Grid(window).Items.IndexOf(selected.Item));
        Assert.Equal(1, selected.Column.DisplayIndex);
    }

    [WpfFact]
    public void Spreadsheet_SelectInEditorOnlySelectsInTheHiddenRawTextBox()
    {
        // Pins today's behavior: SelectInEditor has no spreadsheet branch.
        EditTextWindow window = CreateWindow(Table);
        SwitchMode(window, EtwEditorMode.Spreadsheet);

        window.SelectInEditor(Table.IndexOf("lima"), 4);

        Assert.Equal("lima", RawTextBox(window).SelectedText);
        Assert.Empty(Grid(window).SelectedCells);
    }
}
