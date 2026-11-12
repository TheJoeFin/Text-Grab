using System.Windows.Input;
using Text_Grab;
using Text_Grab.Core.Editing;
using Text_Grab.Models;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// <see cref="IEditorSession"/> as the Edit Text Window exposes it (<see cref="EditTextWindow.Session"/>).
/// </summary>
public class EditorSessionTests
{
    private const string ThreeLines = "one\r\ntwo\r\nthree";

    [WpfFact]
    public void TextAndSelection_AreTheRawTextEditors()
    {
        EditTextWindow window = CreateWindow(ThreeLines);
        IEditorSession session = window.Session;

        session.Selection = new TextSpan(5, 3);

        Assert.Equal(ThreeLines, session.Text);
        Assert.Equal((5, 3), (RawTextBox(window).SelectionStart, RawTextBox(window).SelectionLength));
        Assert.Equal("two", session.SelectedText);
        Assert.Equal(new TextSpan(5, 3), session.Selection);
    }

    [WpfFact]
    public void SettingText_ReplacesEverythingAsOneUndoableEdit()
    {
        EditTextWindow window = CreateWindow(ThreeLines);

        window.Session.Text = "replaced";
        Assert.Equal("replaced", RawText(window));

        ApplicationCommands.Undo.Execute(null, RawTextBox(window));
        Assert.Equal(ThreeLines, RawText(window));
    }

    [WpfFact]
    public void ReplaceRange_LeavesTheReplacementSelectedAndCanBeUndone()
    {
        EditTextWindow window = CreateWindow(ThreeLines);
        IEditorSession session = window.Session;

        session.ReplaceRange(new TextSpan(5, 3), "2");

        Assert.Equal("one\r\n2\r\nthree", session.Text);
        Assert.Equal(new TextSpan(5, 1), session.Selection);
        ApplicationCommands.Undo.Execute(null, RawTextBox(window));
        Assert.Equal(ThreeLines, session.Text);
    }

    [WpfFact]
    public void AppendText_AddsToTheEndWithoutMovingTheSelection()
    {
        EditTextWindow window = CreateWindow(ThreeLines);
        IEditorSession session = window.Session;
        session.Selection = new TextSpan(0, 3);

        session.AppendText("\r\nfour");

        Assert.Equal(ThreeLines + "\r\nfour", session.Text);
        Assert.Equal(new TextSpan(0, 3), session.Selection);
    }

    [WpfFact]
    public void SelectedTextOrAllText_UsesTheSelectionWhenThereIsOne()
    {
        EditTextWindow window = CreateWindow(ThreeLines);
        IEditorSession session = window.Session;
        session.Selection = new TextSpan(0, 0);
        Assert.Equal(ThreeLines, session.GetSelectedTextOrAllText());

        session.Selection = new TextSpan(5, 3);
        Assert.Equal("two", session.GetSelectedTextOrAllText());

        session.ReplaceSelectedTextOrAllText("TWO");
        Assert.Equal("one\r\nTWO\r\nthree", session.Text);

        session.Selection = new TextSpan(0, 0);
        session.ReplaceSelectedTextOrAllText("all");
        Assert.Equal("all", session.Text);
    }

    [WpfFact]
    public void TextChanged_IsRaisedForEditsMadeStraightInTheTextBox()
    {
        EditTextWindow window = CreateWindow(ThreeLines);
        int changes = 0;
        window.Session.TextChanged += (_, _) => changes++;

        RawTextBox(window).Text = "typed";
        window.Session.AppendText("!");

        Assert.Equal(2, changes);
    }

    [WpfFact]
    public void ModeAndModeChanged_FollowTheWindow()
    {
        EditTextWindow window = CreateWindow(ThreeLines);
        IEditorSession session = window.Session;
        List<EtwEditorMode> seen = [];
        session.ModeChanged += (_, _) => seen.Add(session.Mode);

        SwitchMode(window, EtwEditorMode.Spreadsheet);
        SwitchMode(window, EtwEditorMode.Markdown);
        SwitchMode(window, EtwEditorMode.Text);

        Assert.Equal([EtwEditorMode.Spreadsheet, EtwEditorMode.Markdown, EtwEditorMode.Text], seen);
    }

    [WpfFact]
    public void SelectInEditor_InMarkdownModeSelectsTheRenderedText()
    {
        const string markdown = "Some **bold** text";
        EditTextWindow window = CreateWindow(markdown);
        SwitchMode(window, EtwEditorMode.Markdown);

        window.Session.SelectInEditor(new TextSpan(markdown.IndexOf("**bold**"), "**bold**".Length));

        Assert.Equal("bold", MarkdownEditor(window).Selection.Text);
        Assert.Equal(0, RawTextBox(window).SelectionLength);
    }

    [WpfFact]
    public void SelectInEditor_InTextAndSpreadsheetModeSelectsTheRawText()
    {
        EditTextWindow window = CreateWindow(ThreeLines);

        window.Session.SelectInEditor(new TextSpan(5, 3));
        Assert.Equal("two", RawTextBox(window).SelectedText);

        SwitchMode(window, EtwEditorMode.Spreadsheet);
        window.Session.SelectInEditor(new TextSpan(0, 3));

        Assert.Equal("one", RawTextBox(window).SelectedText);
        Assert.Empty(Grid(window).SelectedCells);
    }
}
