using System;
using System.Collections.Generic;

namespace Text_Grab.Models;

internal sealed class SpreadsheetUndoState
{
    public SpreadsheetUndoState(string documentJson, int? focusRow, int? focusColumn)
    {
        DocumentJson = documentJson ?? string.Empty;
        FocusRow = focusRow;
        FocusColumn = focusColumn;
    }

    public string DocumentJson { get; }

    public int? FocusRow { get; }

    public int? FocusColumn { get; }
}

internal sealed class SpreadsheetUndoHistory
{
    private readonly Stack<SpreadsheetUndoState> undoStack = [];
    private readonly Stack<SpreadsheetUndoState> redoStack = [];

    public bool CanUndo => undoStack.Count > 0;

    public bool CanRedo => redoStack.Count > 0;

    public void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
    }

    public void RecordChange(SpreadsheetUndoState? beforeChange, SpreadsheetUndoState? afterChange)
    {
        if (beforeChange is null
            || afterChange is null
            || string.Equals(beforeChange.DocumentJson, afterChange.DocumentJson, StringComparison.Ordinal))
        {
            return;
        }

        undoStack.Push(beforeChange);
        redoStack.Clear();
    }

    public SpreadsheetUndoState? Undo(SpreadsheetUndoState? currentState)
    {
        if (currentState is null || undoStack.Count == 0)
            return null;

        SpreadsheetUndoState previousState = undoStack.Pop();
        redoStack.Push(currentState);
        return previousState;
    }

    public SpreadsheetUndoState? Redo(SpreadsheetUndoState? currentState)
    {
        if (currentState is null || redoStack.Count == 0)
            return null;

        SpreadsheetUndoState nextState = redoStack.Pop();
        undoStack.Push(currentState);
        return nextState;
    }
}
