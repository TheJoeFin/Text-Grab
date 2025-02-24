using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Text_Grab.Controls;

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

    private readonly List<WordBorder> RemovingWordBorders;

    private readonly Canvas Canvas;

    private readonly ICollection<WordBorder> WordBorders;

    public UndoRedoOperation GetUndoRedoOperation() => UndoRedoOperation.AddWordBorder;

    public void Undo()
    {
        foreach (WordBorder wordBorder in RemovingWordBorders)
        {
            try
            {
                Canvas.Children.Add(wordBorder);
                WordBorders.Add(wordBorder);
            }
            catch (ArgumentException) { }
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
