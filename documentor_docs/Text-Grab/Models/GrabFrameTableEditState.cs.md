# GrabFrameTableEditState Documentation

**File Path:** `Text-Grab/Models/GrabFrameTableEditState.cs`

## Overview

The `GrabFrameTableEditState` class manages the state and logic for manually editing row and column table separators in Text-Grab's Grab Frame tool. It handles adding, validating, normalizing, scaling, and committing custom row and column positions, as well as tracking active placement and preview states.

---

## Enums

### `GrabFrameTablePlacementMode`

Represents the current mode for adding a new table separator.

* **`None`**: No placement interaction is currently active.
* **`AddRow`**: The user is currently placing a horizontal row separator.
* **`AddColumn`**: The user is currently placing a vertical column separator.

---

## Class Constants and Properties

### `GrabFrameTableEditState`

#### Constants
* **`public const double MinimumSeparatorGap = 6;`**
  * The default minimum distance required between adjacent separators or borders.

#### Properties
* **`public List<double> ManualColumnSeparators { get; private set; }`**
  * Holds the list of normalized double precision X-coordinates representing manual column separators. Initialized to an empty list.
* **`public List<double> ManualRowSeparators { get; private set; }`**
  * Holds the list of normalized double precision Y-coordinates representing manual row separators. Initialized to an empty list.
* **`public GrabFrameTablePlacementMode PlacementMode { get; private set; }`**
  * Gets the active placement mode (`None`, `AddRow`, or `AddColumn`).
* **`public double? PreviewPosition { get; private set; }`**
  * The current proposed position for a new separator being placed. `null` if no placement is in progress or if no position has been computed.
* **`public bool IsPreviewValid { get; private set; }`**
  * Indicates whether the current `PreviewPosition` meets all placement criteria (distance from edges and existing separators).
* **`public bool IsPlacementActive`**
  * Computed boolean property (`PlacementMode != GrabFrameTablePlacementMode.None`). Returns `true` if currently placing a row or column.

---

## Methods

### Placement State Management

#### `BeginPlacement(GrabFrameTablePlacementMode placementMode)`
* **Description:** Initiates a row or column placement action.
* **Parameters:**
  * `placementMode`: The mode to transition to (`AddRow` or `AddColumn`).
* **Behavior:** Sets `PlacementMode` to the given parameter, resets `PreviewPosition` to `null`, and sets `IsPreviewValid` to `false`.

#### `CancelPlacement()`
* **Description:** Cancels the current placement action without saving changes.
* **Behavior:** Resets `PlacementMode` to `None`, `PreviewPosition` to `null`, and `IsPreviewValid` to `false`.

#### `ClearAll()`
* **Description:** Resets the edit state entirely.
* **Behavior:** Calls `CancelPlacement()` and resets `ManualRowSeparators` and `ManualColumnSeparators` to empty lists.

#### `GetExistingSeparatorsForPlacement()`
* **Description:** Retrieves the existing separators corresponding to the current active placement mode.
* **Returns:** `IReadOnlyList<double>` containing `ManualRowSeparators` if `PlacementMode` is `AddRow`, `ManualColumnSeparators` if `PlacementMode` is `AddColumn`, or an empty list if `None`.

---

### Separator Data Modification

#### `SetManualSeparators(IEnumerable<double>? manualRowSeparators, IEnumerable<double>? manualColumnSeparators)`
* **Description:** Replaces existing row and column separator lists with new collections.
* **Parameters:**
  * `manualRowSeparators`: Sequence of double values for row positions (can be `null`).
  * `manualColumnSeparators`: Sequence of double values for column positions (can be `null`).
* **Behavior:** Normalizes both inputs using `NormalizeSeparators()` and assigns them to `ManualRowSeparators` and `ManualColumnSeparators`.

#### `ScaleSeparators(double rowScale, double columnScale)`
* **Description:** Rescales existing separators and any active preview position by specified scaling factors (e.g., during window or image resizing).
* **Parameters:**
  * `rowScale`: Scale factor to apply to row separators.
  * `columnScale`: Scale factor to apply to column separators.
