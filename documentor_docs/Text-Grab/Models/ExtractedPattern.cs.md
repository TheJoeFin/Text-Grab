# Technical Documentation: `Text-Grab/Models/ExtractedPattern.cs`

## Overview

The `ExtractedPattern` class in the `Text_Grab.Models` namespace is responsible for generating, managing, and recommending regular expression (regex) patterns derived from a source string. It pre-computes regex patterns across six precision levels (ranging from 0 to 5) upon instantiation so that switching between precision levels is instantaneous.

Additionally, the class provides static heuristic methods to analyze text samples and automatically recommend an optimal starting precision level, as well as static utility methods for UI labels and descriptions.

---

## Class Constants

| Constant | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `MinPrecisionLevel` | `int` | `0` | The minimum supported precision level (Least precise). |
| `MaxPrecisionLevel` | `int` | `5` | The maximum supported precision level (Most precise). |
| `DefaultPrecisionLevel` | `int` | `3` | The default precision level used as a fallback. |

---

## Precision Levels Summary

The class recognizes 6 precision levels:

| Level | Label | Description |
| :---: | :--- | :--- |
| **0** | Any Text | Least Precise - Matches any non-whitespace (`\S+`) |
| **1** | Words | Word Characters - Matches letters, digits, underscore (`\w+`) |
| **2** | Length | Word Characters with Count - Preserves length but not character types |
| **3** | Types | Character Types with Counts - Distinguishes letters from digits (Default) |
| **4** | Per Char | Individual Character Match - Each position with case-insensitive Latin letters |
| **5** | Exact | Most Precise - Exact escaped string match |

---

## Properties

### `OriginalText`
```csharp
public string OriginalText { get; }
```
Gets the original input string from which all regex patterns were extracted.

---

### `IgnoreCase`
```csharp
public bool IgnoreCase { get; set; }
```
Gets or sets a value indicating whether the generated regular expressions use case-insensitive matching (e.g., using inline `(?i)` flags). 

* **Behavior on set:** If the value changes, it updates the internal backing field `_ignoreCase` and calls `GenerateAllPrecisionLevels()` to immediately rebuild the cached pattern dictionary for all levels.

---

### `AllPatterns`
```csharp
public IReadOnlyDictionary<int, string> AllPatterns => _patternsByLevel;
```
Exposes a read-only dictionary mapping precision levels (`int`, 0–5) to their pre-generated regex pattern strings (`string`).

---

## Constructors

### `ExtractedPattern(string text, bool ignoreCase = false)`
```csharp
public ExtractedPattern(string text, bool ignoreCase = false)
```
Initializes a new instance of the `ExtractedPattern` class.

* **Parameters:**
  * `text`: The source string to extract patterns from.
  * `ignoreCase`: Optional. If `true`, generates case-insensitive patterns. Defaults to `false`.
* **Execution Flow:**
  1. Sets `OriginalText` to `text`.
  2. Sets `_ignoreCase` to `ignoreCase`.
  3. Executes `GenerateAllPrecisionLevels()`.

---

## Instance Methods

### `GetPattern(int precisionLevel)`
```csharp
public string GetPattern(int precisionLevel)
```
Retrieves the pre-generated regex pattern for the specified precision level.

* **Parameters:** `precisionLevel` (`int`) – The desired level (0–5).
* **Returns:** `string` – The regex pattern corresponding to the specified precision level.
* **Logic:**
  * If `precisionLevel` is less than `MinPrecisionLevel` (0) or greater than `MaxPrecisionLevel` (5), `precisionLevel` defaults to `DefaultPrecisionLevel` (3).
  * Looks up the level in `_patternsByLevel`. If found, returns the pattern; otherwise, returns `string.Empty`.

---

