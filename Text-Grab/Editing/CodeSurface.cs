using System;
using System.Windows.Controls.Primitives;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Text_Grab.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Models;
using Text_Grab.Utilities;

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
internal sealed class CodeSurface : IEditorSurface, ILineViewport
{
    private readonly CodeEditor editor;

    public CodeSurface(CodeEditor editor)
    {
        this.editor = editor;
        editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    public EtwEditorMode Mode => EtwEditorMode.Code;

    /// <summary>True while <see cref="SetText"/> is changing the editor's document.</summary>
    public bool IsApplyingText { get; private set; }

    /// <summary>The editor's selection (or caret, when nothing is selected) in plain-text offsets.</summary>
    public TextSpan Selection
    {
        get => new(editor.SelectionStart, editor.SelectionLength);
        set
        {
            TextSpan clamped = value.ClampTo(editor.Document.TextLength);
            editor.Select(clamped.Start, clamped.Length);
        }
    }

    public event EventHandler? ViewportChanged;

    /// <summary>
    /// The document line at the top of the viewport, from 0, plus how far into its row on screen the
    /// view has scrolled.
    /// </summary>
    /// <remarks>
    /// A document line, so a line wrapped onto several rows, or a collapsed region, counts as one line.
    /// </remarks>
    public double FirstVisibleLine
    {
        get
        {
            TextView view = editor.TextArea.TextView;
            double top = view.ScrollOffset.Y;
            if (view.GetDocumentLineByVisualTop(top) is not DocumentLine line)
                return 0;

            double lineTop = view.GetVisualTopByDocumentLine(line.LineNumber);
            // The line's row as laid out, which is taller than the default when it wraps.
            double height = view.GetVisualLine(line.LineNumber)?.Height ?? view.DefaultLineHeight;
            double fraction = height > 0 ? Math.Clamp((top - lineTop) / height, 0, 1) : 0;
            return line.LineNumber - 1 + fraction;
        }
    }

    /// <summary>How far down the code editor is scrolled, from 0 at the top to 1 at the bottom.</summary>
    public double VerticalScrollFraction
    {
        get
        {
            IScrollInfo scrollInfo = editor.TextArea.TextView;
            double scrollable = scrollInfo.ExtentHeight - scrollInfo.ViewportHeight;
            return scrollable > 0 ? Math.Clamp(scrollInfo.VerticalOffset / scrollable, 0, 1) : 0;
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

    /// <summary>
    /// Fills the editor with <paramref name="text"/> as a fresh document, with nothing to undo: what
    /// <see cref="SetText"/> does, then the undo history is cleared.
    /// </summary>
    /// <remarks>
    /// Used where the editor takes the text up rather than an edit being made to it: entering code mode
    /// and opening a file. Undoing that fill would empty the document or bring back the previous one.
    /// </remarks>
    public void LoadText(string text)
    {
        SetText(text);
        editor.Document.UndoStack.ClearAll();
    }

    public void SelectAndShow(TextSpan range)
    {
        Selection = range;
        // A Find match or other shown range may sit inside a collapsed region.
        editor.ExpandFoldingsAround(Selection.Start, Selection.End);
        TextLocation location = editor.Document.GetLocation(Selection.Start);
        editor.ScrollTo(location.Line, location.Column);
        editor.TextArea.Focus();
    }

    public void ScrollToEnd() => editor.ScrollToEnd();

    public bool ScrollByWheel(int wheelDelta) => SmoothScrollBehavior.ScrollBy(editor, wheelDelta);
}
