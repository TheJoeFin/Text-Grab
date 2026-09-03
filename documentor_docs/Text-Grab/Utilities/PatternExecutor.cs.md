# Technical Documentation: `PatternExecutor.cs`

**File Path:** `Text-Grab/Utilities/PatternExecutor.cs`  
**Namespace:** `Text_Grab.Utilities`

---

## 1. Overview

The `PatternExecutor` class serves as a static unified dispatcher for executing patterns against input text in Text-Grab. It provides a single entry point to evaluate two kinds of pattern matching items (`PatternItem`):

1. **Recognizers** (`PatternKind.Recognizer`): Dispatched to `RecognizerExecutor`.
2. **Saved Regexes** (`PatternKind.SavedRegex`): Evaluated directly using .NET's `System.Text.RegularExpressions.Regex`.

By abstracting the dispatching logic, UI surfaces (such as the Edit Text Window, Grab Templates, and search utilities) can process recognizers and saved regular expressions using a uniform API interface.

### Key Characteristics
- **Static Class**: Requires no instantiation.
- **Fail-Safe Execution**: Designed never to throw exceptions during pattern matching. Invalid regular expression syntax or regex timeouts are caught internally and safely result in no matches (empty collections or empty strings).
- **Time-Bounded Regex**: Enforces a 5-second timeout on regex execution to prevent catastrophic backtracking.

---

## 2. Dependencies and Class Signature

### Declaration
```csharp
namespace Text_Grab.Utilities;

public static class PatternExecutor
```

### Dependencies
- `System`
- `System.Collections.Generic`
- `System.Linq`
- `System.Text.RegularExpressions`
- `Text_Grab.Models`

---

## 3. Fields & Constants

### `RegexTimeout`
```csharp
private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);
```
* **Type**: `TimeSpan` (Static Readonly)
* **Value**: 5 seconds (`TimeSpan.FromSeconds(5)`)
* **Purpose**: Sets a strict execution time limit for regular expression evaluations to prevent application hangs caused by complex or malicious regex patterns.

---

## 4. Public API Reference

### 4.1 `HasMatch`

Determines whether the given `PatternItem` finds at least one match within the input text.

```csharp
public static bool HasMatch(PatternItem item, string text)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `item` | `PatternItem` | The pattern instance (Recognizer or SavedRegex) to evaluate. |
| `text` | `string` | The target string to search within. |

#### Returns
- `bool`: `true` if one or more matches are found; otherwise, `false`.

#### Implementation Details
- Delegates directly to `GetMatches(item, text)` and checks if the resulting collection count is greater than zero (`.Count > 0`).

---

### 4.2 `GetMatches`

Evaluates the `PatternItem` against the provided text and returns a list of all matches ordered by position.

```csharp
public static IReadOnlyList<RecognizerMatch> GetMatches(PatternItem item, string text)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `item` | `PatternItem` | The pattern instance to evaluate. |
| `text` | `string` | The target text to search. |

#### Returns
- `IReadOnlyList<RecognizerMatch>`: A collection of `RecognizerMatch` objects representing the matches found. Returns an empty array `[]` if no matches are found, if inputs are invalid/null, or if errors occur.

#### Execution Logic
1. **Validation**: Returns `[]` immediately if `item` is `null` or if `text` is null/empty.
2. **Recognizer Path**: If `item.Kind == PatternKind.Recognizer` and `item.Recognizer` is not `null`, delegates matching to `RecognizerExecutor.GetMatches(item.Recognizer, text)`.
3. **Saved Regex Path**: If `item.Kind == PatternKind.SavedRegex` and `item.SavedRegex` is not `null`, delegates matching to the private method `GetRegexMatches(item.SavedRegex.Pattern, text)`.
4. **Fallback**: Returns `[]` if `item.Kind` does not match recognized pattern types or if properties are null.

*Note: Regular expression matches reuse the `RecognizerMatch` data structure, storing the matched text as both `RecognizerMatch.Text` and `RecognizerMatch.ResolvedValue`.*

