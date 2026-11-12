using System.Windows.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Editing;
using Text_Grab.Models;

namespace Tests;

/// <summary>
/// The sync rules <see cref="EditorSession"/> took over from the Edit Text Window: which editor is
/// synced in which mode, and the per-editor guard that stops text written from an editor being pushed
/// straight back into it.
/// </summary>
public class EditorSessionSyncTests
{
    [WpfFact]
    public void SyncTextFromActiveEditor_CopiesTheVisibleEditorsTextAndDoesNothingInTextMode()
    {
        Harness harness = new("raw");
        harness.Markdown.Content = "from markdown";

        harness.Session.SyncTextFromActiveEditor();
        Assert.Equal("raw", harness.Session.Text);

        harness.Mode = EtwEditorMode.Markdown;
        harness.Session.SyncTextFromActiveEditor();
        Assert.Equal("from markdown", harness.Session.Text);
    }

    [WpfFact]
    public void SyncTextFromEditor_LeavesTheTextAloneWhenTheEditorHasNothingYet()
    {
        Harness harness = new("raw");
        harness.Spreadsheet.Content = null;

        harness.Session.SyncTextFromEditor(EtwEditorMode.Spreadsheet);

        Assert.Equal("raw", harness.Session.Text);
    }

    [WpfFact]
    public void SetTextFromEditor_FlagsOnlyThatEditorWhileTheTextChanges()
    {
        Harness harness = new("raw");
        List<(bool Markdown, bool Spreadsheet)> seen = [];
        harness.Session.TextChanged += (_, _) =>
            seen.Add((harness.Session.IsSyncingTextFromMarkdown, harness.Session.IsSyncingTextFromSpreadsheet));

        harness.Session.SetTextFromEditor(EtwEditorMode.Markdown, "a");
        harness.Session.SetTextFromEditor(EtwEditorMode.Spreadsheet, "b");
        harness.Session.Text = "c";

        Assert.Equal([(true, false), (false, true), (false, false)], seen);
        Assert.False(harness.Session.IsSyncingTextFromMarkdown || harness.Session.IsSyncingTextFromSpreadsheet);
    }

    [WpfFact]
    public void SyncEditorFromText_IsSkippedForTheEditorTheTextIsBeingWrittenFrom()
    {
        Harness harness = new("raw");
        harness.Session.TextChanged += (_, _) =>
        {
            // What the window's TextChanged handler does: push the new text into the editors.
            harness.Session.SyncEditorFromText(EtwEditorMode.Markdown);
            harness.Session.SyncEditorFromText(EtwEditorMode.Spreadsheet);
        };

        harness.Session.SetTextFromEditor(EtwEditorMode.Markdown, "from markdown");

        Assert.Empty(harness.Markdown.Loaded);
        Assert.Equal(["from markdown"], harness.Spreadsheet.Loaded);
    }

    [WpfFact]
    public void SyncActiveEditorFromText_PushesTheTextIntoTheVisibleEditorOnly()
    {
        Harness harness = new("raw");

        harness.Session.SyncActiveEditorFromText();
        harness.Mode = EtwEditorMode.Spreadsheet;
        harness.Session.SyncActiveEditorFromText();

        Assert.Empty(harness.Markdown.Loaded);
        Assert.Equal(["raw"], harness.Spreadsheet.Loaded);
    }

    private sealed class Harness
    {
        public Harness(string text)
        {
            TextBox textBox = new() { Text = text };
            TextBoxSurface textSurface = new(textBox);
            Session = new EditorSession(textSurface, Markdown, Spreadsheet, () => Mode);
        }

        public EtwEditorMode Mode { get; set; } = EtwEditorMode.Text;

        public FakeSurface Markdown { get; } = new(EtwEditorMode.Markdown);

        public FakeSurface Spreadsheet { get; } = new(EtwEditorMode.Spreadsheet);

        public EditorSession Session { get; }
    }

    private sealed class FakeSurface(EtwEditorMode mode) : IEditorSurface
    {
        public EtwEditorMode Mode => mode;

        public string? Content { get; set; } = string.Empty;

        public List<string> Loaded { get; } = [];

        public string? GetText() => Content;

        public void SetText(string text) => Loaded.Add(text);

        public void SelectAndShow(TextSpan range) { }
    }
}
