# Technical Documentation: `Text-Grab/Models/TemplatePatternMatch.cs`

## Overview

The `TemplatePatternMatch` class is a data model in the `Text_Grab.Models` namespace. It represents a reference to a saved regular expression (regex) pattern within a `GrabTemplate`. 

When a template is executed, the configuration stored in a `TemplatePatternMatch` instance dictates how a specific regex pattern (referenced via `PatternId` or `PatternName`) is evaluated against full-area OCR text, and how extracted matches are filtered and formatted according to `MatchMode` and `Separator`.

---

## File Info

* **File Path:** `Text-Grab/Models/TemplatePatternMatch.cs`
* **Namespace:** `Text_Grab.Models`
* **Class Name:** `TemplatePatternMatch`

---

## Output Template Placeholder Syntax

The documentation comments in this file define the expected syntax for referencing patterns in output templates using the `{p:...}` format:

| Syntax Placeholder | Description |
| :--- | :--- |
| `{p:PatternName:first}` | Extracts the **first** regex match. |
| `{p:PatternName:last}` | Extracts the **last** regex match. |
| `{p:PatternName:all:, }` | Extracts **all** matches, joined by the specified separator (e.g., `, `). |
| `{p:PatternName:2}` | Extracts the **2nd** match (1-based index). |
| `{p:PatternName:1,3}` | Extracts the **1st and 3rd** matches, joined by the separator. |

---

## Class Properties

### `PatternId`
* **Type:** `string`
* **Default Value:** `string.Empty`
* **Description:** Represents the `Id` corresponding to a `StoredRegex`. This unique identifier ensures durable resolution of the pattern even if the pattern name is subsequently changed or updated.

### `PatternName`
* **Type:** `string`
* **Default Value:** `string.Empty`
* **Description:** The display name of the pattern, mirroring the `Name` property of a `StoredRegex` at creation time. This name is also used inside the template placeholder syntax (`{p:PatternName:...}`).

### `MatchMode`
* **Type:** `string`
* **Default Value:** `"first"`
* **Description:** Specifies how matches are selected from the regex execution results. Accepted modes/formats mentioned in the specification:
  * `"first"`: Selects the first match.
  * `"last"`: Selects the last match.
  * `"all"`: Selects all matches.
  * A single 1-based index (e.g., `"2"`).
  * Comma-separated 1-based indices (e.g., `"1,3,5"`).

### `Separator`
* **Type:** `string`
* **Default Value:** `", "`
* **Description:** The delimiter string used to join extracted strings when `MatchMode` is set to `"all"` or specifies multiple indices (e.g., `"1,3"`).

---

## Constructors

### Parameterless Constructor
```csharp
public TemplatePatternMatch()
```
Initializes a new instance of `TemplatePatternMatch` with default values:
* `PatternId` = `""`
* `PatternName` = `""`
* `MatchMode` = `"first"`
* `Separator` = `", "`

### Parameterized Constructor
```csharp
public TemplatePatternMatch(
    string patternId, 
    string patternName, 
    string matchMode = "first", 
    string separator = ", ")
```
Initializes a new instance of `TemplatePatternMatch` with provided values.

#### Parameters:
* **`patternId`** (`string`): The durable identifier of the saved regex pattern.
* **`patternName`** (`string`): The display name used for matching and placeholder construction.
* **`matchMode`** (`string`, optional): Selection mode for matches. Defaults to `"first"`.
* **`separator`** (`string`, optional): Delimiter used for multiple matches. Defaults to `", "`.