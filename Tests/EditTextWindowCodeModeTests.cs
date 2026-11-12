using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Windows.Controls;
using System.Windows.Input;
using Text_Grab;
using Text_Grab.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Models;
using static Tests.EditTextWindowTestHost;

namespace Tests;

/// <summary>
/// Code mode (issue #645): the text in an AvalonEdit editor, kept in step with the raw text box in
/// both directions, text and selection.
/// </summary>
public class EditTextWindowCodeModeTests
{
    private const string CSharpSample = "using System;\r\n\r\nnamespace Demo;\r\n\r\npublic class Greeter\r\n{\r\n    public string Name { get; set; }\r\n}\r\n";

    [WpfFact]
    public void EnteringCodeMode_ShowsTheTextAndDetectsTheLanguage()
    {
        EditTextWindow window = CreateWindow(CSharpSample);

        SwitchMode(window, EtwEditorMode.Code);

        Assert.Equal(EtwEditorMode.Code, window.Session.Mode);
        Assert.Equal(CSharpSample, CodeEditor(window).Text);
        Assert.Equal(CodeLanguages.CSharp, window.CodeLanguageName);
        Assert.Equal("C#", CodeEditor(window).SyntaxHighlighting?.Name);
    }

    [WpfFact]
    public void TypingInTheCodeEditor_UpdatesTheTextAndCaret()
    {
        EditTextWindow window = CreateWindow("int x = 1;");
        SwitchMode(window, EtwEditorMode.Code);
        TextEditor editor = CodeEditor(window);

        editor.CaretOffset = "int x = ".Length;
        editor.TextArea.PerformTextInput("4");
        DoEvents();

        Assert.Equal("int x = 41;", window.Session.Text);
        Assert.Equal(RawText(window), editor.Text);
        Assert.Equal(new TextSpan("int x = 4".Length, 0), window.Session.Selection);
    }

    [WpfFact]
    public void ATransformAppliesToWhatIsSelectedInTheCodeEditor()
    {
        EditTextWindow window = CreateWindow("one\r\ntwo\r\nthree");
        SwitchMode(window, EtwEditorMode.Code);
        TextEditor editor = CodeEditor(window);

        editor.Select(0, "one\r\ntwo".Length);
        DoEvents();
        window.JoinLinesInEditTextWindow(" ", trimLineBeforeJoining: false);
        DoEvents();

        Assert.Equal("one two\r\nthree", window.Session.Text);
        Assert.Equal("one two\r\nthree", editor.Text);
    }

    [WpfFact]
    public void AWholeTextChange_ReachesTheCodeEditorAsOneUndoableEdit()
    {
        EditTextWindow window = CreateWindow("alpha\r\nbeta");
        SwitchMode(window, EtwEditorMode.Code);
        TextEditor editor = CodeEditor(window);

        window.Session.Text = "alpha\r\nBETA";
        DoEvents();

        Assert.Equal("alpha\r\nBETA", editor.Text);
        Assert.True(editor.CanUndo);

        editor.Undo();
        DoEvents();

        Assert.Equal("alpha\r\nbeta", editor.Text);
        Assert.Equal("alpha\r\nbeta", window.Session.Text);
    }

    [WpfFact]
    public void SettingTheSessionSelection_SelectsInTheCodeEditor()
    {
        EditTextWindow window = CreateWindow("first second third");
        SwitchMode(window, EtwEditorMode.Code);
        TextEditor editor = CodeEditor(window);

        window.SelectInEditor("first ".Length, "second".Length);
        DoEvents();

        Assert.Equal("second", editor.SelectedText);
        Assert.Equal("second", window.Session.SelectedText);
    }

    [WpfFact]
    public void SwitchingModes_KeepsEditsMadeInEachEditor()
    {
        EditTextWindow window = CreateWindow("abc");
        SwitchMode(window, EtwEditorMode.Code);
        CodeEditor(window).Document.Insert(3, "def");
        DoEvents();

        SwitchMode(window, EtwEditorMode.Text);
        Assert.Equal("abcdef", RawText(window));

        RawTextBox(window).AppendText("ghi");
        SwitchMode(window, EtwEditorMode.Code);
        Assert.Equal("abcdefghi", CodeEditor(window).Text);
    }

    [WpfFact]
    public void PickingALanguage_IsKeptOverDetection()
    {
        EditTextWindow window = CreateWindow(CSharpSample);
        window.SetCodeLanguage(CodeLanguages.Python);

        SwitchMode(window, EtwEditorMode.Code);

        Assert.Equal(CodeLanguages.Python, window.CodeLanguageName);
        Assert.Equal("Python", CodeEditor(window).SyntaxHighlighting?.Name);
    }

    [WpfFact]
    public void HistoryRestoresTheCodeLanguage()
    {
        HistoryInfo history = new()
        {
            TextContent = "x = 1",
            EditorMode = EtwEditorMode.Code,
            CodeLanguage = CodeLanguages.Python,
        };

        EditTextWindow window = new(history);

        Assert.Equal(CodeLanguages.Python, window.CodeLanguageName);
        Assert.Equal(CodeLanguages.Python, window.AsHistoryItem().CodeLanguage);
    }

