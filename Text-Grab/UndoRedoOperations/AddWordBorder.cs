using System.Collections.Generic;
using System.Windows.Controls;
using Text_Grab.Controls;
using Text_Grab.UndoRedoOperations;

namespace Text_Grab.UndoRedoOperations;

internal class AddWordBorder : Operation, IUndoRedoOperation
{
    public AddWordBorder(uint transactionId, WordBorder wordBorder, 
        Canvas canvas,List<WordBorder> wordBorders) : base(transactionId)
    {
        WordBorder = wordBorder;
        Canvas = canvas;
        WordBorders = wordBorders;
    }

    private WordBorder WordBorder;

    private Canvas Canvas;

    private List<WordBorder> WordBorders;

    public UndoRedoOperation GetUndoRedoOperation() => UndoRedoOperation.AddWordBorder;

    public void Undo()
    {
        Canvas.Children.Remove(WordBorder);
        WordBorders.Remove(WordBorder);
    }

    public void Redo()
    {
        Canvas.Children.Add(WordBorder);
        WordBorders.Add(WordBorder);
    }
}