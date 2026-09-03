# Documentation Guide: `Tests/UiAutomationUtilitiesTests.cs`

## Overview

The `UiAutomationUtilitiesTests` class is a suite of unit tests written in C# using the **xUnit** testing framework. Its purpose is to validate the functionality of helper methods contained within `UIAutomationUtilities` (from `Text_Grab.Utilities`). 

These tests ensure the correct behavior of functions related to:
* Text normalization and deduplication.
* Target window selection logic based on bounding rectangles.
* Filtering UI Automation `ControlType` objects for name fallback evaluation.
* Generating sample points and probe coordinates for UI hit testing.
* Clipping bounding boxes (rectangle intersection).
* Deduplicating and sorting UI Automation overlay items for visual ordering.

---

## Technical Dependencies

* **Framework**: xUnit (`[Fact]`, `Assert`)
* **Libraries / Namespaces**:
  * `System.Linq`: Enumerable extension methods (`Select`).
  * `System.Windows`: WPF layout elements (`Rect`, `Point`).
  * `System.Windows.Automation`: UI Automation types (`ControlType`).
  * `Text_Grab.Models`: Data models (`WindowSelectionCandidate`, `UiAutomationOverlayItem`, `UiAutomationOverlaySource`).
  * `Text_Grab.Utilities`: System under test (`UIAutomationUtilities`).

---

## Test Cases Breakdown

### 1. Text Processing & Deduplication

#### `NormalizeText_TrimsWhitespaceAndCollapsesEmptyLines`
* **Target Method**: `UIAutomationUtilities.NormalizeText`
* **Purpose**: Verifies that leading/trailing whitespace is removed and inline/empty line whitespace is normalized across OS environment line breaks.
* **Input**: `" Hello world \r\n\r\n Second\tline "`
* **Expected Output**: `"Hello world" + Environment.NewLine + "Second line"`

#### `TryAddUniqueText_DeduplicatesNormalizedValues`
* **Target Method**: `UIAutomationUtilities.TryAddUniqueText`
* **Purpose**: Tests deduplication logic. Ensures that strings with variations in spacing (e.g., `" Hello world "` and `"Hello world"`) resolve to the same normalized key in a lookup `HashSet<string>` and prevent duplicate entries in an output collection.
* **Assertions**:
  * First addition returns `true`.
  * Second addition of visually identical normalized text returns `false`.
  * Output collection length remains `1`.

---

### 2. Target Window Selection Logic

#### `FindTargetWindowCandidate_PrefersCenterPointHit`
* **Target Method**: `UIAutomationUtilities.FindTargetWindowCandidate`
* **Purpose**: Validates candidate selection when a target rectangle's center point lands directly within one of the candidate window bounds.
* **Scenario**: Two candidates (`first` at `0,0,80,80` and `second` at `90,0,80,80`). Target region `(100, 10, 20, 20)` has its center point inside `second`.
* **Assertion**: Returns `second`.

#### `FindTargetWindowCandidate_FallsBackToLargestIntersection`
* **Target Method**: `UIAutomationUtilities.FindTargetWindowCandidate`
* **Purpose**: Tests candidate selection fallback when the target region's center point does not land within any single candidate. It checks that the candidate with the largest area intersection is chosen.
* **Scenario**: Target region `(40, 40, 30, 30)` partially overlaps `first` (`0,0,50,50`) and `second` (`60,0,80,80`). `second` has a larger intersecting region.
* **Assertion**: Returns `second`.

---

### 3. UI Automation Control Type Evaluation

#### `ShouldUseNameFallback_SkipsStructuralControls`
* **Target Method**: `UIAutomationUtilities.ShouldUseNameFallback`
* **Purpose**: Confirms that structural or container UI controls return `false` for name fallback evaluation.
* **Tested Control Types**:
  * `ControlType.Window`
  * `ControlType.Group`
  * `ControlType.Pane`
  * `ControlType.Custom`
  * `ControlType.Button`
  * `ControlType.SplitButton`
  * `ControlType.ComboBox`

