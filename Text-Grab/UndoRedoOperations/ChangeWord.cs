using System.Collections.Generic;
using System.Windows.Controls;
using Text_Grab.Controls;
using Text_Grab.UndoRedoOperations;

namespace Text_Grab.UndoRedoOperations;

internal class ChangeWord : Operation, IUndoRedoOperation
{
    public ChangeWord(uint transactionId, WordBorder wordBorder, 
        string oldWord, string newWord) : base(transactionId)
    {
        WordBorder = wordBorder;
        OldWord = oldWord;
        NewWord = newWord;
        
    }

    private WordBorder WordBorder;

    private string OldWord;

    private string NewWord;

    public UndoRedoOperation GetUndoRedoOperation() => UndoRedoOperation.AddWordBorder;

    public void Undo()
    {
        WordBorder.Word = OldWord;
    }

    public void Redo()
    {
        WordBorder.Word = NewWord;
    }
}