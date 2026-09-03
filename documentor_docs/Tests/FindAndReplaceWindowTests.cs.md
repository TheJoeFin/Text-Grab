# Technical Documentation: `Tests/FindAndReplaceWindowTests.cs`

## Overview

The `FindAndReplaceWindowTests` class contains unit tests written in xUnit to verify the functionality of methods in `FindAndReplaceWindow` (from the `Text_Grab.Controls` namespace). 

Specifically, this test suite ensures that:
1. `FindAndReplaceWindow.GetMatchTextForEditing` correctly constructs an editable text string from a collection of search results while preserving raw whitespace.
2. `FindAndReplaceWindow.ResolveSearchSourceText` correctly selects the appropriate source text depending on editor state, cached text, and search mode flags.

---

## File Details

* **File Path:** `Tests/FindAndReplaceWindowTests.cs`
* **Namespace:** `Tests`
* **Test Class:** `FindAndReplaceWindowTests`
* **Dependencies:**
  * `Text_Grab.Controls`
  * `Text_Grab.Models`
  * xUnit testing framework (`Fact`, `Theory`, `InlineData`, `Assert`)

---

## Test Methods

### 1. `GetMatchTextForEditing_PreservesRawWhitespace()`

* **Type:** Unit Test (`[Fact]`)
* **Target Method:** `FindAndReplaceWindow.GetMatchTextForEditing(List<FindResult> results)`

#### Purpose
Verifies that `GetMatchTextForEditing` constructs a newline-delimited string using the `RawText` property of each `FindResult` object in the provided list, ensuring visual substitution characters (such as dots `·`, arrows `⇥`, or newline symbols `⏎`) are not used in place of actual raw whitespace.

#### Test Execution Logic
1. **Setup:** Initializes a list of `FindResult` objects with visual representations in `Text` and raw string representations in `RawText`:
   * Result 1: `Text = "word·word"`, `RawText = "word word"`
   * Result 2: `Text = "line⏎break"`, `RawText = "line\r\nbreak"` (or system `Environment.NewLine`)
   * Result 3: `Text = "tab⇥value"`, `RawText = "tab\tvalue"`
2. **Action:** Invokes `FindAndReplaceWindow.GetMatchTextForEditing(results)`.
3. **Assertion:** Asserts that the returned string joins the `RawText` values using `Environment.NewLine`:
   ```text
   word word
   line
   break
   tab	value
   ```

---

### 2. `ResolveSearchSourceText_UsesCurrentEditorTextOutsideSpreadsheetMode(...)`

* **Type:** Parameterized Unit Test (`[Theory]`)
* **Target Method:** `FindAndReplaceWindow.ResolveSearchSourceText(string cachedText, string? editorText, bool isSpreadsheetSearch)`

#### Purpose
Verifies the text selection priority rules implemented in `ResolveSearchSourceText` under different combinations of cached text, current editor text, and search mode settings (spreadsheet search vs. non-spreadsheet search).

#### Test Parameters
* `cachedText` (`string`): Previously cached text.
* `editorText` (`string?`): Current text present in the editor (can be `null`).
* `isSpreadsheetSearch` (`bool`): Flag indicating if spreadsheet mode is enabled.
* `expected` (`string`): Expected resolved text output.

#### Test Data Matrix (`[InlineData]`)

| `cachedText` | `editorText` | `isSpreadsheetSearch` | `expected` | Description / Rule Verified |
| :--- | :--- | :--- | :--- | :--- |
| `"old value"` | `"new value"` | `false` | `"new value"` | When not in spreadsheet search mode, the current editor text takes priority over cached text. |
| `"old value"` | `"new value"` | `true` | `"old value"` | When spreadsheet search mode is enabled (`true`), cached text is used instead of editor text. |
| `"cached"` | `null` | `false` | `"cached"` | When not in spreadsheet search mode but editor text is `null`, it falls back to `cachedText`. |

#### Test Execution Logic
1. **Action:** Calls `FindAndReplaceWindow.ResolveSearchSourceText(cachedText, editorText, isSpreadsheetSearch)` with the inline dataset.
2. **Assertion:** Asserts that the returned string matches `expected`.