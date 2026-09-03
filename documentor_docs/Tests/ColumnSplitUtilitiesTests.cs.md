# Technical Documentation: `ColumnSplitUtilitiesTests.cs`

## Overview

The `ColumnSplitUtilitiesTests` class contains xUnit unit tests for the `ColumnSplitUtilities` class—specifically testing its `SplitCell` method. This test suite verifies how cell strings are split into collections of substrings (`IReadOnlyList<string>`) based on various configurable options supplied via the `SplitColumnOptions` model.

---

## File Details

- **File Path:** `Tests/ColumnSplitUtilitiesTests.cs`
- **Namespace:** `Tests`
- **Dependencies:**
  - `Text_Grab.Models`
  - `Text_Grab.Utilities`
  - xUnit (`[Fact]`, `Assert`)

---

## Data Structures & Models Tested

The tests interact directly with the following classes and options configured in `SplitColumnOptions`:

### Enums & Properties Used
* **`SplitMode`**:
  * `SplitMode.Delimiter`: Splits strings based on literal character sequences.
  * `SplitMode.Regex`: Splits strings based on regular expression patterns.
  * `SplitMode.FixedLength`: Splits strings at specific character index thresholds.
* **`SplitterHandling`**:
  * `SplitterHandling.KeepLeft`: Retains the delimiter or regex match attached to the right side of the left substring.
  * `SplitterHandling.KeepRight`: Retains the delimiter or regex match attached to the left side of the right substring.
* **`SplitColumnOptions` Configuration Properties**:
  * `Mode`: Specifies the `SplitMode`.
  * `DelimiterText`: The literal string used for splitting in `Delimiter` mode.
  * `Pattern`: A raw regex string used in `Regex` mode.
  * `PatternItem`: A `PatternItem` object containing a `StoredRegex`, which takes precedence over raw `Pattern`.
  * `IgnoreCase`: Boolean flag to enable case-insensitive matching in regex splits.
  * `Length`: Integer length parameter for `FixedLength` splits.
  * `SplitFromEnd`: Boolean flag to indicate whether fixed-length splitting starts from the end of the string.
  * `SplitterHandling`: Determines how the splitter character/match is retained or discarded.

---

## Test Scenarios Breakdown

The unit tests in `ColumnSplitUtilitiesTests` are divided into six functional behavior categories:

### 1. Delimiter-Based Splitting (`SplitMode.Delimiter`)

* **`SplitCell_Delimiter_SplitsOnLiteralString`**
  * **Input:** `"John Smith"`
  * **Options:** `DelimiterText = " "`
  * **Expected Output:** `["John", "Smith"]`
  * **Behavior:** Splits string on literal space characters.

* **`SplitCell_Delimiter_MultiCharacterDelimiter`**
  * **Input:** `"a, b, c"`
  * **Options:** `DelimiterText = ", "`
  * **Expected Output:** `["a", "b", "c"]`
  * **Behavior:** Handles multi-character literal delimiters.

* **`SplitCell_Delimiter_EmptyDelimiterReturnsWholeValue`**
  * **Input:** `"John Smith"`
  * **Options:** `DelimiterText = ""`
  * **Expected Output:** `["John Smith"]`
  * **Behavior:** An empty delimiter string returns the original input string as a single element.

---

### 2. Regular Expression Splitting (`SplitMode.Regex`)

* **`SplitCell_Regex_SplitsOnPattern`**
  * **Input:** `"ABC - 123 - XY"`
  * **Options:** `Pattern = @"\s*-\s*"`
  * **Expected Output:** `["ABC", "123", "XY"]`
  * **Behavior:** Splits based on regex matching zero or more whitespace characters around a hyphen.

* **`SplitCell_Regex_InvalidPatternReturnsWholeValue`**
  * **Input:** `"anything"`
  * **Options:** `Pattern = "("` (Unbalanced parenthesis/invalid regex)
  * **Expected Output:** `["anything"]`
  * **Behavior:** Invalid regex patterns gracefully fail over and return the original input value wrapped in a list.

* **`SplitCell_Regex_IgnoreCaseSplitsOnLetterRegardlessOfCase`**
  * **Input:** `"aXbxc"`
  * **Options:** `Pattern = "x"`, `IgnoreCase = true`
  * **Expected Output:** `["a", "b", "c"]`
  * **Behavior:** Setting `IgnoreCase = true` allows matching both upper and lowercase variants of pattern characters.

