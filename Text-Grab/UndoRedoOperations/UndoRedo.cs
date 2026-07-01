using System.Collections.Generic;

namespace Text_Grab.UndoRedoOperations;

internal class UndoRedo
{
    public const int UndoRedoTransactionCapacity = 100;

    private uint TransactionId { get; set; }

    private uint HighestUsedTransactionId { get; set; }

    private uint ActiveTransactionIdCount { get; set; }

    private LinkedList<IUndoRedoOperation> RedoStack { get; } = new();

    private LinkedList<IUndoRedoOperation> UndoStack { get; } = new();

    // Exposed for tests so capacity trimming can be verified.
    internal int UndoOperationCount => UndoStack.Count;

    // used for readability.
    public void StartTransaction()
    {
    }

    public void EndTransaction()
    {
        if (TransactionId <= HighestUsedTransactionId)
            TransactionId++;
    }

    public void Reset()
    {
        UndoStack.Clear();
        RedoStack.Clear();
        TransactionId = 0;
        HighestUsedTransactionId = 0;
        ActiveTransactionIdCount = 0;
    }

    internal void AddOperationToUndoStack(IUndoRedoOperation operation)
    {
        // A transaction is a run of operations sharing a TransactionId, so a
        // new transaction starts whenever the incoming id differs from the
        // last stacked operation. Counting here (instead of in EndTransaction)
        // also covers operations inserted without transaction bracketing.
        if (UndoStack.Last is null || UndoStack.Last.Value.TransactionId != operation.TransactionId)
            ++ActiveTransactionIdCount;

        UndoStack.AddLast(operation);

        // Trim whole transactions from the oldest end so the stack cannot pin
        // an unbounded number of WordBorder controls and their visual trees.
        while (ActiveTransactionIdCount > UndoRedoTransactionCapacity && UndoStack.First is not null)
        {
            uint transactionIdToRemove = UndoStack.First.Value.TransactionId;
            while (UndoStack.First is not null && UndoStack.First.Value.TransactionId == transactionIdToRemove)
                UndoStack.RemoveFirst();

            --ActiveTransactionIdCount;
        }
    }

    private void ClearRedoStack()
    {
        if (RedoStack.Count != 0)
            RedoStack.Clear();
    }

    public bool HasUndoOperations() => UndoStack.Count != 0;

    public bool HasRedoOperations() => RedoStack.Count != 0;

    public void InsertUndoRedoOperation(UndoRedoOperation operation, object operationArgs)
    {
        switch (operation)
        {
            case UndoRedoOperation.AddWordBorder:
                InsertAddWordBorderOperation((GrabFrameOperationArgs)operationArgs);
                break;
            case UndoRedoOperation.ChangeWord:
                InsertChangeWordOperation((GrabFrameOperationArgs)operationArgs);
                break;
            case UndoRedoOperation.RemoveWordBorder:
                InsertRemoveWordBorderOperation((GrabFrameOperationArgs)operationArgs);
                break;
            case UndoRedoOperation.ResizeWordBorder:
                InsertResizeWordBorderOperation((GrabFrameOperationArgs)operationArgs);
                break;
            case UndoRedoOperation.ChangedImage:
                InsertChangedImageOperation((GrabFrameOperationArgs)operationArgs);
                break;
            case UndoRedoOperation.None:
            default:
                break;
        }

        if (operation != UndoRedoOperation.None)
        {
            HighestUsedTransactionId = TransactionId;
            ClearRedoStack();
        }
    }

    private void InsertChangeWordOperation(GrabFrameOperationArgs args) => AddOperationToUndoStack(
        new ChangeWord(TransactionId, args.WordBorder, args.OldWord, args.NewWord));

    private void InsertAddWordBorderOperation(GrabFrameOperationArgs args) => AddOperationToUndoStack(
        new AddWordBorder(TransactionId, args.WordBorder, args.GrabFrameCanvas, args.WordBorders));

    private void InsertRemoveWordBorderOperation(GrabFrameOperationArgs args) => AddOperationToUndoStack(
        new RemoveWordBorder(TransactionId, args.RemovingWordBorders, args.GrabFrameCanvas, args.WordBorders));

    private void InsertResizeWordBorderOperation(GrabFrameOperationArgs args) => AddOperationToUndoStack(
        new ResizeWordBorder(TransactionId, args.WordBorder, args.OldSize, args.NewSize));

    private void InsertChangedImageOperation(GrabFrameOperationArgs args) => AddOperationToUndoStack(
        new ChangedImage(TransactionId, args.DestinationImage, args.RemovingWordBorders, args.GrabFrameCanvas, args.WordBorders, args.OldImage, args.NewImage));

    public void Undo()
    {
        if (UndoStack.Count == 0 || UndoStack.Last is null)
            return;

        LinkedListNode<IUndoRedoOperation>? operationNode = UndoStack.Last;
        uint currentTransactionId = operationNode.Value.TransactionId;
        while (operationNode != null && operationNode.Value.TransactionId == currentTransactionId)
        {
            LinkedListNode<IUndoRedoOperation>? prev = operationNode.Previous;
            IUndoRedoOperation operation = operationNode.Value;
            operation.Undo();

            // Add operation into redo stack.
            RedoStack.AddLast(operation);

            // Remove from Undo Stack.
            UndoStack.RemoveLast();

            operationNode = prev;
        }

        if (ActiveTransactionIdCount > 0)
            --ActiveTransactionIdCount;
    }

    public void Redo()
    {
        if (RedoStack.Count == 0 || RedoStack.Last is null)
            return;

        LinkedListNode<IUndoRedoOperation>? operationNode = RedoStack.Last;
        uint currentTransactionId = operationNode.Value.TransactionId;
        while (operationNode != null && operationNode.Value.TransactionId == currentTransactionId)
        {
            LinkedListNode<IUndoRedoOperation>? prev = operationNode.Previous;
            IUndoRedoOperation operation = RedoStack.Last.Value;
            operation.Redo();

            // Add operation into Undo Stack.
            UndoStack.AddLast(operation);

            // Remove from the Redo Stack.
            RedoStack.RemoveLast();

            operationNode = prev;
        }

        ++ActiveTransactionIdCount;
    }
}
