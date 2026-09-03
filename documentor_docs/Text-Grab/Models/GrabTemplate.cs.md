# Technical Documentation: `GrabTemplate.cs`

**Namespace:** `Text_Grab.Models`  
**Class:** `GrabTemplate` (partial)

---

## 1. Overview

The `GrabTemplate` class represents a reusable capture template within the Text-Grab application. It defines metadata, reference canvas properties, capture regions, pattern references, and recognizer references required to parse fixed-layout documents (such as invoices or business cards) or process raw text.

It uses an `OutputTemplate` format string to construct final structured text by evaluating region OCR results, regular expression pattern matches, and recognizer output.

---

## 2. Output Template Syntax Summary

The `OutputTemplate` property supports placeholder syntax documented in the source file header:

### Region Placeholders
* `{N}`: Evaluates to the OCR text from region `N` (1-based index).
* `{N:trim}`: Trims whitespace from region `N` text.
* `{N:upper}`: Converts region `N` text to uppercase.
* `{N:lower}`: Converts region `N` text to lowercase.

### Pattern Placeholders (Regex)
* `{p:Name:first}`: First regex match of the pattern named `Name`.
* `{p:Name:last}`: Last regex match of the pattern named `Name`.
* `{p:Name:all:, }`: All matches joined by a specified separator (e.g., `, `).
* `{p:Name:2}`: The 2nd regex match (1-based index).
* `{p:Name:1,3}`: Specific matches (e.g., 1st and 3rd) joined by a separator.

### Escape Sequences
* `\n`: Newline
* `\t`: Tab
* `\\`: Literal backslash
* `\{`: Literal opening brace

---

## 3. Class Properties

### Data Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Id` | `string` | `Guid.NewGuid().ToString()` | Unique persistent identifier for the template. |
| `Name` | `string` | `string.Empty` | Human-readable name used in UI menus and list boxes. |
| `Description` | `string` | `string.Empty` | Optional detailed description used for tooltips. |
| `CreatedDate` | `DateTimeOffset` | `DateTimeOffset.Now` | Timestamp when the template was created. |
| `LastUsedDate` | `DateTimeOffset?` | `null` | Timestamp when the template was last executed for capture. |
| `SourceImagePath` | `string` | `string.Empty` | File path to a reference background image used in the template designer. |
| `ReferenceImageWidth` | `double` | `800` | Width of the reference image in pixels (used for coordinate scaling). |
| `ReferenceImageHeight` | `double` | `600` | Height of the reference image in pixels (used for coordinate scaling). |
| `Regions` | `List<TemplateRegion>` | `[]` | List of defined document capture regions with 1-based region numbers. |
| `OutputTemplate` | `string` | `string.Empty` | Format string defining final output structure and placeholder positions. |
| `PatternMatches` | `List<TemplatePatternMatch>` | `[]` | List mapping saved regex patterns (`StoredRegex`) to match-selection modes. |
| `RecognizerMatches` | `List<TemplateRecognizerMatch>` | `[]` | List mapping built-in recognizers (`BuiltInRecognizer`) to match modes and output types. |

### Computed / Read-Only Properties

* **`IsValid` (`bool`)**  
  Returns `true` if the template meets the minimum requirements for execution (`Name` and `OutputTemplate` are non-null and not whitespace-only). Otherwise, returns `false`.

* **`IsTextOnly` (`bool`)**  
  Returns `true` if `Regions.Count == 0`. Indicates that the template operates strictly on existing text without requiring image region capture.

---

## 4. Constructors

* **`GrabTemplate()`**  
  Parameterless constructor initializing default values.

* **`GrabTemplate(string name)`**  
  Initializes a new instance of `GrabTemplate` with the specified `Name`.

---

## 5. Methods

### Public Methods

#### `GetReferencedRegionNumbers()`
```csharp
public IEnumerable<int> GetReferencedRegionNumbers()
```
* **Description:** Parses the `OutputTemplate` string using regex to find all referenced region numbers.
* **Returns:** An `IEnumerable<int>` containing the distinct 1-based region numbers referenced in the template placeholders (e.g., returns `1` for `{1:trim}`).

#### `GetReferencedPatternNames()`
```csharp
public IEnumerable<string> GetReferencedPatternNames()
```
* **Description:** Parses the `OutputTemplate` string to extract all pattern names referenced via the `{p:Name:mode}` placeholder syntax.
* **Returns:** An `IEnumerable<string>` containing the extracted pattern names.

---

### Internal Generated Regex Methods

The class uses C# source generators (`[GeneratedRegex]`) for regex operations:

* **`RefRegionNumbers()`**
  * **Pattern:** `@"\{(\d+)(?::[a-z]+)?\}"`
  * **Purpose:** Matches region placeholders like `{1}` or `{2:upper}` and captures the digit in Group 1.

* **`RefPatternNames()`**
  * **Pattern:** `@"\{p:([^:}]+):[^}]+\}"`
  * **Purpose:** Matches pattern placeholders like `{p:Email:first}` and captures the pattern name in Group 1.

---

## 6. Dependencies & External Types

The `GrabTemplate` model relies on the following internal type references within `Text_Grab.Models`:
* `TemplateRegion`: Represents individual graphical regions defined on the reference image canvas.
* `TemplatePatternMatch`: Maps stored regex patterns to output options.
* `TemplateRecognizerMatch`: Maps built-in text recognizers to template match output settings.