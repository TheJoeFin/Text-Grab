using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using TextOptions = System.Windows.Media.TextOptions;

namespace Text_Grab.Controls;

/// <summary>
/// Shows a line longer than <see cref="CodeEditor.LongLineLength"/> as its first
/// <see cref="CodeEditor.LongLineLength"/> characters followed by one marker standing for the rest.
/// </summary>
/// <remarks>
/// WPF lays out the whole of a line to show any of it, and a line of a million characters (a report's
/// embedded data, minified script) takes it tens of seconds. The hidden text stays in the document, so
/// copying, saving and the other editors still have all of it.
/// </remarks>
internal sealed class LongLineElementGenerator(CodeEditor editor) : VisualLineElementGenerator
{
    public override int GetFirstInterestedOffset(int startOffset)
    {
        DocumentLine line = CurrentContext.Document.GetLineByOffset(startOffset);
        if (line.Length <= CodeEditor.LongLineLength)
            return -1;

        // Also from inside the hidden part, as where a folded region that ends there leaves off.
        int hiddenStart = Math.Max(startOffset, line.Offset + CodeEditor.LongLineLength);
        return hiddenStart < line.EndOffset ? hiddenStart : -1;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        DocumentLine line = CurrentContext.Document.GetLineByOffset(offset);
        int hiddenLength = line.EndOffset - offset;

        VisualLineElementTextRunProperties properties = new(CurrentContext.GlobalTextRunProperties);
        properties.SetForegroundBrush(editor.LineNumbersForeground ?? Brushes.Gray);
        TextLine marker = FormattedTextElement.PrepareText(
            TextFormatter.Create(TextOptions.GetTextFormattingMode(CurrentContext.TextView)),
            $" … {hiddenLength:N0} more characters",
            properties);

        return new FormattedTextElement(marker, hiddenLength);
    }

    /// <summary>True when <paramref name="offset"/> is in the hidden part of a long line.</summary>
    internal static bool IsHidden(TextDocument document, int offset)
    {
        DocumentLine line = document.GetLineByOffset(offset);
        return line.Length > CodeEditor.LongLineLength && offset - line.Offset >= CodeEditor.LongLineLength;
    }
}

/// <summary>
/// AvalonEdit's <see cref="DocumentHighlighter"/>, except that a line longer than
/// <see cref="CodeEditor.LongLineLength"/> is neither highlighted nor scanned: it shows as plain text and
/// passes on the highlighting state it starts with.
/// </summary>
/// <remarks>
/// <para>
/// Highlighting a line of a million characters takes the highlighter seconds and produces some hundred
/// thousand colored sections, and applying those to the line is quadratic: the UI never comes back.
/// Every line below it also needs the state at its end, which means scanning all of it again after any
/// edit above. Carrying the state over is a guess (a long line could open a comment it never closes),
/// but it is the one editors make, and long lines are nearly always data or minified code that closes
/// what it opens.
/// </para>
/// <para>
/// Adapted from <c>ICSharpCode.AvalonEdit.Highlighting.DocumentHighlighter</c> (AvalonEdit 6.3,
/// MIT license); only <see cref="HighlightLine"/> and <see cref="HighlightUpTo"/> differ. AvalonEdit has
/// no hook for this: the scanning loop is private.
/// </para>
/// </remarks>
internal sealed class LongLineHighlighter : ILineTracker, IHighlighter
{
    /// <summary>The span stack at the end of each line (0 being the start of the document); null until computed.</summary>
    private readonly CompressingTreeList<ImmutableStack<HighlightingSpan>?> storedSpanStacks = new(ReferenceEquals);
    private readonly CompressingTreeList<bool> isValid = new((a, b) => a == b);
    private readonly TextDocument document;
    private readonly IHighlightingDefinition definition;
    private readonly HighlightingEngine engine;
    private readonly WeakLineTracker weakLineTracker;
    private bool isHighlighting;
    private bool isInHighlightingGroup;
    private bool isDisposed;
    private int firstInvalidLine;

    public LongLineHighlighter(TextDocument document, IHighlightingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(definition);

        this.document = document;
        this.definition = definition;
        engine = new HighlightingEngine(definition.MainRuleSet);
        document.VerifyAccess();
        weakLineTracker = WeakLineTracker.Register(document, this);
        InvalidateSpanStacks();
    }

    public IDocument Document => document;

    public HighlightingColor? DefaultTextColor => null;

    public event HighlightingStateChangedEventHandler? HighlightingStateChanged;

    public void Dispose()
    {
        weakLineTracker.Deregister();
        isDisposed = true;
    }

    void ILineTracker.BeforeRemoveLine(DocumentLine line)
    {
        CheckIsHighlighting();
        int lineNumber = line.LineNumber;
        storedSpanStacks.RemoveAt(lineNumber);
        isValid.RemoveAt(lineNumber);
        if (lineNumber < isValid.Count)
        {
            isValid[lineNumber] = false;
            if (lineNumber < firstInvalidLine)
                firstInvalidLine = lineNumber;
        }
    }

    void ILineTracker.SetLineLength(DocumentLine line, int newTotalLength)
    {
        CheckIsHighlighting();
        int lineNumber = line.LineNumber;
        isValid[lineNumber] = false;
        if (lineNumber < firstInvalidLine)
            firstInvalidLine = lineNumber;
    }

