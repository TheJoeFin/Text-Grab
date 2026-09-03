# Developer Documentation: `Tests/PatternExecutorTests.cs`

## Overview

The `PatternExecutorTests` class provides xUnit unit tests for validating the behavior of `PatternItem` catalog retrieval and `PatternExecutor` execution logic within the application. It verifies how pattern matching operates across both saved Regular Expression items (`PatternKind.SavedRegex`) and built-in entity recognizers (`PatternKind.Recognizer`).

---

## File Location & Namespace

* **File Path:** `Tests/PatternExecutorTests.cs`
* **Namespace:** `Tests`
* **Imports:**
  * `Text_Grab.Models`
  * `Text_Grab.Utilities`

---

## Private Helper Methods

The test class includes two private helper methods to generate standardized test inputs without depending on external configuration or machine state:

1. **`SavedEmail()`**
   * **Returns:** `PatternItem`
   * **Description:** Instantiates a deterministic, saved regex `PatternItem` backed by a `StoredRegex` configured with the name `"Email Address"`, the regex pattern `\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b`, and `true` (presumably indicating active/enabled state).
2. **`RecognizerByName(string name)`**
   * **Returns:** `PatternItem`
   * **Description:** Looks up a `BuiltInRecognizer` by name using `BuiltInRecognizer.GetByName(name)` and wraps it in a `PatternItem`. Throws an `InvalidOperationException` if the specified recognizer does not exist.

---

## Test Categories & Test Methods

The test suite is structured into three main functional categories:

### 1. `PatternItem` Catalog Tests

These tests check the population, ordering, and retrieval methods of `PatternItem`.

* **`GetAll_ListsSavedRegexesBeforeRecognizers()`**
  * **Objective:** Verifies that `PatternItem.GetAll()` sorts results such that all items with `PatternKind.SavedRegex` appear in the list before any items with `PatternKind.Recognizer`.
  * **Assertions:** Confirms at least one recognizer exists and that the index of the last saved regex is strictly less than the index of the first recognizer.

* **`GetAll_IncludesEveryRecognizerWithSmartGroup()`**
  * **Objective:** Ensures that every recognizer provided by `BuiltInRecognizer.GetAll()` is present as a `PatternItem` in `PatternItem.GetAll()`, and that every recognizer `PatternItem` has its `GroupLabel` set to `PatternItem.SmartGroup`.
  * **Assertions:** Asserts equal counts between `BuiltInRecognizer.GetAll()` and filtered recognizer items, and asserts `GroupLabel == PatternItem.SmartGroup` across all of them.

* **`GetByName_FindsRecognizer_CaseInsensitive()`**
  * **Objective:** Tests that `PatternItem.GetByName(string)` can locate built-in recognizers regardless of string casing (e.g., passing `"EMAIL"`).
  * **Assertions:** Asserts that the returned item is not null and has `Kind == PatternKind.Recognizer`.

---

### 2. `PatternExecutor` – Recognizer-Backed Tests

These tests validate `PatternExecutor` functions when executing built-in recognizers (`PatternKind.Recognizer`).

* **`HasMatch_Recognizer_DetectsEntity()`**
  * **Objective:** Asserts that `PatternExecutor.HasMatch` correctly returns `true` when the target entity exists within the input string and `false` when absent.
  * **Example Tested:** Searching for "Email" recognizer in `"reach me at a@b.com"` (returns `true`) vs. `"no address here"` (returns `false`).

* **`Apply_Recognizer_NormalizesCurrencyResolvedValue()`**
  * **Objective:** Tests `PatternExecutor.Apply` when requesting `RecognizerOutputKind.ResolvedValue`.
  * **Example Tested:** Passing `"it costs $5"` to the "Currency" recognizer yields `"5 Dollar"`.

* **`Apply_Recognizer_MatchedText_KeepsOriginalSpan()`**
  * **Objective:** Tests `PatternExecutor.Apply` when requesting `RecognizerOutputKind.MatchedText`.
  * **Example Tested:** Passing `"it costs $5"` to the "Currency" recognizer yields the exact matched substring `"$5"`.

---

### 3. `PatternExecutor` – Saved-Regex-Backed Tests

These tests validate `PatternExecutor` execution when backed by regular expressions (`PatternKind.SavedRegex`).

* **`GetMatches_SavedRegex_ReportsSpanAndMatchedText()`**
  * **Objective:** Asserts that `PatternExecutor.GetMatches` returns a `RecognizerMatch` containing the correct matched text, resolved value, start index, and match length.
  * **Details Tested:** Input `"write to a@b.com please"` yields a match at index `9`, length `7`, text `"a@b.com"`, and resolved value `"a@b.com"` (regex matches set resolved value equal to matched text).

* **`HasMatch_SavedRegex_TrueWhenPresent()`**
  * **Objective:** Tests `PatternExecutor.HasMatch` returning `true` if a regex match exists in the input text and `false` if missing.

* **`Apply_SavedRegex_All_JoinsMatchedTextWithSeparator()`**
  * **Objective:** Tests `PatternExecutor.Apply` using the `"all"` extraction mode, confirming that multiple matches are joined using the default separator (`", "`).
  * **Details Tested:** `"a@b.com and c@d.org"` yields `"a@b.com, c@d.org"`.

* **`Apply_SavedRegex_RespectsModeAndSeparator()`**
  * **Objective:** Tests index/selection modes (`"first"`, `"last"`, `"2"`) and custom separators when running `PatternExecutor.Apply`.
  * **Details Tested:**
    * Mode `"first"` -> `"a@b.com"`
    * Mode `"last"` -> `"c@d.org"`
    * Mode `"2"` (1-based index) -> `"c@d.org"`
    * Mode `"all"` with custom separator `" | "` -> `"a@b.com | c@d.org"`

* **`Apply_SavedRegex_NoMatch_ReturnsEmpty()`**
  * **Objective:** Asserts that `PatternExecutor.Apply` returns `string.Empty` when no regex matches are found in the input text.

* **`GetMatches_InvalidRegex_ReturnsEmpty_DoesNotThrow()`**
  * **Objective:** Confirms error handling resilience. If a `StoredRegex` contains an invalid/unclosed regular expression (e.g., `"([unclosed"`), `PatternExecutor.GetMatches` returns an empty collection instead of throwing an exception.

* **`GetMatches_EmptyText_ReturnsEmpty()`**
  * **Objective:** Confirms that passing `string.Empty` as input to `PatternExecutor.GetMatches` returns an empty collection for both saved regexes and built-in recognizers.

---

## Interacting Types and Dependencies Referenced in Code

* **`PatternItem`**: Represents a selectable pattern item. Exposes methods like `GetAll()` and `GetByName()`, properties like `Kind` and `GroupLabel`, and static field/property `SmartGroup`.
* **`PatternKind`**: Enum indicating the backing pattern type (`SavedRegex` or `Recognizer`).
* **`StoredRegex`**: Model class holding saved regular expression definitions (Name, Pattern string, and active state).
* **`BuiltInRecognizer`**: Utility class providing built-in recognizers via `GetAll()` and `GetByName()`.
* **`PatternExecutor`**: The core execution utility class providing methods:
  * `HasMatch(PatternItem item, string text)`
  * `GetMatches(PatternItem item, string text)`
  * `Apply(PatternItem item, string text, string mode, string separator = ..., RecognizerOutputKind outputKind = ...)`
* **`RecognizerMatch`**: Result object holding properties `Text`, `ResolvedValue`, `Start`, and `Length`.
* **`RecognizerOutputKind`**: Enum controlling output text format (`ResolvedValue` vs. `MatchedText`).