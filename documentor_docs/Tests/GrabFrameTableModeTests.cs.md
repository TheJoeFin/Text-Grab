# Documentation: `Tests/GrabFrameTableModeTests.cs`

## Overview

The `GrabFrameTableModeTests` class contains unit tests designed to validate the helper/decision logic in `GrabFrame` related to **Table Mode** functionality and **Word Border Merging**. It uses the xUnit testing framework to verify that `GrabFrame` static methods make correct decisions given various input states, such as selected border counts, paragraph detection settings, PDF text flags, and language models.

---

## File Details

* **File Path:** `Tests/GrabFrameTableModeTests.cs`
* **Namespace:** `Tests`
* **Target Class Under Test:** `Text_Grab.Views.GrabFrame`
* **Dependencies:**
  * `Text_Grab.Models` (Provides `GlobalLang`, `UiAutomationLang`, etc.)
  * `Text_Grab.Views` (Provides `GrabFrame`)
  * `xUnit` (Testing framework providing `[Theory]`, `[Fact]`, `[InlineData]`, and `Assert`)

---

## Test Methods

### 1. `ShouldAllowWordBorderMerging_RequiresMultipleSelectedWordBorders`

* **Attribute:** `[Theory]`
* **Purpose:** Verifies that word border merging (`GrabFrame.ShouldAllowWordBorderMerging`) is only permitted when two or more word borders are selected.

#### Parameters
* `selectedWordBorderCount` (`int`): The number of currently selected word borders.
* `expected` (`bool`): The expected boolean result returned by `GrabFrame.ShouldAllowWordBorderMerging`.

#### Test Matrix (`[InlineData]`)
| `selectedWordBorderCount` | `expected` | Description |
| :--- | :--- | :--- |
| `2` | `true` | Merging is allowed when 2 borders are selected. |
| `1` | `false` | Merging is disabled for a single border. |
| `0` | `false` | Merging is disabled when no borders are selected. |

---

### 2. `ShouldRefreshOcrBordersForTableModeActivation_OnlyRefreshesForParagraphGroupedOcrBorders`

* **Attribute:** `[Theory]`
* **Purpose:** Verifies the decision logic of `GrabFrame.ShouldRefreshOcrBordersForTableModeActivation` across various combinations of UI state flags when using a standard language (`GlobalLang("en-US")`).

#### Parameters
* `isTableModeSelected` (`bool`): Indicates if Table Mode is active.
* `paragraphDetectionEnabled` (`bool`): Indicates if paragraph detection is enabled.
* `hasNativePdfText` (`bool`): Indicates if native PDF text is present.
* `hasMergedParagraphBorders` (`bool`): Indicates if merged paragraph borders exist.
* `expected` (`bool`): Expected boolean result indicating whether OCR borders must be refreshed.

#### Test Matrix (`[InlineData]`)
| `isTableModeSelected` | `paragraphDetectionEnabled` | `hasNativePdfText` | `hasMergedParagraphBorders` | `expected` |
| :---: | :---: | :---: | :---: | :---: |
| `true` | `true` | `false` | `true` | `true` |
| `true` | `true` | `false` | `false` | `false` |
| `true` | `true` | `true` | `true` | `false` |
| `true` | `false` | `false` | `true` | `false` |
| `false` | `true` | `false` | `true` | `false` |

#### Key Logic Tested
OCR borders are refreshed upon Table Mode activation **only** when all of the following conditions are met:
1. Table Mode is selected (`true`).
2. Paragraph detection is enabled (`true`).
3. Native PDF text is **not** present (`false`).
4. Merged paragraph borders are present (`true`).

---

### 3. `ShouldRefreshOcrBordersForTableModeActivation_ReturnsFalseForUiAutomation`

* **Attribute:** `[Fact]`
* **Purpose:** Verifies that `GrabFrame.ShouldRefreshOcrBordersForTableModeActivation` strictly returns `false` when the active language model is an instance of `UiAutomationLang`, regardless of other supporting conditions being `true`.

#### Test Setup & Execution
* **Language:** `new UiAutomationLang()`
* **`isTableModeSelected`:** `true`
* **`paragraphDetectionEnabled`:** `true`
* **`hasNativePdfText`:** `false`
* **`hasMergedParagraphBorders`:** `true`

#### Assertion
Asserts that `actual` is `false` (`Assert.False(actual)`).

---

## Execution Flow Summary

1. The test runner discovers tests via xUnit attributes (`[Theory]`, `[Fact]`).
2. For `[Theory]` methods, xUnit iterates through each `[InlineData]` set, passing the values into the test method parameters.
3. Test methods call the static decision methods on `GrabFrame`:
   * `GrabFrame.ShouldAllowWordBorderMerging(...)`
   * `GrabFrame.ShouldRefreshOcrBordersForTableModeActivation(...)`
4. Assertions compare the returned value from `GrabFrame` against the expected value.