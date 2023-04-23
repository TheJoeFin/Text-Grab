using System.Collections.Generic;
using System.Windows.Controls;
using Text_Grab.Controls;
using Text_Grab.UndoRedoOperations;

namespace Text_Grab.UndoRedoOperations;

internal class RemoveWordBorder : Operation, IUndoRedoOperation
{
    public RemoveWordBorder(uint transactionId, List<WordBorder> removingWordBorders,
        Canvas canvas, ICollection<WordBorder> wordBorders) : base(transactionId)
    {
        RemovingWordBorders = removingWordBorders;
        Canvas = canvas;
        WordBorders = wordBorders;
    }

    private List<WordBorder> RemovingWordBorders;
    
    private Canvas Canvas;

    private ICollection<WordBorder> WordBorders;

    public UndoRedoOperation GetUndoRedoOperation() => UndoRedoOperation.AddWordBorder;

    public void Undo()
    {
        foreach (WordBorder wordBorder in RemovingWordBorders)
        {
            Canvas.Children.Add(wordBorder);
            WordBorders.Add(wordBorder);
        }
    }

    public void Redo()
    {
        foreach (WordBorder wordBorder in RemovingWordBorders)
        {
            Canvas.Children.Remove(wordBorder);
            WordBorders.Remove(wordBorder);
        }
    }
}