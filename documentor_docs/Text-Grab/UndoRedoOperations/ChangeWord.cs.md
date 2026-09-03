# Technical Documentation: `Text-Grab/UndoRedoOperations/ChangeWord.cs`

## Overview

The `ChangeWord` class is part of the `Text_Grab.UndoRedoOperations` namespace. Its primary purpose is to represent an undo/redo operation for updating the string value (`Word`) of a `WordBorder` control. It stores the state prior to and following a word edit, allowing the system to revert or re-apply text edits.

---

## Class Signature

```csharp
namespace Text_Grab.UndoRedoOperations;

internal class ChangeWord : Operation, IUndoRedoOperation
```

* **Access Modifier:** `internal`
* **Base Class:** `Operation`
* **Implemented Interface:** `IUndoRedoOperation`

---

## Key Components

### Private Fields

| Field Name | Type | Description |
| :--- | :--- | :--- |
| `WordBorder` | `WordBorder` | A reference to the target `WordBorder` instance whose text is being changed. |
| `OldWord` | `string` | The text contained in `WordBorder.Word` prior to the change. |
| `NewWord` | `string` | The new text applied to `WordBorder.Word`. |

---

### Constructor

```csharp
public ChangeWord(uint transactionId, WordBorder wordBorder, string oldWord, string newWord) 
    : base(transactionId)
```

#### Parameters:
* **`transactionId`** (`uint`): The identifier for the transaction, passed directly to the base `Operation` constructor.
* **`wordBorder`** (`WordBorder`): The target UI control instance.
* **`oldWord`** (`string`): The original text string before modification.
* **`newWord`** (`string`): The updated text string after modification.

#### Logic:
Initializes the base `Operation` with `transactionId` and assigns `wordBorder`, `oldWord`, and `newWord` to their corresponding internal instance fields.

---

### Methods

#### 1. `GetUndoRedoOperation()`

```csharp
public UndoRedoOperation GetUndoRedoOperation()
```

* **Return Value:** `UndoRedoOperation.AddWordBorder`
* **Description:** Identifies the type of operation associated with this class within the undo/redo framework.

#### 2. `Undo()`

```csharp
public void Undo()
```

* **Return Value:** `void`
* **Description:** Reverts the change by reassigning `WordBorder.Word` back to `OldWord`.

#### 3. `Redo()`

```csharp
public void Redo()
```

* **Return Value:** `void`
* **Description:** Re-applies the change by setting `WordBorder.Word` to `NewWord`.

---

## Execution Flow

1. **Instantiation:** When a word within a `WordBorder` is modified, a `ChangeWord` instance is constructed with the `transactionId`, a reference to the `WordBorder` object, the previous text string (`oldWord`), and the new text string (`newWord`).
2. **Undo Action:** Calling `Undo()` sets the target `WordBorder.Word` property back to the value stored in `OldWord`.
3. **Redo Action:** Calling `Redo()` sets the target `WordBorder.Word` property to the value stored in `NewWord`.