# Technical Documentation: `Text-Grab/Models/WinAiOcrLinesWords.cs`

## Overview

The `WinAiOcrLinesWords.cs` file defines wrapper models that adapt Microsoft Windows AI Imaging OCR results into generic interfaces used within the Text-Grab application (`IOcrLinesWords`, `IOcrLine`, and `IOcrWord`). 

These classes encapsulate native Windows AI OCR data structures (`RecognizedText`, `RecognizedLine`, and `RecognizedWord`) and expose their properties in a standardized format, including text contents, bounding boxes, text angles, and line/word hierarchies.

---

## File Details

* **File Path:** `Text-Grab/Models/WinAiOcrLinesWords.cs`
* **Namespace:** `Text_Grab.Models`
* **Dependencies:**
  * `Microsoft.Windows.AI.Imaging`
  * `System`
  * `System.Text`
  * `Windows.Foundation`

---

## Classes Summary

| Class Name | Implemented Interface | Primary Purpose |
| :--- | :--- | :--- |
| `WinAiOcrLinesWords` | `IOcrLinesWords` | Wraps a `RecognizedText` object representing the overall OCR output. |
| `WinAiOcrLine` | `IOcrLine` | Wraps a `RecognizedLine` object representing a single line of recognized text. |
| `WinAiOcrWord` | `IOcrWord` | Wraps a `RecognizedWord` object representing an individual word within a line. |

---

## Class Breakdown

### 1. `WinAiOcrLinesWords`

Implements the `IOcrLinesWords` interface. It serves as the top-level container for OCR results produced by `Microsoft.Windows.AI.Imaging`.

#### Properties

* `OriginalRecognizedText` (`RecognizedText`): Stores the raw native `RecognizedText` instance passed during initialization.
* `Text` (`string`): Contains the concatenated, trimmed plain text extracted from all lines.
* `Angle` (`float`): Represents the angle of the recognized text (`TextAngle`).
* `Lines` (`IOcrLine[]`): An array of `IOcrLine` objects representing individual lines of text.

#### Constructor

```csharp
public WinAiOcrLinesWords(RecognizedText recognizedText)
```

**Initialization Logic:**
1. Sets `OriginalRecognizedText` to the provided `recognizedText`.
2. Assigns `Angle` from `recognizedText.TextAngle`.
3. Checks if `recognizedText.Lines` is not `null`:
   * Converts each `RecognizedLine` into a `WinAiOcrLine` instance and assigns the resulting array to `Lines`.
   * Iterates through each `RecognizedLine` and appends its text followed by a line break to a `StringBuilder`.
4. If `recognizedText.Lines` is `null`, initializes `Lines` as an empty array (`[]`).
5. Converts the `StringBuilder` content to a string, trims trailing/leading whitespace, and sets `Text`.

---

### 2. `WinAiOcrLine`

Implements the `IOcrLine` interface. Represents a single line of recognized text contained within a `WinAiOcrLinesWords` object.

#### Properties

* `OriginalLine` (`RecognizedLine`): Stores the raw native `RecognizedLine` instance.
* `Text` (`string`): The text string contained in the line.
* `Words` (`IOcrWord[]`): An array of `IOcrWord` objects corresponding to words within this line.
* `BoundingBox` (`Rect`): A `Windows.Foundation.Rect` defining the geographic boundaries of the line in the source image.

#### Constructor

```csharp
public WinAiOcrLine(RecognizedLine recognizedLine)
```

**Initialization Logic:**
1. Sets `OriginalLine` to `recognizedLine`.
2. Sets `Text` to `recognizedLine.Text`.
3. Converts each `RecognizedWord` in `recognizedLine.Words` into a `WinAiOcrWord` instance using `Array.ConvertAll` and assigns it to `Words`.
4. Constructs a `Rect` for `BoundingBox` using `recognizedLine.BoundingBox.TopLeft` and `recognizedLine.BoundingBox.BottomRight`.

---

### 3. `WinAiOcrWord`

Implements the `IOcrWord` interface. Represents an individual word within a line (`WinAiOcrLine`).

#### Properties

* `OriginalWord` (`RecognizedWord`): Stores the raw native `RecognizedWord` instance.
* `Text` (`string`): The text string of the word.
* `BoundingBox` (`Rect`): A `Windows.Foundation.Rect` defining the location boundaries of the word in the source image.

#### Constructor

```csharp
public WinAiOcrWord(RecognizedWord recognizedWord)
```

**Initialization Logic:**
1. Sets `OriginalWord` to `recognizedWord`.
2. Sets `Text` to `recognizedWord.Text`.
3. Constructs a `Rect` for `BoundingBox` using `recognizedWord.BoundingBox.TopLeft` and `recognizedWord.BoundingBox.BottomRight`.

---

## Structure & Object Mapping Flow

```
RecognizedText (Microsoft.Windows.AI.Imaging)
 └── WinAiOcrLinesWords (IOcrLinesWords)
      ├── Angle (float)
      ├── Text (string)
      └── Lines: WinAiOcrLine[] (IOcrLine[])
           └── RecognizedLine
                ├── Text (string)
                ├── BoundingBox (Rect from TopLeft/BottomRight)
                └── Words: WinAiOcrWord[] (IOcrWord[])
                     └── RecognizedWord
                          ├── Text (string)
                          └── BoundingBox (Rect from TopLeft/BottomRight)
```