---

### 3. Fixed-Length Splitting (`SplitMode.FixedLength`)

* **`SplitCell_FixedLength_SplitsFromStart`**
  * **Input:** `"ABC12345"`
  * **Options:** `Length = 3`, default `SplitFromEnd = false`
  * **Expected Output:** `["ABC", "12345"]`
  * **Behavior:** Extracts the first 3 characters into the first segment and puts the remaining characters into the second segment.

* **`SplitCell_FixedLength_SplitsFromEnd`**
  * **Input:** `"ABC12345"`
  * **Options:** `Length = 3`, `SplitFromEnd = true`
  * **Expected Output:** `["ABC12", "345"]`
  * **Behavior:** Measures 3 characters from the end of the string to split into segments.

* **`SplitCell_FixedLength_LengthBeyondValueClampsToWhole`**
  * **Input:** `"short"`
  * **Options:** `Length = 100`
  * **Expected Output:** `["short", ""]`
  * **Behavior:** If the configured split length exceeds the input string length, the full string is returned as the first element and an empty string is returned as the second element.

---

### 4. Splitter Retention / Handling (`SplitterHandling`)

* **`SplitCell_Delimiter_KeepLeft_AttachesSplitterToLeftPart`**
  * **Input:** `"20.30"`
  * **Options:** `DelimiterText = "."`, `SplitterHandling = SplitterHandling.KeepLeft`
  * **Expected Output:** `["20.", "30"]`
  * **Behavior:** Attaches the delimiter character to the end of the left split segment.

* **`SplitCell_Delimiter_KeepRight_AttachesSplitterToRightPart`**
  * **Input:** `"20.30"`
  * **Options:** `DelimiterText = "."`, `SplitterHandling = SplitterHandling.KeepRight`
  * **Expected Output:** `["20", ".30"]`
  * **Behavior:** Attaches the delimiter character to the beginning of the right split segment.

* **`SplitCell_Delimiter_KeepLeft_MultipleSplitters`**
  * **Input:** `"a.b.c"`
  * **Options:** `DelimiterText = "."`, `SplitterHandling = SplitterHandling.KeepLeft`
  * **Expected Output:** `["a.", "b.", "c"]`
  * **Behavior:** Applies `KeepLeft` across all occurrences of the delimiter.

* **`SplitCell_Regex_KeepRight_AttachesMatchToRightPart`**
  * **Input:** `"a-b-c"`
  * **Options:** `Pattern = "-"`, `SplitterHandling = SplitterHandling.KeepRight`
  * **Expected Output:** `["a", "-b", "-c"]`
  * **Behavior:** Applies `KeepRight` handling when using regex matching patterns.

---

### 5. Stored Pattern Item Splitting (`PatternItem`)

* **`SplitCell_PatternItem_SavedRegex_SplitsOnMatchedSpans`**
  * **Input:** `"a #FFFFFF b #000000 c"`
  * **Options:** `PatternItem = PatternItem(StoredRegex("Hex", @"#[0-9a-fA-F]{6}"))`
  * **Expected Output:** `["a ", " b ", " c"]`
  * **Behavior:** Splits string by removing matches corresponding to predefined stored regular expression objects (e.g., Hex color codes).

* **`SplitCell_PatternItem_NoMatchReturnsWholeValue`**
  * **Input:** `"no colors here"`
  * **Options:** `PatternItem = PatternItem(StoredRegex("Hex", @"#[0-9a-fA-F]{6}"))`
  * **Expected Output:** `["no colors here"]`
  * **Behavior:** If no matches are found for the `PatternItem`, the intact input string is returned.

* **`SplitCell_PatternItem_TakesPrecedenceOverRawPattern`**
  * **Input:** `"a #FFFFFF b"`
  * **Options:** `PatternItem = PatternItem(StoredRegex("Hex", @"#[0-9a-fA-F]{6}"))`, `Pattern = " "`
  * **Expected Output:** `["a ", " b"]`
  * **Behavior:** Confirms `PatternItem` has explicit evaluation priority over the raw `Pattern` property if both are supplied.

---

### 6. Edge Cases & Null Handling

* **`SplitCell_NullValueTreatedAsEmpty`**
  * **Input:** `null`
  * **Options:** `DelimiterText = ","`
  * **Expected Output:** `[""]`
  * **Behavior:** Passing a null string into `ColumnSplitUtilities.SplitCell` returns a collection containing a single empty string (`""`).