# Technical Documentation Guide: `Tests/RecognizerExecutorTests.cs`

## Overview

The `RecognizerExecutorTests.cs` file contains unit tests written in xUnit for verifying the functionality of entity recognition, template parsing, placeholder substitution, and value resolution in the `Text_Grab` project. 

The tests cover three core components:
1. **`BuiltInRecognizer`**: Catalog management, retrieval by ID or name, and case-insensitivity.
2. **`RecognizerExecutor`**: Match detection (`HasMatch`), entity extraction (`GetMatches`), execution modes (`ApplyRecognizer`), and normalization formatting (`FormatResolvedValue`).
3. **`GrabTemplateExecutor`**: Recognizer placeholder parsing (`ParseRecognizerMatchesFromOutputTemplate`) and placeholder replacement within templates (`ApplyRecognizerPlaceholders`, `ApplyTextOnlyTemplate`).

---

## Class Setup & Helper Methods

The `RecognizerExecutorTests` class defines two private static helper methods to streamline test setup:

### `Get(string id)`
* **Signature:** `private static BuiltInRecognizer Get(string id)`
* **Purpose:** Retrieves a `BuiltInRecognizer` instance by its ID using `BuiltInRecognizer.GetById(id)`.
* **Behavior:** Throws an `InvalidOperationException` with the message `missing recognizer {id}` if the recognizer is not found.

### `ResultWith(string text, params (string Key, object Value)[] resolution)`
* **Signature:** `private static ModelResult ResultWith(string text, params (string Key, object Value)[] resolution)`
* **Purpose:** Constructs a mock `ModelResult` instance for testing resolution formatting without invoking external recognizer logic.
* **Behavior:** Sets `Text` to the provided string, sets `Start` to `0`, `End` to `text.Length - 1`, and populates a `SortedDictionary<string, object>` resolution map with the key-value tuples provided.

---

## Test Categories and Test Methods

### 1. BuiltInRecognizer Catalog

Tests the discovery and lookup mechanisms for built-in text recognizers.

* **`GetAll_ReturnsFullCatalog()`**
  * Verifies that `BuiltInRecognizer.GetAll()` returns a complete catalog containing exactly 14 recognizers.
* **`GetById_And_GetByName_AreCaseInsensitive()`**
  * Verifies case-insensitive lookup:
    * `GetById("NUMBER")` returns a non-null object.
    * `GetByName("date / time")` returns a non-null object.
  * Verifies that invalid IDs or names (`"does-not-exist"`) return `null`.

---

### 2. Match Detection and Span Reporting

Tests basic entity extraction using `RecognizerExecutor.GetMatches` and presence checking using `RecognizerExecutor.HasMatch`.

* **`GetMatches_Number_FindsAllNumbersWithResolvedValues()`**
  * Calls `RecognizerExecutor.GetMatches` with the `"number"` recognizer on input `"I have 25 apples and 3.5 kg"`.
  * Verifies that 2 matches are found with corresponding texts and resolved values (`"25"` and `"3.5"`).
* **`GetMatches_ReportsCorrectSpan()`**
  * Verifies positional metadata for an `"email"` recognizer match in `"write to a@b.com please"`.
  * Checks that `Text` is `"a@b.com"`, `Start` index is `9`, and `Length` equals `7`.
* **`HasMatch_TrueWhenEntityPresent_FalseOtherwise()`**
  * Verifies that `RecognizerExecutor.HasMatch` returns `true` when an email address is present in `"reach me at a@b.com"` and `false` when no email address is present in `"no address here"`.
* **`GetMatches_EmptyText_ReturnsEmpty()`**
  * Asserts that passing an empty string `string.Empty` to `GetMatches` returns an empty list.

---

### 3. Match Execution Modes and Formatting

Tests `RecognizerExecutor.ApplyRecognizer` across different modes, custom separators, and fallback scenarios.

* **`ApplyRecognizer_All_JoinsWithSeparator()`**
  * Tests mode `"all"`. Joining matches `"25"` and `"3.5"` produces `"25, 3.5"`.
* **`ApplyRecognizer_First_ReturnsFirst()`**
  * Tests mode `"first"`. Returns only the first matched number `"25"`.
* **`ApplyRecognizer_Last_ReturnsLast()`**
  * Tests mode `"last"`. Returns only the last matched number `"3.5"`.
* **`ApplyRecognizer_NthIndex_ReturnsThatMatch()`**
  * Tests 1-based index selection using mode `"2"`. Returns the second matched number `"3.5"`.
* **`ApplyRecognizer_CustomSeparator_IsUsed()`**
  * Tests custom separator `" | "` with mode `"all"`. Formats output as `"1 | 2"`.
* **`ApplyRecognizer_NoMatch_ReturnsEmpty()`**
  * Asserts that `ApplyRecognizer` returns `string.Empty` when no entities are found.

---

### 4. Output Kind Modes

Tests `RecognizerOutputKind` options when running `ApplyRecognizer`.

* **`ApplyRecognizer_ResolvedValue_NormalizesCurrency()`**
  * Uses `RecognizerOutputKind.ResolvedValue` with the `"currency"` recognizer on `"it costs $5"`.
  * Asserts that the value is normalized from `"$5"` to `"5 Dollar"`.
* **`ApplyRecognizer_MatchedText_KeepsOriginalSpan()`**
  * Uses `RecognizerOutputKind.MatchedText` with the `"currency"` recognizer on `"it costs $5"`.
  * Asserts that the output retains the exact matched string `"$5"`.

---

