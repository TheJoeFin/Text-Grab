# Technical Documentation: `WindowSelectionUtilitiesTests.cs`

## Overview

The `WindowSelectionUtilitiesTests` class contains unit tests written for the `Text-Grab` project. Its primary purpose is to verify the behavior of the hit-testing logic in `WindowSelectionUtilities.FindWindowAtPoint`. Specifically, these tests ensure that point-in-rectangle lookups accurately return either the first candidate window covering a given point or `null` if no candidate encloses the point.

---

## File Details

- **File Path:** `Tests/WindowSelectionUtilitiesTests.cs`
- **Namespace:** `Tests`
- **Testing Framework:** xUnit (indicated by `[Fact]` attributes and `Assert` calls)

---

## Dependencies & Imports

- **`System.Windows`**: Provides UI framework primitives used for spatial bounds and coordinates:
  - `Rect`: Defines positional and dimensional bounds (X, Y, Width, Height).
  - `Point`: Represents a 2D coordinate point (X, Y).
- **`Text_Grab.Models`**: Provides the data structure:
  - `WindowSelectionCandidate`: Represents a candidate window evaluated during hit testing.
- **`Text_Grab.Utilities`**: Provides the static utility class under test:
  - `WindowSelectionUtilities`: Contains the `FindWindowAtPoint` method.

---

## Test Class: `WindowSelectionUtilitiesTests`

This class hosts test cases evaluating how `WindowSelectionUtilities.FindWindowAtPoint` selects a candidate `WindowSelectionCandidate` from a list based on a target `Point`.

### Test Methods

#### 1. `FindWindowAtPoint_ReturnsFirstMatchingCandidate()`

* **Purpose**: Verifies that when multiple candidates overlap a target point, `FindWindowAtPoint` returns the first candidate in the list that contains the point.
* **Test Setup**:
  - `topCandidate`: `WindowSelectionCandidate` initialized with handle `(nint)1`, bounds `Rect(0, 0, 40, 40)`, title `"Top"`, process ID `100`.
  - `lowerCandidate`: `WindowSelectionCandidate` initialized with handle `(nint)2`, bounds `Rect(0, 0, 60, 60)`, title `"Lower"`, process ID `101`.
  - Target point: `Point(20, 20)` (contained within both candidate rectangles).
* **Execution**: Passes `[topCandidate, lowerCandidate]` array and `Point(20, 20)` to `WindowSelectionUtilities.FindWindowAtPoint`.
* **Assertion**: `Assert.Same(topCandidate, found)` ensures that the method returns reference-equal object matching `topCandidate` (the first valid match).

#### 2. `FindWindowAtPoint_ReturnsNullWhenPointIsOutsideEveryCandidate()`

* **Purpose**: Verifies that `FindWindowAtPoint` returns `null` when a provided target point does not fall within the bounds of any candidate windows in the list.
* **Test Setup**:
  - `candidate`: `WindowSelectionCandidate` initialized with handle `(nint)1`, bounds `Rect(0, 0, 40, 40)`, title `"Only"`, process ID `100`.
  - Target point: `Point(80, 80)` (outside `Rect(0, 0, 40, 40)`).
* **Execution**: Passes `[candidate]` array and `Point(80, 80)` to `WindowSelectionUtilities.FindWindowAtPoint`.
* **Assertion**: `Assert.Null(found)` ensures that the returned value is `null`.

---

## Summary of Logic Verified

| Test Method | Input Candidates | Target Point | Expected Result |
| :--- | :--- | :--- | :--- |
| `FindWindowAtPoint_ReturnsFirstMatchingCandidate` | `[ (0,0,40,40), (0,0,60,60) ]` | `(20, 20)` | First matching candidate (`topCandidate`) |
| `FindWindowAtPoint_ReturnsNullWhenPointIsOutsideEveryCandidate` | `[ (0,0,40,40) ]` | `(80, 80)` | `null` |