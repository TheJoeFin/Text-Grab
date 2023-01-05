using System.Windows;
using Text_Grab.Controls;

namespace Text_Grab.UndoRedoOperations;

internal class MoveWordBorder : Operation, IUndoRedoOperation
{
    public MoveWordBorder(uint transactionId, WordBorder wordBorder,
        Point oldPoint, Point newPoint) : base(transactionId)
    {
        WordBorder = wordBorder;
        OldPoint = oldPoint;
        NewPoint = newPoint;
    }

    private WordBorder WordBorder;

    private Point OldPoint;
    private Point NewPoint;

    public UndoRedoOperation GetUndoRedoOperation() => UndoRedoOperation.AddWordBorder;

    public void Undo()
    {
        WordBorder.Left = OldPoint.X;
        WordBorder.Top = OldPoint.Y;
    }

    public void Redo()
    {
        WordBorder.Left = NewPoint.X;
        WordBorder.Top = NewPoint.Y;
    }
}