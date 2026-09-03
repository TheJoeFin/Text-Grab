# Technical Documentation: `SpreadsheetUndoHistory.cs`

**File Path:** `Text-Grab/Models/SpreadsheetUndoHistory.cs`  
**Namespace:** `Text_Grab.Models`

---

## 1. Overview

The `SpreadsheetUndoHistory.cs` file provides lightweight undo and redo state management for spreadsheet data within the Text-Grab application. It consists of two `internal sealed` classes:

1. **`SpreadsheetUndoState`**: An immutable snapshot representing a spreadsheet's state, including its serialized JSON representation and optional focus coordinates (row and column).
2. **`SpreadsheetUndoHistory`**: A state manager using standard stack data structures (`undoStack` and `redoStack`) to record changes and support undo/redo operations.

---

## 2. Class Definitions & Key Components

### 2.1 `SpreadsheetUndoState`

Represents an individual state snapshot of the spreadsheet.

#### Properties

| Property | Type | Access | Description |
| :--- | :--- | :--- | :--- |
| `DocumentJson` | `string` | `get` | The JSON string representation of the spreadsheet document. Defaults to `string.Empty` if initialized with `null`. |
| `FocusRow` | `int?` | `get` | The nullable index of the currently focused row. |
| `FocusColumn` | `int?` | `get` | The nullable index of the currently focused column. |

#### Constructor

```csharp
public SpreadsheetUndoState(string documentJson, int? focusRow, int? focusColumn)
```
* **Parameters:**
  * `documentJson` (`string`): The document contents serialized as JSON. If `null`, it is stored as `string.Empty`.
  * `focusRow` (`int?`): Optional row index that currently has focus.
  * `focusColumn` (`int?`): Optional column index that currently has focus.

---

### 2.2 `SpreadsheetUndoHistory`

Manages the stacks of `SpreadsheetUndoState` instances to execute state traversals.

#### Fields

| Field | Type | Description |
| :--- | :--- | :--- |
| `undoStack` | `Stack<SpreadsheetUndoState>` | Private stack holding past spreadsheet states for undo actions. |
| `redoStack` | `Stack<SpreadsheetUndoState>` | Private stack holding undone states available for redo actions. |

#### Properties

| Property | Type | Access | Description |
| :--- | :--- | :--- | :--- |
| `CanUndo` | `bool` | `get` | Returns `true` if `undoStack` contains one or more elements; otherwise, `false`. |
| `CanRedo` | `bool` | `get` | Returns `true` if `redoStack` contains one or more elements; otherwise, `false`. |

---

## 3. Method Specifications

### `Clear()`
```csharp
public void Clear()
```
* **Description:** Empties both the `undoStack` and `redoStack`.
* **Side Effects:** Resets `CanUndo` and `CanRedo` to `false`.

---

### `RecordChange()`
```csharp
public void RecordChange(SpreadsheetUndoState? beforeChange, SpreadsheetUndoState? afterChange)
```
* **Description:** Records a new state transition into the undo history.
* **Execution Logic:**
  1. Checks if `beforeChange` is `null`, `afterChange` is `null`, or if `beforeChange.DocumentJson` matches `afterChange.DocumentJson` using `StringComparison.Ordinal`.
  2. If any of these validation checks are met, the method returns early and records nothing.
  3. Pushes `beforeChange` onto the `undoStack`.
  4. Clears the `redoStack`.

---

### `Undo()`
```csharp
public SpreadsheetUndoState? Undo(SpreadsheetUndoState? currentState)
```
* **Description:** Reverts to the most recent state on the `undoStack`.
* **Parameters:**
  * `currentState` (`SpreadsheetUndoState?`): The current state of the spreadsheet prior to performing the undo operation.
* **Returns:** The previous `SpreadsheetUndoState` popped from `undoStack`, or `null` if the operation cannot be completed.
* **Execution Logic:**
  1. If `currentState` is `null` or `undoStack` is empty (`Count == 0`), returns `null`.
  2. Pops the top state from `undoStack`.
  3. Pushes `currentState` onto the `redoStack`.
  4. Returns the popped state.

---

### `Redo()`
```csharp
public SpreadsheetUndoState? Redo(SpreadsheetUndoState? currentState)
```
* **Description:** Re-applies the most recent state on the `redoStack`.
* **Parameters:**
  * `currentState` (`SpreadsheetUndoState?`): The current state of the spreadsheet prior to performing the redo operation.
* **Returns:** The next `SpreadsheetUndoState` popped from `redoStack`, or `null` if the operation cannot be completed.
* **Execution Logic:**
  1. If `currentState` is `null` or `redoStack` is empty (`Count == 0`), returns `null`.
  2. Pops the top state from `redoStack`.
  3. Pushes `currentState` onto the `undoStack`.
  4. Returns the popped state.

---

## 4. Operation Workflow Summary

```
               RecordChange(before, after)
             [Valid state & content changed]
                          │
                          ▼
            Push `before` to undoStack
                Clear redoStack
                          │
          ┌───────────────┴───────────────┐
          │                               │
    Undo(currentState)             Redo(currentState)
          │                               │
          ▼                               ▼
Pop state from undoStack        Pop state from redoStack
Push currentState to redoStack  Push currentState to undoStack
Return popped state             Return popped state
```