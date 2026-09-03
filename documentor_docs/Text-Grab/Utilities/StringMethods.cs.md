# Documentation Guide: `Text-Grab/Utilities/StringMethods.cs`

## Overview

The `StringMethods` class in `Text-Grab.Utilities` is a static helper class providing string manipulation, text parsing, OCR error correction, regular expression generation, and formatting utilities. It contains extension methods and lookup dictionaries designed to clean OCR results, format line-based text, detect word/line boundaries, and construct or explain regex patterns.

---

## Constants & Static Collections

The class exposes several public static data structures used across string cleaning and mapping routines:

| Collection | Type | Description |
| :--- | :--- | :--- |
| `specialCharList` | `List<char>` | Characters considered special for regex escaping operations: `\`, ` `, `.`, `,`, `$`, `^`, `{`, `[`, `(`, `|`, `)`, `*`, `+`, `?`, `=` |
| `ReservedChars` | `List<char>` | Reserved/invalid characters commonly targeted for replacement (e.g., path/filename safety): ` `, `"`, `*`, `/`, `:`, `<`, `>`, `?`, `\`, `|`, `+`, `,`, `.`, `;`, `=`, `[`, `]`, `!`, `@` |
| `GreekCyrillicLatinMap` | `Dictionary<char, char>` | Mappings from visually similar Greek, Cyrillic, and accented characters to standard Latin character equivalents |
| `NumbersToLetters` | `Dictionary<char, char>` | Mappings converting common OCR numerical misreads to letter equivalents (e.g., `'0' -> 'o'`, `'4' -> 'h'`, `'8' -> 'B'`) |
| `LettersToNumbers` | `Dictionary<char, char>` | Mappings converting common OCR letter misreads to numeric equivalents (e.g., `'o' -> '0'`, `'l' -> '1'`, `'s' -> '5'`) |
| `GuidCorrections` | `Dictionary<char, char>` | Specialized OCR mappings tailored for fixing corrupted GUID strings (e.g., `'#' -> 'f'`, `'@' -> '0'`, `'/' -> '7'`) |

---

## Nested Types

### `CharType` Enum
Defines character classifications used during pattern extraction:
* `Letter`
* `Number`
* `Space`
* `Special`
* `Other`

### `CharRun` Class
Represents a contiguous sequence of characters of the same `CharType`:
* `TypeOfChar` (`CharType`): The category of character in the run.
* `Character` (`char`): The representative character.
* `NumberOfRun` (`int`): The count of consecutive occurrences.

---

## Method Reference by Category

### 1. Dictionary & Character Mapping

#### `ReplaceWithDictionary(this string str, Dictionary<char, char> dict)`
* **Returns:** `string`
* Iterates through characters in `str`, replacing any character found as a key in `dict` with its corresponding mapped value.

#### `ReplaceGreekOrCyrillicWithLatin(this string str)`
* **Returns:** `string`
* Replaces visual lookalike Greek, Cyrillic, and accented characters with Latin characters using `GreekCyrillicLatinMap`.

#### `CorrectCommonGuidErrors(this string guid)`
* **Returns:** `string`
* Cleans a GUID string by:
  1. Removing spaces.
  2. Stripping line breaks occurring immediately before or after hyphens (`-\r\n` and `\r\n-`).
  3. Translating misread characters using `GuidCorrections`.

#### `TryFixToLetters(this string fixToLetters)`
* **Returns:** `string`
* Replaces digit characters with corresponding letter equivalents using `NumbersToLetters`.

#### `TryFixToNumbers(this string fixToNumbers)`
* **Returns:** `string`
* Replaces letter characters with corresponding numeric equivalents using `LettersToNumbers`.

#### `TryFixNumberLetterErrors(this string stringToFix)`
* **Returns:** `string`
* Analyzes a string (must be at least 5 characters long):
  * Computes the fraction of numeric digits and letter characters relative to string length.
  * If digits exceed 60% (`> 0.6`), converts letter misreads to numbers via `TryFixToNumbers()`.
  * If letters exceed 60% (`> 0.6`), converts digit misreads to letters via `TryFixToLetters()`.
  * Returns original string if length is less than 5 or neither threshold is met.

#### `TryFixEveryWordLetterNumberErrors(this string stringToFix)`
* **Returns:** `string`
* Splits input string by spaces, applies `TryFixNumberLetterErrors` to each individual word, re-joins them with spaces, cleans trailing spaces around tab and newline characters (`\t `, `\r `, `\n `), and trims leading/trailing whitespace.

---

### 2. Cursor, Line, & Search Index Navigation

#### `AllIndexesOf(this string str, string searchString)`
* **Returns:** `IEnumerable<int>`
* Yields all start indices of `searchString` in `str`. Steps forward by `searchString.Length` after each match.

#### `FindAllIndicesOfString(this string sourceString, string stringToFind)`
* **Returns:** `IEnumerable<int>`
* Yields index positions where `stringToFind` matches `sourceString`, iterating step-by-step through the valid index range.

#### `CursorWordBoundaries(this string input, int cursorPosition)`
* **Returns:** `(int start, int length)`
* Calculates the start index and length of the word located at or near `cursorPosition`:
  * Clamps `cursorPosition` within valid string bounds.
  * If the cursor sits on whitespace, shifts to the nearest letter index via `FindNearestLetterIndex`.
  * Expands boundaries left and right until encountering whitespace or string bounds.

#### `GetWordAtCursorPosition(this string input, int cursorPosition)`
* **Returns:** `string`
* Calls `CursorWordBoundaries` and extracts the substring corresponding to the word at `cursorPosition`.

#### `GetStartAndLengthOfLineAtPosition(this string text, int position)`
* **Returns:** `(int startSelectionIndex, int selectionLength)`
* Identifies the start index and total character length (including `Environment.NewLine`) of the line containing the character at `position`.

#### `GetCharactersToLeftOfNewLine(ref string mainString, int index, int numberOfCharacters)`
* **Returns:** `string`
* Retrieves up to `numberOfCharacters` to the left of `index` up to the preceding newline character. Prepends `"..."` if truncated.

#### `GetCharactersToRightOfNewLine(ref string mainString, int index, int numberOfCharacters)`
* **Returns:** `string`
* Retrieves up to `numberOfCharacters` to the right of `index` up to the next newline character. Appends `"..."` if truncated.

#### `GetNewLineIndexToLeft(ref string mainString, int index)`
* **Returns:** `int`
* Scans backwards from `index` to find the nearest preceding newline character index.

#### `GetNewLineIndexToRight(ref string mainString, int index)`
* **Returns:** `int`
* Scans forwards from `index` to find the nearest following newline character index.

---

### 3. Line Formatting & Structural Transformation

#### `MakeStringSingleLine(this string textToEdit)`
* **Returns:** `string`
* Replaces all line break characters (`\r\n`, `Environment.NewLine`, `\n`, `\r`) with spaces, condenses multiple consecutive spaces into single spaces using `MultiSpaces()`, and trims leading/trailing space.

#### `JoinLines(this string textToJoin, string joiningText, bool trimLineBeforeJoining, string textAtBeginning = "", string textAtEnd = "")`
* **Returns:** `string`
* Normalizes newlines, splits text into lines, optionally trims each line, joins them with `joiningText`, and wraps the result between `textAtBeginning` and `textAtEnd`.

#### `ToCamel(this string stringToCamel)`
* **Returns:** `string`
* Converts text to title/camel case by capitalizing letters that immediately follow whitespace, punctuation, or line breaks.

#### `DetermineToggleCase(string textToModify)`
* **Returns:** `CurrentCase` (`Upper`, `Lower`, `Camel`, or `Unknown`)
* Inspects letter casing across `textToModify`:
  * Returns `CurrentCase.Upper` if all letters are uppercase.
  * Returns `CurrentCase.Lower` if all letters are lowercase.
  * Returns `CurrentCase.Camel` if a mix of cases exists.
  * Returns `CurrentCase.Unknown` if string is null, empty, or whitespace.

#### `UnstackStrings(this string stringToUnstack, int numberOfColumns)`
* **Returns:** `string`
* Takes a newline-separated list of items and rearranges them into `numberOfColumns` tab-separated columns per row.

#### `UnstackGroups(this string stringGroupedToUnstack, int numberOfRows)`
* **Returns:** `string`
* Transposes a newline-separated list divided into vertical blocks by re-organizing it into `numberOfRows` rows with tab-separated values.

#### `RemoveDuplicateLines(this string stringToDeduplicate)`
* **Returns:** `string`
* Deduplicates lines while preserving the original line order of first occurrence.

#### `ShuffleLines(this string textToShuffle, Random? random = null)`
* **Returns:** `string`
* Randomly shuffles lines using the Fisher-Yates algorithm. Accepts an optional `Random` instance (defaults to `Random.Shared`).

#### `RemoveFromEachLine(this string stringToEdit, int numberOfChars, SpotInLine spotInLine)`
* **Returns:** `string`
* Trims `numberOfChars` from either `SpotInLine.Beginning` or `SpotInLine.End` of every line.

#### `AddCharsToEachLine(this string stringToEdit, string stringToAdd, SpotInLine spotInLine)`
* **Returns:** `string`
* Prepends (`SpotInLine.Beginning`) or appends (`SpotInLine.End`) `stringToAdd` to every non-empty line.

#### `LimitCharactersPerLine(this string stringToEdit, int characterLimit, SpotInLine spotInLine)`
* **Returns:** `string`
* Truncates every line to `characterLimit` characters, keeping content from either the `SpotInLine.Beginning` or `SpotInLine.End`.

---

### 4. Regular Expression Generation, Escaping, & Explanation

#### `ReplaceReservedCharacters(this string stringToClean)`
* **Returns:** `string`
* Replaces all characters present in `ReservedChars` with hyphens (`-`), then merges consecutive hyphens using `MultiDashes()`.

#### `EscapeSpecialRegexChars(this string stringToEscape, bool matchExactly)`
* **Returns:** `string`
* Escapes regex special characters defined in `specialCharList`. If `matchExactly` is `false`, asterisks (`*`) are converted to `.*` instead of `\*`.

#### `ExtractSimplePattern(this string stringToExtract, int precisionLevel = 3, bool ignoreCase = false)`
* **Returns:** `string`
* Generates a regular expression pattern matching `stringToExtract` according to precision levels 0 through 5:
  * **Level 0:** Returns `\S+` (matches non-whitespace sequence).
  * **Level 1:** Returns `\w+` (matches word character sequence).
  * **Level 2:** Replaces runs of letters/numbers with `\w{count}`, spaces with `\s`, and special characters with escaped characters.
  * **Level 3 (Default):** Replaces letter runs with `[A-Za-z]{count}`, digits with `\d{count}`, spaces with `\s`, and special chars with escaped characters.
  * **Level 4:** Matches individual character positions (escaped exact characters, spaces as `\s`, using `(?i)` flag).
  * **Level 5:** Returns `Regex.Escape(stringToExtract)` for exact match.

#### `ExplainRegexPattern(this string pattern)`
* **Returns:** `string`
* Generates a human-readable, line-by-line breakdown of standard regular expression components present in `pattern`, including:
  * Overall case sensitivity analysis (checking for `(?i)` and `(?-i)`).
  * Character classes (e.g., `\d`, `\s`, `\w`, `\S`, `\W`, `\D`, `\b`, `[...]`).
  * Quantifiers (`+`, `*`, `?`, `{n}`).
  * Grouping constructs and inline flags (`(?i)`, `(?-i)`).
  * Anchors (`^`, `$`) and wildcard `.`.

---

### 5. Validation, Counting, & Utility Methods

#### `IsValidEmailAddress(this string input)`
* **Returns:** `bool`
* Validates `input` against a compiled regular expression (`Email()`) matching standard email formatting rules.

#### `IsBasicLatin(this char c)`
* **Returns:** `bool`
* Returns `true` if character unicode point is within the Basic Latin range (`U+0000` to `U+007F`).

#### `EndsWithNewline(this string s)`
* **Returns:** `bool`
* Checks if `s` ends with a newline character matching `\n$`.

#### `RemoveNonWordChars(this string strIn)`
* **Returns:** `string`
* Strips all characters except word characters and whitespace (`[^\w\s]`). Includes a 5-second timeout safeguard (`RegexMatchTimeoutException`) returning `string.Empty` upon timeout.

#### `CountMatches(string text, string pattern)`
* **Returns:** `int`
* Counts non-overlapping ordinal occurrences of substring `pattern` inside `text`.

#### `CountRegexMatches(string text, string pattern)`
* **Returns:** `int`
* Returns the count of regex matches of `pattern` within `text` (multiline enabled). Returns `0` if `pattern` is invalid.

---

## Private Helper Methods & Source-Generated Regexes

### Helper Methods
* **`FindNearestLetterIndex(string input, int cursorPosition)`**: Scans outwards (left and right) from `cursorPosition` to find the nearest non-whitespace character.
* **`ShortenRegexPattern(this string pattern)`**: Optimizes generated regex strings by scanning for repeating sub-pattern chunks (lengths from 4 to `length / 3`) and replacing repetitions with quantifiers like `(chunk){count}`.
* **`Split(string str, int chunkSize)`**: Splits a string into uniform chunks of size `chunkSize`.

### Source-Generated Regexes (`[GeneratedRegex]`)
* **`NewlineRegex()`**: `(\r\n|\n|\r)` — Matches any standard line break sequence.
* **`NewlineEnding()`**: `\n$` — Matches a line feed at string termination.
* **`Email()`**: Email validation pattern.
* **`MultiSpaces()`**: `[ ]{2,}` — Matches two or more consecutive space characters.
* **`MultiDashes()`**: `-+` — Matches one or more consecutive hyphens.