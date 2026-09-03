# Documentation: `Tests/ResultTableManualSeparatorTests.cs`

## Overview

The `ResultTableManualSeparatorTests` class contains unit tests written for the `Text_Grab` project. Its primary purpose is to verify the behavior of manual row and column separators when parsing bounding boxes (`WordBorderInfo`) into structured tabular text using the `ResultTable` model.

Specifically, these tests ensure that passing explicit manual separator coordinates (`manualRowSeparators` and `manualColumnSeparators`) into `ResultTable.AnalyzeAsTable(...)` correctly overrides automatic grouping, splitting text across lines or columns as expected.

---

## Technical Details & Dependencies

* **Namespace:** `Tests`
* **Test Framework:** `xUnit` (using `[WpfFact]` attributes for WPF-compatible test execution)
* **Namespaces Used:**
  * `System.Drawing` (for `Rectangle`)
  * `System.Text` (for `StringBuilder`)
  * `System.Windows` (for `Rect`)
  * `Text_Grab.Models` (for `WordBorderInfo` and `ResultTable`)

---

## Component Breakdown

### 1. Helper Method

#### `CreateWord(string word, double left, double top, double width, double height)`
* **Type:** `private static WordBorderInfo`
* **Description:** A utility function used to instantiate a `WordBorderInfo` object with a specified text string and rectangular dimensions.
* **Parameters:**
  * `word` (`string`): The text content of the word.
  * `left` (`double`): X-coordinate of the top-left corner.
  * `top` (`double`): Y-coordinate of the top-left corner.
  * `width` (`double`): Width of the bounding box.
  * `height` (`double`): Height of the bounding box.
* **Return Value:** A `WordBorderInfo` object with the `Word` and `BorderRect` properties initialized.

---

### 2. Test Methods

#### `AnalyzeAsTable_ManualRowSeparatorSplitsMergedRowOutput()`
* **Attribute:** `[WpfFact]`
* **Purpose:** Verifies that a manual row separator splits two closely positioned words into separate rows when automatic analysis merges them onto a single line.
* **Test Flow:**
  1. **Data Setup:** Creates two word borders ("Top" at `top: 10` and "Bottom" at `top: 17`).
  2. **Automatic Table Analysis:**
     * Calls `AnalyzeAsTable` without manual separators.
     * Evaluates text output via `ResultTable.GetTextFromTabledWordBorders`.
     * **Assertion:** Verifies automatic output merges the words into `"Top Bottom"`.
  3. **Manual Table Analysis:**
     * Re-creates the word borders.
     * Calls `AnalyzeAsTable` with `manualRowSeparators: [18d]`.
     * Evaluates text output via `ResultTable.GetTextFromTabledWordBorders`.
  4. **Assertions:**
     * Verifies the output is split by a line break: `$"Top{Environment.NewLine}Bottom"`.
     * Verifies `manualTable.ManualRowSeparators` contains `[18d]`.

---

#### `AnalyzeAsTable_ManualColumnSeparatorSplitsMergedColumnOutput()`
* **Attribute:** `[WpfFact]`
* **Purpose:** Verifies that a manual column separator enforces column separation (tab formatting) between words horizontally positioned across the boundary line.
* **Test Flow:**
  1. **Data Setup:** Creates four word borders forming a 2x2 grid ("LeftTop", "RightTop", "LeftBottom", "RightBottom").
  2. **Automatic Table Analysis:**
     * Calls `AnalyzeAsTable` without manual separators.
     * Evaluates text output via `ResultTable.GetTextFromTabledWordBorders`.
     * **Assertion:** Verifies automatic output treats columns with space separation: `$"LeftTop RightTop{Environment.NewLine}LeftBottom RightBottom"`.
  3. **Manual Table Analysis:**
     * Re-creates the four word borders.
     * Calls `AnalyzeAsTable` with `manualColumnSeparators: [25d]`.
     * Evaluates text output via `ResultTable.GetTextFromTabledWordBorders`.
  4. **Assertions:**
     * Verifies output uses tab separators (`\t`) between columns: `$"LeftTop\tRightTop{Environment.NewLine}LeftBottom\tRightBottom"`.
     * Verifies `manualTable.ManualColumnSeparators` contains `[25d]`.

---

## Summary of Expectations Tested

| Test Method | Input Separators | Expected Format Output | Verified Property |
| :--- | :--- | :--- | :--- |
| `AnalyzeAsTable_ManualRowSeparatorSplitsMergedRowOutput` | `manualRowSeparators: [18d]` | Words split into lines (`Top\r\nBottom`) | `manualTable.ManualRowSeparators` |
| `AnalyzeAsTable_ManualColumnSeparatorSplitsMergedColumnOutput` | `manualColumnSeparators: [25d]` | Words separated by tabs (`LeftTop\tRightTop...`) | `manualTable.ManualColumnSeparators` |