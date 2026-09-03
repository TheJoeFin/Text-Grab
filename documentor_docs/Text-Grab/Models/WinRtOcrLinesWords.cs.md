# Technical Documentation: `WinRtOcrLinesWords.cs`

**File Path:** `Text-Grab/Models/WinRtOcrLinesWords.cs`  
**Namespace:** `Text_Grab.Models`  

---

## 1. Overview

The `WinRtOcrLinesWords.cs` file provides concrete wrapper implementations for the Windows Runtime Optical Character Recognition API (`Windows.Media.Ocr`). It adapts WinRT OCR data models (`OcrResult`, `OcrLine`, and `OcrWord`) into generalized application interfaces (`IOcrLinesWords`, `IOcrLine`, and `IOcrWord`).

This file contains three public classes:
1. `WinRtOcrLinesWords` — Wraps an `OcrResult` object containing overall text, text angle, and an array of lines.
2. `WinRtOcrLine` — Wraps an `OcrLine` object containing line text, a bounding box, and an array of words.
3. `WinRtOcrWord` — Wraps an `OcrWord` object containing word text and a bounding box.

---

## 2. Dependencies

* **`Text_Grab.Utilities`**: Provides utility/extension methods (specifically `GetBoundingRect()` used on `OcrLine`).
* **`Windows.Foundation`**: Provides `Rect` for bounding box coordinates.
* **`Windows.Media.Ocr`**: Provides underlying WinRT OCR classes (`OcrResult`, `OcrLine`, `OcrWord`).

---

## 3. Class Specifications

### 3.1. Class `WinRtOcrLinesWords`

Implements the `IOcrLinesWords` interface. Serves as the top-level container for an entire OCR result set.

#### Properties
| Property | Type | Description |
| :--- | :--- | :--- |
| `OriginalOcrResult` | `OcrResult` | The underlying native WinRT `OcrResult` object. |
| `Text` | `string` | The full extracted text content from the OCR result. |
| `Angle` | `float` | The detected rotation angle of the text (in degrees). Defaults to `0.0f` if `TextAngle` is `null`. |
| `Lines` | `IOcrLine[]` | An array of wrapped lines implementing `IOcrLine`. |

#### Constructor
```csharp
public WinRtOcrLinesWords(OcrResult ocrResult)
```
**Behavior:**
1. Assigns `ocrResult` to `OriginalOcrResult`.
2. Evaluates `ocrResult.TextAngle`. If non-null, converts to `float`; otherwise sets `Angle` to `0.0f`.
3. Allocates the `Lines` array with a length matching `ocrResult.Lines.Count`.
4. Iterates through `ocrResult.Lines`, instantiating a new `WinRtOcrLine` for each entry and storing it in the `Lines` array.
5. Assigns `ocrResult.Text` to `Text`.

---

### 3.2. Class `WinRtOcrLine`

Implements the `IOcrLine` interface. Represents a single line of text extracted by the OCR engine.

#### Properties
| Property | Type | Description |
| :--- | :--- | :--- |
| `OriginalLine` | `OcrLine` | The underlying native WinRT `OcrLine` object. |
| `Text` | `string` | The text content of the line. |
| `Words` | `IOcrWord[]` | An array of individual word wrappers (`WinRtOcrWord`) implementing `IOcrWord`. |
| `BoundingBox` | `Windows.Foundation.Rect` | The bounding rectangle for the entire line of text. |

#### Constructor
```csharp
public WinRtOcrLine(OcrLine ocrLine)
```
**Behavior:**
1. Assigns `ocrLine` to `OriginalLine`.
2. Assigns `ocrLine.Text` to `Text`.
3. Allocates the `Words` array with a length matching `ocrLine.Words.Count`.
4. Iterates through `ocrLine.Words`, instantiating a new `WinRtOcrWord` for each entry and storing it in the `Words` array.
5. Calls `ocrLine.GetBoundingRect()` (returning a `System.Windows.Rect`) and converts it to a `Windows.Foundation.Rect` assigned to `BoundingBox` using `bRect.Left`, `bRect.Top`, `bRect.Width`, and `bRect.Height`.

---

### 3.3. Class `WinRtOcrWord`

Implements the `IOcrWord` interface. Represents an individual word within an OCR line.

#### Properties
| Property | Type | Description |
| :--- | :--- | :--- |
| `OriginalWord` | `OcrWord` | The underlying native WinRT `OcrWord` object. |
| `Text` | `string` | The text content of the individual word. |
| `BoundingBox` | `Windows.Foundation.Rect` | The bounding rectangle for the word (`ocrWord.BoundingRect`). |

#### Constructor
```csharp
public WinRtOcrWord(OcrWord ocrWord)
```
**Behavior:**
1. Assigns `ocrWord` to `OriginalWord`.
2. Assigns `ocrWord.Text` to `Text`.
3. Assigns `ocrWord.BoundingRect` directly to `BoundingBox`.

---

## 4. Object Hierarchy & Data Flow

When a `WinRtOcrLinesWords` instance is constructed from an `OcrResult`:

```
OcrResult (WinRT)
 └── WinRtOcrLinesWords (IOcrLinesWords)
      ├── Text
      ├── Angle
      └── Lines: WinRtOcrLine[] (IOcrLine[])
           └── [Index i] WinRtOcrLine
                ├── Text
                ├── BoundingBox
                └── Words: WinRtOcrWord[] (IOcrWord[])
                     └── [Index j] WinRtOcrWord
                          ├── Text
                          └── BoundingBox
```