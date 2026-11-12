using System;
using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Editing;

/// <summary>
/// The Edit Text Window's <see cref="IEditorSession"/>.
/// </summary>
/// <remarks>
/// The raw text editor holds the document in every mode, so text and selection always go to
/// <see cref="TextBoxSurface"/>, even while the Markdown or spreadsheet editor is showing. Only
/// <see cref="SelectInEditor"/> goes to the surface for the current mode.
/// </remarks>
internal sealed class EditorSession : IEditorSession
{
    private readonly TextBoxSurface textSurface;
    private readonly IEditorSurface markdownSurface;
    private readonly IEditorSurface spreadsheetSurface;
    private readonly Func<EtwEditorMode> getMode;

    public EditorSession(
        TextBoxSurface textSurface,
        IEditorSurface markdownSurface,
        IEditorSurface spreadsheetSurface,
        Func<EtwEditorMode> getMode)
    {
        this.textSurface = textSurface;
        this.markdownSurface = markdownSurface;
        this.spreadsheetSurface = spreadsheetSurface;
        this.getMode = getMode;

        textSurface.TextChanged += (_, _) => TextChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Text
    {
        get => textSurface.Text;
        set => textSurface.Text = value;
    }

    public EtwEditorMode Mode => getMode();

    public TextSpan Selection
    {
        get => textSurface.Selection;
        set => textSurface.Selection = value;
    }

    public string SelectedText => textSurface.SelectedText;

    public event EventHandler? TextChanged;

    public event EventHandler? ModeChanged;

    public void ReplaceSelection(string replacement) => textSurface.ReplaceSelection(replacement);

    public void ReplaceRange(TextSpan range, string replacement)
    {
        Selection = range;
        ReplaceSelection(replacement);
    }

    public void AppendText(string text) => textSurface.AppendText(text);

    public string GetSelectedTextOrAllText() => Selection.Length == 0 ? Text : SelectedText;

    public void ReplaceSelectedTextOrAllText(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        if (Selection.Length == 0)
            Text = replacement;
        else
            ReplaceSelection(replacement);
    }

    public void SelectInEditor(TextSpan range) => ActiveSurface.SelectAndShow(range);

    public void ScrollToEnd() => textSurface.ScrollToEnd();

    /// <summary>Called by the window after it switches editors.</summary>
    internal void RaiseModeChanged() => ModeChanged?.Invoke(this, EventArgs.Empty);

    private IEditorSurface ActiveSurface => Mode switch
    {
        EtwEditorMode.Markdown => markdownSurface,
        EtwEditorMode.Spreadsheet => spreadsheetSurface,
        _ => textSurface,
    };
}
