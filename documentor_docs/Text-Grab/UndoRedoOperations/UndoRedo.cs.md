# Documentation: `Text-Grab/UndoRedoOperations/UndoRedo.cs`

## Overview

The `UndoRedo` class manages undo and redo functionality for canvas and text editing operations within Text-Grab. It maintains double-ended operation stacks (`UndoStack` and `RedoStack`) composed of objects implementing `IUndoRedoOperation`. Operations are grouped into distinct **transactions** using a transaction identifier (`TransactionId`), allowing multiple granular actions to be undone or redone together as a single atomic unit.

The class also enforces memory management by capping the number of active transactions permitted in the undo history (`UndoRedoTransactionCapacity = 100`), trimming the oldest transactions when the threshold is exceeded.

---

## Class Details

* **Namespace:** `Text_Grab.UndoRedoOperations`
* **Access Modifier:** `internal`
* **Class Name:** `UndoRedo`

---

## Constants & Properties

### Constants

| Name | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `UndoRedoTransactionCapacity` | `int` | `100` | Defines the maximum number of active transactions retained in the undo stack before trimming off the oldest transactions. |

### Private Properties

| Name | Type | Description |
| :--- | :--- | :--- |
| `TransactionId` | `uint` | Tracks the current active transaction ID used when assigning new operations. |
| `HighestUsedTransactionId` | `uint` | Tracks the highest transaction ID assigned to an operation. |
| `ActiveTransactionIdCount` | `uint` | Tracks the count of unique transactions currently present in the undo stack. |
| `RedoStack` | `LinkedList<IUndoRedoOperation>` | Maintains operations available for redo, ordered chronologically. |
| `UndoStack` | `LinkedList<IUndoRedoOperation>` | Maintains operations available for undo, ordered chronologically. |

### Internal Properties

| Name | Type | Description |
| :--- | :--- | :--- |
| `UndoOperationCount` | `int` | Exposes `UndoStack.Count` internally for unit testing and verifying capacity trimming. |

---

## Key Methods & Operations

### Transaction Management

#### `StartTransaction()`
* **Signature:** `public void StartTransaction()`
* **Description:** A no-op placeholder method intended to improve code readability at call sites where a transaction sequence begins.

#### `EndTransaction()`
* **Signature:** `public void EndTransaction()`
* **Description:** Marks the end of a transaction sequence. Increments `TransactionId` if `TransactionId <= HighestUsedTransactionId`, ensuring subsequent operations receive a new transaction ID if operations were added during the active transaction.

#### `Reset()`
* **Signature:** `public void Reset()`
* **Description:** Clears both `UndoStack` and `RedoStack`, resetting `TransactionId`, `HighestUsedTransactionId`, and `ActiveTransactionIdCount` back to `0`.

---

### Insertion & Stack Maintenance

#### `InsertUndoRedoOperation(UndoRedoOperation operation, object operationArgs)`
* **Signature:** `public void InsertUndoRedoOperation(UndoRedoOperation operation, object operationArgs)`
* **Description:** Factory-like dispatcher method that accepts an `UndoRedoOperation` enum value and arguments cast to `GrabFrameOperationArgs`.
* **Behavior:**
  1. Casts `operationArgs` to `GrabFrameOperationArgs`.
  2. Dispatches to the appropriate helper method based on `operation`:
     * `UndoRedoOperation.AddWordBorder` $\rightarrow$ `InsertAddWordBorderOperation`
     * `UndoRedoOperation.ChangeWord` $\rightarrow$ `InsertChangeWordOperation`
     * `UndoRedoOperation.RemoveWordBorder` $\rightarrow$ `InsertRemoveWordBorderOperation`
     * `UndoRedoOperation.ResizeWordBorder` $\rightarrow$ `InsertResizeWordBorderOperation`
     * `UndoRedoOperation.ChangedImage` $\rightarrow$ `InsertChangedImageOperation`
     * `UndoRedoOperation.None` / `default` $\rightarrow$ No insertion.
  3. If `operation != UndoRedoOperation.None`, updates `HighestUsedTransactionId = TransactionId` and clears the redo stack via `ClearRedoStack()`.

