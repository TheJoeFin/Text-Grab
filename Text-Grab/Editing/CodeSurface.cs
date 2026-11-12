using System;
using Text_Grab.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Editing;

/// <summary>
/// The code editor, an AvalonEdit <see cref="CodeEditor"/>. It holds the same plain text as the raw
/// text box, character for character, so session offsets need no mapping.
/// </summary>
/// <remarks>
/// <see cref="SetText"/> pushes only the part that changed (<see cref="TextEdit.Between"/>), as one
/// undoable edit, so the editor's caret, scroll position and undo history survive a transform made
/// through the session. <see cref="IsApplyingText"/> is set meanwhile, so the window does not copy the
/// same change straight back into the text box.
/// </remarks>
internal sealed class CodeSurface(CodeEditor editor) : IEditorSurface
{
    public EtwEditorMode Mode => EtwEditorMode.Code;

    /// <summary>True while <see cref="SetText"/> is changing the editor's document.</summary>
    public bool IsApplyingText { get; private set; }

    /// <summary>The editor's selection (or caret, when nothing is selected) in plain-text offsets.</summary>
    public TextSpan Selection
    {
        get => new(editor.SelectionStart, editor.SelectionLength);
        set
        {
            TextSpan clamped = Clamp(value);
            editor.Select(clamped.Start, clamped.Length);
        }
    }

    public string? GetText() => editor.Document.Text;

    public void SetText(string text)
    {
        TextEdit edit = TextEdit.Between(editor.Document.Text, text);
        if (edit.IsEmpty)
            return;

        IsApplyingText = true;
        try
        {
            editor.Document.Replace(edit.Removed.Start, edit.Removed.Length, edit.Inserted);
        }
        finally
        {
            IsApplyingText = false;
        }
    }

    public void SelectAndShow(TextSpan range)
    {
        Selection = range;
        ICSharpCode.AvalonEdit.Document.TextLocation location = editor.Document.GetLocation(Selection.Start);
        editor.ScrollTo(location.Line, location.Column);
        editor.TextArea.Focus();
    }

    public void ScrollToEnd() => editor.ScrollToEnd();

    private TextSpan Clamp(TextSpan range)
    {
        int length = editor.Document.TextLength;
        int start = Math.Clamp(range.Start, 0, length);
        return new TextSpan(start, Math.Clamp(range.Length, 0, length - start));
    }
}
