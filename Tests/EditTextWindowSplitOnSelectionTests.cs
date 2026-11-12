using Text_Grab;
using Text_Grab.Models;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// Characterization tests for the Edit Text Window's Split Lines Before / After Each Selection
/// commands, written against the code before A1 lifted them into <c>StringMethods</c>.
/// </summary>
public class EditTextWindowSplitOnSelectionTests
{
    private static void SelectFirst(EditTextWindow window, string text, string selection)
        => window.SelectInEditor(text.IndexOf(selection), selection.Length);

    [WpfFact]
    public void SplitBefore_BreaksTheLineBeforeEveryOccurrence()
    {
        const string text = "one, two, three";
        EditTextWindow window = CreateWindow(text);
        SelectFirst(window, text, ", ");

        EditTextWindow.SplitOnSelectionCmd.Execute(null, RawTextBox(window));

        Assert.Equal("one\r\n, two\r\n, three", RawText(window));
    }

    [WpfFact]
    public void SplitAfter_BreaksTheLineAfterEveryOccurrence()
    {
        const string text = "one, two, three";
        EditTextWindow window = CreateWindow(text);
        SelectFirst(window, text, ", ");

        EditTextWindow.SplitAfterSelectionCmd.Execute(null, RawTextBox(window));

        Assert.Equal("one, \r\ntwo, \r\nthree", RawText(window));
    }

    [WpfFact]
    public void Split_MatchesCaseSensitively()
    {
        const string text = "A a A";
        EditTextWindow window = CreateWindow(text);
        SelectFirst(window, text, "A");

        EditTextWindow.SplitOnSelectionCmd.Execute(null, RawTextBox(window));

        Assert.Equal("\r\nA a \r\nA", RawText(window));
    }

    [WpfFact]
    public void Split_ReplacesNonOverlappingOccurrencesFromTheLeft()
    {
        const string text = "aaaaa";
        EditTextWindow window = CreateWindow(text);
        window.SelectInEditor(0, 2);

        EditTextWindow.SplitAfterSelectionCmd.Execute(null, RawTextBox(window));

        Assert.Equal("aa\r\naa\r\na", RawText(window));
    }

    [WpfFact]
    public void Split_OccurrenceAtTheStartGetsALeadingLineBreak()
    {
        const string text = "x-y-z";
        EditTextWindow window = CreateWindow(text);
        window.SelectInEditor(0, 1);

        EditTextWindow.SplitOnSelectionCmd.Execute(null, RawTextBox(window));

        Assert.Equal("\r\nx-y-z", RawText(window));
    }

    [WpfFact]
    public void Split_WithNoSelection_DoesNothing()
    {
        // CanExecute is false, so neither the split nor its "No text selected" message runs.
        EditTextWindow window = CreateWindow("one, two");

        EditTextWindow.SplitOnSelectionCmd.Execute(null, RawTextBox(window));
        EditTextWindow.SplitAfterSelectionCmd.Execute(null, RawTextBox(window));

        Assert.Equal("one, two", RawText(window));
    }

    [WpfFact]
    public void Split_InSpreadsheetMode_DoesNothing()
    {
        const string text = "a,b\tc,d";
        EditTextWindow window = CreateWindow(text);
        SwitchMode(window, EtwEditorMode.Spreadsheet);
        SelectFirst(window, text, ",");

        EditTextWindow.SplitOnSelectionCmd.Execute(null, Grid(window));
        EditTextWindow.SplitAfterSelectionCmd.Execute(null, Grid(window));

        Assert.Equal(text, RawText(window));
    }
}
