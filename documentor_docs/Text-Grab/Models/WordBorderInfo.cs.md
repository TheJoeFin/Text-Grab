# Technical Documentation: `WordBorderInfo.cs`

**File Path:** `Text-Grab/Models/WordBorderInfo.cs`  
**Namespace:** `Text_Grab.Models`

---

## 1. Overview

The `WordBorderInfo` class is a data model within the `Text-Grab` application. It serves as a data transfer object (DTO) or lightweight model that encapsulates metadata, positional coordinates, formatting information, and classification state for a single recognized word or bounding region.

It provides a parameterless constructor for direct initialization, as well as a specialized constructor that extracts and maps data from a `WordBorder` control instance (`Text_Grab.Controls.WordBorder`).

---

## 2. Class Definition

```csharp
namespace Text_Grab.Models;

public class WordBorderInfo
```

### Dependencies
* `System`
* `System.Windows` (Provides `Rect`)
* `Text_Grab.Controls` (Provides the `WordBorder` control type)

---

## 3. Properties

Below is a detailed list of all public properties defined in `WordBorderInfo`:

| Property | Data Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Word` | `string` | `string.Empty` | The raw text or word extracted. |
| `DisplayText` | `string` | `string.Empty` | The text formatted or modified for display purposes. |
| `BorderRect` | `Rect` | `Rect.Empty` | A `System.Windows.Rect` representing the bounding box (X, Y, Width, Height) of the word. |
| `DisplayLineHeight` | `double` | `0` | The line height calculated or assigned for rendering display text. |
| `KeepSingleLineOutput` | `bool` | `false` | Indicates whether the word output should remain strictly on a single line. |
| `LineNumber` | `int` | `0` | The line index or sequence number to which this word belongs. |
| `ResultColumnID` | `int` | `0` | Column identifier for structured/grid layout placement. |
| `ResultRowID` | `int` | `0` | Row identifier for structured/grid layout placement. |
| `MatchingBackground` | `string` | `"Transparent"` | Color representation (as a string) used for the background highlight or matching state. |
| `IsBarcode` | `bool` | `false` | Flag indicating whether the detected region represents a barcode. |

---

## 4. Constructors

### 4.1. Default Constructor

```csharp
public WordBorderInfo()
```
Initializes a new instance of the `WordBorderInfo` class with default property values as specified in property declarations.

---

### 4.2. Parameterized Constructor

```csharp
public WordBorderInfo(WordBorder wordBorder)
```

Initializes a new instance of `WordBorderInfo` by copying and mapping state from an existing `WordBorder` control object (`wordBorder`).

#### Transformation & Mapping Logic:

1. **`Word`**: Directly assigned from `wordBorder.Word`.
2. **`DisplayText`**: Derived conditionally:
   * Checked condition: `wordBorder.KeepSingleLineOutput || !string.Equals(wordBorder.DisplayText, wordBorder.Word, StringComparison.Ordinal)`
   * If `KeepSingleLineOutput` is `true` **OR** `DisplayText` does not equal `Word` (using ordinal string comparison): set to `wordBorder.DisplayText`.
   * Otherwise: set to `string.Empty`.
3. **`DisplayLineHeight`**: Directly assigned from `wordBorder.DisplayLineHeight`.
4. **`KeepSingleLineOutput`**: Directly assigned from `wordBorder.KeepSingleLineOutput`.
5. **`LineNumber`**: Directly assigned from `wordBorder.LineNumber`.
6. **`ResultColumnID`**: Directly assigned from `wordBorder.ResultColumnID`.
7. **`ResultRowID`**: Directly assigned from `wordBorder.ResultRowID`.
8. **`MatchingBackground`**: Converted to a string via `wordBorder.MatchingBackground.ToString()`.
9. **`IsBarcode`**: Directly assigned from `wordBorder.IsBarcode`.
10. **`BorderRect`**: Constructed as a new `System.Windows.Rect` using dimensions from `wordBorder`:
    * `X = wordBorder.Left`
    * `Y = wordBorder.Top`
    * `Width = wordBorder.Width`
    * `Height = wordBorder.Height`

---

## 5. Execution & Data Flow Summary

```
+-------------------------------+
|   Text_Grab.Controls.WordBorder |
|-------------------------------|
| - Word                        |
| - DisplayText                 |
| - Left, Top, Width, Height    |
| - LineNumber, Row/Col IDs     |
| - MatchingBackground, etc.    |
+-------------------------------+
                |
                | (Passed to constructor)
                v
+-------------------------------+
|    WordBorderInfo (Model)     |
|-------------------------------|
| - Word                        |
| - DisplayText (Conditional)   |
| - BorderRect (Rect struct)    |
| - MatchingBackground (String) |
| - LineNumber, Row/Col IDs     |
+-------------------------------+
```

1. A `WordBorder` control instance exists containing positional and contextual layout information.
2. Instantiating `WordBorderInfo(wordBorder)` extracts the UI control's physical geometry (`Left`, `Top`, `Width`, `Height`) into a framework-neutral `Rect` (`BorderRect`).
3. Text representation logic evaluates whether `DisplayText` requires preservation or can default to `string.Empty` if redundant with `Word`.
4. Background information is converted from its native type on `WordBorder` into a string representation (`MatchingBackground`).