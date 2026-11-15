using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Text_Grab.Controls;

internal enum SelectionHandle
{
    Start,
    End,
}

/// <summary>
/// The two touch grippers at the ends of a RichTextBox selection: a stem along the edge of the selection
/// and a knob hanging below the line. Drawn over the text box (not inside its scrolled content), so a knob
/// below the last visible line isn't clipped. It only draws and hit-tests; the touch behavior drives it.
/// </summary>
internal sealed class TouchSelectionHandlesAdorner : Adorner
{
    private const double KnobRadius = 8;
    private const double StemWidth = 2;
    // Fingers are bigger than knobs. The touch target is centred a little lower than the knob,
    // so it covers less of the text on the line above.
    private const double TouchRadius = 22;
    private const double TouchTargetDrop = 6;

    private readonly RichTextBox box;
    private Rect startCaret = Rect.Empty;
    private Rect endCaret = Rect.Empty;

    public TouchSelectionHandlesAdorner(RichTextBox box) : base(box)
    {
        this.box = box;
    }

    /// <summary>The middle of the line the handle sits on, in text box coordinates.</summary>
    public Point GetLinePoint(SelectionHandle handle)
    {
        Rect caret = handle == SelectionHandle.Start ? startCaret : endCaret;
        return new Point(caret.X, caret.Top + caret.Height / 2);
    }

    /// <summary>
    /// Places the handles on the selection's ends, each shown only while its end is inside
    /// <paramref name="viewport"/> (text box coordinates).
    /// </summary>
    public void Update(Rect viewport)
    {
        TextSelection selection = box.Selection;
        Rect start = Rect.Empty;
        Rect end = Rect.Empty;

        if (!selection.IsEmpty)
        {
            start = Visible(selection.Start.GetCharacterRect(LogicalDirection.Forward), viewport);
            end = Visible(selection.End.GetCharacterRect(LogicalDirection.Backward), viewport);
        }

        if (start == startCaret && end == endCaret)
            return;

        startCaret = start;
        endCaret = end;
        InvalidateVisual();
    }

    public void Clear() => Update(Rect.Empty);

    public bool IsShowingAny => !startCaret.IsEmpty || !endCaret.IsEmpty;

    public SelectionHandle? HitTestHandle(Point point)
    {
        double startDistance = startCaret.IsEmpty ? double.MaxValue : (TouchCenter(startCaret) - point).Length;
        double endDistance = endCaret.IsEmpty ? double.MaxValue : (TouchCenter(endCaret) - point).Length;

        if (startDistance > TouchRadius && endDistance > TouchRadius)
            return null;

        return startDistance < endDistance ? SelectionHandle.Start : SelectionHandle.End;
    }

    // Only the touch targets take input; a touch on the stem over the text belongs to the text.
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) =>
        HitTestHandle(hitTestParameters.HitPoint) is null ? null : new PointHitTestResult(this, hitTestParameters.HitPoint);

    protected override void OnRender(DrawingContext drawingContext)
    {
        Brush brush = box.TryFindResource("Teal") as Brush ?? SystemColors.HighlightBrush;
        DrawHandle(drawingContext, startCaret, brush);
        DrawHandle(drawingContext, endCaret, brush);
    }

    private static void DrawHandle(DrawingContext drawingContext, Rect caret, Brush brush)
    {
        if (caret.IsEmpty)
            return;

        drawingContext.DrawRectangle(brush, null, new Rect(caret.X - StemWidth / 2, caret.Top, StemWidth, caret.Height));
        drawingContext.DrawEllipse(brush, null, new Point(caret.X, caret.Bottom + KnobRadius), KnobRadius, KnobRadius);
    }

    private static Point TouchCenter(Rect caret) => new(caret.X, caret.Bottom + KnobRadius + TouchTargetDrop);

    private static Rect Visible(Rect caret, Rect viewport)
    {
        if (caret.IsEmpty || viewport.IsEmpty)
            return Rect.Empty;

        double middle = caret.Top + caret.Height / 2;
        bool inView = middle >= viewport.Top - 1 && middle <= viewport.Bottom + 1
            && caret.X >= viewport.Left - 1 && caret.X <= viewport.Right + 1;
        return inView ? caret : Rect.Empty;
    }
}
