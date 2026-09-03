# Technical Documentation: `RemoveWordBorder.cs`

## Overview

The `RemoveWordBorder` class is an `internal` class located in the `Text_Grab.UndoRedoOperations` namespace. It represents an undo/redo operation specifically designed to manage the removal and restoration of `WordBorder` visual elements from a WPF `Canvas` control and an underlying `WordBorder` collection.

It inherits from the base class `Operation` and implements the `IUndoRedoOperation` interface.

---

## Class Declaration

```csharp
namespace Text_Grab.UndoRedoOperations;

internal class RemoveWordBorder : Operation, IUndoRedoOperation
```

---

## Private Fields

The class maintains state using three `private readonly` fields initialized upon instantiation:

| Field Name | Type | Description |
| :--- | :--- | :--- |
| `RemovingWordBorders` | `List<WordBorder>` | Stores the list of `WordBorder` objects that are subject to being removed or re-added. |
| `Canvas` | `Canvas` | The UI `Canvas` control where the `WordBorder` visual controls reside (`Canvas.Children`). |
| `WordBorders` | `ICollection<WordBorder>` | The collection tracking active `WordBorder` instances within the application logic. |

---

## Constructor

```csharp
public RemoveWordBorder(
    uint transactionId, 
    List<WordBorder> removingWordBorders,
    Canvas canvas, 
    ICollection<WordBorder> wordBorders) : base(transactionId)
```

### Parameters
* **`transactionId`** (`uint`): Passed to the `Operation` base class constructor to identify the transaction.
* **`removingWordBorders`** (`List<WordBorder>`): The target list of word borders being removed.
* **`canvas`** (`Canvas`): Reference to the WPF `Canvas` container.
* **`wordBorders`** (`ICollection<WordBorder>`): Reference to the collection tracking the word borders.

---

## Public Methods

### `GetUndoRedoOperation()`

```csharp
public UndoRedoOperation GetUndoRedoOperation()
```

* **Return Type**: `UndoRedoOperation`
* **Returns**: `UndoRedoOperation.AddWordBorder`
* **Purpose**: Identifies the inverse operation associated with this class. Since this class performs a removal, its operation type enum equivalent for undo purposes is `AddWordBorder`.

---

### `Undo()`

```csharp
public void Undo()
```

* **Purpose**: Reverses the removal operation by re-adding the specified `WordBorder` objects back to the `Canvas` and the `WordBorders` collection.
* **Logic**:
  1. Iterates through each `WordBorder` in `RemovingWordBorders`.
  2. Inside a `try-catch` block:
     * Adds the border to `Canvas.Children`.
     * Adds the border to the `WordBorders` collection.
  3. Catches and suppresses `ArgumentException` (which can occur if an element is already present in a visual child collection).

---

### `Redo()`

```csharp
public void Redo()
```

* **Purpose**: Re-executes the removal operation by removing the `WordBorder` objects from both the UI canvas and the tracking collection.
* **Logic**:
  1. Iterates through each `WordBorder` in `RemovingWordBorders`.
  2. Removes the border from `Canvas.Children`.
  3. Removes the border from the `WordBorders` tracking collection.

---

## Summary of Operation Flow

```
+-----------------------------------------------------------------------+
|                         RemoveWordBorder                              |
+-----------------------------------------------------------------------+
|  Fields:                                                              |
|   - TransactionId (via Operation base class)                          |
|   - RemovingWordBorders: List<WordBorder>                             |
|   - Canvas: Canvas                                                    |
|   - WordBorders: ICollection<WordBorder>                              |
+-----------------------------------------------------------------------+
                                  |
         +------------------------+------------------------+
         |                                                 |
         v                                                 v
   [ Undo() ]                                        [ Redo() ]
   Restores removed borders:                         Re-removes borders:
   1. Canvas.Children.Add(wordBorder)                1. Canvas.Children.Remove(wordBorder)
   2. WordBorders.Add(wordBorder)                    2. WordBorders.Remove(wordBorder)
   (Ignores ArgumentException)
```