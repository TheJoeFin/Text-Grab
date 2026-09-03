# Technical Documentation: `GrabFrameViewScaleUtilities.cs`

## Overview

The `GrabFrameViewScaleUtilities` class is a static utility class in the `Text_Grab.Utilities` namespace. It provides constants and helper methods for handling view scaling, enforcing minimum window dimensions, positioning windows within display work areas, and stepping scale values up or down.

---

## Class Information

- **Namespace:** `Text_Grab.Utilities`
- **Class:** `public static class GrabFrameViewScaleUtilities`
- **Dependencies:** `System`, `System.Windows`, `Text_Grab`

---

## Constants

The class defines five public constant values used to set scaling bounds, minimum window dimensions, and zoom step increments:

| Constant Name | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `MaximumLoadedDocumentScale` | `double` | `5.0` | The upper limit for document scale (500%). |
| `MinimumLoadedDocumentScale` | `double` | `0.5` | The lower limit for document scale (50%). |
| `MinimumLoadedDocumentWindowHeight` | `double` | `450` | Default minimum height bound for a window. |
| `MinimumLoadedDocumentWindowWidth` | `double` | `800` | Default minimum width bound for a window. |
| `ScaleStep` | `double` | `0.25` | The increment value used when stepping scale up or down (25%). |

---

## Method Documentation

### 1. `CoerceScale(double scale)`

Coerces a provided scale value into the valid range defined by `MinimumLoadedDocumentScale` and `MaximumLoadedDocumentScale`.

#### Signature
```csharp
public static double CoerceScale(double scale)
```

#### Parameters
- **`scale`** (`double`): The target scale value to validate and constrain.

#### Returns
- **`double`**: 
  - `1.0` if `scale` is not a finite number (`double.IsFinite(scale)` returns `false`).
  - A clamped `double` value bounded between `MinimumLoadedDocumentScale` (`0.5`) and `MaximumLoadedDocumentScale` (`5.0`).

#### Logic Flow
1. Checks if `scale` is finite using `double.IsFinite(scale)`. If false, returns `1.0`.
2. Clamps `scale` between `0.5` and `5.0` using `Math.Clamp`.

---

### 2. `GetMinimumWindowRect(Rect currentWindowRect, Size minimumWindowSize, Rect workArea)`

Calculates a new window rectangle (`Rect`) that satisfies minimum size constraints while attempting to preserve the window's original center point and stay within the bounds of a specified work area (screen display bounds).

#### Signature
```csharp
public static Rect GetMinimumWindowRect(Rect currentWindowRect, Size minimumWindowSize, Rect workArea)
```

#### Parameters
- **`currentWindowRect`** (`Rect`): The current bounds of the target window.
- **`minimumWindowSize`** (`Size`): The minimum width and height required for the window.
- **`workArea`** (`Rect`): The target display work area bounds (e.g., monitor screen bounds minus taskbars).

#### Returns
- **`Rect`**: A calculated `Rect` meeting minimum size requirements and clamped within the `workArea`. Returns `currentWindowRect` directly if `currentWindowRect` is invalid.

#### Logic Flow
1. **Validation**: Calls `currentWindowRect.IsGood()`. If `false`, returns `currentWindowRect` immediately.
2. **Target Dimensions Calculation**:
   - `targetWidth` = `Math.Max(currentWindowRect.Width, minimumWindowSize.Width)`
   - `targetHeight` = `Math.Max(currentWindowRect.Height, minimumWindowSize.Height)`
3. **Centering**:
   - Determines center point coordinates `(centerX, centerY)` from `currentWindowRect`.
   - Constructs a `desiredRect` centered at `(centerX, centerY)` with dimensions `targetWidth` and `targetHeight`.
4. **Work Area Validation**:
   - Calls `workArea.IsGood()`. If `false`, returns `desiredRect`.
5. **Work Area Boundary Fitting**:
   - Constrains width and height to not exceed `workArea.Width` and `workArea.Height` via `Math.Min`.
   - Clamps the `Left` position between `workArea.Left` and `workArea.Right - width`.
   - Clamps the `Top` position between `workArea.Top` and `workArea.Bottom - height`.
6. Returns the final repositioned and resized `Rect(left, top, width, height)`.

---

### 3. `StepScale(double currentScale, int direction)`

Increments or decrements a scale value by `ScaleStep` (`0.25`) based on the specified direction, ensuring the result is properly coerced within valid scale boundaries.

#### Signature
```csharp
public static double StepScale(double currentScale, int direction)
```

#### Parameters
- **`currentScale`** (`double`): The starting scale value.
- **`direction`** (`int`): An integer indicating the direction to scale:
  - `< 0`: Zoom out / decrease scale.
  - `> 0`: Zoom in / increase scale.
  - `0`: No change.

#### Returns
- **`double`**: The updated scale value after applying the step increment/decrement and running `CoerceScale`.

#### Logic Flow
1. Coerces `currentScale` via `CoerceScale(currentScale)`.
2. Normalizes `direction` into `-1`, `1`, or `0`:
   - Negative values map to `-1`.
   - Positive values map to `1`.
   - `0` maps to `0`.
3. If normalized direction is `0`, returns `coercedScale` unchanged.
4. Adds `(normalizedDirection * ScaleStep)` to `coercedScale`.
5. Passes the new sum through `CoerceScale` to ensure it stays within bounds, then returns the result.