    [WpfFact]
    public void TheCodeEditorLeavesCtrlDAndCtrlIToTheWindow()
    {
        CodeEditor editor = new();

        Assert.DoesNotContain(editor.TextArea.CommandBindings.Cast<CommandBinding>(),
            binding => binding.Command == AvalonEditCommands.DeleteLine || binding.Command == AvalonEditCommands.IndentSelection);
    }

    [WpfFact]
    public void FoldingMarkers_AppearOnlyForLanguagesWithAFoldingRule()
    {
        EditTextWindow window = CreateWindow(CSharpSample);
        SwitchMode(window, EtwEditorMode.Code);
        CodeEditor editor = CodeEditor(window);

        Assert.NotNull(editor.Foldings);
        Assert.Contains(editor.TextArea.LeftMargins, margin => margin is FoldingMargin);
        // The class body and the property's braces are on one line, so only the class folds.
        Assert.Single(editor.Foldings!.AllFoldings);

        window.SetCodeLanguage(CodeLanguages.Python);

        Assert.Null(editor.Foldings);
        Assert.DoesNotContain(editor.TextArea.LeftMargins, margin => margin is FoldingMargin);
    }

    [WpfFact]
    public void Folding_FollowsEditsAndKeepsCollapsedRegionsCollapsed()
    {
        EditTextWindow window = CreateWindow("class A\r\n{\r\n    int x;\r\n}\r\n");
        window.SetCodeLanguage(CodeLanguages.CSharp);
        SwitchMode(window, EtwEditorMode.Code);
        CodeEditor editor = CodeEditor(window);
        FoldingSection classBody = Assert.Single(editor.Foldings!.AllFoldings);
        classBody.IsFolded = true;

        window.Session.Text += "class B\r\n{\r\n    int y;\r\n}\r\n";
        DoEvents();
        editor.UpdateFoldings();

        Assert.Equal(2, editor.Foldings!.AllFoldings.Count());
        Assert.True(editor.Foldings.AllFoldings.First().IsFolded);
        Assert.False(editor.Foldings.AllFoldings.Last().IsFolded);
    }

    [WpfFact]
    public void ShowingARangeInsideACollapsedRegion_ExpandsIt()
    {
        EditTextWindow window = CreateWindow("class A\r\n{\r\n    int target;\r\n}");
        window.SetCodeLanguage(CodeLanguages.CSharp);
        SwitchMode(window, EtwEditorMode.Code);
        FoldingSection classBody = Assert.Single(CodeEditor(window).Foldings!.AllFoldings);
        classBody.IsFolded = true;

        window.SelectInEditor(window.Session.Text.IndexOf("target"), "target".Length);

        Assert.False(classBody.IsFolded);
    }

    [WpfFact]
    public void TheCodeEditor_ScrollsInPixelsSoTheSmoothWheelCanEaseIt()
    {
        string longText = string.Join("\r\n", Enumerable.Range(1, 400).Select(i => $"int line{i} = {i};"));
        EditTextWindow window = CreateWindow(longText);
        SwitchMode(window, EtwEditorMode.Code);
        CodeEditor editor = CodeEditor(window);
        ScrollViewer scrollViewer = Text_Grab.Utilities.WindowUtilities.GetScrollViewer(editor)!;

        Assert.True(Text_Grab.Utilities.SmoothScrollBehavior.GetIsEnabled(editor));

        // A fractional offset survives: the editor scrolls by pixels, not whole lines, so each frame
        // of the eased scroll moves it a little rather than snapping line to line.
        scrollViewer.ScrollToVerticalOffset(37.5);
        LayOut(window);
        Assert.Equal(37.5, scrollViewer.VerticalOffset, 3);

        // One wheel notch down engages the eased scroll instead of AvalonEdit's instant jump.
        Assert.True(Text_Grab.Utilities.SmoothScrollBehavior.ScrollBy(editor, -120));
    }

    [WpfFact]
    public void EveryCodeLanguage_HasAHighlightingDefinition()
    {
        foreach (CodeLanguage language in CodeLanguages.All)
            Assert.True(HighlightingManager.Instance.GetDefinition(language.Name) is not null, $"No highlighting definition named '{language.Name}'.");
    }

    [Theory]
    [InlineData(null, CodeLanguages.Python, ".py")]
    [InlineData(null, null, ".txt")]
    [InlineData(@"C:\Temp\script.ps1", CodeLanguages.Python, ".ps1")]
    public void SaveExtension_InCodeModeFollowsTheLanguage(string? openedFilePath, string? languageName, string expected)
    {
        Assert.Equal(expected, EditTextWindow.GetDefaultSaveExtension(openedFilePath, EtwEditorMode.Code, null, languageName));
    }
}
