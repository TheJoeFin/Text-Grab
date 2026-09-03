# Documentation: `Tests/FullscreenGrabWindowLayoutTests.cs`

## Overview

The `FullscreenGrabWindowLayoutTests` class contains unit tests written for the `Text_Grab` application. Its primary purpose is to test and verify the layout-related calculations and window state management helper methods defined in `FullscreenGrab` (from `Text_Grab.Views`).

Specifically, these tests validate:
1. The calculation of the clip bounds rect based on window dimensions (`GetFullscreenClipBounds`).
2. The conditional logic determining whether the window must be forcefully maximized based on its current `WindowState`, width, and height (`ShouldForceMaximize`).

---

## File Metadata

* **File Path:** `Tests/FullscreenGrabWindowLayoutTests.cs`
* **Namespace:** `Tests`
* **Dependencies / Imports:**
  * `System.Windows` (provides `Rect`, `Size`, and `WindowState`)
  * `Text_Grab.Views` (provides access to the `FullscreenGrab` view class)

---

## Test Methods Summary

| Test Method | Input Parameters | Purpose / Assertion |
| :--- | :--- | :--- |
| `GetFullscreenClipBounds_UsesRenderedWindowSize` | `double width`, `double height` | Asserts that `FullscreenGrab.GetFullscreenClipBounds` returns a `Rect` originating at `(0, 0)` with the given width and height. |
| `ShouldForceMaximize_ReturnsTrue_WhenOverlayIsNotFullScreen` | `WindowState state`, `double width`, `double height` | Asserts that `FullscreenGrab.ShouldForceMaximize` returns `true` when the window state is not maximized or when dimensions are below expected limits. |
| `ShouldForceMaximize_ReturnsFalse_WhenMaximizedAndLargeEnough` | `double width`, `double height` | Asserts that `FullscreenGrab.ShouldForceMaximize` returns `false` when the window is maximized and has sufficient dimensions. |

---

## Detailed Test Method Descriptions

### 1. `GetFullscreenClipBounds_UsesRenderedWindowSize`

```csharp
[Theory]
[InlineData(40, 40)]
[InlineData(1920, 1080)]
public void GetFullscreenClipBounds_UsesRenderedWindowSize(double width, double height)
```

* **Type:** Data-driven unit test (`[Theory]`)
* **Test Data Cases:**
  * `width: 40`, `height: 40`
  * `width: 1920`, `height: 1080`
* **Execution Logic:**
  1. Constructs an expected `System.Windows.Rect` object starting at position `(0, 0)` with dimensions `width` and `height`.
  2. Invokes `FullscreenGrab.GetFullscreenClipBounds(new Size(width, height))`.
  3. Asserts that the returned `Rect` matches the expected `Rect` using `Assert.Equal`.

---

### 2. `ShouldForceMaximize_ReturnsTrue_WhenOverlayIsNotFullScreen`

```csharp
[Theory]
[InlineData(WindowState.Normal, 1920, 1080)]   // not maximized -> force
[InlineData(WindowState.Minimized, 1920, 1080)] // not maximized -> force
[InlineData(WindowState.Maximized, 40, 40)]     // tiny despite maximized -> force
[InlineData(WindowState.Maximized, 1920, 100)]  // too short -> force
[InlineData(WindowState.Maximized, 100, 1080)]  // too narrow -> force
public void ShouldForceMaximize_ReturnsTrue_WhenOverlayIsNotFullScreen(WindowState state, double width, double height)
```

* **Type:** Data-driven unit test (`[Theory]`)
* **Test Data Cases:**
  * `WindowState.Normal`, `1920x1080`: Triggers force-maximize because state is not `Maximized`.
  * `WindowState.Minimized`, `1920x1080`: Triggers force-maximize because state is not `Maximized`.
  * `WindowState.Maximized`, `40x40`: Triggers force-maximize because dimensions are too small.
  * `WindowState.Maximized`, `1920x100`: Triggers force-maximize because height (`100`) is too short.
  * `WindowState.Maximized`, `100x1080`: Triggers force-maximize because width (`100`) is too narrow.
* **Execution Logic:**
  1. Calls `FullscreenGrab.ShouldForceMaximize(state, width, height)`.
  2. Asserts that the return value is `true` using `Assert.True`.

---

### 3. `ShouldForceMaximize_ReturnsFalse_WhenMaximizedAndLargeEnough`

```csharp
[Theory]
[InlineData(1920, 1080)]
[InlineData(1366, 768)]
[InlineData(200, 200)]
public void ShouldForceMaximize_ReturnsFalse_WhenMaximizedAndLargeEnough(double width, double height)
```

* **Type:** Data-driven unit test (`[Theory]`)
* **Test Data Cases:**
  * `1920x1080` (with `WindowState.Maximized`)
  * `1366x768` (with `WindowState.Maximized`)
  * `200x200` (with `WindowState.Maximized`)
* **Execution Logic:**
  1. Calls `FullscreenGrab.ShouldForceMaximize(WindowState.Maximized, width, height)`.
  2. Asserts that the return value is `false` using `Assert.False`.