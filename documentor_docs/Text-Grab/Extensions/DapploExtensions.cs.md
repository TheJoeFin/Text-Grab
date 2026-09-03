# Technical Documentation: `Text-Grab/Extensions/DapploExtensions.cs`

## Overview

The `DapploExtensions.cs` file provides static extension methods for the `DisplayInfo` type (from the `Dapplo.Windows.User32` library) within the `Text_Grab.Extensions` namespace. Its primary purpose is to adjust display metrics (`Rect` bounds and center `Point`) according to the monitor's specific DPI scale factor retrieved via native Windows APIs.

---

## File Information

* **File Path:** `Text-Grab/Extensions/DapploExtensions.cs`
* **Namespace:** `Text_Grab.Extensions`
* **Dependencies:**
  * `Dapplo.Windows.User32`
  * `System.Windows`

---

## Class Definition

### `public static class DapploExtensions`

A static utility class containing extension methods designed to perform scale-adjusted geometric calculations on `DisplayInfo` objects.

---

## Extension Methods

### 1. `ScaledCenterPoint`

Calculates the center point of a display monitor scaled according to the monitor's current DPI scaling factor.

#### Signature
```csharp
public static Point ScaledCenterPoint(this DisplayInfo displayInfo)
```

#### Parameters
* **`displayInfo`** (`DisplayInfo`): The target display instance on which the extension method is called.

#### Return Value
* **`Point`** (`System.Windows.Point`): A WPF `Point` representing the scaled center coordinates `(X, Y)` of the monitor.

#### How It Works
1. Retrieves the display's unscaled bounds (`Rect displayRect`) from `displayInfo.Bounds`.
2. Calls `NativeMethods.GetScaleFactorForMonitor` passing `displayInfo.MonitorHandle` to retrieve the `uint scaleFactor` (e.g., `100` for 100%, `125` for 125%, `150` for 150%).
3. Converts the scale factor to a floating-point fraction:
   $$\text{scaleFraction} = \frac{\text{scaleFactor}}{100.0}$$
4. Retrieves the raw center point (`Point rawCenter`) from `displayRect.CenterPoint()`.
5. Calculates the scaled center coordinates by dividing both `X` and `Y` by `scaleFraction`:
   $$\text{X}_{\text{scaled}} = \frac{\text{rawCenter.X}}{\text{scaleFraction}}$$
   $$\text{Y}_{\text{scaled}} = \frac{\text{rawCenter.Y}}{\text{scaleFraction}}$$
6. Returns the scaled `Point`.

---

### 2. `ScaledBounds`

Calculates the bounding rectangle (`Rect`) of a display monitor scaled according to the monitor's current DPI scaling factor.

#### Signature
```csharp
public static Rect ScaledBounds(this DisplayInfo displayInfo)
```

#### Parameters
* **`displayInfo`** (`DisplayInfo`): The target display instance on which the extension method is called.

#### Return Value
* **`Rect`** (`System.Windows.Rect`): A WPF `Rect` representing the scaled origin position (`X`, `Y`) and dimensions (`Width`, `Height`) of the display.

#### How It Works
1. Retrieves the display bounds (`Rect displayRect`) from `displayInfo.Bounds`.
2. Retrieves the monitor's scale factor using `NativeMethods.GetScaleFactorForMonitor(displayInfo.MonitorHandle, out uint scaleFactor)`.
3. Converts the scale factor to a decimal fraction (`scaleFraction = scaleFactor / 100.0`).
4. Scales all four properties of the bounding rectangle (`X`, `Y`, `Width`, `Height`) by dividing each by `scaleFraction`:
   * $\text{X}_{\text{scaled}} = \frac{\text{displayRect.X}}{\text{scaleFraction}}$
   * $\text{Y}_{\text{scaled}} = \frac{\text{displayRect.Y}}{\text{scaleFraction}}$
   * $\text{Width}_{\text{scaled}} = \frac{\text{displayRect.Width}}{\text{scaleFraction}}$
   * $\text{Height}_{\text{scaled}} = \frac{\text{displayRect.Height}}{\text{scaleFraction}}$
5. Constructs and returns a new `Rect` with these scaled values.

---

## Scaling Logic Summary

Both methods perform scaling using the formula:

$$\text{Value}_{\text{scaled}} = \frac{\text{Value}_{\text{raw}}}{\frac{\text{scaleFactor}}{100.0}}$$

This converts unscaled system screen coordinates into scaled logical WPF coordinates for proper placement and sizing across displays with different DPI settings.