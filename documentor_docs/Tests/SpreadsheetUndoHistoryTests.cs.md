# Technical Documentation: `SpreadsheetUndoHistoryTests.cs`

## Overview

The `SpreadsheetUndoHistoryTests.cs` file contains unit tests for verifying the behavior of the `SpreadsheetUndoHistory` class (located in `Text_Grab.Models`). These tests use the xUnit framework to validate state tracking, undoing edits, redoing edits, handling no-operation (no-op) changes, and clearing redo history when new edits occur.

---

## Context and Dependencies

* **Namespace:** `Tests`
* **Imports:**
  * `Text_Grab.Models`
* **Testing Framework:** xUnit (indicated by `[Fact]` attributes and `Assert` assertions)

---

## Test Class Summary

### `SpreadsheetUndoHistoryTests`

A unit test class containing three distinct test methods targeting the state management capabilities of `SpreadsheetUndoHistory`.

---

## Test Methods Breakdown

### 1. `RecordChange_UndoAndRedo_RestoreExpectedStates()`

* **Purpose:** Ensures that recording a valid document state change enables the undo stack, and performing an undo followed by a redo accurately restores state properties and toggles availability flags (`CanUndo` and `CanRedo`).
* **Execution Flow:**
  1. Instantiates `SpreadsheetUndoHistory`.
  2. Constructs an `originalState` (`FocusRow: 1`, `FocusColumn: 2`) and an `editedState` (`FocusRow: 3`, `FocusColumn: 4`).
  3. Calls `history.RecordChange(originalState, editedState)`.
  4. Asserts `CanUndo` is `true` and `CanRedo` is `false`.
  5. Executes `history.Undo(editedState)`.
  6. Asserts the returned state matches `originalState` in `DocumentJson`, `FocusRow`, and `FocusColumn`.
  7. Asserts `CanUndo` is now `false` and `CanRedo` is `true`.
  8. Executes `history.Redo(undoneState)`.
  9. Asserts the returned state matches `editedState` in `DocumentJson`, `FocusRow`, and `FocusColumn`.
  10. Asserts `CanUndo` is `true` and `CanRedo` is `false`.

---

### 2. `RecordChange_NoOpChange_DoesNotCreateUndoEntry()`

* **Purpose:** Verifies that attempting to record a change where the `DocumentJson` remains identical between states does not create an undo history entry.
* **Execution Flow:**
  1. Instantiates `SpreadsheetUndoHistory`.
  2. Constructs an initial `SpreadsheetUndoState` with `DocumentJson = "{\"Rows\":[[\"same\"]]}"`, `FocusRow = 0`, `FocusColumn = 0`.
  3. Calls `history.RecordChange` with a new state containing the identical `DocumentJson` string but updated row/column coordinates (`5`, `6`).
  4. Asserts both `CanUndo` and `CanRedo` remain `false`.

---

### 3. `RecordChange_NewEditClearsRedoHistory()`

* **Purpose:** Ensures that if an undo operation is performed (making `CanRedo` `true`) and a new edit is subsequently recorded, the redo history stack is cleared.
* **Execution Flow:**
  1. Instantiates `SpreadsheetUndoHistory`.
  2. Creates three states: `stateA`, `stateB`, and `stateC`.
  3. Records a change from `stateA` to `stateB`.
  4. Calls `history.Undo(stateB)` to navigate back to `stateA` and verifies `CanRedo` is `true`.
  5. Records a new change from `undoneState` (representing `stateA`) to `stateC`.
  6. Asserts that `CanUndo` is `true` and `CanRedo` has been reset to `false`.

---

## Observed Component Interfaces

Based on the test interactions within this file, the underlying models support the following contracts:

### `SpreadsheetUndoState`
* **Constructor:** `SpreadsheetUndoState(string documentJson, int focusRow, int focusColumn)`
* **Properties:**
  * `DocumentJson` (`string`): JSON representation of the spreadsheet content.
  * `FocusRow` (`int`): Row index focused in this state.
  * `FocusColumn` (`int`): Column index focused in this state.

### `SpreadsheetUndoHistory`
* **Properties:**
  * `CanUndo` (`bool`): Indicates if an undo operation is available.
  * `CanRedo` (`bool`): Indicates if a redo operation is available.
* **Methods:**
  * `RecordChange(SpreadsheetUndoState beforeState, SpreadsheetUndoState afterState)`: Registers a transition between states. If `DocumentJson` is unchanged, the entry is ignored.
  * `Undo(SpreadsheetUndoState currentState)` -> `SpreadsheetUndoState?`: Rolls back to the previous state.
  * `Redo(SpreadsheetUndoState currentState)` -> `SpreadsheetUndoState?`: Re-applies a previously undone state.