---

### 4.3 `Apply`

Runs the specified pattern against the input string, extracts the matches, filters/selects them according to the requested `mode`, and joins them using a `separator`.

```csharp
public static string Apply(
    PatternItem item,
    string text,
    string mode = "all",
    string separator = ", ",
    RecognizerOutputKind output = RecognizerOutputKind.ResolvedValue)
```

#### Parameters
| Parameter | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `item` | `PatternItem` | *Required* | The pattern configuration to apply. |
| `text` | `string` | *Required* | The text to be processed. |
| `mode` | `string` | `"all"` | Selection strategy for matches (e.g., `"first"`, `"last"`, `"all"`, nth index, or sets like `"1,3"`). |
| `separator` | `string` | `", "` | The delimiter used to join selected match results into the output string. |
| `output` | `RecognizerOutputKind` | `RecognizerOutputKind.ResolvedValue` | Specifies whether recognizer matches yield raw text or resolved values. (Saved regex matches always yield raw matched text). |

#### Returns
- `string`: The formatted result string containing matched elements joined by the specified separator. Returns `string.Empty` if `item` is null or no matches are found.

#### Execution Logic
1. Checks if `item` is `null`. If so, returns `string.Empty`.
2. **Recognizer Dispatch**: If `item.Kind == PatternKind.Recognizer` and `item.Recognizer` is not `null`, delegates execution to `RecognizerExecutor.ApplyRecognizer(item.Recognizer, text, mode, separator, output)`.
3. **Saved Regex Path**:
   - Calls `GetMatches(item, text)`.
   - If no matches exist (`matches.Count == 0`), returns `string.Empty`.
   - Extracts string values from `m.Text` for each match into a `List<string>`.
   - Calls `GrabTemplateExecutor.ExtractMatchesByMode(values, mode, separator)` to filter, select, and join the matched strings based on `mode` and `separator`.

---

## 5. Private Helper Methods

### `GetRegexMatches`

Executes regular expression matching over target text using safe handling patterns.

```csharp
private static IReadOnlyList<RecognizerMatch> GetRegexMatches(string pattern, string text)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `pattern` | `string` | The regular expression pattern string. |
| `text` | `string` | Target text to evaluate. |

#### Returns
- `IReadOnlyList<RecognizerMatch>`: Evaluated matches mapped into `RecognizerMatch` instances. Returns an empty array `[]` on failure or empty input.

#### Implementation Details
- Returns `[]` if `pattern` is null or empty.
- Executes `Regex.Matches` using:
  - `RegexOptions.Multiline`
  - Timeout specified by `RegexTimeout` (5 seconds).
- Maps each successful match (`m.Success`) into a new `RecognizerMatch`:
  ```csharp
  new RecognizerMatch(m.Index, m.Length, m.Value, m.Value)
  ```
  *(Index, Length, Text = `m.Value`, ResolvedValue = `m.Value`)*
- **Exception Handling**: Catches both `RegexMatchTimeoutException` and `ArgumentException` (for invalid regex syntax) and returns `[]` without throwing exceptions to calling contexts.

---

## 6. Execution Flow Summary

```
                        [ PatternExecutor.Apply / GetMatches ]
                                         |
                                         v
                         Is PatternItem Null or Text Empty?
                                  /            \
                                Yes             No
                               /                 \
                  [ Return Empty / [] ]           v
                                        Check item.Kind
                                         /            \
                       Kind == Recognizer              Kind == SavedRegex
                              /                              \
                             v                                v
            [ Delegate to RecognizerExecutor ]      [ Call GetRegexMatches ]
                                                              |
                                                              v
                                                    Regex.Matches (Multiline, 5s)
                                                              |
                                                  +-----------+-----------+
                                                  |                       |
                                               Success                 Exception
                                                  |                (Timeout/Invalid)
                                                  v                       |
                                       [ Return RecognizerMatches ]  [ Return [] ]
```