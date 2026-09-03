# Technical Documentation: `Text-Grab/Models/OcrLinesWords.cs`

## Overview

The `OcrLinesWords.cs` file defines three public interfaces within the `Text_Grab.Models` namespace. These interfaces establish a standardized, hierarchical contract for representing Optical Character Recognition (OCR) results at three distinct levels of granularity: overall result, individual line, and individual word.

- **File Path:** `Text-Grab/Models/OcrLinesWords.cs`
- **Namespace:** `Text_Grab.Models`
- **External Dependencies:** `Windows.Foundation` (for the `Rect` structure)

---

## Purpose

The primary purpose of this file is to provide abstract interface definitions for structured OCR output. By separating the definition of OCR structural elements (`IOcrLinesWords`, `IOcrLine`, and `IOcrWord`) from implementation logic, the codebase allows components to process and handle recognized text, line layouts, bounding boxes, and orientation angles agnostically.

---

## Data Hierarchy

The interfaces defined in this file model a three-tier parent-child hierarchy:

```text
IOcrLinesWords (Top-Level Container)
 └── IOcrLine[] Lines (Array of Lines)
      └── IOcrWord[] Words (Array of Words)
```

1. **`IOcrLinesWords`**: Represents the root object containing overall text, orientation angle, and an array of lines.
2. **`IOcrLine`**: Represents a single line of text containing line text, positional bounding box, and an array of words.
3. **`IOcrWord`**: Represents an individual word containing the text segment and its specific positional bounding box.

---

## Key Components & Interface Specifications

### 1. `IOcrLinesWords`

The top-level interface representing a complete OCR extraction result.

```csharp
public interface IOcrLinesWords
{
    string Text { get; set; }
    IOcrLine[] Lines { get; set; }
    float Angle { get; set; }
}
```

#### Properties

| Property | Type | Accessors | Description |
| :--- | :--- | :--- | :--- |
| `Text` | `string` | `get; set;` | Gets or sets the overall extracted text string from the OCR result. |
| `Lines` | `IOcrLine[]` | `get; set;` | Gets or sets an array of objects implementing `IOcrLine`, representing the individual lines of text. |
| `Angle` | `float` | `get; set;` | Gets or sets the rotation angle (in degrees) associated with the recognized text block. |

---

### 2. `IOcrLine`

Represents a single line of recognized text within the broader OCR result.

```csharp
public interface IOcrLine
{
    string Text { get; set; }
    IOcrWord[] Words { get; set; }
    Rect BoundingBox { get; set; }
}
```

#### Properties

| Property | Type | Accessors | Description |
| :--- | :--- | :--- | :--- |
| `Text` | `string` | `get; set;` | Gets or sets the text string for this specific line. |
| `Words` | `IOcrWord[]` | `get; set;` | Gets or sets an array of objects implementing `IOcrWord`, representing the constituent words within the line. |
| `BoundingBox` | `Windows.Foundation.Rect` | `get; set;` | Gets or sets the rectangular bounding box defining the spatial location and dimensions of the line. |

---

### 3. `IOcrWord`

Represents the finest level of granularity: an individual recognized word.

```csharp
public interface IOcrWord
{
    string Text { get; set; }
    Rect BoundingBox { get; set; }
}
```

#### Properties

| Property | Type | Accessors | Description |
| :--- | :--- | :--- | :--- |
| `Text` | `string` | `get; set;` | Gets or sets the text string of the individual word. |
| `BoundingBox` | `Windows.Foundation.Rect` | `get; set;` | Gets or sets the rectangular bounding box defining the spatial location and dimensions of the word. |

---

## Dependencies

- **`Windows.Foundation.Rect`**: Used by both `IOcrLine` and `IOcrWord` to define bounding box coordinates (position and dimensions) for recognized text elements.