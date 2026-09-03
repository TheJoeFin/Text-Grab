# Technical Documentation: `Tests/UndoRedoTests.cs`

## Overview

The `UndoRedoTests.cs` file contains unit tests for verifying the core behaviors of the `UndoRedo` stack implementation in the `Text_Grab.UndoRedoOperations` namespace. It uses the xUnit test framework to validate operation capacity limits, trimming behavior, transaction grouping, execution of undo operations, and state resetting.

---

## File Structure & Dependencies

- **Namespace:** `Tests`
- **Imports:** `Text_Grab.UndoRedoOperations`
- **Test Framework:** xUnit (`[Fact]`, `Assert`)

---

## Component Details

### 1. Nested Helper Class: `FakeOperation`

`FakeOperation` is a private, sealed test double implementing `IUndoRedoOperation`. It simulates individual undo/redo operations and tracks execution counts for testing purposes.

```csharp
private sealed class FakeOperation(uint transactionId) : IUndoRedoOperation
```

#### Properties
- **`TransactionId`** (`uint`): Gets the transaction identifier assigned upon initialization.
- **`UndoCount`** (`int`, private set): Tracks how many times the `Undo()` method has been invoked.
- **`RedoCount`** (`int`, private set): Tracks how many times the `Redo()` method has been invoked.

#### Methods
- **`GetUndoRedoOperation()`**: Returns `UndoRedoOperation.None`.
- **`Undo()`**: Increments `UndoCount` by 1.
- **`Redo()`**: Increments `RedoCount` by 1.

---

### 2. Test Class: `UndoRedoTests`

`UndoRedoTests` contains unit test methods marked with the `[Fact]` attribute.

#### Test Methods Summary

| Test Method | Objective |
| :--- | :--- |
| `UndoStack_TrimsOldestTransactions_WhenOverCapacity` | Verifies that the undo stack trims older transactions when pushed beyond `UndoRedo.UndoRedoTransactionCapacity`. |
| `UndoStack_KeepsAllOperations_WhenUnderCapacity` | Ensures no operations are discarded when the total count is within capacity limits. |
| `Undo_RunsAllOperationsOfNewestTransaction` | Confirms that invoking `Undo()` executes all operations associated with the most recent `TransactionId` while preserving earlier transactions. |
| `Reset_ClearsAllOperations` | Ensures `Reset()` purges both undo and redo stacks completely. |

---

## Detailed Test Scenarios & Logic

### 1. `UndoStack_TrimsOldestTransactions_WhenOverCapacity`

* **Goal:** Verify capacity limiting and automatic trimming of old transactions.
* **Execution Flow:**
  1. Instantiates a new `UndoRedo` object.
  2. Calculates `transactionCount` as `UndoRedo.UndoRedoTransactionCapacity + 50`.
  3. Iterates from `0` to `transactionCount - 1`, adding two `FakeOperation` instances per transaction ID to the undo stack using `AddOperationToUndoStack()`.
* **Assertion:**
  - Verifies that `undoRedo.UndoOperationCount` equals `UndoRedo.UndoRedoTransactionCapacity * 2`.

---

### 2. `UndoStack_KeepsAllOperations_WhenUnderCapacity`

* **Goal:** Verify that operations under capacity limits are entirely retained.
* **Execution Flow:**
  1. Instantiates a new `UndoRedo` object.
  2. Adds 10 `FakeOperation` instances (each with transaction IDs `0` through `9`) to the undo stack.
* **Assertion:**
  - Verifies that `undoRedo.UndoOperationCount` is equal to `10`.

---

### 3. `Undo_RunsAllOperationsOfNewestTransaction`

* **Goal:** Test grouped transaction execution during an undo operation and verify stack status.
* **Execution Flow:**
  1. Instantiates a new `UndoRedo` object.
  2. Creates three `FakeOperation` instances:
     - `olderOperation` (Transaction ID: `1`)
     - `newerOperation1` (Transaction ID: `2`)
     - `newerOperation2` (Transaction ID: `2`)
  3. Adds all three operations to the undo stack in order.
  4. Calls `undoRedo.Undo()`.
* **Assertions:**
  - `olderOperation.UndoCount` is `0` (was not undone).
  - `newerOperation1.UndoCount` is `1` (undone as part of transaction 2).
  - `newerOperation2.UndoCount` is `1` (undone as part of transaction 2).
  - `undoRedo.UndoOperationCount` is `1` (only `olderOperation` remains on the undo stack).
  - `undoRedo.HasRedoOperations()` returns `true` (undone items moved to redo stack).

---

### 4. `Reset_ClearsAllOperations`

* **Goal:** Verify complete clearing of both undo and redo history via `Reset()`.
* **Execution Flow:**
  1. Instantiates a new `UndoRedo` object.
  2. Pushes an operation (Transaction ID: `1`) to the undo stack.
  3. Calls `Undo()` to populate the redo stack.
  4. Pushes another operation (Transaction ID: `2`) to the undo stack.
  5. Calls `undoRedo.Reset()`.
* **Assertions:**
  - `undoRedo.HasUndoOperations()` returns `false`.
  - `undoRedo.HasRedoOperations()` returns `false`.

---

## Interacted `UndoRedo` System API Surface

Based strictly on the calls in this test file, the underlying `UndoRedo` class provides the following properties and methods:

- **Properties / Constants:**
  - `UndoRedo.UndoRedoTransactionCapacity` (`int` static constant or property)
  - `UndoOperationCount` (`int` property)
- **Methods:**
  - `AddOperationToUndoStack(IUndoRedoOperation operation)`
  - `Undo()`
  - `Reset()`
  - `HasUndoOperations()` (`bool`)
  - `HasRedoOperations()` (`bool`)