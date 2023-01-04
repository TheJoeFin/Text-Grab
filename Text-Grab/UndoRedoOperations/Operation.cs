using System.Collections.Generic;
using System.Windows.Controls;
using Text_Grab.Controls;
using Windows.Foundation;

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
    RemoveWordBorder,
    MoveWordBorder,
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

    public Size OldSize { get; set; }

    public Size NewSize { get; set; }

    public Point OldPoint { get; set; }

    public Point NewPoint { get; set; }
}