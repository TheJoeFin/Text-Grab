# Documentation: `Text-Grab/Extensions/ControlExtensions.cs`

## Overview

The `ControlExtensions` static class provides extension methods for Windows Presentation Foundation (WPF) UI elements (`Viewbox` and `FrameworkElement`). These methods perform geometric calculations to determine:
1. The effective scaling factor applied to content inside a `Viewbox`.
2. The absolute visual bounding rectangle (`Rect`) of a `FrameworkElement`, either relative to the screen or relative to the application's main window.

---

## Namespace & Dependencies

**Namespace:** `Text_Grab`

**Dependencies:**
- `System`
- `System.Windows`
- `System.Windows.Controls`

---

## Class Definition

```csharp
public static class ControlExtensions
```

A public static container class containing extension methods designed for WPF visual controls.

---

## Methods

### 1. `GetHorizontalScaleFactor`

Calculates the actual scale factor applied to a `Viewbox` child element. Although named `GetHorizontalScaleFactor`, it takes into account WPF `Viewbox` `Stretch="Uniform"` behavior by returning the minimum of the horizontal and vertical scale ratios to reflect true content scaling.

#### Signature
```csharp
public static double GetHorizontalScaleFactor(this Viewbox viewbox)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `viewbox` | `Viewbox` | The target WPF `Viewbox` control instance. |

#### Return Value
* **Type:** `double`
* **Value:** The calculated uniform scale factor applied to the child control. Returns `1.0` if the child is invalid, non-existent, or if dimensions are infinite or below minimum valid thresholds.

#### Implementation Logic
1. **Child Validation:**
   Checks if `viewbox.Child` is a `FrameworkElement`. If not, returns `1.0`.
2. **Dimension Retrieval:**
   Obtains `ActualWidth` and `ActualHeight` for both the container (`viewbox`) and the inner content (`childElement`).
3. **Horizontal Sanity Checks:**
   Validates that width values are finite, `outsideWidth > 0`, and `insideWidth > 4`. If any condition fails, returns `1.0`.
4. **Horizontal Scale Calculation:**
   Calculates the initial horizontal ratio: `scale = outsideWidth / insideWidth`.
5. **Vertical Constraint Adjustment (Uniform Scaling):**
   Checks if height values are finite, `outsideHeight > 0`, and `insideHeight > 4`. If valid, calculates vertical ratio (`scaleY = outsideHeight / insideHeight`) and updates `scale` to `Math.Min(scale, scaleY)`. This accounts for uniform scaling constraints where vertical bounds limit overall visual scaling.
6. **Final Validation:**
   Verifies that `scale` is finite and greater than `0`. Returns `1.0` if invalid; otherwise, returns the computed `scale`.

---

### 2. `GetAbsolutePlacement`

Calculates the positioning rectangle (`Rect`) of a `FrameworkElement`. It determines the position relative to either the physical screen or the application's `MainWindow`.

#### Signature
```csharp
public static Rect GetAbsolutePlacement(this FrameworkElement element, bool relativeToScreen = false)
```

#### Parameters
| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `element` | `FrameworkElement` | *Required* | The target WPF `FrameworkElement` whose visual bounds are being queried. |
| `relativeToScreen` | `bool` | `false` | If `true`, returns coordinates relative to the screen. If `false`, returns coordinates relative to `Application.Current.MainWindow`. |

#### Return Value
* **Type:** `Rect`
* **Value:** A `Rect` containing `(X, Y)` position coordinates along with `ActualWidth` and `ActualHeight` of the element. Returns `Rect.Empty` if position translation fails.

#### Implementation Logic
1. **Screen Point Translation:**
   Attempts to map the element's origin point `(0, 0)` to screen coordinates using `element.PointToScreen(new Point(0, 0))`.
2. **Exception Handling:**
   If `PointToScreen` fails (e.g., if the element is not rendered or disconnected from a visual tree):
   * Catches `System.Exception`.
   * Under `DEBUG` build configurations (`#if DEBUG`), rethrows the exception.
   * Under non-DEBUG configurations, returns `Rect.Empty`.
3. **Screen-Relative Mode:**
   If `relativeToScreen` is set to `true`, returns a `Rect` constructed from the screen coordinates (`absolutePos.X`, `absolutePos.Y`) and the element's dimensions (`ActualWidth`, `ActualHeight`).
4. **MainWindow-Relative Mode:**
   If `relativeToScreen` is `false`:
   * Retrieves screen position of `Application.Current.MainWindow` at `(0, 0)`.
   * Subtracts the main window's screen coordinates from the element's screen coordinates:
     * `X = absolutePos.X - posMW.X`
     * `Y = absolutePos.Y - posMW.Y`
   * Returns a `Rect` constructed with these relative coordinates and the element's `ActualWidth` and `ActualHeight`.