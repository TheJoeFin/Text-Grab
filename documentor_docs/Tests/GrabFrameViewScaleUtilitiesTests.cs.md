# Technical Documentation: `GrabFrameViewScaleUtilitiesTests.cs`

## Overview

The `GrabFrameViewScaleUtilitiesTests` class contains xUnit unit tests designed to verify the scaling and window rectangle adjustment logic provided by `GrabFrameViewScaleUtilities` in the `Text_Grab.Utilities` namespace. 

It tests two main functions:
1. `StepScale`: Increments or decrements view scale values and enforces lower and upper clamping boundaries.
2. `GetMinimumWindowRect`: Ensures a window rectangle meets minimum size requirements, centers the expanded window around the original center point, and keeps it within the screen's work area boundaries.

---

## File Details

* **File Path:** `Tests/GrabFrameViewScaleUtilitiesTests.cs`
* **Namespace:** `Tests`
* **Dependencies:**
  * `System.Windows` (provides `Rect` and `Size` structures)
  * `Text_Grab.Utilities` (provides `GrabFrameViewScaleUtilities`)
  * `Xunit` (provides testing attributes `[Theory]`, `[Fact]`, `[InlineData]`, and the `Assert` class)

---

## Class Structure & Test Methods

### Class: `GrabFrameViewScaleUtilitiesTests`

`public class GrabFrameViewScaleUtilitiesTests`

Contains all unit test cases for validating scaling increments, minimum window resizing, centering, and work area boundary clamping.

---

### Test Methods

#### 1. `StepScale_AdjustsAndClampsAsExpected`

```csharp
[Theory]
[InlineData(1.0, 1, 1.25)]
[InlineData(1.0, -1, 0.75)]
[InlineData(0.5, -1, 0.5)]
[InlineData(5.0, 1, 5.0)]
public void StepScale_AdjustsAndClampsAsExpected(double currentScale, int direction, double expected)
```

* **Purpose:** Verifies that `GrabFrameViewScaleUtilities.StepScale` accurately adjusts a scale value up or down depending on the direction parameter, and respects min/max scale limits.
* **Type:** Parameterized Test (`[Theory]`)
* **Test Cases Data:**
  * `(1.0, 1, 1.25)`: Increasing scale from `1.0` by 1 step yields `1.25`.
  * `(1.0, -1, 0.75)`: Decreasing scale from `1.0` by 1 step yields `0.75`.
  * `(0.5, -1, 0.5)`: Decreasing scale from the minimum bound `0.5` remains clamped at `0.5`.
  * `(5.0, 1, 5.0)`: Increasing scale from the maximum bound `5.0` remains clamped at `5.0`.
* **Assertion:** `Assert.Equal(expected, actual, 3)` ensures equality up to 3 decimal places.

---

#### 2. `GetMinimumWindowRect_LeavesLargeWindowUnchanged`

```csharp
[Fact]
public void GetMinimumWindowRect_LeavesLargeWindowUnchanged()
```

* **Purpose:** Ensures that if a window's current rectangle already exceeds the minimum document window dimensions, `GetMinimumWindowRect` returns the original rectangle unchanged.
* **Test Setup:**
  * `currentWindowRect`: Position `(300, 200)`, Width `900`, Height `700`.
  * `minimumWindowSize`: Width = `MinimumLoadedDocumentWindowWidth`, Height = `MinimumLoadedDocumentWindowHeight`.
  * `workArea`: Position `(0, 0)`, Width `1920`, Height `1080`.
* **Assertion:** Verifies `actual` equals `currentWindowRect`.

---

#### 3. `GetMinimumWindowRect_ExpandsAndCentersWithinWorkArea`

```csharp
[Fact]
public void GetMinimumWindowRect_ExpandsAndCentersWithinWorkArea()
```

* **Purpose:** Tests that when a window rectangle is smaller than the required minimum dimensions, `GetMinimumWindowRect` expands the dimensions to the minimum required size and centers the newly sized window relative to the initial window's position.
* **Test Setup:**
  * `currentWindowRect`: Position `(500, 250)`, Width `400`, Height `300`.
  * `minimumWindowSize`: Derived from `MinimumLoadedDocumentWindowWidth` and `MinimumLoadedDocumentWindowHeight`.
  * `workArea`: Position `(0, 0)`, Width `1920`, Height `1080`.
* **Expected Result:** A `Rect` located at `(300, 175)` with dimensions `800 x 450`.
* **Assertion:** `Assert.Equal(new Rect(300, 175, 800, 450), actual)`.

---

#### 4. `GetMinimumWindowRect_ClampsExpandedWindowInsideWorkArea`

```csharp
[Fact]
public void GetMinimumWindowRect_ClampsExpandedWindowInsideWorkArea()
```

* **Purpose:** Tests that expanding a small window near the screen boundary does not cause the window to extend beyond the visible screen `workArea`. It verifies that the resulting rectangle is shifted back inside the work area.
* **Test Setup:**
  * `currentWindowRect`: Position `(1500, 700)`, Width `400`, Height `300`.
  * `minimumWindowSize`: Derived from `MinimumLoadedDocumentWindowWidth` and `MinimumLoadedDocumentWindowHeight`.
  * `workArea`: Position `(0, 0)`, Width `1920`, Height `1080`.
* **Expected Result:** A `Rect` located at `(1120, 625)` with dimensions `800 x 450` (preventing the right edge from exceeding `1920`).
* **Assertion:** `Assert.Equal(new Rect(1120, 625, 800, 450), actual)`.

---

## Referenced External Members

The tests rely on static properties and methods from `GrabFrameViewScaleUtilities`:

* `GrabFrameViewScaleUtilities.StepScale(double currentScale, int direction)`
* `GrabFrameViewScaleUtilities.GetMinimumWindowRect(Rect currentWindowRect, Size minimumWindowSize, Rect workArea)`
* `GrabFrameViewScaleUtilities.MinimumLoadedDocumentWindowWidth`
* `GrabFrameViewScaleUtilities.MinimumLoadedDocumentWindowHeight`