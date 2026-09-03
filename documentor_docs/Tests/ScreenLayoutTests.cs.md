# Technical Documentation: `Tests/ScreenLayoutTests.cs`

## Overview

The `ScreenLayoutTests.cs` file contains a suite of unit tests for verifying multi-monitor geometry calculations and screen library consistency within the project. It uses xUnit testing primitives to validate rectangle bounds calculations, center point locations, bounding box containment, and coordinate parity between `Dapplo.Windows.User32` and `System.Windows.Forms` APIs.

---

## File Details

- **File Path:** `Tests/ScreenLayoutTests.cs`
- **Namespace:** `Tests`
- **Dependencies:**
  - `Dapplo.Windows.User32`
  - `System.Windows`
  - `Text_Grab`

---

## Mock Data / Test Fixtures

The class defines six `private static Rect` instances representing multi-monitor coordinate setups across different screen layouts:

### Setup 1 (`display1` to `display3`)
- **`display1`**: `X = 0`, `Y = 0`, `Width = 3440`, `Height = 1400`
- **`display2`**: `X = 3440`, `Y = -1163`, `Width = 2400`, `Height = 3760`
- **`display3`**: `X = -1920`, `Y = 387`, `Width = 1920`, `Height = 1030`

### Setup 2 (`display4` to `display6`)
- **`display4`**: `X = 0`, `Y = 0`, `Width = 1920`, `Height = 1152`
- **`display5`**: `X = 3840`, `Y = -468`, `Width = 3840`, `Height = 2040`
- **`display6`**: `X = 1920`, `Y = -460`, `Width = 1920`, `Height = 1032`

---

## Key Components & Test Methods

### 1. `ShouldFindCenterOfEachRect()`

#### Purpose
Verifies that the center point calculated for each display rectangle lies strictly within that rectangle and does not overlap into non-adjacent display bounds within its set.

#### Implementation Details
- Uses `.CenterPoint()` to obtain the center `Point` of `display1` through `display6`.
- Asserts that `display.Contains(center)` is `true` for each display's own center point.
- Asserts that `display.Contains(center)` is `false` when comparing display bounds against center points of other displays within the same group (`[1, 2, 3]` and `[4, 5, 6]`).

---

### 2. `SmallRectanglesContained()`

#### Purpose
Tests centered bounding box placement for the first display group (`display1`, `display2`, `display3`).

#### Implementation Details
- Sets a fixed `sideLength` of `40`.
- Calculates top-left coordinates (`smallLeft`, `smallTop`) to center a 40x40 `Rect` inside each display's center point.
- Asserts that each generated 40x40 rectangle (`smallRect1`, `smallRect2`, `smallRect3`) is contained inside its parent display (`Assert.True`).
- Asserts that each small rectangle is NOT contained inside the other displays in the setup (`Assert.False`).

---

### 3. `SmallRectanglesContained456()`

#### Purpose
Tests centered bounding box placement for the second display group (`display4`, `display5`, `display6`).

#### Implementation Details
- Uses the same logic as `SmallRectanglesContained()`.
- Generates 40x40 rectangles (`smallRect4`, `smallRect5`, `smallRect6`) centered in `display4`, `display5`, and `display6`.
- Validates that each small rectangle is contained within its corresponding display rect and not within the sibling display rects.

---

### 4. `CompareDapploToWinForms()`

#### Purpose
Validates system display integration by comparing display metrics retrieved via `Dapplo.Windows.User32.DisplayInfo` against `System.Windows.Forms.Screen`.

#### Implementation Details
- Queries active monitors using both APIs:
  - `Dapplo.Windows.User32.DisplayInfo.AllDisplayInfos`
  - `System.Windows.Forms.Screen.AllScreens`
- Asserts that both libraries detect the same number of displays (`Assert.Equal(dapploDisplays.Length, winFormsDisplays.Length)`).
- Iterates over the displays and calculates the center point for both Dapplo bounds and converted WinForms bounds (`winFormsDisplays[i].Bounds.AsRect()`).
- Asserts that the calculated center points match between both APIs (`Assert.Equal(dapploCenterPoint, winFormsCenterPoint)`).

---

## Extension Methods Used

The test class utilizes custom extension methods defined in the project:
- **`.CenterPoint()`**: Extends `Rect` to compute its center `Point`.
- **`.AsRect()`**: Converts a `System.Drawing.Rectangle` (from WinForms bounds) to a `System.Windows.Rect`.