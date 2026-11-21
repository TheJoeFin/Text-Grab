using System;
using System.Windows.Controls;
using Text_Grab.Core.Editing;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Editing;

/// <summary>
/// The raw text editor, a <see cref="TextBox"/>. Its text is the session's text in every mode, so
/// besides showing ranges in text mode it is where <see cref="EditorSession"/> reads and edits the document.
/// </summary>
internal sealed class TextBoxSurface : IEditorSurface, ILineViewport
{
    private readonly TextBox textBox;

    public TextBoxSurface(TextBox textBox)
    {
        this.textBox = textBox;
        textBox.TextChanged += (_, _) => TextChanged?.Invoke(this, EventArgs.Empty);
        // Handled ones too: the text box's own ScrollViewer marks the event handled.
        textBox.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler((_, _) => ViewportChanged?.Invoke(this, EventArgs.Empty)),
            handledEventsToo: true);
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

    public event EventHandler? ViewportChanged;

    /// <summary>The text box's scroll offset in lines of <see cref="LineHeight"/>.</summary>
    public double FirstVisibleLine
        => WindowUtilities.GetScrollViewer(textBox) is ScrollViewer viewer ? viewer.VerticalOffset / LineHeight(textBox) : 0;

    /// <summary>How far down the text box is scrolled, from 0 at the top to 1 at the bottom.</summary>
    public double VerticalScrollFraction
        => WindowUtilities.GetScrollViewer(textBox) is ScrollViewer viewer && viewer.ScrollableHeight > 0
            ? Math.Clamp(viewer.VerticalOffset / viewer.ScrollableHeight, 0, 1)
            : 0;

    /// <summary>
    /// The height of one line of <paramref name="textBox"/>'s text, as WPF lays out a text box whose
    /// line height is not set: its font's line spacing at its font size.
    /// </summary>
    public static double LineHeight(TextBox textBox) => textBox.FontSize * textBox.FontFamily.LineSpacing;

    /// <summary>The text box's text; it is the session's text, so there is nothing to sync.</summary>
    public string? GetText() => Text;

    public void SetText(string text) => Text = text;

    public void ReplaceSelection(string replacement) => textBox.SelectedText = replacement;

    public void AppendText(string text) => textBox.AppendText(text);

    public void ScrollToEnd() => textBox.ScrollToEnd();

    public bool ScrollByWheel(int wheelDelta) => SmoothScrollBehavior.ScrollBy(textBox, wheelDelta);

    public void SelectAndShow(TextSpan range)
    {
        textBox.Select(range.Start, range.Length);
        textBox.Focus();
    }
}
