using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Text_Grab.Controls;

namespace Text_Grab.UndoRedoOperations;

internal class AddWordBorder : Operation, IUndoRedoOperation
{
    public AddWordBorder(uint transactionId, WordBorder wordBorder,
        Canvas canvas, ICollection<WordBorder> wordBorders) : base(transactionId)
    {
        WordBorder = wordBorder;
        Canvas = canvas;
        WordBorders = wordBorders;
    }

    private readonly WordBorder WordBorder;

    private readonly Canvas Canvas;

    private readonly ICollection<WordBorder> WordBorders;

    public UndoRedoOperation GetUndoRedoOperation() => UndoRedoOperation.AddWordBorder;

    public void Undo()
    {
        Canvas.Children.Remove(WordBorder);
        WordBorders.Remove(WordBorder);
    }

    public void Redo()
    {
        try
        {
            Canvas.Children.Add(WordBorder);
            WordBorders.Add(WordBorder);
        }
        catch (ArgumentException) { }
    }
}
