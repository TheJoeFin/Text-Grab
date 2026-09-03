# Technical Documentation: `AddWordBorder.cs`

## Overview

The `AddWordBorder.cs` file defines an internal class `AddWordBorder` within the `Text_Grab.UndoRedoOperations` namespace. It represents an undoable and redoable operation for adding a `WordBorder` visual element to both a WPF `Canvas` control and a underlying collection of `WordBorder` objects.

The class inherits from `Operation` and implements the `IUndoRedoOperation` interface, participating in the application's undo/redo state management framework.

---

## File Information

* **File Path:** `Text-Grab/UndoRedoOperations/AddWordBorder.cs`
* **Namespace:** `Text_Grab.UndoRedoOperations`
* **Access Modifier:** `internal`

---

## Structure & Hierarchy

```
Operation (Base Class)
  └── AddWordBorder (Class)
        └── Implements: IUndoRedoOperation
```

---

## Class Members

### Fields

| Field | Type | Access / Modifiers | Description |
| :--- | :--- | :--- | :--- |
| `WordBorder` | `WordBorder` | `private readonly` | The `WordBorder` UI element instance being added or removed. |
| `Canvas` | `Canvas` | `private readonly` | The WPF `Canvas` UI container where the `WordBorder` is visually displayed. |
| `WordBorders` | `ICollection<WordBorder>` | `private readonly` | The collection that tracks the active `WordBorder` items. |

---

### Constructor

```csharp
public AddWordBorder(
    uint transactionId, 
    WordBorder wordBorder, 
    Canvas canvas, 
    ICollection<WordBorder> wordBorders) : base(transactionId)
```

#### Parameters
* **`transactionId`** (`uint`): A unique identifier for the transaction, passed down to the base `Operation` constructor.
* **`wordBorder`** (`WordBorder`): The `WordBorder` instance associated with this operation.
* **`canvas`** (`Canvas`): Target `Canvas` control containing the visual tree element.
* **`wordBorders`** (`ICollection<WordBorder>`): Target collection storing references to `WordBorder` instances.

---

### Methods

#### `GetUndoRedoOperation()`

```csharp
public UndoRedoOperation GetUndoRedoOperation()
```

* **Return Type:** `UndoRedoOperation`
* **Description:** Identifies the type of operation by returning the enum value `UndoRedoOperation.AddWordBorder`.

---

#### `Undo()`

```csharp
public void Undo()
```

* **Return Type:** `void`
* **Description:** Reverts the addition of the word border.
* **Execution Logic:**
  1. Removes `WordBorder` from `Canvas.Children`.
  2. Removes `WordBorder` from the `WordBorders` collection.

---

#### `Redo()`

```csharp
public void Redo()
```

* **Return Type:** `void`
* **Description:** Re-applies the addition of the word border.
* **Execution Logic:**
  1. Executes within a `try-catch` block catching `ArgumentException`.
  2. Adds `WordBorder` back to `Canvas.Children`.
  3. Adds `WordBorder` back to the `WordBorders` collection.
  4. Silently handles (swallows) any `ArgumentException` thrown during the re-addition process.