# Text-Grab Documentation: `ShapeExtensions.cs`

## Overview

The `ShapeExtensions` static class is located within the `Text_Grab` namespace in `Text-Grab/Extensions/ShapeExtensions.cs`. It provides a set of extension methods for geometric shapes, primarily bridging `System.Drawing.Rectangle` and WPF's `System.Windows.Rect`.

Its main responsibilities include:
- Converting between `System.Drawing.Rectangle` and `System.Windows.Rect`.
- Scaling `System.Windows.Rect` instances based on DPI settings or custom scale factors.
- Validating the mathematical integrity of a `System.Windows.Rect`.
- Calculating geometric properties, such as finding the center point of a `Rect`.

---

## Technical Details

- **Namespace:** `Text_Grab`
- **Class Type:** `public static class ShapeExtensions`
- **Dependencies:**
  - `System`
  - `System.Drawing`
  - `System.Windows`

---

## Method Documentation

### Type Conversions

#### `AsRect(this Rectangle rectangle)`
Converts a `System.Drawing.Rectangle` (which uses integer-based dimensions) into a `System.Windows.Rect` (which uses double-precision dimensions).

* **Parameters:**
  * `rectangle` (`System.Drawing.Rectangle`): The source rectangle.
* **Returns:** `System.Windows.Rect` — A WPF rectangle constructed with the same `X`, `Y`, `Width`, and `Height` values.

---

#### `AsRectangle(this Rect rect)`
Converts a `System.Windows.Rect` into a `System.Drawing.Rectangle` by explicitly casting double-precision fields (`X`, `Y`, `Width`, `Height`) to integer values (`int`).

* **Parameters:**
  * `rect` (`System.Windows.Rect`): The source WPF rectangle.
* **Returns:** `System.Drawing.Rectangle` — A drawing rectangle with integer coordinates and dimensions.

---

### DPI & Fractional Scaling

#### `GetScaledDownByDpi(this Rect rect, DpiScale dpi)`
Scales down a `Rect` instance by dividing its position (`X`, `Y`) and dimensions (`Width`, `Height`) by the horizontal and vertical DPI scale factors from a `DpiScale` object.

* **Parameters:**
  * `rect` (`System.Windows.Rect`): The source rectangle.
  * `dpi` (`System.Windows.DpiScale`): The target DPI scale factors (`DpiScaleX` and `DpiScaleY`).
* **Returns:** `System.Windows.Rect` — A new scaled-down `Rect`.

---

#### `GetScaledUpByDpi(this Rect rect, DpiScale dpi)`
Scales up a `Rect` instance by multiplying its position (`X`, `Y`) and dimensions (`Width`, `Height`) by the horizontal and vertical DPI scale factors from a `DpiScale` object.

* **Parameters:**
  * `rect` (`System.Windows.Rect`): The source rectangle.
  * `dpi` (`System.Windows.DpiScale`): The target DPI scale factors (`DpiScaleX` and `DpiScaleY`).
* **Returns:** `System.Windows.Rect` — A new scaled-up `Rect`.

---

#### `GetScaledUpByFraction(this Rect rect, Double scaleFactor)`
Scales both the position (`X`, `Y`) and dimensions (`Width`, `Height`) of a `Rect` uniformly by multiplying each property by `scaleFactor`.

* **Parameters:**
  * `rect` (`System.Windows.Rect`): The source rectangle.
  * `scaleFactor` (`System.Double`): The multiplier applied to `X`, `Y`, `Width`, and `Height`.
* **Returns:** `System.Windows.Rect` — A new uniformly scaled `Rect`.

---

#### `GetScaleSizeByFraction(this Rect rect, Double scaleFactor)`
Scales only the dimensions (`Width` and `Height`) of a `Rect` by multiplying them by `scaleFactor`, while leaving the top-left origin coordinates (`X` and `Y`) unchanged.

* **Parameters:**
  * `rect` (`System.Windows.Rect`): The source rectangle.
  * `scaleFactor` (`System.Double`): The multiplier applied to `Width` and `Height`.
* **Returns:** `System.Windows.Rect` — A new `Rect` with scaled dimensions at the original origin.

---

### Validation and Geometry

#### `IsGood(this Rect rect)`
Determines whether a `Rect` is valid and well-formed for processing.

* **Parameters:**
  * `rect` (`System.Windows.Rect`): The rectangle to evaluate.
* **Returns:** `bool` — Returns `true` if all properties (`X`, `Y`, `Width`, `Height`) are valid real numbers, and both `Width` and `Height` are non-zero. Returns `false` if any property meets any of the following conditions:
  * Is `double.IsNaN`
  * Is `double.IsNegativeInfinity`
  * Is `double.IsPositiveInfinity`
  * Has `Width == 0` or `Height == 0`

---

#### `CenterPoint(this Rect rect)`
Calculates the geometric center point of the provided `Rect`.

* **Parameters:**
  * `rect` (`System.Windows.Rect`): The source rectangle.
* **Returns:** `System.Windows.Point` — A point with coordinates:
  * `x = rect.Left + (rect.Width / 2)`
  * `y = rect.Top + (rect.Height / 2)`