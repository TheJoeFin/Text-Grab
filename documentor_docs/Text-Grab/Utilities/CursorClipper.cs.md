# Technical Documentation: `CursorClipper.cs`

**File Path:** `Text-Grab/Utilities/CursorClipper.cs`  
**Namespace:** `Text_Grab.Utilities`

---

## Overview

The `CursorClipper` class is a static utility within the `Text_Grab.Utilities` namespace designed to constrain (clip) the system mouse cursor to the visual boundaries of a specific WPF `FrameworkElement`, as well as remove any existing cursor constraints. This functionality is typically used during drag or selection operations where cursor movement needs to be locked within a defined region on screen.

---

## Class Signature

```csharp
public static class CursorClipper
```

---

## Methods

### 1. `ClipCursor`

Constrains the system mouse cursor to the bounding area of a specified `FrameworkElement`.

#### Signature
```csharp
public static bool ClipCursor(FrameworkElement element)
```

#### Parameters
* **`element`** (`FrameworkElement`): The target WPF element whose bounding area will constrain the cursor.

#### Returns
* **`bool`**: Returns `true` if the cursor constraint was successfully applied via Win32 OS Interop; otherwise, `false` if the element's visual source or composition target cannot be resolved.

#### Detailed Execution Flow
1. **Define Baseline DPI:** Establishes a standard baseline DPI value (`dpi96 = 96.0`).
2. **Retrieve Screen Coordinates:** Converts the element's relative top-left point `(0, 0)` to absolute screen coordinates using `element.PointToScreen(new Point(0, 0))`.
3. **Validate Visual Source:** Acquires the `PresentationSource` from the visual element using `PresentationSource.FromVisual(element)`.
   * If `source` or `source.CompositionTarget` is `null`, the method aborts and returns `false`.
4. **Calculate DPI Scaling:** Obtains the horizontal (`M11`) and vertical (`M22`) device transformation factors from `CompositionTarget.TransformToDevice` and scales them relative to standard 96 DPI:
   $$\text{dpiX} = 96.0 \times \text{TransformToDevice.M11}$$
   $$\text{dpiY} = 96.0 \times \text{TransformToDevice.M22}$$
5. **Compute Bounding Dimensions:** Calculates the pixel width and height of the target element, factoring in DPI scaling and an additional 1-pixel buffer:
   $$\text{width} = \lfloor (\text{ActualWidth} + 1) \times \frac{\text{dpiX}}{96.0} \rfloor$$
   $$\text{height} = \lfloor (\text{ActualHeight} + 1) \times \frac{\text{dpiY}}{96.0} \rfloor$$
6. **Construct Bounding Rectangle:** Initializes an `OSInterop.RECT` structure defining screen coordinates:
   * `left`: `(int)topLeft.X`
   * `top`: `(int)topLeft.Y`
   * `right`: `(int)topLeft.X + width`
   * `bottom`: `(int)topLeft.Y + height`
7. **Apply Interop Call:** Invokes `OSInterop.ClipCursor(ref rect)` with the constructed rectangle and returns the result.

---

### 2. `UnClipCursor`

Removes any previously applied bounding constraint on the mouse cursor, allowing it to move freely across the entire screen space.

#### Signature
```csharp
public static bool UnClipCursor()
```

#### Parameters
* *None*

#### Returns
* **`bool`**: Returns `true` if the cursor constraint was successfully removed; otherwise, `false`.

#### Detailed Execution Flow
1. Calls `OSInterop.ClipCursor(IntPtr.Zero)`, passing a null pointer to the native API to release the clipping boundary.
2. Returns the boolean result of the `OSInterop.ClipCursor` call.

---

## External Dependencies Referenced in Code

* **`System`**: Provides core system types, specifically `IntPtr`.
* **`System.Windows`**: Provides WPF framework types: `FrameworkElement`, `Point`, and `PresentationSource`.
* **`OSInterop`**: An internal utility class (external to this file) providing interop data structures (`OSInterop.RECT`) and P/Invoke method definitions (`OSInterop.ClipCursor`).