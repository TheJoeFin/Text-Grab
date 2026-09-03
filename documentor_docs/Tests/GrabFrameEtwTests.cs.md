# Technical Documentation: `Tests/GrabFrameEtwTests.cs`

## Overview

The `GrabFrameEtwTests.cs` file contains unit tests for verifying conditional logic related to Edit Text Windows (ETW) and Grab Frame interactions within the application. It uses xUnit parameterized tests (`[Theory]` and `[InlineData]`) to validate decision-making methods found in `WindowUtilities` and `GrabFrame`.

---

## Dependencies and Namespaces

* **Namespaces Used:**
  * `Text_Grab.Utilities`: Provides access to utility classes such as `WindowUtilities`.
  * `Text_Grab.Views`: Provides access to view-related classes such as `GrabFrame`.
* **Namespace of File:** `Tests`
* **Testing Framework:** xUnit (inferred from `[Theory]`, `[InlineData]`, and `Assert.Equal`).

---

## Class: `GrabFrameEtwTests`

**Type:** `public class`  
**Purpose:** Serves as a test suite for testing logic associated with opening new Edit Text Windows in spreadsheet mode and updating linked destination text.

---

## Test Methods

### 1. `ShouldOpenNewEtwInSpreadsheetMode_OnlyReturnsTrueForNewTableEtw`

#### Overview
Tests the logic of `WindowUtilities.ShouldOpenNewEtwInSpreadsheetMode` to ensure that spreadsheet mode is only enabled when a new Edit Text Window (ETW) is created in table mode and no existing ETW is already present.

#### Method Signature
```csharp
[Theory]
[InlineData(true, false, true)]
[InlineData(true, true, false)]
[InlineData(false, false, false)]
[InlineData(false, true, false)]
public void ShouldOpenNewEtwInSpreadsheetMode_OnlyReturnsTrueForNewTableEtw(
    bool isTableModeSelected,
    bool hasExistingEditTextWindow,
    bool expected)
```

#### Parameters
* `isTableModeSelected` (`bool`): Indicates whether table mode is selected.
* `hasExistingEditTextWindow` (`bool`): Indicates if an Edit Text Window is already open/existing.
* `expected` (`bool`): The expected boolean return value from `WindowUtilities.ShouldOpenNewEtwInSpreadsheetMode`.

#### Method Under Test
* `WindowUtilities.ShouldOpenNewEtwInSpreadsheetMode(isTableModeSelected, hasExistingEditTextWindow)`

#### Test Matrix (`InlineData`)
| `isTableModeSelected` | `hasExistingEditTextWindow` | Expected Result (`expected`) |
| :--- | :--- | :--- |
| `true` | `false` | `true` |
| `true` | `true` | `false` |
| `false` | `false` | `false` |
| `false` | `true` | `false` |

---

### 2. `ShouldUpdateLinkedDestinationText_PreservesSpreadsheetSelectionWhenClosing`

#### Overview
Tests the logic of `GrabFrame.ShouldUpdateLinkedDestinationText` across various combinations of state flags to determine whether a linked destination text component should be updated.

#### Method Signature
```csharp
[Theory]
[InlineData(true, true, true, true, false, false, false, true)]
[InlineData(true, true, true, true, false, true, false, true)]
[InlineData(true, true, true, true, false, true, true, false)]
[InlineData(true, true, true, true, true, false, false, false)]
[InlineData(true, true, false, true, false, false, false, false)]
public void ShouldUpdateLinkedDestinationText_PreservesSpreadsheetSelectionWhenClosing(
    bool isFromEditWindow,
    bool hasDestinationTextBox,
    bool shouldAlwaysUpdateEtw,
    bool isEditTextToggleEnabled,
    bool hasActiveGrabTemplate,
    bool preserveLinkedSpreadsheetSelection,
    bool isDestinationSpreadsheetMode,
    bool expected)
```

#### Parameters
* `isFromEditWindow` (`bool`): Indicates if the update originates from an edit window.
* `hasDestinationTextBox` (`bool`): Indicates if a destination text box exists.
* `shouldAlwaysUpdateEtw` (`bool`): Flag specifying whether ETW should always update.
* `isEditTextToggleEnabled` (`bool`): Indicates if the edit text toggle feature is enabled.
* `hasActiveGrabTemplate` (`bool`): Indicates if a grab template is currently active.
* `preserveLinkedSpreadsheetSelection` (`bool`): Flag indicating if linked spreadsheet selection should be preserved.
* `isDestinationSpreadsheetMode` (`bool`): Indicates if the destination is currently in spreadsheet mode.
* `expected` (`bool`): The expected boolean return value from `GrabFrame.ShouldUpdateLinkedDestinationText`.

#### Method Under Test
* `GrabFrame.ShouldUpdateLinkedDestinationText(...)`

#### Test Matrix (`InlineData`)
| `isFromEditWindow` | `hasDestinationTextBox` | `shouldAlwaysUpdateEtw` | `isEditTextToggleEnabled` | `hasActiveGrabTemplate` | `preserveLinkedSpreadsheetSelection` | `isDestinationSpreadsheetMode` | Expected Result |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `true` | `true` | `true` | `true` | `false` | `false` | `false` | `true` |
| `true` | `true` | `true` | `true` | `false` | `true` | `false` | `true` |
| `true` | `true` | `true` | `true` | `false` | `true` | `true` | `false` |
| `true` | `true` | `true` | `true` | `true` | `false` | `false` | `false` |
| `true` | `true` | `false` | `true` | `false` | `false` | `false` | `false` |