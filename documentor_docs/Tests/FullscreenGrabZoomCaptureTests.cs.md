# Technical Documentation: `FullscreenGrabZoomCaptureTests.cs`

## Overview

The `FullscreenGrabZoomCaptureTests` class contains xUnit unit tests designed to verify the coordinate transformation and cropping rect logic in the `FullscreenGrab.TryGetBitmapCropRectForSelection` method (from the `Text_Grab.Views` namespace).

The primary focus of these tests is ensuring that screen selection rectangles are accurately converted into integer crop rectangles (`Int32Rect`) for frozen bitmaps under various conditions, including:
* Standard 1:1 selection (no zoom or scaling).
* Selection performed within a zoomed/translated view.
* Selection subjected to both zoom scaling and device DPI/scaling matrices.

---

## Class Information

* **Namespace:** `Tests`
* **Class Name:** `FullscreenGrabZoomCaptureTests`
* **Target Method Under Test:** `Text_Grab.Views.FullscreenGrab.TryGetBitmapCropRectForSelection`
* **Dependencies:**
  * `System.Windows` (`Rect`, `Point`, `Int32Rect`)
  * `System.Windows.Media` (`Matrix`, `TransformGroup`, `ScaleTransform`, `TranslateTransform`)
  * `Text_Grab.Views` (`FullscreenGrab`)

---

## Test Methods

### 1. `TryGetBitmapCropRectForSelection_UsesSelectionRectWithoutZoom()`

* **Purpose:** Verifies that when no zoom transform is applied (`zoomTransform = null`) and the matrix is identity, the output `Int32Rect` matches the input selection `Rect` exactly.
* **Inputs:**
  * `selectionRect`: `Rect(10, 20, 30, 40)`
  * `matrix`: `Matrix.Identity`
  * `zoomTransform`: `null`
  * `bitmapWidth`: `200`
  * `bitmapHeight`: `200`
* **Assertions:**
  * Method returns `true`.
  * `cropRect.X` equals `10`.
  * `cropRect.Y` equals `20`.
  * `cropRect.Width` equals `30`.
  * `cropRect.Height` equals `40`.

---

### 2. `TryGetBitmapCropRectForSelection_MapsZoomedSelectionBackToFrozenBitmap()`

* **Purpose:** Verifies that a selection drawn over a zoomed and translated view accurately maps back to the unzoomed source coordinates on the bitmap.
* **Setup:**
  * Constructs a `TransformGroup` containing:
    * `ScaleTransform`: Scale = (2, 2), Center = (50, 50)
    * `TranslateTransform`: Offset = (-10, 15)
  * Defines a source `Rect`: `(40, 50, 20, 10)`
  * Uses `TransformRect` to generate `displayedSelectionRect` by transforming the source rect through the `TransformGroup`.
* **Inputs:**
  * `displayedSelectionRect`: Computed transformed rectangle.
  * `matrix`: `Matrix.Identity`
  * `zoomTransform`: The constructed `TransformGroup`.
  * `bitmapWidth`: `200`
  * `bitmapHeight`: `200`
* **Assertions:**
  * Method returns `true`.
  * Resulting `cropRect` matches the original `sourceRect`:
    * `cropRect.X` equals `40`.
    * `cropRect.Y` equals `50`.
    * `cropRect.Width` equals `20`.
    * `cropRect.Height` equals `10`.

---

### 3. `TryGetBitmapCropRectForSelection_AppliesDeviceScalingAfterUndoingZoom()`

* **Purpose:** Verifies correct calculation when both a zoom scale transform and a device scaling matrix (e.g., DPI scale of 1.5x) are applied.
* **Setup:**
  * `zoomTransform`: `ScaleTransform(2, 2)`
  * `selectionRect`: `Rect(20, 30, 40, 50)`
  * `matrix`: `Matrix(1.5, 0, 0, 1.5, 0, 0)`
* **Inputs:**
  * `selectionRect`: `Rect(20, 30, 40, 50)`
  * `matrix`: Scale matrix with factor 1.5 on X and Y axes.
  * `zoomTransform`: 2x scaling transform.
  * `bitmapWidth`: `200`
  * `bitmapHeight`: `200`
* **Assertions:**
  * Method returns `true`.
  * `cropRect.X` equals `15`.
  * `cropRect.Y` equals `22`.
  * `cropRect.Width` equals `30`.
  * `cropRect.Height` equals `38`.

---

## Private Helper Methods

### `TransformRect(Rect rect, Matrix matrix)`

* **Purpose:** Transforms the four corners of a WPF `Rect` using a `Matrix` and returns a new bounding `Rect` enclosing the transformed points.
* **Signature:** 
  ```csharp
  private static Rect TransformRect(Rect rect, Matrix matrix)
  ```
* **Process:**
  1. Extracts four corner points from `rect`:
     * Top-Left
     * Top-Right (`rect.Right`, `rect.Top`)
     * Bottom-Left (`rect.Left`, `rect.Bottom`)
     * Bottom-Right
  2. Transforms each point using `matrix.Transform(...)`.
  3. Finds the minimum and maximum X and Y values among the transformed points using LINQ (`Min` and `Max`).
  4. Returns a new `Rect` constructed from `Point(left, top)` and `Point(right, bottom)`.