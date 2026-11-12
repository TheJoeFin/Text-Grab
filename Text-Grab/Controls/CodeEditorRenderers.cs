using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Windows;
using System.Windows.Media;
using Text_Grab.Core.Editing;

namespace Text_Grab.Controls;

/// <summary>
/// Shades the other places in the document that hold the same text as the current selection, so a
/// selected word shows everywhere it repeats.
/// </summary>
internal sealed class SelectionOccurrencesRenderer(CodeEditor editor) : IBackgroundRenderer
{
    /// <summary>Selections shorter than this are too common to be worth shading.</summary>
    public const int MinimumLength = 2;

    /// <summary>Selections longer than this are not "a word"; skip them.</summary>
    public const int MaximumLength = 200;

    public Brush? Fill { get; set; }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Fill is null || textView.Document is null || !textView.VisualLinesValid)
            return;

        ICSharpCode.AvalonEdit.Editing.Selection selection = editor.TextArea.Selection;
        if (selection.IsEmpty || selection.IsMultiline)
            return;

        string needle = selection.GetText();
        if (!IsWorthShading(needle))
            return;

        bool wholeWord = IsWord(needle);
        int selectionStart = selection.SurroundingSegment.Offset;

        BackgroundGeometryBuilder builder = new() { AlignToWholePixels = true, CornerRadius = 2 };
        bool any = false;

        foreach (VisualLine visualLine in textView.VisualLines)
        {
            DocumentLine documentLine = visualLine.FirstDocumentLine;
            string lineText = textView.Document.GetText(documentLine);

            int index = 0;
            while ((index = lineText.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                int offset = documentLine.Offset + index;
                int end = index + needle.Length;

                if (offset != selectionStart && (!wholeWord || IsWholeWordAt(lineText, index, end)))
                {
                    builder.AddSegment(textView, new TextSegment { StartOffset = offset, Length = needle.Length });
                    any = true;
                }

                index = end;
            }
        }

        if (!any)
            return;

        Geometry? geometry = builder.CreateGeometry();
        if (geometry is not null)
            drawingContext.DrawGeometry(Fill, null, geometry);
    }

    internal static bool IsWorthShading(string text)
        => text.Length is >= MinimumLength and <= MaximumLength && !string.IsNullOrWhiteSpace(text);

    internal static bool IsWord(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }

        return true;
    }

    private static bool IsWholeWordAt(string line, int start, int end)
        => (start == 0 || !IsWordChar(line[start - 1])) && (end >= line.Length || !IsWordChar(line[end]));

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}

/// <summary>
/// Outlines the bracket beside the caret and the one it pairs with (see <see cref="BracketMatcher"/>).
/// </summary>
internal sealed class BracketHighlightRenderer(CodeEditor editor) : IBackgroundRenderer
{
    public Brush? Fill { get; set; }

    public Pen? Outline { get; set; }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Fill is null || textView.Document is null || !textView.VisualLinesValid)
            return;

        TextDocument document = textView.Document;
        int caret = editor.TextArea.Caret.Offset;

        // Hand the matcher only the stretch it may scan, not the whole document.
        int windowStart = Math.Max(0, caret - BracketMatcher.MaxScanDistance);
        int windowEnd = Math.Min(document.TextLength, caret + BracketMatcher.MaxScanDistance);
        string window = document.GetText(windowStart, windowEnd - windowStart);

        if (BracketMatcher.Find(window, caret - windowStart) is not (int bracket, int partner))
            return;

        BackgroundGeometryBuilder builder = new() { AlignToWholePixels = true, CornerRadius = 2 };
        builder.AddSegment(textView, new TextSegment { StartOffset = windowStart + bracket, Length = 1 });
        builder.AddSegment(textView, new TextSegment { StartOffset = windowStart + partner, Length = 1 });

        Geometry? geometry = builder.CreateGeometry();
        if (geometry is not null)
            drawingContext.DrawGeometry(Fill, Outline, geometry);
    }
}
