using System;
using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Editing;

/// <summary>
/// The Edit Text Window's <see cref="IEditorSession"/>.
/// </summary>
/// <remarks>
/// <para>
/// The raw text editor holds the document in every mode, so text and selection always go to
/// <see cref="TextBoxSurface"/>, even while the Markdown, spreadsheet or code editor is showing.
/// <see cref="SelectInEditor"/> goes to the surface for the current mode. In code mode the window also
/// mirrors the selection between the code editor and the text box, so selection-based commands see
/// what the person selected.
/// </para>
/// <para>
/// The Markdown and spreadsheet editors are kept in step with the text in both directions: the
/// window syncs the text from an editor when that editor's content changed
/// (<see cref="SyncTextFromEditor"/>, <see cref="SetTextFromEditor"/>), and pushes the text into the
/// visible editor when the text changed underneath it (<see cref="SyncEditorFromText"/>). While text
/// is being written from an editor, that editor's flag is set, so the text box's TextChanged does not
/// push the same text straight back into it.
/// </para>
/// </remarks>
internal sealed class EditorSession : IEditorSession
{
    private readonly TextBoxSurface textSurface;
    private readonly IEditorSurface markdownSurface;
    private readonly IEditorSurface spreadsheetSurface;
    private readonly IEditorSurface codeSurface;
    private readonly Func<EtwEditorMode> getMode;
    private bool isSyncingTextFromSpreadsheet = false;
    private bool isSyncingTextFromMarkdown = false;
    private bool isSyncingTextFromCode = false;

    public EditorSession(
        TextBoxSurface textSurface,
        IEditorSurface markdownSurface,
        IEditorSurface spreadsheetSurface,
        IEditorSurface codeSurface,
        Func<EtwEditorMode> getMode)
    {
        this.textSurface = textSurface;
        this.markdownSurface = markdownSurface;
        this.spreadsheetSurface = spreadsheetSurface;
        this.codeSurface = codeSurface;
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

    public void ScrollToEnd()
    {
        textSurface.ScrollToEnd();

        // The code editor is the only other editor that shows the text as text; the rest re-render it.
        if (Mode == EtwEditorMode.Code && codeSurface is CodeSurface code)
            code.ScrollToEnd();
    }

    /// <summary>Called by the window after it switches editors.</summary>
    internal void RaiseModeChanged() => ModeChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>True while <see cref="Text"/> is being written from the spreadsheet editor.</summary>
    internal bool IsSyncingTextFromSpreadsheet => isSyncingTextFromSpreadsheet;

    /// <summary>True while <see cref="Text"/> is being written from the Markdown editor.</summary>
    internal bool IsSyncingTextFromMarkdown => isSyncingTextFromMarkdown;

    /// <summary>True while <see cref="Text"/> is being written from the code editor.</summary>
    internal bool IsSyncingTextFromCode => isSyncingTextFromCode;

    /// <summary>True while <see cref="Text"/> is being written from any editor other than the text box.</summary>
    internal bool IsSyncingTextFromEditor => isSyncingTextFromSpreadsheet || isSyncingTextFromMarkdown || isSyncingTextFromCode;

    /// <summary>
    /// Applies one edit the code editor just made to its own document to <see cref="Text"/>, with the
    /// code editor's flag set, and leaves the caret after the inserted text.
    /// </summary>
    /// <remarks>
    /// The code editor holds the same plain text as the text box, so it is synced edit by edit rather
    /// than by copying the whole document on every keystroke as the other editors are. Copying the whole
    /// text would also add a whole-text undo unit to the hidden text box per keystroke.
    /// </remarks>
    internal void ApplyEditFromCodeEditor(TextEdit edit)
    {
        isSyncingTextFromCode = true;
        try
        {
            ReplaceRange(edit.Removed, edit.Inserted);
            Selection = new TextSpan(edit.Removed.Start + edit.Inserted.Length, 0);
        }
        finally
        {
            isSyncingTextFromCode = false;
        }
    }

    /// <summary>
    /// Copies what the visible editor shows into <see cref="Text"/>. A no-op in text mode, where the
    /// text box is the editor.
    /// </summary>
    internal void SyncTextFromActiveEditor()
    {
        if (Mode != EtwEditorMode.Text)
            SyncTextFromEditor(Mode);
    }

    /// <summary>
    /// Pushes <see cref="Text"/> into the visible editor, the same way the window does when the text
    /// box's text changes. Used by anything that writes straight into the text while another editor
    /// is showing (streamed audio transcription, most of all); a no-op in text mode.
    /// </summary>
    internal void SyncActiveEditorFromText()
    {
        if (Mode != EtwEditorMode.Text)
            SyncEditorFromText(Mode);
    }

    /// <summary>
    /// Copies what the <paramref name="source"/> editor shows into <see cref="Text"/>, unless it has
    /// nothing to give yet.
    /// </summary>
    internal void SyncTextFromEditor(EtwEditorMode source)
    {
        if (SurfaceFor(source).GetText() is string text)
            SetTextFromEditor(source, text);
    }

    /// <summary>
    /// Writes <paramref name="text"/>, which came from the <paramref name="source"/> editor, into
    /// <see cref="Text"/> with that editor's flag set for the duration.
    /// </summary>
    internal void SetTextFromEditor(EtwEditorMode source, string text)
    {
        switch (source)
        {
            case EtwEditorMode.Spreadsheet:
                isSyncingTextFromSpreadsheet = true;
                Text = text;
                isSyncingTextFromSpreadsheet = false;
                break;
            case EtwEditorMode.Markdown:
                isSyncingTextFromMarkdown = true;
                Text = text;
                isSyncingTextFromMarkdown = false;
                break;
            case EtwEditorMode.Code:
                // Normally already equal: the code editor syncs edit by edit (ApplyEditFromCodeEditor).
                if (Text == text)
                    return;

                isSyncingTextFromCode = true;
                Text = text;
                isSyncingTextFromCode = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, "Only the Markdown, spreadsheet and code editors sync into the text.");
        }
    }

    /// <summary>
    /// Rebuilds the <paramref name="target"/> editor from <see cref="Text"/>, unless the text is being
    /// written from that same editor right now.
    /// </summary>
    internal void SyncEditorFromText(EtwEditorMode target)
    {
        switch (target)
        {
            case EtwEditorMode.Spreadsheet when isSyncingTextFromSpreadsheet:
            case EtwEditorMode.Markdown when isSyncingTextFromMarkdown:
            case EtwEditorMode.Code when isSyncingTextFromCode:
                return;
            case EtwEditorMode.Spreadsheet:
            case EtwEditorMode.Markdown:
            case EtwEditorMode.Code:
                SurfaceFor(target).SetText(Text);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "The text box is the text; there is nothing to sync into it.");
        }
    }

    private IEditorSurface ActiveSurface => SurfaceFor(Mode);

    private IEditorSurface SurfaceFor(EtwEditorMode mode) => mode switch
    {
        EtwEditorMode.Markdown => markdownSurface,
        EtwEditorMode.Spreadsheet => spreadsheetSurface,
        EtwEditorMode.Code => codeSurface,
        _ => textSurface,
    };
}
