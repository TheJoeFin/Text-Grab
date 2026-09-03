# Technical Documentation: `Text-Grab/UndoRedoOperations/Operation.cs`

## Overview

The `Operation.cs` file defines the foundational abstractions, interfaces, enums, and argument structures used to support undo and redo functionality within the Text-Grab application. 

It establishes:
* A base abstract class (`Operation`) for tracking transaction identifiers.
* An interface (`IUndoRedoOperation`) defining contracts for undoable and redoable actions.
* An enumeration (`UndoRedoOperation`) categorizing supported action types.
* A argument structure (`GrabFrameOperationArgs`) used to pass contextual state between frame operations.

---

## Namespace & Imports

**Namespace:** `Text_Grab.UndoRedoOperations`

**Dependencies:**
* `System.Collections.Generic`: Provides collection types (`ICollection<T>`, `List<T>`).
* `System.Windows`: WPF core primitives (`Rect`).
* `System.Windows.Controls`: WPF UI controls (`Canvas`, `Image`).
* `System.Windows.Media`: WPF media primitives (`ImageSource`).
* `Text_Grab.Controls`: Application-specific UI components (`WordBorder`).

---

## Key Components

### 1. `Operation` (Abstract Class)

An `internal abstract` base class representing a generic undo/redo operation tied to a specific transaction ID.

#### Properties
| Property | Type | Access | Description |
| :--- | :--- | :--- | :--- |
| `TransactionId` | `uint` | `public get;` | A unique identifier for grouping or identifying the transaction. |

#### Constructors
* `protected Operation(uint transationId)`: Initializes a new instance of the class and sets the `TransactionId`.

---

### 2. `UndoRedoOperation` (Enum)

A `public` enumeration that lists the distinct types of undo/redo operations supported.

#### Enum Values
* `None`: Represents no operation or an uninitialized state.
* `ChangedImage`: Indicates an image change action.
* `AddWordBorder`: Indicates the addition of a word border.
* `ChangeWord`: Indicates a text modification within a word border.
* `RemoveWordBorder`: Indicates the removal of a word border.
* `ResizeWordBorder`: Indicates a resize action applied to a word border.

---

### 3. `IUndoRedoOperation` (Interface)

A `public` interface defining the required members for any class implementing executable undo and redo logic.

#### Properties
| Property | Type | Description |
| :--- | :--- | :--- |
| `TransactionId` | `uint` | Gets the transaction ID associated with the operation instance. |

#### Methods
* `void Undo()`: Reverts the effects of the operation.
* `void Redo()`: Re-applies the effects of the operation.
* `UndoRedoOperation GetUndoRedoOperation()`: Returns the specific `UndoRedoOperation` enum value representing the operation instance type.

---

### 4. `GrabFrameOperationArgs` (Struct)

A `public` value type used to bundle state parameters required to perform, undo, or redo operations on a grab frame UI context.

#### Properties
| Property | Type | Description |
| :--- | :--- | :--- |
| `GrabFrameCanvas` | `Canvas` | The WPF `Canvas` container where word borders are drawn/managed. |
| `WordBorders` | `ICollection<WordBorder>` | A collection of active `WordBorder` elements. |
| `WordBorder` | `WordBorder` | A specific target `WordBorder` involved in the operation. |
| `RemovingWordBorders` | `List<WordBorder>` | A list of `WordBorder` elements scheduled for or subjected to removal. |
| `OldSize` | `Rect` | The original bounding rectangle before a resize or move. |
| `NewSize` | `Rect` | The new bounding rectangle after a resize or move. |
| `OldWord` | `string` | The text content before a edit. |
| `NewWord` | `string` | The text content after an edit. |
| `DestinationImage` | `Image` | Target WPF `Image` control associated with the frame operation. |
| `OldImage` | `ImageSource?` | The original image source prior to an image modification. |
| `NewImage` | `ImageSource?` | The updated image source following an image modification. |

---

## How It Works

1. **Transaction Identification:** The `Operation` abstract class and `IUndoRedoOperation` interface require every operation to carry a `TransactionId` of type `uint`. This allows multiple atomic sub-actions to be grouped under a single transaction if needed.
2. **Standardized Execution:** Implementations of `IUndoRedoOperation` encapsulate the exact state transitions inside `Undo()` and `Redo()` methods. The operation type can be queried at runtime using `GetUndoRedoOperation()`.
3. **Data Transport:** `GrabFrameOperationArgs` acts as a strongly typed parameter bag. When an operation occurs (e.g., changing an image, editing text, or resizing/adding/removing a `WordBorder`), instances of `GrabFrameOperationArgs` hold snapshot values (`OldSize`/`NewSize`, `OldWord`/`NewWord`, `OldImage`/`NewImage`) along with references to affected controls (`GrabFrameCanvas`, `DestinationImage`, `WordBorder`).