# Technical Documentation: `TextSearchUtilities.cs`

## Overview

The `TextSearchUtilities` class is an `internal static` helper class in the `Text_Grab.Utilities` namespace. It provides static helper methods for text validation, formatting matched text for UI display, and constructing `Regex` instances configured with default options and execution timeouts.

---

## Class Signature

```csharp
namespace Text_Grab.Utilities;

internal static class TextSearchUtilities
```

---

## Constants & Fields

### `DefaultRegexTimeout`
* **Type**: `TimeSpan`
* **Visibility**: `private static readonly`
* **Value**: `TimeSpan.FromSeconds(5)`
* **Description**: A default timeout limit of 5 seconds applied to all created `Regex` instances to prevent long-running pattern matching operations or catastrophic backtracking.

---

## Methods

### 1. `HasSearchText`

Determines whether a provided search string contains content (is not `null` and not empty).

#### Signature
```csharp
internal static bool HasSearchText(string? searchText)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `searchText` | `string?` | The string input to check. Can be `null`. |

#### Returns
* `bool`: `true` if `searchText` is neither `null` nor an empty string (`""`); otherwise, `false`.

---

### 2. `FormatMatchTextForDisplay`

Formats a matched text string so it can be cleanly displayed in UI components.

#### Signature
```csharp
internal static string FormatMatchTextForDisplay(string matchText)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `matchText` | `string` | The match string to format. |

#### Behavior & Logic
1. **Non-Whitespace Check**:
   If `matchText` contains at least one non-whitespace character (`!matchText.All(char.IsWhiteSpace)`), it executes the `MakeStringSingleLine()` extension method on `matchText` and returns the result.
   
2. **Whitespace-Only Processing**:
   If `matchText` consists entirely of whitespace characters, it iterates through each character in `matchText` and replaces invisible whitespace characters with visible symbol representations:

| Input Character / Sequence | Visual Replacement Symbol | Character Description |
| :--- | :--- | :--- |
| `\r\n` | `⏎` | Carriage Return + Line Feed sequence |
| `' '` | `·` | Space (Middle Dot) |
| `'\t'` | `⇥` | Tab |
| `'\r'` | `␍` | Standalone Carriage Return |
| `'\n'` | `⏎` | Standalone Line Feed / Newline |
| *Other Whitespace* | `␣` | Open Box (Fallback for other whitespace) |

#### Returns
* `string`: The formatted, single-line, or symbol-encoded display string.

---

### 3. `CreateFindAndReplaceSearchRegex`

Constructs a `Regex` instance configured specifically for find-and-replace search operations.

#### Signature
```csharp
internal static Regex CreateFindAndReplaceSearchRegex(string pattern, bool usePatternMode, bool exactMatch)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `pattern` | `string` | The regular expression pattern string. |
| `usePatternMode` | `bool` | Indicates whether pattern/regex mode is enabled. |
| `exactMatch` | `bool` | Indicates whether case sensitivity should be strictly enforced. |

#### Behavior & Options Applied
* Base Option: `RegexOptions.Multiline`
* Conditional Option: `RegexOptions.IgnoreCase` is added **only if** both `exactMatch` is `false` **and** `usePatternMode` is `false`.
* Timeout: `DefaultRegexTimeout` (5 seconds).

#### Returns
* `Regex`: A configured `Regex` instance.

---

### 4. `CreateReplacementRegex`

Constructs a `Regex` instance for replacement operations.

#### Signature
```csharp
internal static Regex CreateReplacementRegex(string pattern, bool exactMatch)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `pattern` | `string` | The regular expression pattern string. |
| `exactMatch` | `bool` | Specifies whether case sensitivity should be strictly matched (`true`) or ignored (`false`). |

#### Behavior & Options Applied
* Option: `RegexOptions.None` if `exactMatch` is `true`.
* Option: `RegexOptions.IgnoreCase` if `exactMatch` is `false`.
* Timeout: `DefaultRegexTimeout` (5 seconds).

#### Returns
* `Regex`: A configured `Regex` instance.

---

### 5. `CreateGrabFrameSearchRegex`

Constructs a `Regex` instance for grab frame search scenarios.

#### Signature
```csharp
internal static Regex CreateGrabFrameSearchRegex(string pattern, bool exactMatch)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `pattern` | `string` | The regular expression pattern string. |
| `exactMatch` | `bool` | Specifies whether case sensitivity is enforced (`true`) or ignored (`false`). |

#### Behavior & Options Applied
* Option: `RegexOptions.Multiline` if `exactMatch` is `true`.
* Option: `RegexOptions.Multiline | RegexOptions.IgnoreCase` if `exactMatch` is `false`.
* Timeout: `DefaultRegexTimeout` (5 seconds).

#### Returns
* `Regex`: A configured `Regex` instance.

---

## External Extension Dependencies

* `MakeStringSingleLine()`: Called inside `FormatMatchTextForDisplay` when processing strings containing non-whitespace characters. (Defined externally in the codebase extension methods).