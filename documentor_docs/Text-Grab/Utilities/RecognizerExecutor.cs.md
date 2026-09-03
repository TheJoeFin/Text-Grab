# Technical Documentation: `RecognizerExecutor.cs`

## Overview

The `RecognizerExecutor` class within the `Text_Grab.Utilities` namespace is a utility that runs Microsoft Recognizers-Text recognizers synchronously against text strings. It encapsulates text analysis features such as entity extraction, date/time resolution, numerical/unit normalization, and text replacement logic.

It is utilized across several application features including:
* **Edit Text Window** (applying entity recognizers directly)
* **Grab Templates** (`{r:Name:mode}` placeholder parsing)
* **Search Features** (Grab Frame, Quick Simple Lookup, Find & Replace)

---

## Data Structures & Types

### 1. `RecognizerOutputKind` (Enum)

Determines what representation of a matched entity should be output during operations.

| Enum Member | Description |
| :--- | :--- |
| `ResolvedValue` | Emits the normalized or resolved representation of the match (e.g., resolving `"next tuesday"` into a date string like `"2026-07-07"` or `"$5"` into `"5 Dollar"`). |
| `MatchedText` | Emits the exact raw string snippet as extracted from the source text. |

---

### 2. `RecognizerMatch` (Readonly Record Struct)

Represents a single recognized entity found within a source text string.

```csharp
public readonly record struct RecognizerMatch(int Start, int Length, string Text, string ResolvedValue);
```

#### Properties:
* **`Start`** (`int`): Zero-based starting character index of the match within the source text.
* **`Length`** (`int`): Total character count of the matched substring.
* **`Text`** (`string`): The original matched substring from the source text.
* **`ResolvedValue`** (`string`): The normalized/formatted string value of the match, or falling back to `Text` if no resolution exists.

---

## Class Architecture: `RecognizerExecutor`

`RecognizerExecutor` is a `public static class`.

### Constants

* **`DefaultCulture`** (`string`): Defaults to `Culture.English` (`"en-us"` via Microsoft Recognizers-Text). Centralizes culture setting for recognizers.

---

### Public Methods

#### `GetMatches(BuiltInRecognizer recognizer, string text, string? culture = null)`
* **Returns**: `IReadOnlyList<RecognizerMatch>`
* **Description**: Processes `text` using the specified `recognizer` model.
* **Behavior**:
  * Validates inputs; if `recognizer` is `null` or `text` is `null`/empty, returns an empty collection (`[]`).
  * Wraps execution in a `try-catch` block so failures return an empty list rather than throwing exceptions.
  * Culture defaults to `DefaultCulture` if unspecified.
  * Calculates match length as `(r.End - r.Start) + 1` to account for `Microsoft.Recognizers.Text` using an inclusive end index.
  * Orders matches by character position (`Start`).

#### `HasMatch(BuiltInRecognizer recognizer, string text, string? culture = null)`
* **Returns**: `bool`
* **Description**: Returns `true` if `GetMatches` yields at least one recognized entity in `text`, otherwise `false`.

#### `ApplyRecognizer(...)`
* **Signature**:
  ```csharp
  public static string ApplyRecognizer(
      BuiltInRecognizer recognizer,
      string text,
      string matchMode = "all",
      string separator = ", ",
      RecognizerOutputKind output = RecognizerOutputKind.ResolvedValue,
      string? culture = null)
  ```
* **Returns**: `string`
* **Description**: Executes entity recognition on `text`, selects match indices specified by `matchMode` (e.g., `"first"`, `"last"`, `"all"`, nth, or range indices), and joins the selected output values using `separator`.
* **Behavior**:
  * Calls `GetMatches`. Returns `string.Empty` if no matches are found.
  * Extracts values based on `output` (`Text` or `ResolvedValue`).
  * Delegates match filtering and formatting to `GrabTemplateExecutor.ExtractMatchesByMode(...)`.

#### `FormatResolvedValue(ModelResult result)`
* **Returns**: `string`
* **Description**: Translates a `ModelResult.Resolution` dictionary into a user-friendly normalized string.
* **Resolution Resolution Logic**:
  1. **Empty Dictionary Check**: If `Resolution` is `null` or empty, returns `result.Text`.
  2. **DateTime / Sets / Ranges (`"values"` key)**:
     * Reads structural elements from the `"values"` list regardless of internal underlying collection typing (e.g., `Dictionary<string, string>` vs `Dictionary<string, object>`).
     * Inspects the first element dictionary:
       * Checks for `"value"` key (validated via `IsResolvedValue`).
       * Checks for `"start"` and `"end"` keys (formats as `"{start} → {end}"`).
       * Checks for `"timex"` key (returns timex expression).
  3. **Units & Single Quantities (`"value"` key)**:
     * Extracts `"value"` string.
     * If a `"unit"` key exists and is non-empty, appends it (formatted as `"{value} {unit}"`).
  4. **Fallback**: Returns `result.Text` if resolution keys are unrecognized or unresolved.

---

### Private Helper Methods

#### `IsResolvedValue(string? value)`
* **Returns**: `bool`
* **Logic**: Evaluates whether a string is non-null, non-empty, and not equal to `"not resolved"` (case-insensitive).

#### `FirstElement(object? value)`
* **Returns**: `object?`
* **Logic**: Iterates over a non-string `IEnumerable` and returns its first element. Returns `null` if empty or invalid type.

#### `AsStringDictionary(object? value)`
* **Returns**: `Dictionary<string, string>?`
* **Logic**: Coerces an object implementing `IDictionary<string, string>` or non-generic `IDictionary` into a concrete `Dictionary<string, string>`, providing backward and forward compatibility across `Microsoft.Recognizers.Text` library versions. Returns `null` if the input is not a dictionary.