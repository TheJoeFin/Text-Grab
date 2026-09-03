# Technical Documentation: `ColumnSplitUtilities.cs`

## Overview

The `ColumnSplitUtilities.cs` file is located in the `Text_Grab.Utilities` namespace. It provides a non-throwing utility framework for splitting text strings (such as spreadsheet cell values) into multiple substring parts based on user-defined splitting criteria.

The file contains two enumerations, a data record for configuration parameters, and a static utility class that implements string-splitting algorithms using literal delimiters, regular expressions, pattern items, or fixed length offsets.

---

## Data Structures & Configuration

### 1. `SplitMode` (Enum)

Defines the strategy used to split a target text string:

| Value | Description |
| :--- | :--- |
| `Delimiter` | Splits text using a literal string delimiter. |
| `Regex` | Splits text using a regular expression or a predefined `PatternItem`. |
| `FixedLength` | Splits text into two parts at a specified character index offset. |

---

### 2. `SplitterHandling` (Enum)

Specifies how the matching delimiter or matched regex text (the "splitter") is handled during the split operation:

| Value | Behavior |
| :--- | :--- |
| `Remove` | Excludes the matching delimiter/regex pattern from all resulting parts. |
| `KeepLeft` | Attaches the matching delimiter/regex pattern to the end of the left part. |
| `KeepRight` | Attaches the matching delimiter/regex pattern to the beginning of the right part. |

*Note: `SplitterHandling` is ignored when `SplitMode` is set to `FixedLength`.*

---

### 3. `SplitColumnOptions` (Record)

A C# record that defines all parameters needed to perform a split operation.

#### Properties

* **`Mode`** (`SplitMode`, Default: `SplitMode.Delimiter`): The primary splitting strategy.
* **`DelimiterText`** (`string`, Default: `string.Empty`): The string literal matched when `Mode` is `SplitMode.Delimiter`.
* **`Pattern`** (`string`, Default: `string.Empty`): The raw regular expression pattern used when `Mode` is `SplitMode.Regex` and no `PatternItem` is provided.
* **`PatternItem`** (`PatternItem?`, Default: `null`): A predefined smart pattern object. When present and `Mode` is `SplitMode.Regex`, this overrides `Pattern`.
* **`IgnoreCase`** (`bool`, Default: `false`): Enables case-insensitive matching when processing `Pattern` as a raw regex.
* **`SplitterHandling`** (`SplitterHandling`, Default: `SplitterHandling.Remove`): Controls whether match delimiters are removed or attached to adjacent split parts.
* **`Length`** (`int`, Default: `0`): The character position/length offset used when `Mode` is `SplitMode.FixedLength`.
* **`SplitFromEnd`** (`bool`, Default: `false`): When `true` and `Mode` is `FixedLength`, measures the split index from the end of the text string instead of the start.

---

## Utility Class: `ColumnSplitUtilities`

A public static class that processes input text strings according to a `SplitColumnOptions` configuration.

### Static Fields

* **`RegexTimeout`** (`TimeSpan`): Set to 2 seconds (`TimeSpan.FromSeconds(2)`). Used to prevent catastrophic backtracking during regular expression evaluation.

---

### Public Methods

#### `SplitCell(string value, SplitColumnOptions options)`

The entry point for all cell-splitting operations.

* **Parameters:**
  * `value` (`string`): The target text to split. If `null`, it is treated as `string.Empty`.
  * `options` (`SplitColumnOptions`): Configuration options describing how to perform the split.
* **Exceptions:**
  * `ArgumentNullException`: Thrown if `options` is `null`.
* **Returns:** `IReadOnlyList<string>` containing the resulting text segments.
* **Execution Strategy:**
  Uses a switch expression on `options.Mode`:
  * `SplitMode.Delimiter` $\rightarrow$ Calls `SplitOnDelimiter`.
  * `SplitMode.Regex` $\rightarrow$ Calls `SplitOnPattern`.
  * `SplitMode.FixedLength` $\rightarrow$ Calls `SplitOnLength`.
  * Any unhandled/default mode returns `[value]`.

---

### Private Helper Methods

#### `SplitOnDelimiter(string value, string delimiter, SplitterHandling handling)`

