using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Controls;

namespace Text_Grab.UndoRedoOperations;

internal abstract class Operation
{
    protected Operation(uint transationId) => TransactionId = transationId;

    public uint TransactionId { get; }
}

public enum UndoRedoOperation
{
    None,
    AddWordBorder,
    ChangeWord,
    RemoveWordBorder,
    ResizeWordBorder,
}

public interface IUndoRedoOperation
{
    void Undo();

    void Redo();

    UndoRedoOperation GetUndoRedoOperation();

    uint TransactionId { get; }
}

public struct GrabFrameOperationArgs
{
    public Canvas GrabFrameCanvas { get; set; }

    public ICollection<WordBorder> WordBorders { get; set; }

    public WordBorder WordBorder { get; set; }

    public List<WordBorder> RemovingWordBorders { get; set; }

    public Rect OldSize { get; set; }

    public Rect NewSize { get; set; }

    public string OldWord { get; set; }

    public string NewWord { get; set; }
}