#### Private Operation Dispatch Helpers
Each helper instantiates an operation object with `TransactionId` and appends it to the undo stack:
* `InsertChangeWordOperation(GrabFrameOperationArgs args)` $\rightarrow$ creates `ChangeWord`
* `InsertAddWordBorderOperation(GrabFrameOperationArgs args)` $\rightarrow$ creates `AddWordBorder`
* `InsertRemoveWordBorderOperation(GrabFrameOperationArgs args)` $\rightarrow$ creates `RemoveWordBorder`
* `InsertResizeWordBorderOperation(GrabFrameOperationArgs args)` $\rightarrow$ creates `ResizeWordBorder`
* `InsertChangedImageOperation(GrabFrameOperationArgs args)` $\rightarrow$ creates `ChangedImage`

#### `AddOperationToUndoStack(IUndoRedoOperation operation)`
* **Signature:** `internal void AddOperationToUndoStack(IUndoRedoOperation operation)`
* **Description:** Adds an operation to `UndoStack` and handles transaction counting and stack capacity trimming.
* **Logic:**
  1. Checks if `UndoStack` is empty or if the last operation's `TransactionId` differs from the incoming `operation.TransactionId`. If so, increments `ActiveTransactionIdCount`.
  2. Appends `operation` to the end of `UndoStack`.
  3. Checks if `ActiveTransactionIdCount > UndoRedoTransactionCapacity`. If exceeded, removes all nodes from the head (`First`) of `UndoStack` belonging to the oldest `TransactionId`, and decrements `ActiveTransactionIdCount`. Repeats until active transactions are within capacity.

#### `ClearRedoStack()`
* **Signature:** `private void ClearRedoStack()`
* **Description:** Empties `RedoStack` if it contains any elements.

---

### Undo & Redo Execution

#### `Undo()`
* **Signature:** `public void Undo()`
* **Description:** Reverses the most recent transaction's operations.
* **Execution Flow:**
  1. Checks if `UndoStack` is empty. If so, returns immediately.
  2. Identifies the `TransactionId` of the last operation (`UndoStack.Last`).
  3. Iterates backwards through `UndoStack`, continuing while nodes share the same `TransactionId`:
     * Calls `IUndoRedoOperation.Undo()` on the operation.
     * Moves the operation node to `RedoStack`.
     * Removes the operation node from `UndoStack`.
  4. Decrements `ActiveTransactionIdCount` if `ActiveTransactionIdCount > 0`.

#### `Redo()`
* **Signature:** `public void Redo()`
* **Description:** Re-applies the most recently undone transaction.
* **Execution Flow:**
  1. Checks if `RedoStack` is empty. If so, returns immediately.
  2. Identifies the `TransactionId` of the last operation (`RedoStack.Last`).
  3. Iterates backwards through `RedoStack`, continuing while nodes share the same `TransactionId`:
     * Calls `IUndoRedoOperation.Redo()` on the operation.
     * Moves the operation node back to `UndoStack`.
     * Removes the operation node from `RedoStack`.
  4. Increments `ActiveTransactionIdCount`.

---

### Status Queries

#### `HasUndoOperations()`
* **Signature:** `public bool HasUndoOperations()`
* **Return Value:** `bool`
* **Description:** Returns `true` if `UndoStack` contains one or more operations; otherwise `false`.

#### `HasRedoOperations()`
* **Signature:** `public bool HasRedoOperations()`
* **Return Value:** `bool`
* **Description:** Returns `true` if `RedoStack` contains one or more operations; otherwise `false`.

---

## Transaction Lifecycle Overview

```
[StartTransaction()] ──> (Perform operations via InsertUndoRedoOperation)
                                 │
                                 ▼
                         Add to UndoStack
                                 │
                                 ▼
                         Clear RedoStack
                                 │
                                 ▼
[EndTransaction()]   ──> Increment TransactionId (if operations occurred)
```