### 5. Template Placeholder Substitution

Tests `GrabTemplateExecutor.ApplyRecognizerPlaceholders` to ensure template placeholders formatted as `{r:...}` are substituted correctly.

* **`ApplyRecognizerPlaceholders_AllMatches_Substitutes()`**
  * Evaluates `"Found {r:Number:all}"` against `"1 2 3"`.
  * Expects output `"Found 1, 2, 3"`.
* **`ApplyRecognizerPlaceholders_TextOutput_UsesMatchedText()`**
  * Evaluates `"{r:Currency:first:text}"` against `"it costs $5"`.
  * Expects output `"$5"`.
* **`ApplyRecognizerPlaceholders_UnknownRecognizer_LeavesPlaceholder()`**
  * Evaluates `"{r:Nope:first}"` against input containing numbers.
  * Asserts that an unknown recognizer leaves the placeholder untouched (`"{r:Nope:first}"`).
* **`ApplyRecognizerPlaceholders_LeavesPatternPlaceholdersUntouched()`**
  * Evaluates `"{p:Email:first} {r:Number:first}"` against `"value 5"`.
  * Asserts that pattern placeholders (`{p:...}`) are ignored by the recognizer pass, resulting in `"{p:Email:first} 5"`.

---

### 6. Template Parsing

Tests `GrabTemplateExecutor.ParseRecognizerMatchesFromOutputTemplate` for extracting structured match rules from raw placeholder strings.

* **`ParseRecognizerMatches_ExtractsModeAndOutputKind()`**
  * Parses placeholder `"{r:Number:all:text}"`.
  * Verifies extracted properties:
    * `RecognizerName`: `"Number"`
    * `MatchMode`: `"all"`
    * `OutputKind`: `RecognizerOutputKind.MatchedText`
    * `RecognizerId`: Matches `Get("number").Id`
* **`ParseRecognizerMatches_WithSeparator_ParsesValueOutputAndSeparator()`**
  * Parses placeholder `"{r:Number:all:value:; }"`.
  * Verifies extracted properties:
    * `MatchMode`: `"all"`
    * `OutputKind`: `RecognizerOutputKind.ResolvedValue`
    * `Separator`: `"; "`

---

### 7. Resolution Format Handling (`FormatResolvedValue`)

Tests `RecognizerExecutor.FormatResolvedValue` against various Microsoft Recognizers resolution data structures to ensure compatibility and guard against breaking type changes.

| Test Method | Input Resolution Data Shape | Expected Output |
| :--- | :--- | :--- |
| **`FormatResolvedValue_ValuesAsStringDictionaries_ReadsValue`** | `"values"` = `List<Dictionary<string, string>>` with key `["value"] = "2026-01-15"` | `"2026-01-15"` |
| **`FormatResolvedValue_ValuesAsObjectDictionaries_StillReadsValue`** | `"values"` = `List<Dictionary<string, object>>` with key `["value"] = "2026-01-20"` | `"2026-01-20"` |
| **`FormatResolvedValue_ValuesWithStartAndEnd_FormatsRange`** | `"values"` dictionary containing `["start"] = "2026-01-01"` and `["end"] = "2026-01-05"` | `"2026-01-01 → 2026-01-05"` |
| **`FormatResolvedValue_ValuesWithOnlyTimex_FallsBackToTimex`** | `"values"` dictionary containing only `["timex"] = "XXXX-WXX-1"` | `"XXXX-WXX-1"` |
| **`FormatResolvedValue_NotResolvedValue_FallsBackToText`** | `"values"` dictionary containing `["value"] = "not resolved"` | Matched text fallback (`"someday"`) |
| **`FormatResolvedValue_ValueAndUnit_JoinsWithSpace`** | Dictionary root containing `("value", "5")` and `("unit", "Dollar")` | `"5 Dollar"` |
| **`FormatResolvedValue_EmptyResolution_ReturnsText`** | Empty resolution dictionary | Matched text fallback (`"plain text"`) |

---

### 8. Live DateTime Recognizer Integration

Tests end-to-end match resolution against the live `datetime` recognizer implementation provided by the underlying library.

* **`GetMatches_DateTime_ResolvesAbsoluteDate()`**
  * Input text: `"meeting on 2026-01-15"`.
  * Asserts `ResolvedValue` produces `"2026-01-15"`.
* **`GetMatches_DateTime_ResolvesDateRange()`**
  * Input text: `"from 2026-01-01 to 2026-01-05"`.
  * Asserts `ResolvedValue` produces `"2026-01-01 → 2026-01-05"`.

---

### 9. GrabTemplate Execution

Tests high-level template execution using recognizer placeholders.

* **`ApplyTextOnlyTemplate_RecognizerPlaceholder_Resolves()`**
  * Constructs a `GrabTemplate` with name `"Numbers"` and `OutputTemplate = "Numbers: {r:Number:all}"`.
  * Calls `GrabTemplateExecutor.ApplyTextOnlyTemplate(template, "got 1 and 2")`.
  * Asserts the returned string is `"Numbers: 1, 2"`.

---

## Key Dependency References

* **`Microsoft.Recognizers.Text`**: Third-party framework providing underlying model result objects (`ModelResult`).
* **`Text_Grab.Models`**: Provides data models including `GrabTemplate`, `TemplateRecognizerMatch`, `RecognizerMatch`, and `RecognizerOutputKind`.
* **`Text_Grab.Utilities`**: Exposes static execution utilities including `BuiltInRecognizer`, `RecognizerExecutor`, and `GrabTemplateExecutor`.