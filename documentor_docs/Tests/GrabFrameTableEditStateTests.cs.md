# Documentation: `Tests/GrabFrameTableEditStateTests.cs`

## Overview

The `GrabFrameTableEditStateTests` class contains unit tests written using the xUnit testing framework to verify the behavior of the `GrabFrameTableEditState` class (from the `Text_Grab.Models` namespace). 

The primary purpose of this test file is to validate the state management of table frame editing, specifically focusing on:
1. Adding and sorting manual row/column separators upon committing a preview.
2. Rejecting separator previews that are placed too close to existing dividers.

---

## File Information

- **File Path:** `Tests/GrabFrameTableEditStateTests.cs`
- **Namespace:** `Tests`
- **Dependencies:**
  - `Text_Grab.Models`

---

## Tested Components & Features

Based strictly on the unit tests in this file, the class under test (`GrabFrameTableEditState`) interacts with the following elements:

### Enum / Types Referenced
- **`GrabFrameTablePlacementMode`**: Defines placement modes, including:
  - `AddRow`
  - `AddColumn`

### Methods & Properties Verified on `GrabFrameTableEditState`
- **`SetManualSeparators(double[] rows, double[] columns)`**: Sets initial manual separators for rows and columns.
- **`BeginPlacement(GrabFrameTablePlacementMode mode)`**: Initiates the placement process for a row or column.
- **`TryUpdatePreview(double position, double min, double max, IEnumerable<double> existingSeparators)`**: Evaluates whether a candidate separator position is valid given bounds and existing separators. Returns a boolean indicating validity.
- **`TryCommitPreview()`**: Attempts to commit the current preview position into the permanent list of separators. Returns a boolean indicating success.
- **`ManualRowSeparators`**: Property holding the collection/array of manual row separator positions.
- **`PreviewPosition`**: Property holding the current candidate preview position.
- **`IsPreviewValid`**: Property indicating whether the current preview state is valid.

---

## Detailed Test Cases

### 1. `TryCommitPreview_AddsAndSortsManualSeparators`

* **Goal:** Verify that committing a valid preview successfully adds the new row separator to `ManualRowSeparators` and stores the collection in sorted order.

* **Execution Flow:**
  1. Instantiates `GrabFrameTableEditState`.
  2. Calls `SetManualSeparators([40], [70])` to set an initial row separator at position `40`.
  3. Calls `BeginPlacement(GrabFrameTablePlacementMode.AddRow)` to start adding a row.
  4. Calls `TryUpdatePreview(20, 0, 100, state.ManualRowSeparators)` to place a preview position at `20`.
  5. Calls `TryCommitPreview()` to apply the preview position.

* **Assertions:**
  - `TryUpdatePreview(...)` returns `true`.
  - `TryCommitPreview()` returns `true`.
  - `state.ManualRowSeparators` equals `[20d, 40d]` (confirming position `20` was added before existing position `40` in sorted order).

---

### 2. `TryUpdatePreview_RejectsSeparatorTooCloseToExistingDivider`

* **Goal:** Verify that attempting to place a column separator too close to an existing divider (e.g., position `22` vs existing divider at `20`) renders the preview invalid and prevents committing.

* **Execution Flow:**
  1. Instantiates `GrabFrameTableEditState`.
  2. Calls `BeginPlacement(GrabFrameTablePlacementMode.AddColumn)` to start adding a column.
  3. Calls `TryUpdatePreview(22, 0, 100, [20d])` with a position of `22` and an existing separator at `20`.
  4. Calls `TryCommitPreview()`.

* **Assertions:**
  - `TryUpdatePreview(...)` returns `false`.
  - `state.PreviewPosition` equals `22d`.
  - `state.IsPreviewValid` is `false`.
  - `TryCommitPreview()` returns `false`.