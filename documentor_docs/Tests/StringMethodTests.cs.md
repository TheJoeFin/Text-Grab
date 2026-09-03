# Developer Documentation: `Tests/StringMethodTests.cs`

## Overview

The `StringMethodTests.cs` file contains a suite of unit tests for validating string manipulation, parsing, text cleanup, and helper methods in the **Text_Grab** application (specifically targeting utility methods found in `Text_Grab` and `Text_Grab.Utilities`).

The test suite uses **xUnit** as its testing framework (`[Fact]`, `[Theory]`, `[InlineData]`, `Assert`).

---

## Class Architecture & Helper Types

### `StringMethodTests`
The main class containing unit tests. It defines standard xUnit test methods (`[Fact]` for single scenario tests and `[Theory]` for parameterized tests).

### `PredictableRandom` (Nested Class)
```csharp
private sealed class PredictableRandom(params int[] values) : Random
```
* **Inheritance**: Extends `System.Random`.
* **Purpose**: A deterministic random number generator mock used specifically for testing randomized operations (e.g., `ShuffleLines`).
* **Mechanism**:
  * Receives a fixed sequence of integer `values` via constructor parameter and enqueues them in a `Queue<int>`.
  * Overrides `Next(int maxValue)`.
  * On each call to `Next()`, dequeues the next integer, asserts that the queue is not empty, verifies that the returned integer is within `[0, maxValue - 1]`, and returns it.

### `multiLineInput` (Field)
* **Type**: `private static string`
* **Purpose**: A multi-line string fixture containing mock text used across multiple cursor- and word-extraction tests.

---

## Test Coverage Breakdown

The unit tests are structured around specific extension methods and utility functions within `Text_Grab`.

---

### 1. Line Flattening and Joining

| Test Method | Extension/Method Tested | Description |
| :--- | :--- | :--- |
| `MakeMultiLineStringSingleLine()` | `MakeStringSingleLine()` | Verifies that a multi-line string with leading and trailing newlines is collapsed into a single, clean line of text. |
| `MakeStringSingleLine_NewlineOnly_ReturnsEmptyString()` | `MakeStringSingleLine()` | Verifies that passing only newline characters (`Environment.NewLine`) returns `string.Empty`. |
| `JoinLines_WithJoiningTextAndAffixes_AsExpected()` | `JoinLines()` | Validates joining multiline input using a delimiter (`", "`), optional prefix (`"["`), and optional suffix (`"]"`) without trimming individual lines. |
| `JoinLines_TrimEachLineBeforeJoining_AsExpected()` | `JoinLines()` | Verifies that `JoinLines()` correctly trims whitespace from individual lines before joining them using a delimiter (`" \| "`). |
| `JoinLines_TrailingLineBreak_DoesNotAddExtraJoiningText()` | `JoinLines()` | Ensures that a trailing newline in the input string does not produce an extra trailing delimiter in the joined output. |

---

### 2. Cursor Operations & Word Boundary Detection

| Test Method | Extension/Method Tested | Description |
| :--- | :--- | :--- |
| `ReturnWordAtCursorPositionSix(...)` | `CursorWordBoundaries(int)` | Verifies that `CursorWordBoundaries` correctly calculates the start index and length of the word located at cursor index `6`. |
| `CursorWordBoundaries_ClampsEndOfTextToNearestWord(...)` | `CursorWordBoundaries(int)` | Validates cursor behavior near boundaries (e.g., cursor placed at or beyond the string length) to ensure it clamps to the nearest word. |
| `CursorWordBoundaries_AllWhitespace_ReturnsEmptyRange()` | `CursorWordBoundaries(int)` | Confirms that calling `CursorWordBoundaries` on a purely whitespace string returns an empty range (`start`, `0` length). |
| `ReturnPreviewsFromWord(...)` | `GetCharactersToLeftOfNewLine`, `GetCharactersToRightOfNewLine` | Validates preview text extraction to the left and right of specified word boundaries using reference parameters on multiline text. |
| `ReturnWordAtCursorWithNewLines(...)` | `GetWordAtCursorPosition(int)` | Validates extracting words from a multiline string across various cursor positions, including out-of-bounds, negative, and large indices. |
| `TestGetLineStartAndLength()` | `GetStartAndLengthOfLineAtPosition(int)` | Tests finding the start offset and length of the line intersecting character index `20`. |

---

### 3. OCR Correction & Text Manipulation Utilities

