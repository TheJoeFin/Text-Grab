using System.Collections.Generic;
using Windows.Foundation;
using System.Windows.Controls;
using Text_Grab.Controls;
using Text_Grab.UndoRedoOperations;

namespace Text_Grab.UndoRedoOperations;

internal class ResizeWordBorder : Operation, IUndoRedoOperation
{
    public ResizeWordBorder(uint transactionId, WordBorder wordBorder,
        Size oldSize, Size newSize) : base(transactionId)
    {
        WordBorder = wordBorder;
        OldSize = oldSize;
        NewSize = newSize;
    }

    private WordBorder WordBorder;

    private  Size OldSize;
    private  Size NewSize;

    public UndoRedoOperation GetUndoRedoOperation() => UndoRedoOperation.AddWordBorder;

    public void Undo()
    {
        WordBorder.Width = OldSize.Width;
        WordBorder.Height = OldSize.Height;
    }

    public void Redo()
    {
        WordBorder.Width = NewSize.Width;
        WordBorder.Height = NewSize.Height;
    }
}