### `GenerateAllPrecisionLevels()` *(Private)*
```csharp
private void GenerateAllPrecisionLevels()
```
Loops through precision levels `0` through `5` (`MinPrecisionLevel` to `MaxPrecisionLevel`). For each level, it calls the extension method `OriginalText.ExtractSimplePattern(level, _ignoreCase)` (from `Text_Grab.Utilities`) and stores the resulting string in the internal `_patternsByLevel` dictionary.

---

## Static UI Helper Methods

### `GetLevelDescription(int level)`
```csharp
public static string GetLevelDescription(int level)
```
Returns a detailed, human-readable string description explaining what pattern matching rules are applied at the specified level.

### `GetLevelLabel(int level)`
```csharp
public static string GetLevelLabel(int level)
```
Returns a short string label (e.g., `"Any Text"`, `"Words"`, `"Length"`, `"Types"`, `"Per Char"`, `"Exact"`) suited for display in user interfaces. If an invalid level is supplied, returns `"Level {level}"`.

---

## Heuristic Analysis (`DetermineStartingLevel`)

```csharp
public static int DetermineStartingLevel(string? selection)
```

Analyzes the input text structure and characteristics to automatically suggest the most contextually relevant starting precision level (0 to 5).

### Decision Flow Table

When a text selection is evaluated, rules are processed in the following order:

| Priority | Condition | Target Level | Reasoning |
| :---: | :--- | :---: | :--- |
| **1** | `selection` is `null` or whitespace | `3` | Falls back to `DefaultPrecisionLevel`. |
| **2** | Length = 1 | `5` | Single character text favors exact matching. |
| **3** | Length > 25 | `2` | Very long text uses length-based pattern to avoid over-specification. |
| **4** | Pure Digits (`IsAllDigits`) | `2` | Number sequences use length-flexible patterns. |
| **5** | 3 or more words (`WordCount >= 3`) | `1` | Multi-word phrases benefit from structural word matching. |
| **6** | Multiple Delimiters (`HasMultipleDelimiters`) | `3` | Text with $\ge 2$ delimiters (`-`, `_`, `:`, `.`, `/`, `\`, `\|`) favors character-class patterns. |
| **7** | Mixed Alphanumeric (`IsAlphanumericMixed`) | `3` | Mixed letters + digits with no spaces (e.g., IDs/codes) use character-type patterns. |
| **8** | Length between 2 and 4 inclusive | `4` | Short text strings use per-character matching for minor variations. |
| **9** | Simple Word (`IsSimpleWord`) | `4` | Single word containing only letters uses per-character matching. |
| **10** | Special Chars present and length $\le 10$ | `3` | Short text containing special/regex characters uses separator-agnostic patterns. |
| **Fallback**| None of the above conditions met | `3` | Default precision level. |

---

## Internal Helper Methods

The following static private methods support `DetermineStartingLevel`:

* **`IsAllDigits(string text)`**: Returns `true` if trimmed string length is $> 0$ and every character satisfies `char.IsDigit`.
* **`HasMultipleDelimiters(string text)`**: Checks if the text contains 2 or more occurrences from the set `['-', '_', ':', '.', '/', '\\', '|']`.
* **`WordCount(string text)`**: Splits string on whitespace and returns the count of non-empty substrings.
* **`IsAlphanumericMixed(string text)`**: Returns `true` if the text contains at least one letter, at least one digit, and no whitespace characters.
* **`IsSimpleWord(string text)`**: Returns `true` if trimmed text length is $> 0$, contains only letters (`char.IsLetter`), and contains no whitespace.
* **`HasSpecialChars(string text)`**: Returns `true` if any character in the text is included in `StringMethods.specialCharList` or is not a letter or digit (`!char.IsLetterOrDigit`).

---

## Dependencies

* **`System.Collections.Generic`**: For dictionary structures (`Dictionary<int, string>`, `IReadOnlyDictionary<int, string>`).
* **`System.Linq`**: For LINQ extension methods (`All`, `Any`, `Count`).
* **`Text_Grab.Utilities`**: Provides the `ExtractSimplePattern` extension method used during generation and `StringMethods.specialCharList` used in character analysis.