| Test Method | Extension/Method Tested | Description |
| :--- | :--- | :--- |
| `TryFixToLetters_ReplacesDigitsWithLetters_AsExpected(...)` | `TryFixToLetters()` | Tests replacing numeric digits that were likely OCR-misidentified into their intended character equivalents (e.g., `0` $\rightarrow$ `o`, `3` $\rightarrow$ `e`). |
| `TryFixNumOrLetters(...)` | `TryFixEveryWordLetterNumberErrors()` | Verifies context-aware OCR error fixes across mixed word and number boundaries (e.g., `he11o` $\rightarrow$ `hello`). |
| `TryFixToLetters_ReplacesLettersWithDigits_AsExpected(...)` | `TryFixToNumbers()` | Tests replacing letter characters that were likely OCR-misidentified into numeric equivalents (e.g., `o` $\rightarrow$ `0`, `S` $\rightarrow$ `5`). |
| `TestReplaceGreekAndCyrillic(...)` | `ReplaceGreekOrCyrillicWithLatin()` | Tests replacing visually similar Greek or Cyrillic characters (homoglyphs) with standard Latin equivalents. |
| `TestGuidCorrections(...)` | `CorrectCommonGuidErrors()` | Tests restoring improperly formatted GUID strings by repairing common OCR character misreadings (e.g., `g` $\rightarrow$ `9`, `S` $\rightarrow$ `5`, `l` $\rightarrow$ `1`) and stripping newlines/spaces. |

---

### 4. Line Reordering and Line Content Manipulation

| Test Method | Extension/Method Tested | Description |
| :--- | :--- | :--- |
| `RemoveDuplicateLines_AsExpected()` | `RemoveDuplicateLines()` | Tests removing repeated duplicate lines from input text while maintaining original unique line order. |
| `ShuffleLines_UsesProvidedRandom()` | `ShuffleLines(Random)` | Verifies deterministic shuffling of lines using the `PredictableRandom` test double. |
| `ShuffleLines_PreservesTrailingNewline()` | `ShuffleLines(Random)` | Ensures line shuffling preserves any trailing newline existing in the original string. |
| `TestUnstackGroups()` | `UnstackGroups(int)` | Tests converting a single vertical list into grouped tab-separated columns based on a block size (e.g., block size `5`). |
| `TestUnstackString()` | `UnstackStrings(int)` | Tests unstacking interleaved line patterns into grouped tab-separated columns based on stride size (e.g., `3`). |
| `TestRemoveThisString(...)` | `RemoveAllInstancesOf(string)` | Tests removing all occurrences of a target substring from a target line. |
| `TestReverseString(...)` | `StringBuilder.ReverseWordsForRightToLeft()` | Tests reversing word order within each line using `StringBuilder` extension logic. |
| `TestRemoveFromEachLines(...)` | `RemoveFromEachLine(int, SpotInLine)` | Tests stripping a fixed count of characters from either `SpotInLine.Beginning` or `SpotInLine.End` across all lines. |
| `TestAddToEachLines(...)` | `AddCharsToEachLine(string, SpotInLine)` | Tests prefixing (`SpotInLine.Beginning`) or suffixing (`SpotInLine.End`) a fixed string across all lines. |
| `TestLimitEachLine(...)` | `LimitCharactersPerLine(int, SpotInLine)` | Tests truncating line lengths from `SpotInLine.Beginning` or `SpotInLine.End` up to a maximum character limit. |

---

### 5. String Parsing, Matching & Character Analysis

| Test Method | Extension/Method Tested | Description |
| :--- | :--- | :--- |
| `ReplaceReservedCharacters(...)` | `ReplaceReservedCharacters()` | Tests replacing reserved/illegal characters (e.g., `<`, `>`, `:`, `/`, `\`, `?`, `*`) with hyper-dashes (`-`). |
| `ExtractSimplePatternFromEachString(...)` | `ExtractSimplePattern(int)` | Tests pattern extraction logic using varying precision levels (`0` through `5`), mapping input strings to regular expression patterns. |
| `TestIsValidEmailAddress(...)` | `IsValidEmailAddress()` | Tests email format validation logic, ensuring valid formats return `true` and invalid/malformed formats return `false`. |
| `TestDetermineToggleCase(...)` | `StringMethods.DetermineToggleCase(string)` | Tests detection of standard string casing types, returning `CurrentCase.Upper`, `CurrentCase.Lower`, `CurrentCase.Camel`, or `CurrentCase.Unknown`. |
| `TestIsBasicLatin(...)` | `IsBasicLatin()` | Tests character evaluation for standard Basic Latin range validation versus extended Unicode (e.g., accented characters like `À`, `Ü`). |

---

## Dependencies & Imports

* **`System`**: Core primitives and standard utilities (`Environment.NewLine`, `Random`).
* **`System.Collections.Generic`**: Used for `Queue<int>` in test mocks.
* **`System.Text`**: Used for `StringBuilder`.
* **`Text_Grab` & `Text_Grab.Utilities`**: Target application namespaces containing methods under test.
* **`Xunit`**: Test attributes (`Fact`, `Theory`, `InlineData`) and assertion tools (`Assert`).