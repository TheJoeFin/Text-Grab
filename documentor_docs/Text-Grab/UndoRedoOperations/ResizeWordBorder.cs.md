# Technical Documentation: `ResizeWordBorder.cs`

## Overview

The `ResizeWordBorder` class is an internal component of the `Text_Grab.UndoRedoOperations` namespace. It encapsulates the state changes required to revert (undo) or re-apply (redo) a resizing and repositioning action performed on a `WordBorder` control.

It inherits from the base `Operation` class and implements the `IUndoRedoOperation` interface.

---

## Technical Details

* **File Path:** `Text-Grab/UndoRedoOperations/ResizeWordBorder.cs`
* **Namespace:** `Text_Grab.UndoRedoOperations`
* **Class Name:** `ResizeWordBorder`
* **Access Modifier:** `internal`
* **Base Class:** `Operation`
* **Interfaces Implemented:** `IUndoRedoOperation`

---

## Fields

| Field | Type | Access Modifier | Description |
| :--- | :--- | :--- | :--- |
| `WordBorder` | `WordBorder` | `private` | Reference to the target `WordBorder` control being resized/repositioned. |
| `OldSize` | `Rect` | `private` | The original dimensions and position (`Width`, `Height`, `Left`, `Top`) of the `WordBorder` before the resize operation. |
| `NewSize` | `Rect` | `private` | The new dimensions and position (`Width`, `Height`, `Left`, `Top`) of the `WordBorder` after the resize operation. |

---

## Constructor

```csharp
public ResizeWordBorder(uint transactionId, WordBorder wordBorder, Rect oldSize, Rect newSize) : base(transactionId)
```

### Parameters
* **`transactionId`** (`uint`): Passed to the base `Operation` constructor to uniquely identify or group the transaction.
* **`wordBorder`** (`WordBorder`): The target control to be manipulated during undo/redo operations.
* **`oldSize`** (`Rect`): The bounding rectangle representing the state prior to the resize operation.
* **`newSize`** (`Rect`): The bounding rectangle representing the state after the resize operation.

---

## Methods

### `GetUndoRedoOperation()`

```csharp
public UndoRedoOperation GetUndoRedoOperation()
```

* **Return Value:** `UndoRedoOperation.AddWordBorder`
* **Description:** Identifies the operation type by returning the corresponding `UndoRedoOperation` enum value (`UndoRedoOperation.AddWordBorder`).

---

### `Undo()`

```csharp
public void Undo()
```

* **Description:** Restores the `WordBorder` object to its previous state using the dimensions and coordinates stored in `OldSize`.
* **Execution Logic:**
  1. Sets `WordBorder.Width` to `OldSize.Width`.
  2. Sets `WordBorder.Height` to `OldSize.Height`.
  3. Sets `WordBorder.Left` to `OldSize.Left`.
  4. Sets `WordBorder.Top` to `OldSize.Top`.

---

### `Redo()`

```csharp
public void Redo()
```

* **Description:** Re-applies the new dimensions and coordinates stored in `NewSize` to the `WordBorder` object.
* **Execution Logic:**
  1. Sets `WordBorder.Width` to `NewSize.Width`.
  2. Sets `WordBorder.Height` to `NewSize.Height`.
  3. Sets `WordBorder.Left` to `NewSize.Left`.
  4. Sets `WordBorder.Top` to `NewSize.Top`.