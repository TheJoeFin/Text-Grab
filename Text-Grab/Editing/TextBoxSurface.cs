using System;
using System.Windows.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Models;

namespace Text_Grab.Editing;

/// <summary>
/// The raw text editor, a <see cref="TextBox"/>. Its text is the session's text in every mode, so
/// besides showing ranges in text mode it is where <see cref="EditorSession"/> reads and edits the document.
/// </summary>
internal sealed class TextBoxSurface : IEditorSurface
{
    private readonly TextBox textBox;

    public TextBoxSurface(TextBox textBox)
    {
        this.textBox = textBox;
        textBox.TextChanged += (_, _) => TextChanged?.Invoke(this, EventArgs.Empty);
    }

    public EtwEditorMode Mode => EtwEditorMode.Text;

    public string Text
    {
        get => textBox.Text;
        set => textBox.Text = value;
    }

    public TextSpan Selection
    {
        get => new(textBox.SelectionStart, textBox.SelectionLength);
        set => textBox.Select(value.Start, value.Length);
    }

    public string SelectedText => textBox.SelectedText;

    public event EventHandler? TextChanged;

    public void ReplaceSelection(string replacement) => textBox.SelectedText = replacement;

    public void AppendText(string text) => textBox.AppendText(text);

    public void ScrollToEnd() => textBox.ScrollToEnd();

    public void SelectAndShow(TextSpan range)
    {
        textBox.Select(range.Start, range.Length);
        textBox.Focus();
    }
}
