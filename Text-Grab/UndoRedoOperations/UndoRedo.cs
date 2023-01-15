using System;
using System.Collections.Generic;

namespace Text_Grab.UndoRedoOperations;

class UndoRedo
{
    public const int UndoRedoTransactionCapacity = 100;

    private uint TransactionId { get; set; }

    private uint HighestUsedTransactionId { get; set; }

    private uint ActiveTransactionIdCount { get; set; }

    private LinkedList<IUndoRedoOperation> RedoStack { get; } = new();

    private LinkedList<IUndoRedoOperation> UndoStack { get; } = new();

    // used for readability.
    public void StartTransaction()
    {
    }

    public void EndTransaction()
    {
        if (TransactionId <= HighestUsedTransactionId)
        {
            TransactionId++;
            ActiveTransactionIdCount++;
        }
    }

    public void Reset()
    {
        UndoStack.Clear();
        RedoStack.Clear();
        TransactionId = 0;
        HighestUsedTransactionId = 0;
        ActiveTransactionIdCount = 0;
    }

    private void AddOperationToUndoStack(IUndoRedoOperation operation)
    {
        if (ActiveTransactionIdCount >= UndoRedoTransactionCapacity)
        {
            uint? transactionIdToRemove = UndoStack.First?.Value.TransactionId;
            while (UndoStack.First?.Value.TransactionId == transactionIdToRemove)
            {
                if (UndoStack.Count != 0)
                    UndoStack.RemoveFirst();
            }

            --ActiveTransactionIdCount;
        }

        UndoStack.AddLast(operation);
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

    public void Undo()
    {
        if (UndoStack.Count == 0 || UndoStack.Last is null)
            return;

        var operationNode = UndoStack.Last;
        var currentTransactionId = operationNode.Value.TransactionId;
        while (operationNode != null && operationNode.Value.TransactionId == currentTransactionId)
        {
            var prev = operationNode.Previous;
            var operation = operationNode.Value;
            operation.Undo();

            // Add operation into redo stack.
            RedoStack.AddLast(operation);

            // Remove from Undo Stack.
            UndoStack.RemoveLast();

            operationNode = prev;
        }

        --ActiveTransactionIdCount;
    }

    public void Redo()
    {
        if (RedoStack.Count == 0 || RedoStack.Last is null)
            return;

        var operationNode = RedoStack.Last;
        var currentTransactionId = operationNode.Value.TransactionId;
        while (operationNode != null && operationNode.Value.TransactionId == currentTransactionId)
        {
            var prev = operationNode.Previous;
            var operation = RedoStack.Last.Value;
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