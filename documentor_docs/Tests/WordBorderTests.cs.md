# Technical Documentation: `Tests/WordBorderTests.cs`

## Overview

The `WordBorderTests.cs` file contains unit tests for verifying the behavior of the `WordBorder` control from the `Text_Grab.Controls` namespace. Specifically, this test class validates how the `WordBorder` instance processes multi-line display text into single-line output when single-line mode is explicitly enabled.

---

## File Details

* **File Path:** `Tests/WordBorderTests.cs`
* **Namespace:** `Tests`
* **Imports:** 
  * `Text_Grab.Controls`

---

## Class: `WordBorderTests`

`public class WordBorderTests`

Serves as the test container for unit tests targeting the `WordBorder` control.

---

## Test Methods

### `ParagraphDisplayText_KeepsLogicalWordSingleLine()`

* **Attribute:** `[WpfFact]`
* **Purpose:** Verifies that when `KeepSingleLineOutput` is set to `true`, the `Word` property formats multi-line `DisplayText` as a single-line string, while `DisplayText` retains its original line breaks.

#### Test Execution Flow

1. **Initialization:**
   Instantiates a new instance of `WordBorder` and initializes its properties:
   * `KeepSingleLineOutput`: Set to `true`.
   * `DisplayLineHeight`: Set to `18`.
   * `DisplayText`: Set to a multi-line string containing `Environment.NewLine`:
     ```csharp
     $"Static cling{Environment.NewLine}is useful"
     ```

2. **Assertions:**
   * **`Assert.Equal("Static cling is useful", wordBorder.Word)`**
     Verifies that the `Word` property returns the text formatted into a single line, replacing the line break with a space.
   * **`Assert.Equal($"Static cling{Environment.NewLine}is useful", wordBorder.DisplayText)`**
     Verifies that the `DisplayText` property retains the original input string, including the `Environment.NewLine`.
   * **`Assert.True(wordBorder.KeepSingleLineOutput)`**
     Verifies that the `KeepSingleLineOutput` property value remains `true`.

---

## Properties Tested

Based on the test method in this file, the following properties of the `WordBorder` class are referenced and validated:

| Property | Type / Value Set in Test | Verified Behavior |
| :--- | :--- | :--- |
| `KeepSingleLineOutput` | `bool` (`true`) | Remains `true` after assignment. |
| `DisplayLineHeight` | `double` / `int` (`18`) | Set during initialization. |
| `DisplayText` | `string` (contains `Environment.NewLine`) | Preserves original line breaks. |
| `Word` | `string` (read/evaluated) | Converts multi-line `DisplayText` to a single-line representation when `KeepSingleLineOutput` is `true`. |