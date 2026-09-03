# Technical Documentation: `Text-Grab/Models/GeneratedOcrLinesWords.cs`

## Overview

The `GeneratedOcrLinesWords.cs` file defines a set of model classes within the `Text_Grab.Models` namespace designed to represent synthesized or custom-generated Optical Character Recognition (OCR) results. 

Instead of relying directly on engine-native OCR outputs, this file provides concrete implementations of the `IOcrLinesWords`, `IOcrLine`, and `IOcrWord` interfaces. It allows text content and bounding box coordinates to be structured programmatically into a hierarchical OCR representation (Result Container $\rightarrow$ Lines $\rightarrow$ Words).

---

## Architecture & Hierarchy

The file contains three related public classes structured in a parent-child hierarchy:

```
GeneratedOcrLinesWords (IOcrLinesWords)
 └── IOcrLine[] Lines (Contains instances of GeneratedOcrLine)
      └── IOcrWord[] Words (Contains instances of GeneratedOcrWord)
```

1. **`GeneratedOcrLinesWords`**: Represents the top-level container holding overall text content, rotation angle, and an array of OCR lines.
2. **`GeneratedOcrLine`**: Represents a single line of text within the OCR result, holding line-level text, a bounding rectangle, and an array of word components.
3. **`GeneratedOcrWord`**: Represents an individual word item with text and a bounding rectangle.

---

## Detailed Class Reference

### 1. `GeneratedOcrLinesWords`

Implements: `IOcrLinesWords`

#### Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Text` | `string` | `string.Empty` | The full text string contained across all lines. |
| `Lines` | `IOcrLine[]` | `[]` | An array of lines (`IOcrLine`) comprising the OCR output. |
| `Angle` | `float` | `0` | The rotation angle of the text block in degrees. |

#### Factory Methods

##### `FromParagraph(string text, Rect boundingBox)`
Generates a `GeneratedOcrLinesWords` instance from a input block of text and a bounding box (`Windows.Foundation.Rect`).

* **Parameters:**
  * `text` (`string`): The text content to convert into an OCR structure.
  * `boundingBox` (`Rect`): The bounding box coordinates associated with the paragraph.
* **Returns:** A populated `GeneratedOcrLinesWords` object.
* **Logic:**
  1. Trims leading and trailing whitespace from `text`. If `text` is `null`, it defaults to `string.Empty`.
  2. Sets `Angle` to `0`.
  3. If the normalized text is empty or whitespace (`string.IsNullOrWhiteSpace`), sets `Lines` to an empty array (`[]`).
  4. If valid text is present, populates `Lines` with a single `GeneratedOcrLine` generated via `GeneratedOcrLine.FromText(normalizedText, boundingBox)`.

---

### 2. `GeneratedOcrLine`

Implements: `IOcrLine`

#### Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Text` | `string` | `string.Empty` | The text content of the specific line. |
| `Words` | `IOcrWord[]` | `[]` | An array of words (`IOcrWord`) within this line. |
| `BoundingBox` | `Rect` | Default `Rect` | The rectangular region enclosing the line. |

#### Factory Methods

##### `FromText(string text, Rect boundingBox)`
Creates a `GeneratedOcrLine` instance containing a single `GeneratedOcrWord`.

* **Parameters:**
  * `text` (`string`): The line text string.
  * `boundingBox` (`Rect`): The bounding rectangle for the line.
* **Returns:** A populated `GeneratedOcrLine` object.
* **Logic:**
  1. Sets `Text` to the provided `text`.
  2. Sets `BoundingBox` to the provided `boundingBox`.
  3. Populates `Words` with a single-element array containing a `GeneratedOcrWord` initialized with the same `text` and `boundingBox`.

---

### 3. `GeneratedOcrWord`

Implements: `IOcrWord`

#### Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Text` | `string` | `string.Empty` | The text string of the word. |
| `BoundingBox` | `Rect` | Default `Rect` | The rectangular region enclosing the word. |

---

## Dependencies

* **`Windows.Foundation`**: Provides the `Rect` struct used for defining `BoundingBox` dimensions and coordinates.

---

## Key Execution Flow Example

When `GeneratedOcrLinesWords.FromParagraph("Hello World", rect)` is called:

1. `FromParagraph` trims `"Hello World"` and creates a `GeneratedOcrLinesWords` object.
2. It invokes `GeneratedOcrLine.FromText("Hello World", rect)`.
3. `FromText` creates a `GeneratedOcrLine` object and nests a `GeneratedOcrWord` with `Text = "Hello World"` and `BoundingBox = rect`.
4. The resulting data object hierarchy is completely populated:
   * **`GeneratedOcrLinesWords.Text`** = `"Hello World"`
   * **`GeneratedOcrLinesWords.Lines[0].Text`** = `"Hello World"`
   * **`GeneratedOcrLinesWords.Lines[0].Words[0].Text`** = `"Hello World"`