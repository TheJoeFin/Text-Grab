using Text_Grab.Models;

namespace Tests;

public class SpreadsheetUndoHistoryTests
{
    [Fact]
    public void RecordChange_UndoAndRedo_RestoreExpectedStates()
    {
        SpreadsheetUndoHistory history = new();
        SpreadsheetUndoState originalState = new("{\"Rows\":[[\"one\"]]}", 1, 2);
        SpreadsheetUndoState editedState = new("{\"Rows\":[[\"two\"]]}", 3, 4);

        history.RecordChange(originalState, editedState);

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);

        SpreadsheetUndoState? undoneState = history.Undo(editedState);

        Assert.NotNull(undoneState);
        Assert.Equal(originalState.DocumentJson, undoneState.DocumentJson);
        Assert.Equal(originalState.FocusRow, undoneState.FocusRow);
        Assert.Equal(originalState.FocusColumn, undoneState.FocusColumn);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);

        SpreadsheetUndoState? redoneState = history.Redo(undoneState);

        Assert.NotNull(redoneState);
        Assert.Equal(editedState.DocumentJson, redoneState.DocumentJson);
        Assert.Equal(editedState.FocusRow, redoneState.FocusRow);
        Assert.Equal(editedState.FocusColumn, redoneState.FocusColumn);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void RecordChange_NoOpChange_DoesNotCreateUndoEntry()
    {
        SpreadsheetUndoHistory history = new();
        SpreadsheetUndoState state = new("{\"Rows\":[[\"same\"]]}", 0, 0);

        history.RecordChange(state, new SpreadsheetUndoState(state.DocumentJson, 5, 6));

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void RecordChange_NewEditClearsRedoHistory()
    {
        SpreadsheetUndoHistory history = new();
        SpreadsheetUndoState stateA = new("{\"Rows\":[[\"A\"]]}", 0, 0);
        SpreadsheetUndoState stateB = new("{\"Rows\":[[\"B\"]]}", 0, 1);
        SpreadsheetUndoState stateC = new("{\"Rows\":[[\"C\"]]}", 1, 0);

        history.RecordChange(stateA, stateB);
        SpreadsheetUndoState? undoneState = history.Undo(stateB);

        Assert.NotNull(undoneState);
        Assert.True(history.CanRedo);

        history.RecordChange(undoneState, stateC);

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }
}