* **Behavior:**
  * If `rowScale` is finite and greater than 0, multiplies all `ManualRowSeparators` by `rowScale` and normalizes the list.
  * If `columnScale` is finite and greater than 0, multiplies all `ManualColumnSeparators` by `columnScale` and normalizes the list.
  * If `PreviewPosition` is active, rounds and rescales `PreviewPosition` using `rowScale` or `columnScale` depending on the active `PlacementMode`.

---

### Preview and Commit Logic

#### `TryUpdatePreview(double requestedPosition, double minimumPosition, double maximumPosition, IEnumerable<double> existingSeparators, double minimumGap = MinimumSeparatorGap)`
* **Description:** Evaluates a requested position for a new separator against boundaries and existing separators.
* **Parameters:**
  * `requestedPosition`: Target coordinate for the separator preview.
  * `minimumPosition`: Lower boundary constraint.
  * `maximumPosition`: Upper boundary constraint.
  * `existingSeparators`: Collection of existing separator positions to validate distance against.
  * `minimumGap`: Minimum required distance between separators/edges (defaults to `MinimumSeparatorGap` = 6).
* **Returns:** `bool` indicating whether the updated preview position is valid.
* **Behavior:**
  * If `IsPlacementActive` is `false`, sets `PreviewPosition` to `null`, `IsPreviewValid` to `false`, and returns `false`.
  * Calls `TryNormalizeSeparatorPosition(...)`. Updates `PreviewPosition` with the resulting normalized position and sets `IsPreviewValid` to the result.

#### `TryCommitPreview()`
* **Description:** Commits the current preview position into the permanent list of separators.
* **Returns:** `bool` indicating whether the commit succeeded.
* **Behavior:**
  * Returns `false` if placement is inactive, preview is invalid, or `PreviewPosition` is `null`.
  * Adds `PreviewPosition` to `ManualRowSeparators` (if `PlacementMode` is `AddRow`) or `ManualColumnSeparators` (if `PlacementMode` is `AddColumn`).
  * Re-sorts and normalizes the list.
  * Returns `true`.

---

## Static Helper Methods

#### `NormalizeSeparators(IEnumerable<double>? separators)`
* **Description:** Cleans, rounds, deduplicates, and sorts a list of separator positions.
* **Parameters:**
  * `separators`: Sequence of double values, or `null`.
* **Returns:** `List<double>` containing valid, rounded integer positions sorted in ascending order.
* **Logic:**
  1. Returns an empty list if `separators` is `null`.
  2. Filters out non-finite double values (`double.IsNaN`, infinity).
  3. Rounds positions to the nearest integer using `Math.Round()`.
  4. Removes duplicates using `.Distinct()`.
  5. Sorts values in ascending order (`.OrderBy()`).

#### `TryNormalizeSeparatorPosition(double requestedPosition, double minimumPosition, double maximumPosition, IEnumerable<double>? existingSeparators, double minimumGap, out double normalizedPosition)`
* **Description:** Validates and calculates a rounded position for a single separator.
* **Parameters:**
  * `requestedPosition`: The input double position.
  * `minimumPosition`: Min bounding coordinate.
  * `maximumPosition`: Max bounding coordinate.
  * `existingSeparators`: Existing separators to check gaps against.
  * `minimumGap`: Minimum required gap size.
  * `normalizedPosition`: Output parameter yielding the calculated position (0 if invalid).
* **Returns:** `bool` - `true` if position is valid; otherwise `false`.
* **Validation Rules:**
  1. All numerical parameters (`requestedPosition`, `minimumPosition`, `maximumPosition`, `minimumGap`) must be finite.
  2. `maximumPosition` must be strictly greater than `minimumPosition`.
  3. The rounded, clamped position (`Math.Round(Math.Clamp(...))`) must be strictly inside the bounds (cannot equal `minimumPosition` or `maximumPosition`).
  4. The absolute distance between the rounded clamped position and *any* existing normalized separator must be greater than or equal to `minimumGap`.