    void ILineTracker.LineInserted(DocumentLine insertionPos, DocumentLine newLine)
    {
        CheckIsHighlighting();
        int lineNumber = newLine.LineNumber;
        storedSpanStacks.Insert(lineNumber, null);
        isValid.Insert(lineNumber, false);
        if (lineNumber < firstInvalidLine)
            firstInvalidLine = lineNumber;
    }

    void ILineTracker.RebuildDocument() => InvalidateSpanStacks();

    void ILineTracker.ChangeComplete(DocumentChangeEventArgs e)
    {
    }

    private void InvalidateSpanStacks()
    {
        CheckIsHighlighting();
        storedSpanStacks.Clear();
        storedSpanStacks.Add(ImmutableStack<HighlightingSpan>.Empty);
        storedSpanStacks.InsertRange(1, document.LineCount, null);
        isValid.Clear();
        isValid.Add(true);
        isValid.InsertRange(1, document.LineCount, false);
        firstInvalidLine = 1;
    }

    public HighlightedLine HighlightLine(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > document.LineCount)
            throw new ArgumentOutOfRangeException(nameof(lineNumber), lineNumber, $"Must be between 1 and {document.LineCount}.");

        CheckIsHighlighting();
        isHighlighting = true;
        try
        {
            HighlightUpTo(lineNumber - 1);
            DocumentLine line = document.GetLineByNumber(lineNumber);

            // A long line gets no sections, and the engine keeps the state it had before the line.
            HighlightedLine result = IsLong(line)
                ? new HighlightedLine(document, line)
                : engine.HighlightLine(document, line);

            UpdateTreeList(lineNumber);
            return result;
        }
        finally
        {
            isHighlighting = false;
        }
    }

    public IEnumerable<HighlightingColor> GetColorStack(int lineNumber)
    {
        if (lineNumber < 0 || lineNumber > document.LineCount)
            throw new ArgumentOutOfRangeException(nameof(lineNumber), lineNumber, $"Must be between 0 and {document.LineCount}.");

        if (firstInvalidLine <= lineNumber)
            UpdateHighlightingState(lineNumber);

        return storedSpanStacks[lineNumber]!.Select(span => span.SpanColor).Where(color => color is not null);
    }

    public void UpdateHighlightingState(int lineNumber)
    {
        CheckIsHighlighting();
        isHighlighting = true;
        try
        {
            HighlightUpTo(lineNumber);
        }
        finally
        {
            isHighlighting = false;
        }
    }

    private void HighlightUpTo(int targetLineNumber)
    {
        for (int i = 0; i <= targetLineNumber; i++)
        {
            if (firstInvalidLine > i)
            {
                if (firstInvalidLine > targetLineNumber)
                {
                    engine.CurrentSpanStack = storedSpanStacks[targetLineNumber];
                    break;
                }

                engine.CurrentSpanStack = storedSpanStacks[firstInvalidLine - 1];
                i = firstInvalidLine;
            }

            DocumentLine line = document.GetLineByNumber(i);
            if (!IsLong(line))
                engine.ScanLine(document, line);

            UpdateTreeList(i);
        }
    }

    private void UpdateTreeList(int lineNumber)
    {
        if (!EqualSpanStacks(engine.CurrentSpanStack, storedSpanStacks[lineNumber]))
        {
            isValid[lineNumber] = true;
            storedSpanStacks[lineNumber] = engine.CurrentSpanStack;
            if (lineNumber + 1 < isValid.Count)
            {
                isValid[lineNumber + 1] = false;
                firstInvalidLine = lineNumber + 1;
            }
            else
            {
                firstInvalidLine = int.MaxValue;
            }

            if (lineNumber + 1 < document.LineCount)
                HighlightingStateChanged?.Invoke(lineNumber + 1, lineNumber + 1);
        }
        else if (firstInvalidLine == lineNumber)
        {
            isValid[lineNumber] = true;
            firstInvalidLine = isValid.IndexOf(false);
            if (firstInvalidLine < 0)
                firstInvalidLine = int.MaxValue;
        }
    }

    private static bool IsLong(DocumentLine line) => line.Length > CodeEditor.LongLineLength;

    private static bool EqualSpanStacks(ImmutableStack<HighlightingSpan>? a, ImmutableStack<HighlightingSpan>? b)
    {
        if (a == b)
            return true;
        if (a is null || b is null)
            return false;

        while (!a.IsEmpty && !b.IsEmpty)
        {
            if (a.Peek() != b.Peek())
                return false;

            a = a.Pop();
            b = b.Pop();
            if (a == b)
                return true;
        }

        return a.IsEmpty && b.IsEmpty;
    }

    private void CheckIsHighlighting()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (isHighlighting)
            throw new InvalidOperationException("Invalid call - a highlighting operation is currently running.");
    }

    public void BeginHighlighting()
    {
        if (isInHighlightingGroup)
            throw new InvalidOperationException("Highlighting group is already open");

        isInHighlightingGroup = true;
    }

    public void EndHighlighting()
    {
        if (!isInHighlightingGroup)
            throw new InvalidOperationException("Highlighting group is not open");

        isInHighlightingGroup = false;
    }

    public HighlightingColor GetNamedColor(string name) => definition.GetNamedColor(name);
}
