# Technical Documentation: `Tests/TextSearchUtilitiesTests.cs`

## Overview

The `TextSearchUtilitiesTests.cs` file contains a suite of unit tests for the `TextSearchUtilities` class and related string extension methods located in the `Text_Grab.Utilities` namespace. 

Using the **xUnit** testing framework, this file validates:
- Search text validation rules (specifically handling whitespace).
- Regex creation logic for Find-and-Replace and Grab Frame search operations.
- Whitespace formatting for visual display representations.

---

## Metadata

- **File Path:** `Tests/TextSearchUtilitiesTests.cs`
- **Namespace:** `Tests`
- **Dependencies:**
  - `System.Text.RegularExpressions`
  - `Text_Grab.Utilities`

---

## Test Class: `TextSearchUtilitiesTests`

The class `TextSearchUtilitiesTests` contains five unit test methods, combining both parameterized tests (`[Theory]`) and standalone tests (`[Fact]`).

### Summary of Tested `TextSearchUtilities` Capabilities

Based strictly on the invocations in this test file, the suite covers the following methods:
1. `TextSearchUtilities.HasSearchText(string? searchText)`
2. `TextSearchUtilities.CreateFindAndReplaceSearchRegex(string pattern, bool usePatternMode, bool exactMatch)`
3. `TextSearchUtilities.CreateReplacementRegex(string pattern, bool exactMatch)`
4. `TextSearchUtilities.CreateGrabFrameSearchRegex(string pattern, bool exactMatch)`
5. `TextSearchUtilities.FormatMatchTextForDisplay(string input)`
6. `string.EscapeSpecialRegexChars(bool matchExactly)` (Extension method on `string`)

---

## Key Components and Test Methods

### 1. `HasSearchText_TreatsWhitespaceAsSearchableInput`

- **Attribute:** `[Theory]`
- **Parameters:** `string? searchText`, `bool expected`
- **Purpose:** Verifies whether `TextSearchUtilities.HasSearchText` correctly identifies valid search inputs, specifically treating whitespace characters as valid search content while rejecting `null` and empty strings.

#### Data Cases:
| Input (`searchText`) | Expected Result (`expected`) | Reason / Description |
| :--- | :--- | :--- |
| `null` | `false` | Null input is not searchable |
| `""` | `false` | Empty string is not searchable |
| `" "` | `true` | Single space is valid searchable input |
| `"  "` | `true` | Multiple spaces are valid searchable input |
| `"text"` | `true` | Standard text is valid searchable input |
| `"\t"` | `true` | Tab character is valid searchable input |
| `"\n"` | `true` | Line break is valid searchable input |

---

### 2. `CreateFindAndReplaceSearchRegex_MatchesLiteralDoubleSpaces`

- **Attribute:** `[Fact]`
- **Purpose:** Ensures `TextSearchUtilities.CreateFindAndReplaceSearchRegex` produces a `Regex` object that correctly matches literal consecutive spaces when non-pattern, non-exact options are used.
- **How It Works:**
  1. Escapes the search string `"  "` using `"  ".EscapeSpecialRegexChars(matchExactly: false)`.
  2. Calls `TextSearchUtilities.CreateFindAndReplaceSearchRegex` passing `usePatternMode: false` and `exactMatch: false`.
  3. Tests the resulting `Regex` against the target string `"alpha  beta"`.
  4. Asserts that `match.Success` is `true` and the matched value equals `"  "`.

---

### 3. `CreateReplacementRegex_CollapsesDoubleSpaces`

- **Attribute:** `[Fact]`
- **Purpose:** Validates that `TextSearchUtilities.CreateReplacementRegex` constructs a regex capable of performing string replacements (such as replacing double spaces with single spaces).
- **How It Works:**
  1. Generates a replacement `Regex` for double spaces (`"  "` escaped with `matchExactly: false`) using `exactMatch: false`.
  2. Replaces instances of `"  "` in `"alpha  beta  gamma"` with `" "`.
  3. Asserts that the output string equals `"alpha beta gamma"`.

---

### 4. `CreateGrabFrameSearchRegex_TreatsSpacesLiterally`

- **Attribute:** `[Fact]`
- **Purpose:** Verifies that `TextSearchUtilities.CreateGrabFrameSearchRegex` treats space characters literally within search patterns.
- **How It Works:**
  1. Creates a regex for the input pattern `"a b"` with `exactMatch: true`.
  2. Asserts that the regex matches `"a b"`.
  3. Asserts that the regex does **not** match `"ab"`.

---

### 5. `FormatMatchTextForDisplay_MakesWhitespaceMatchesVisible`

- **Attribute:** `[Theory]`
- **Parameters:** `string input`, `string expected`
- **Purpose:** Verifies that `TextSearchUtilities.FormatMatchTextForDisplay` converts invisible whitespace control characters into visible display symbols.

#### Data Cases:
| Input (`input`) | Expected Symbol (`expected`) | Visual Representation |
| :--- | :--- | :--- |
| `" "` | `"·"` | Middle dot for space |
| `"  "` | `"··"` | Double middle dots for two spaces |
| `"\t"` | `"⇥"` | Rightwards arrow to bar for tab |
| `"\n"` | `"⏎"` | Return symbol for newline |
| `"\r"` | `"␍"` | Symbol for carriage return |
| `"\r\n"` | `"⏎"` | Return symbol for combined CRLF |