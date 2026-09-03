# Technical Documentation: `ChangedImage.cs`

**File Path:** `Text-Grab/UndoRedoOperations/ChangedImage.cs`  
**Namespace:** `Text_Grab.UndoRedoOperations`

---

## Overview

The `ChangedImage` class encapsulates an undo/redo operation for image modification events within the `Text-Grab` application. It inherits from the base `Operation` class and implements the `IUndoRedoOperation` interface.

When an image changes, this class tracks the state of the target WPF `Image` control, the old and new `ImageSource` values, and the associated UI elements (`WordBorder` items) residing on a `Canvas` overlay. It provides mechanisms to revert (`Undo`) or re-apply (`Redo`) image changes and their corresponding word border overlays.

---

## Class Definition

```csharp
internal class ChangedImage : Operation, IUndoRedoOperation
```

* **Access Modifier:** `internal`
* **Base Class:** `Operation`
* **Implemented Interfaces:** `IUndoRedoOperation`

---

## Private Fields

| Field Name | Type | Description |
| :--- | :--- | :--- |
| `OldImage` | `ImageSource?` | The `ImageSource` assigned to the destination image prior to the change operation. Can be `null`. |
| `NewImage` | `ImageSource?` | The `ImageSource` assigned to the destination image after the change operation. Can be `null`. |
| `DestinationImage` | `Image` | The WPF `Image` control being updated. |
| `RectanglesCanvas` | `Canvas` | The WPF `Canvas` container that displays `WordBorder` controls over the image. |
| `PreviousWordBorders` | `List<WordBorder>` | A list of `WordBorder` objects present before the image was changed. Used during undo operations. |
| `WordBorders` | `ICollection<WordBorder>` | A reference to the active collection tracking currently rendered `WordBorder` elements. |

---

## Constructor

```csharp
public ChangedImage(
    uint transactionId, 
    Image destination, 
    List<WordBorder> previousWordBorders,
    Canvas canvas, 
    ICollection<WordBorder> wordBorders, 
    ImageSource? oldImage, 
    ImageSource? newImage
) : base(transactionId)
```

### Parameters
* **`transactionId` (`uint`):** Identifier passed to the base `Operation` constructor to group or track transactions.
* **`destination` (`Image`):** The WPF `Image` instance affected by the change.
* **`previousWordBorders` (`List<WordBorder>`):** A list of word borders that existed prior to the change.
* **`canvas` (`Canvas`):** The `Canvas` control where `WordBorder` elements are drawn.
* **`wordBorders` (`ICollection<WordBorder>`):** The active collection of `WordBorder` controls.
* **`oldImage` (`ImageSource?`):** The image source before the modification.
* **`newImage` (`ImageSource?`):** The image source after the modification.

---

## Public Methods

### `GetUndoRedoOperation()`

```csharp
public UndoRedoOperation GetUndoRedoOperation()
```

* **Return Type:** `UndoRedoOperation`
* **Description:** Identifies the operation type. Returns `UndoRedoOperation.ChangedImage`.

---

### `Undo()`

```csharp
public void Undo()
```

* **Description:** Reverts the `DestinationImage` back to `OldImage` and restores the previous `WordBorder` overlays.
* **Execution Logic:**
  1. Sets `DestinationImage.Source` to `OldImage`.
  2. Iterates through each `WordBorder` in `PreviousWordBorders`:
     * Adds the `WordBorder` back into `RectanglesCanvas.Children`.
     * Adds the `WordBorder` back into the `WordBorders` collection.
     * Silently catches and ignores `ArgumentException` (for example, if an element is already present in a visual tree).

---

### `Redo()`

```csharp
public void Redo()
```

* **Description:** Re-applies the `NewImage` to the `DestinationImage` and clears associated word border overlays.
* **Execution Logic:**
  1. Sets `DestinationImage.Source` to `NewImage`.
  2. Clears all child elements from `RectanglesCanvas.Children`.
  3. Clears all items from the `WordBorders` collection.