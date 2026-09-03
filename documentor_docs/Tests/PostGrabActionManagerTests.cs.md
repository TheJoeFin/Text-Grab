# Documentation: `Tests/PostGrabActionManagerTests.cs`

## Overview

The `PostGrabActionManagerTests` class contains unit tests using the **xUnit** framework to validate the behavior of the `PostGrabActionManager` utility class (from `Text_Grab.Utilities`) and its interaction with the `ButtonInfo` model (from `Text_Grab.Models`).

This test file ensures that:
1. Default post-grab actions are correctly initialized with expected count, labels, click event bindings, symbol flags, and contextual UI relevance.
2. Specific post-grab actions execute string transformations or handling correctly (e.g., GUID correction, removing duplicate lines, text-to-speech pass-through).
3. The initial check state evaluation (`GetCheckState`) properly handles `DefaultCheckState` enum values (`Off` vs. `On`).

---

## Namespace & Imports

```csharp
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Tests;
```

* **Dependencies:**
  * `Text_Grab.Models`: Contains data models such as `ButtonInfo` and `DefaultCheckState`.
  * `Text_Grab.Utilities`: Contains the `PostGrabActionManager` utility being tested.

---

## Test Methods Summary

| Test Method Name | Tested Method | Description / Assertion |
| :--- | :--- | :--- |
| `GetDefaultPostGrabActions_ReturnsExpectedCount` | `GetDefaultPostGrabActions()` | Verifies that the default actions list is non-null and contains exactly 6 actions. |
| `GetDefaultPostGrabActions_ContainsExpectedActions` | `GetDefaultPostGrabActions()` | Verifies that the list includes specific button labels: "Fix GUIDs", "Trim each line", "Remove duplicate lines", "Web Search", "Try to insert text", and "Speak text". |
| `GetDefaultPostGrabActions_AllHaveClickEvents` | `GetDefaultPostGrabActions()` | Ensures every action has a non-null, non-empty `ClickEvent` string. |
| `GetDefaultPostGrabActions_AllHaveSymbols` | `GetDefaultPostGrabActions()` | Verifies that `IsSymbol` is set to `true` for all default actions. |
| `GetDefaultPostGrabActions_AllMarkedForFullscreenGrab` | `GetDefaultPostGrabActions()` | Verifies that `IsRelevantForFullscreenGrab` is `true` and `IsRelevantForEditWindow` is `false` for all default actions. |
| `ExecutePostGrabAction_CorrectGuid_TransformsText` | `ExecutePostGrabAction(...)` | Tests the `CorrectGuid_Click` event. Verifies that 'O' characters in a GUID-like string are replaced with zeroes (`0`). |
| `ExecutePostGrabAction_RemoveDuplicateLines_RemovesDuplicates` | `ExecutePostGrabAction(...)` | Tests the `RemoveDuplicateLines_Click` event. Verifies that duplicate lines are removed from multi-line text. |
| `ExecutePostGrabAction_SpeakText_ReturnsTextUnchanged` | `ExecutePostGrabAction(...)` | Tests the `SpeakText_Click` event. Verifies that the input text is returned unmodified while performing TTS. |
| `GetCheckState_DefaultOff_ReturnsFalse` | `GetCheckState(...)` | Verifies that a `ButtonInfo` instance with `DefaultCheckState.Off` returns `false`. |
| `GetCheckState_DefaultOn_ReturnsTrue` | `GetCheckState(...)` | Verifies that a `ButtonInfo` instance with `DefaultCheckState.On` returns `true`. |

---

## Detailed Test Breakdown

### 1. Default Action Initialization Tests

#### `GetDefaultPostGrabActions_ReturnsExpectedCount()`
* **Purpose:** Ensures `PostGrabActionManager.GetDefaultPostGrabActions()` returns a list of exactly 6 items.
* **Assertions:**
  * `Assert.NotNull(actions)`
  * `Assert.Equal(6, actions.Count)`

#### `GetDefaultPostGrabActions_ContainsExpectedActions()`
* **Purpose:** Asserts that the actions list contains specific required default action button texts.
* **Tested Button Labels:**
  * `"Fix GUIDs"`
  * `"Trim each line"`
  * `"Remove duplicate lines"`
  * `"Web Search"`
  * `"Try to insert text"`
  * `"Speak text"`

#### `GetDefaultPostGrabActions_AllHaveClickEvents()`
* **Purpose:** Ensures every `ButtonInfo` in the default list has a valid string populated in its `ClickEvent` property.
* **Assertion:** `Assert.False(string.IsNullOrEmpty(action.ClickEvent))` for all actions.

#### `GetDefaultPostGrabActions_AllHaveSymbols()`
* **Purpose:** Confirms that `IsSymbol` is set to `true` for all default actions.
* **Assertion:** `Assert.True(action.IsSymbol)` for all actions.

#### `GetDefaultPostGrabActions_AllMarkedForFullscreenGrab()`
* **Purpose:** Validates the UI scope flags for the default post-grab actions.
* **Assertions:**
  * `Assert.True(action.IsRelevantForFullscreenGrab)`
  * `Assert.False(action.IsRelevantForEditWindow)`

---

### 2. Action Execution Tests

#### `ExecutePostGrabAction_CorrectGuid_TransformsText()`
* **Type:** Asynchronous (`async Task`)
* **Purpose:** Verifies GUID correction behavior on text containing 'O's instead of '0's.
* **Input:** `"123e4567-e89b-12d3-a456-426614174OOO"`
* **Action Target:** Action with `ClickEvent == "CorrectGuid_Click"`
* **Assertion:** `Assert.Contains("000", result)` (verifies 'O's were corrected to '0's).

#### `ExecutePostGrabAction_RemoveDuplicateLines_RemovesDuplicates()`
* **Type:** Asynchronous (`async Task`)
* **Purpose:** Tests the duplicate line removal logic.
* **Input:** `"Line 1\r\nLine 2\r\nLine 1\r\nLine 3"` (using `Environment.NewLine`)
* **Action Target:** Action with `ClickEvent == "RemoveDuplicateLines_Click"`
* **Assertions:**
  * Line count after splitting by `Environment.NewLine` is `3`.
  * `"Line 1"` appears exactly once in the resulting output.

#### `ExecutePostGrabAction_SpeakText_ReturnsTextUnchanged()`
* **Type:** Asynchronous (`async Task`)
* **Purpose:** Validates that text-to-speech actions process as a pass-through, leaving the input string unaltered.
* **Input:** `"Hello world"`
* **Action Target:** Action with `ClickEvent == "SpeakText_Click"`
* **Assertion:** `Assert.Equal(input, result)`

---

### 3. Check State Evaluation Tests

#### `GetCheckState_DefaultOff_ReturnsFalse()`
* **Purpose:** Tests that passing a `ButtonInfo` initialized with `DefaultCheckState.Off` to `PostGrabActionManager.GetCheckState` evaluates to `false`.
* **Arrange Details:** `new ButtonInfo("Test", "Test_Click", Wpf.Ui.Controls.SymbolRegular.Apps24, DefaultCheckState.Off)`
* **Assertion:** `Assert.False(result)`

#### `GetCheckState_DefaultOn_ReturnsTrue()`
* **Purpose:** Tests that passing a `ButtonInfo` initialized with `DefaultCheckState.On` to `PostGrabActionManager.GetCheckState` evaluates to `true`.
* **Arrange Details:** `new ButtonInfo("Test", "Test_Click", Wpf.Ui.Controls.SymbolRegular.Apps24, DefaultCheckState.On)`
* **Assertion:** `Assert.True(result)`