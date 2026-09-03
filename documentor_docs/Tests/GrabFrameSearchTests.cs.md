# GrabFrameSearchTests Documentation

## Overview

The `GrabFrameSearchTests` class contains xUnit unit tests designed to verify text search and layout-matching helper functions provided by the `GrabFrame` class in the `Text_Grab.Views` namespace.

These tests ensure that text construction, visual ordering, line matching, and character span overlaps function correctly for text recognition and search operations within `GrabFrame`.

---

## Technical Information

* **Namespace:** `Tests`
* **Dependencies:** `Text_Grab.Views`
* **Test Framework:** xUnit (`[Fact]`, `[Theory]`, `[InlineData]`)

---

## Test Class Breakdown

### `GrabFrameSearchTests`

Contains unit test methods verifying static search utilities on `GrabFrame`.

---

## Test Methods

### 1. `BuildSearchText_MapsMultiWordTextBackToSourceItems`
* **Type:** `[Fact]`
* **Purpose:** Tests the `GrabFrame.BuildSearchText` method to ensure it correctly sorts inputs by position, combines text, and returns segment mappings that correctly track text spans back to their original source indices. Also tests using `GrabFrame.SpansOverlap` with the resulting segments.

#### Test Execution Logic:
1. Calls `GrabFrame.BuildSearchText` with:
   * Input tuples: `[("555", 20), ("Call", 0), ("1234", 40)]`
   * `isSpaceJoining`: `true`
   * `isRightToLeft`: `false`
2. **Assertions:**
   * Expects joined text to be ordered by X-position: `"Call 555 1234"`.
   * Expects `segments` collection to map source index, start index, and length:
     * Index `1` ("Call"): Start `0`, Length `4`
     * Index `0` ("555"): Start `5`, Length `3`
     * Index `2` ("1234"): Start `9`, Length `4`
   * Filters segments overlapping with the span `(Start: 5, Length: 8)` using `GrabFrame.SpansOverlap`.
   * Verifies that the matched source indices are `[0, 2]`.

---

### 2. `BuildSearchText_UsesRightToLeftVisualOrder`
* **Type:** `[Fact]`
* **Purpose:** Tests that `GrabFrame.BuildSearchText` respects Right-To-Left (RTL) visual ordering when `isRightToLeft` is set to `true`.

#### Test Execution Logic:
1. Calls `GrabFrame.BuildSearchText` with:
   * Input tuples: `[("right", 100), ("left", 0)]`
   * `isSpaceJoining`: `true`
   * `isRightToLeft`: `true`
2. **Assertion:**
   * Verifies the resulting text is formatted as `"right left"`.

---

### 3. `AreOnSameSearchLine_RequiresMatchingLineAndVerticalAlignment`
* **Type:** `[Theory]`
* **Purpose:** Validates `GrabFrame.AreOnSameSearchLine`, ensuring two elements are considered on the same line only when they share a line index and meet vertical alignment criteria.

#### Parameter Inputs & Expected Results (`InlineData`):

| `firstLineNumber` | `firstTop` | `firstHeight` | `secondLineNumber` | `secondTop` | `secondHeight` | `expected` | Reason |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `0` | `10` | `10` | `0` | `12` | `10` | `true` | Same line number and vertically aligned. |
| `0` | `10` | `10` | `0` | `30` | `10` | `false` | Same line number, but top positions differ significantly (`10` vs `30`). |
| `0` | `10` | `10` | `1` | `10` | `10` | `false` | Different line numbers (`0` vs `1`). |

---

### 4. `SpansOverlap_DetectsOnlyNonEmptyIntersectingRanges`
* **Type:** `[Theory]`
* **Purpose:** Tests `GrabFrame.SpansOverlap` to ensure it accurately detects overlapping numeric ranges and rejects non-overlapping or zero-length ranges.

#### Parameter Inputs & Expected Results (`InlineData`):

| `firstStart` | `firstLength` | `secondStart` | `secondLength` | `expected` | Reason |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `0` | `4` | `2` | `4` | `true` | Range [0, 4) overlaps with range [2, 6). |
| `0` | `4` | `4` | `2` | `false` | Range [0, 4) ends where range [4, 6) begins (adjacent, no overlap). |
| `5` | `3` | `0` | `8` | `true` | Range [5, 8) is fully contained within range [0, 8). |
| `5` | `0` | `5` | `1` | `false` | First span has a length of `0` (empty span). |

---

## Tested Methods on `GrabFrame`

Based strictly on this test file, the `GrabFrame` class in `Text_Grab.Views` exposes the following static helper methods:

1. `BuildSearchText(IEnumerable<(string Text, double XOffset)> items, bool isSpaceJoining, bool isRightToLeft)`
   * Returns a tuple containing built search text and a collection of segments mapping each text section to its source index, start offset, and character length.
2. `AreOnSameSearchLine(int line1, double top1, double height1, int line2, double top2, double height2)`
   * Returns a boolean indicating if two text elements belong to the same visual search line.
3. `SpansOverlap(int start1, int length1, int start2, int length2)`
   * Returns a boolean indicating whether two 1D ranges/spans overlap.