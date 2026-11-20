using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Rendering;
using Text_Grab;
using Text_Grab.Core.Editing;
using Text_Grab.Editing;
using Text_Grab.Models;
using Text_Grab.Utilities;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// What <see cref="IEditorSession"/> tells a pane beside the editor: where the editor is scrolled to,
/// in lines, the selected spreadsheet numbers and the culture.
/// </summary>
public class EditorSessionViewportTests
{
    private static readonly string TwoHundredLines = string.Join("\r\n", Enumerable.Range(1, 200));

    // ---- Line height ----

    [WpfFact]
    public void LineHeight_IsTheSpacingOfTheTextBoxsLaidOutLines()
    {
        EditTextWindow window = CreateWindow("one\r\ntwo\r\nthree");
        TextBox textBox = RawTextBox(window);
        textBox.TextWrapping = TextWrapping.NoWrap;
        LayOut(window);

        double first = textBox.GetRectFromCharacterIndex(0).Top;
        double second = textBox.GetRectFromCharacterIndex(textBox.GetCharacterIndexFromLineIndex(1)).Top;

        Assert.Equal(second - first, TextBoxSurface.LineHeight(textBox), precision: 2);
    }

    // ---- FirstVisibleLine ----

    [WpfFact]
    public void Text_FirstVisibleLineIsTheTextBoxsScrollOffsetInLines()
    {
        EditTextWindow window = CreateScrollableWindow();
        TextBox textBox = RawTextBox(window);
        ScrollViewer viewer = WindowUtilities.GetScrollViewer(textBox)!;

        viewer.ScrollToVerticalOffset(TextBoxSurface.LineHeight(textBox) * 10.5);
        LayOut(window);

        Assert.Equal(10.5, window.Session.FirstVisibleLine, precision: 3);
    }

    [WpfFact]
    public void Markdown_FirstVisibleLineFollowsTheTextBox()
    {
        EditTextWindow window = CreateScrollableWindow();
        TextBox textBox = RawTextBox(window);
        WindowUtilities.GetScrollViewer(textBox)!.ScrollToVerticalOffset(TextBoxSurface.LineHeight(textBox) * 7);
        LayOut(window);

        SwitchMode(window, EtwEditorMode.Markdown);

        Assert.Equal(7, window.Session.FirstVisibleLine, precision: 3);
    }

    [WpfFact]
    public void Code_FirstVisibleLineFollowsTheCodeEditorByDocumentLine()
    {
        EditTextWindow window = CreateWindow(TwoHundredLines);
        SwitchMode(window, EtwEditorMode.Code);
        TextView view = CodeEditor(window).TextArea.TextView;
        Assert.Equal(0, window.Session.FirstVisibleLine);

        // A quarter of the way into line 31 (index 30).
        CodeEditor(window).ScrollToVerticalOffset(view.GetVisualTopByDocumentLine(31) + view.DefaultLineHeight / 4);
        LayOut(window);

        Assert.Equal(30.25, window.Session.FirstVisibleLine, precision: 2);
    }

    [WpfFact]
    public void ViewportChanged_IsRaisedWhenTheFollowedEditorScrolls()
    {
        EditTextWindow window = CreateWindow(TwoHundredLines);
        SwitchMode(window, EtwEditorMode.Code);
        int changes = 0;
        window.Session.ViewportChanged += (_, _) => changes++;

        CodeEditor(window).ScrollToVerticalOffset(300);
        LayOut(window);

        Assert.True(changes > 0);
    }

    // ---- ScrollByWheel ----

    [WpfFact]
    public void ScrollByWheel_ScrollsTheTextBoxAndSaysWhenItCannot()
    {
        EditTextWindow window = CreateScrollableWindow();

        Assert.False(window.Session.ScrollByWheel(120)); // already at the top
        Assert.True(window.Session.ScrollByWheel(-120));
    }

    [WpfFact]
    public void ScrollByWheel_InCodeModeScrollsTheCodeEditor()
    {
        EditTextWindow window = CreateWindow(TwoHundredLines);
        SwitchMode(window, EtwEditorMode.Code);

        Assert.False(window.Session.ScrollByWheel(120));
        Assert.True(window.Session.ScrollByWheel(-120));
    }

    // ---- Selected spreadsheet numbers ----

    [WpfFact]
    public void SelectedCellNumbers_AreNullOutsideSpreadsheetMode()
    {
        EditTextWindow window = CreateWindow("a\t1\r\nb\t2");

        Assert.Null(window.Session.SelectedCellNumbers);
    }

    [WpfFact]
    public void SelectedCellNumbers_AreTheSelectedCellsNumbersAndEmptyWhenThereAreNone()
    {
        EditTextWindow window = CreateWindow("apple\t1.5\r\npear\t2\r\nfig\tn/a");
        SwitchMode(window, EtwEditorMode.Spreadsheet);
        DataGrid grid = Grid(window);
        int changes = 0;
        window.Session.SelectedCellsChanged += (_, _) => changes++;

        grid.SelectedCells.Clear();
        DoEvents();
        Assert.Null(window.Session.SelectedCellNumbers);

        grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[0], grid.Columns[1]));
        grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[1], grid.Columns[1]));
        DoEvents();
        Assert.Equal([1.5, 2], window.Session.SelectedCellNumbers);

        grid.SelectedCells.Clear();
        grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[2], grid.Columns[1]));
        DoEvents();
        Assert.Empty(window.Session.SelectedCellNumbers!);

        Assert.True(changes > 0);
    }

    // ---- Culture ----

    [WpfFact]
    public void Culture_IsTheCurrentCultureUntilALanguageIsPicked()
    {
        EditTextWindow window = CreateWindow("one");

        Assert.Equal(CultureInfo.CurrentCulture, window.Session.Culture);
    }

    private static EditTextWindow CreateScrollableWindow()
    {
        EditTextWindow window = CreateWindow(TwoHundredLines);
        RawTextBox(window).TextWrapping = TextWrapping.NoWrap;
        LayOut(window);
        return window;
    }
}