#### `ShouldUseNameFallback_AllowsVisibleTextContainers`
* **Target Method**: `UIAutomationUtilities.ShouldUseNameFallback`
* **Purpose**: Confirms that elements intended to present direct text elements return `true`.
* **Tested Control Types**:
  * `ControlType.Text`
  * `ControlType.ListItem`
  * `ControlType.MenuItem`
  * `ControlType.TabItem`

---

### 4. Point Sampling & Hit-Testing

#### `GetSamplePoints_UsesCenterPointForSmallSelections`
* **Target Method**: `UIAutomationUtilities.GetSamplePoints`
* **Purpose**: Ensures small selection bounds generate only a single sample point at the geometric center.
* **Input Rect**: `Rect(10, 20, 40, 30)`
* **Expected Result**: A single point `Point(30, 35)`.

#### `GetSamplePoints_UsesGridForLargerSelections`
* **Target Method**: `UIAutomationUtilities.GetSamplePoints`
* **Purpose**: Verifies that larger selection areas generate a 9-point grid across the area.
* **Input Rect**: `Rect(0, 0, 100, 100)`
* **Expected Result**: 9 points including center `(50, 50)`, top-left `(20, 20)`, and bottom-right `(80, 80)`.

#### `GetPointProbePoints_ReturnsCenterThenCrosshairNeighbors`
* **Target Method**: `UIAutomationUtilities.GetPointProbePoints`
* **Purpose**: Verifies crosshair point probing generation around a target point (center point followed by 4 neighbor offset points).
* **Input Point**: `Point(25, 40)`
* **Expected Result**: 5 points:
  1. Center: `(25, 40)`
  2. Left neighbor: `(23, 40)`
  3. Right neighbor: `(27, 40)`
  4. Top neighbor: `(25, 38)`
  5. Bottom neighbor: `(25, 42)`

---

### 5. Bounds Clipping

#### `TryClipBounds_ReturnsIntersectionForOverlappingRects`
* **Target Method**: `UIAutomationUtilities.TryClipBounds`
* **Purpose**: Validates clipping logic when two rectangles overlap.
* **Inputs**: `Rect(10, 10, 50, 50)` and `Rect(30, 25, 50, 50)`
* **Expected Result**: Method returns `true` with an output `Rect` of `(30, 25, 30, 35)`.

#### `TryClipBounds_ReturnsFalseWhenBoundsDoNotIntersect`
* **Target Method**: `UIAutomationUtilities.TryClipBounds`
* **Purpose**: Validates that non-intersecting rectangles fail to clip.
* **Inputs**: `Rect(10, 10, 20, 20)` and `Rect(100, 100, 20, 20)`
* **Expected Result**: Method returns `false` with an output `Rect` of `Rect.Empty`.

---

### 6. Overlay Item Collection Utilities

#### `TryAddUniqueOverlayItem_DeduplicatesNormalizedTextAndBounds`
* **Target Method**: `UIAutomationUtilities.TryAddUniqueOverlayItem`
* **Purpose**: Verifies deduplication of `UiAutomationOverlayItem` objects based on normalized text content and matching bounding coordinates.
* **Scenario**: First item has text `" Hello world "` and bounds `(10.01, 20.01, 30.01, 40.01)`. Second item has text `"Hello world"` and near-identical bounds `(10.04, 20.04, 30.04, 40.04)`.
* **Assertions**:
  * Adding the first item returns `true`.
  * Adding the second item returns `false`.
  * Output list count is `1`.

#### `SortOverlayItems_OrdersTopThenLeft`
* **Target Method**: `UIAutomationUtilities.SortOverlayItems`
* **Purpose**: Validates that overlay items are sorted into reading order (primarily top-to-bottom by Y coordinate, secondarily left-to-right by X coordinate).
* **Input Items**:
  1. `"Bottom"` at `Rect(40, 30, 10, 10)` (Y = 30)
  2. `"Right"` at `Rect(25, 10, 10, 10)` (Y = 10, X = 25)
  3. `"Left"` at `Rect(10, 10, 10, 10)` (Y = 10, X = 10)
* **Expected Sorted Text Sequence**: `["Left", "Right", "Bottom"]`