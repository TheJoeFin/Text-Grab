using Text_Grab.UndoRedoOperations;

namespace Tests;

public class UndoRedoTests
{
    private sealed class FakeOperation(uint transactionId) : IUndoRedoOperation
    {
        public uint TransactionId { get; } = transactionId;

        public int UndoCount { get; private set; }

        public int RedoCount { get; private set; }

        public UndoRedoOperation GetUndoRedoOperation() => UndoRedoOperation.None;

        public void Undo() => UndoCount++;

        public void Redo() => RedoCount++;
    }

    [Fact]
    public void UndoStack_TrimsOldestTransactions_WhenOverCapacity()
    {
        UndoRedo undoRedo = new();
        int transactionCount = UndoRedo.UndoRedoTransactionCapacity + 50;

        for (uint transactionId = 0; transactionId < transactionCount; transactionId++)
        {
            undoRedo.AddOperationToUndoStack(new FakeOperation(transactionId));
            undoRedo.AddOperationToUndoStack(new FakeOperation(transactionId));
        }

        Assert.Equal(UndoRedo.UndoRedoTransactionCapacity * 2, undoRedo.UndoOperationCount);
    }

    [Fact]
    public void UndoStack_KeepsAllOperations_WhenUnderCapacity()
    {
        UndoRedo undoRedo = new();

        for (uint transactionId = 0; transactionId < 10; transactionId++)
            undoRedo.AddOperationToUndoStack(new FakeOperation(transactionId));

        Assert.Equal(10, undoRedo.UndoOperationCount);
    }

    [Fact]
    public void Undo_RunsAllOperationsOfNewestTransaction()
    {
        UndoRedo undoRedo = new();
        FakeOperation olderOperation = new(transactionId: 1);
        FakeOperation newerOperation1 = new(transactionId: 2);
        FakeOperation newerOperation2 = new(transactionId: 2);
        undoRedo.AddOperationToUndoStack(olderOperation);
        undoRedo.AddOperationToUndoStack(newerOperation1);
        undoRedo.AddOperationToUndoStack(newerOperation2);

        undoRedo.Undo();

        Assert.Equal(0, olderOperation.UndoCount);
        Assert.Equal(1, newerOperation1.UndoCount);
        Assert.Equal(1, newerOperation2.UndoCount);
        Assert.Equal(1, undoRedo.UndoOperationCount);
        Assert.True(undoRedo.HasRedoOperations());
    }

    [Fact]
    public void Reset_ClearsAllOperations()
    {
        UndoRedo undoRedo = new();
        undoRedo.AddOperationToUndoStack(new FakeOperation(transactionId: 1));
        undoRedo.Undo();
        undoRedo.AddOperationToUndoStack(new FakeOperation(transactionId: 2));

        undoRedo.Reset();

        Assert.False(undoRedo.HasUndoOperations());
        Assert.False(undoRedo.HasRedoOperations());
    }
}