Splits text by locating literal string occurrences of `delimiter`.

* **Behavior:**
  * Returns `[value]` immediately if `delimiter` is `null` or `string.Empty`.
  * Uses `string.IndexOf` with `StringComparison.Ordinal` in a loop to collect all non-overlapping span offsets `(Start, Length)`.
  * Passes the identified span offsets to `BuildPartsFromSpans`.

#### `SplitOnPattern(string value, SplitColumnOptions options)`

Delegates regex/pattern processing depending on whether a `PatternItem` is provided in `options`.

* **Behavior:**
  * If `options.PatternItem` is not `null`, delegates to `SplitByPatternItem`.
  * Otherwise, delegates to `SplitOnRegex`.

#### `SplitByPatternItem(string value, PatternItem patternItem, SplitterHandling handling)`

Executes a structured pattern item.

* **Behavior:**
  * Invokes `PatternExecutor.GetMatches(patternItem, value)` to obtain `RecognizerMatch` objects.
  * Projects match ranges into `(Start, Length)` tuples.
  * Passes the spans to `BuildPartsFromSpans`.

#### `SplitOnRegex(string value, string pattern, bool ignoreCase, SplitterHandling handling)`

Splits text using `System.Text.RegularExpressions.Regex`.

* **Behavior:**
  * Returns `[value]` immediately if `pattern` is `null` or `string.Empty`.
  * Executes `Regex.Matches` using `RegexOptions.IgnoreCase` (if `ignoreCase` is `true`) and `RegexTimeout`.
  * Extracts successful matches into `(Start, Length)` span tuples and forwards them to `BuildPartsFromSpans`.
  * **Error Handling:** If an exception occurs (such as an invalid regex syntax or execution timeout), the method catches all exceptions and safely returns `[value]` unchanged.

#### `SplitOnLength(string value, int length, bool fromEnd)`

Splits text at a fixed character index.

* **Behavior:**
  * Calculates `splitAt`:
    * If `fromEnd` is `true`: `value.Length - length`.
    * If `fromEnd` is `false`: `length`.
  * Clamps `splitAt` between `0` and `value.Length` using `Math.Clamp`.
  * Slices `value` into two parts using C# range operators: `value[..splitAt]` and `value[splitAt..]`.
  * Returns a list containing the two resulting strings.

#### `BuildPartsFromSpans(string value, List<(int Start, int Length)> spans, SplitterHandling handling)`

The core slice-building algorithm that reconstructs substring parts from a collection of match spans.

* **Process:**
  1. Filters out spans where `Length <= 0`.
  2. Orders spans by their `Start` index ascending.
  3. Iterates through the ordered spans maintaining a cursor offset (starts at `0`):
     * **Overlap Check:** Skips any span where `start < cursor` (overlapping spans are ignored).
     * **`SplitterHandling.KeepLeft`**:
       * Takes slice `value[cursor..end]`.
       * Sets `cursor = end`.
     * **`SplitterHandling.KeepRight`**:
       * Takes slice `value[cursor..start]`.
       * Sets `cursor = start`.
     * **`SplitterHandling.Remove`** (Default):
       * Takes slice `value[cursor..start]`.
       * Sets `cursor = end`.
  4. Appends the remaining slice of text `value[cursor..]` after processing all valid spans.
  5. Returns the resulting list of parts.

---

## Error Handling & Robustness Guarantees

1. **Non-Throwing Design:** Except for throwing `ArgumentNullException` if `options` itself is `null`, `SplitCell` does not throw exceptions due to invalid input data, invalid regular expressions, or execution timeouts. Invalid or failing configurations fall back to returning the original string intact as a single-element list `[value]`.
2. **Regex Timeout Protection:** Regular expressions are executed with a static 2-second timeout (`RegexTimeout`) to prevent catastrophic backtracking from freezing the application thread.
3. **Overlapping Span Prevention:** `BuildPartsFromSpans` automatically ignores overlapping match spans by skipping any span whose start position is less than the current processing cursor (`start < cursor`).
4. **Boundary Clamping:** `SplitOnLength` uses `Math.Clamp` to restrict indices to `[0, value.Length]`, preventing `ArgumentOutOfRangeException` during range slicing.