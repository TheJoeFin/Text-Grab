# Technical Documentation: `HistoryInfo.cs`

## Overview

The `HistoryInfo` class in the `Text_Grab.Models` namespace represents a single captured history item within the Text-Grab application. It acts as a comprehensive data model storing metadata, captured text, OCR parameters, UI states, positioning info, and file paths related to a capture action.

It implements `IEquatable<HistoryInfo>` to allow identity comparison based on a unique identifier (`ID`). It also incorporates System.Text.Json attributes to control how history items are serialized to and deserialized from disk.

---

## Class Declaration

```csharp
namespace Text_Grab.Models;

public class HistoryInfo : IEquatable<HistoryInfo>
```

---

## Properties

### 1. Identity & Core Metadata

* **`ID`** (`string`)
  * **Default**: `""`
  * Unique identifier string for the history item. Drives equality and hash code evaluation.
* **`CaptureDateTime`** (`DateTimeOffset`)
  * Represents the exact timestamp when the capture was taken.
* **`SourceMode`** (`TextGrabMode`)
  * Indicates the operating mode used during text capture (e.g., Full Screen, Grab Frame, etc.).
* **`TextContent`** (`string`)
  * **Default**: `string.Empty`
  * The actual text content extracted or recognized during the capture process.

---

### 2. File & Image Properties

* **`ImageContent`** (`Bitmap?`)
  * **Serialization**: `[JsonIgnore]`
  * In-memory GDI+ `Bitmap` of the captured image. Excluded from JSON serialization.
* **`ImagePath`** (`string`)
  * **Default**: `string.Empty`
  * File path on disk pointing to the saved image associated with this history item.
* **`SourceContentKind`** (`OpenContentKind`)
  * **Default**: `OpenContentKind.Image`
  * Indicates the type of source content (e.g., image or PDF document).
* **`SourcePath`** (`string`)
  * **Default**: `string.Empty`
  * File path to the source document if captured from an external file.
* **`SourcePageIndex`** (`int`)
  * Indicates the page index within the source file if captured from a multi-page document.
* **`IsPdfDocument`** (`bool`)
  * **Serialization**: `[JsonIgnore]`
  * Read-only computed helper property. Returns `true` if `SourceContentKind == OpenContentKind.PdfDocument`.

---

### 3. Language & OCR Engine Properties

* **`LanguageTag`** (`string`)
  * **Default**: `string.Empty`
  * BCP-47 language tag string associated with the capture.
* **`LanguageKind`** (`LanguageKind`)
  * **Default**: `LanguageKind.Global`
  * Enum specifying the OCR language type/engine (e.g., Global, Tesseract, Windows AI, UI Automation).
* **`UsedUiAutomation`** (`bool`)
  * **Serialization**: `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`
  * Flag indicating whether UI Automation was used to extract text rather than an OCR engine. Omitted from JSON output if `false`.
* **`OcrLanguage`** (`ILanguage`)
  * **Serialization**: `[JsonIgnore]`
  * Read-only property that resolves and returns an `ILanguage` instance based on `LanguageKind`, `LanguageTag`, and `UsedUiAutomation`.
  * **Logic**:
    1. Normalizes language data using `LanguageUtilities.NormalizePersistedLanguageIdentity()`.
    2. If the normalized language tag is null or white space, returns a `GlobalLang` wrapping the system's current input language (or falls back to `"en-US"`).
    3. Maps `normalizedLanguageKind` to specific `ILanguage` implementations:
       * `LanguageKind.Global` $\rightarrow$ `GlobalLang`
       * `LanguageKind.Tesseract` $\rightarrow$ `TessLang`
       * `LanguageKind.WindowsAi` $\rightarrow$ `WindowsAiLang`
       * `LanguageKind.WindowsAiDescription` $\rightarrow$ `WindowsAiDescriptionLang`
       * `LanguageKind.UiAutomation` $\rightarrow$ Fallback language via `CaptureLanguageUtilities.GetUiAutomationFallbackLanguage()`
       * Default fallback $\rightarrow$ `GlobalLang` with the current input language or `"en-US"`.

---

