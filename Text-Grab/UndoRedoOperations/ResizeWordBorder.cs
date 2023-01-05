using System.Windows;
using Text_Grab.Controls;

namespace Text_Grab.UndoRedoOperations;

internal class ResizeWordBorder : Operation, IUndoRedoOperation
{
    public ResizeWordBorder(uint transactionId, WordBorder wordBorder,
        Rect oldSize, Rect newSize) : base(transactionId)
    {
        WordBorder = wordBorder;
        OldSize = oldSize;
        NewSize = newSize;
    }

    private WordBorder WordBorder;

    private Rect OldSize;
    private Rect NewSize;

    public UndoRedoOperation GetUndoRedoOperation() => UndoRedoOperation.AddWordBorder;

    public void Undo()
    {
        WordBorder.Width = OldSize.Width;
        WordBorder.Height = OldSize.Height;
        WordBorder.Left = OldSize.Left;
        WordBorder.Top = OldSize.Top;
    }

    public void Redo()
    {
        WordBorder.Width = NewSize.Width;
        WordBorder.Height = NewSize.Height;
        WordBorder.Left = NewSize.Left;
        WordBorder.Top = NewSize.Top;
    }
}