### 4. Position & Screen Dimensions

* **`DpiScaleFactor`** (`double`)
  * **Default**: `1.0`
  * Screen DPI scale factor applied at the time of capture.
* **`SelectionStyle`** (`FsgSelectionStyle`)
  * **Default**: `FsgSelectionStyle.Region`
  * Selection style used during Full Screen Grab.
* **`RectAsString`** (`string`)
  * **Default**: `string.Empty`
  * String representation of the capture bounding rectangle.
* **`PositionRect`** (`Rect`)
  * **Serialization**: `[JsonIgnore]`
  * Wrapper property around `RectAsString`:
    * **Getter**: Parses `RectAsString` using `Rect.Parse()`. Returns `Rect.Empty` if `RectAsString` is empty or white space.
    * **Setter**: Converts the assigned `Rect` value to a string via `.ToString()` and stores it in `RectAsString`.

---

### 5. Table & Editor State

* **`IsTable`** (`bool`)
  * **Default**: `false`
  * Indicates whether the content was captured or formatted as a table.
* **`ManualTableColumnSeparators`** (`List<double>?`)
  * **Serialization**: `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
  * List of X-coordinates representing manual column separator lines.
* **`ManualTableRowSeparators`** (`List<double>?`)
  * **Serialization**: `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
  * List of Y-coordinates representing manual row separator lines.
* **`EditorMode`** (`EtwEditorMode`)
  * **Default**: `EtwEditorMode.Text`
  * Mode of the Edit Text Window editor.
* **`EditTextTableDocumentJson`** (`string?`)
  * **Serialization**: `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
  * JSON string holding serialized table data for the Edit Text Window.
* **`HasCalcPaneOpen`** (`bool`)
  * **Default**: `false`
  * Indicates if the calculation/calculator pane was active for this item.
* **`CalcPaneWidth`** (`int`)
  * **Default**: `0`
  * Stores the width of the calculation pane.

---

### 6. Word Border Data

* **`WordBorderInfoJson`** (`string?`)
  * **Serialization**: `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
  * Serialized JSON containing bounding box data for individual detected words.
* **`WordBorderInfoFileName`** (`string?`)
  * **Serialization**: `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
  * Relative or absolute file path referencing stored word border details.

---

## Methods

### Lifecycle & Cleanup

#### `ShallowCopy()`
```csharp
public HistoryInfo ShallowCopy()
```
* **Returns**: A shallow copy of the current `HistoryInfo` object via `MemberwiseClone()`.
* **Behavior**: Value types and string properties are copied directly. Reference-type fields (such as `ImageContent` or lists) point to the same instances in both copies.

#### `ClearTransientImage()`
```csharp
public void ClearTransientImage()
```
* **Behavior**: Sets `ImageContent` to `null`.
* **Note**: Does not explicitly call `.Dispose()` on `ImageContent`, allowing ongoing asynchronous tasks (e.g., file saving) to finish using the bitmap before it is collected by the Garbage Collector.

#### `ClearTransientWordBorderData()`
```csharp
public void ClearTransientWordBorderData()
```
* **Behavior**: Clears memory overhead by setting `WordBorderInfoJson` to `null`.

---

### Equality & Hashing

`HistoryInfo` evaluates equality strictly by comparing the `ID` string.

#### `Equals(HistoryInfo? other)`
```csharp
public bool Equals(HistoryInfo? other)
```
* Returns `true` if `other` is non-null and `other.ID == this.ID`; otherwise `false`.

#### `Equals(object? obj)`
```csharp
public override bool Equals(object? obj)
```
* Overrides standard `object.Equals`. Casts `obj` as `HistoryInfo` and delegates to `Equals(HistoryInfo?)`.

#### `GetHashCode()`
```csharp
public override int GetHashCode()
```
* Returns `HashCode.Combine(ID)`.

#### Operators `==` and `!=`
```csharp
public static bool operator ==(HistoryInfo? left, HistoryInfo? right)
public static bool operator !=(HistoryInfo? left, HistoryInfo? right)
```
* Overloaded equality operators that use `EqualityComparer<HistoryInfo>.Default.Equals(left, right)`.