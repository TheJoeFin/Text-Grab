# Technical Documentation: `FreeformCaptureUtilitiesTests.cs`

## Overview

The `FreeformCaptureUtilitiesTests` class contains unit tests written in C# to verify the core functionality of the utility methods provided by `Text_Grab.Utilities.FreeformCaptureUtilities`. The tests leverage the xUnit framework alongside `[WpfFact]` attributes to handle WPF thread context requirements when processing WPF graphics types (`System.Windows.Point`, `Rect`, and `PathGeometry`).

---

## File Details

- **File Path:** `Tests/FreeformCaptureUtilitiesTests.cs`
- **Namespace:** `Tests`
- **Tested Target:** `Text_Grab.Utilities.FreeformCaptureUtilities`
- **Dependencies & Namespaces Used:**
  - `System.Drawing`: Provides bitmap and graphics context operations.
  - `System.Windows`: Provides foundational WPF structs (`Rect`, `Point`).
  - `System.Windows.Media`: Provides WPF vector graphics elements (`PathGeometry`).
  - `Text_Grab.Utilities`: Contains the target `FreeformCaptureUtilities` class under test.
  - `Point = System.Windows.Point`: Explicit alias resolving `Point` to WPF's `System.Windows.Point` struct.

---

## Key Components & Test Methods

The test class contains three individual test methods, each validating a distinct utility method in `FreeformCaptureUtilities`.

### 1. `GetBounds_RoundsOutwardToIncludeAllPoints()`

- **Attribute:** `[WpfFact]`
- **Target Method:** `FreeformCaptureUtilities.GetBounds(List<Point>)`
- **Purpose:** Verifies that calculating the bounding box for a set of floating-point coordinates expands/rounds outward to integer boundaries to fully enclose all specified points.
- **Workflow:**
  1. Initializes a list of `System.Windows.Point` elements with fractional coordinates:
     - `(1.2, 2.8)`
     - `(10.1, 4.2)`
     - `(4.6, 9.9)`
  2. Executes `FreeformCaptureUtilities.GetBounds(points)`.
  3. Asserts that the returned `Rect` matches a bounding box spanning from top-left `(1, 2)` to bottom-right `(11, 10)`.

---

### 2. `BuildGeometry_CreatesClosedFigure()`

- **Attribute:** `[WpfFact]`
- **Target Method:** `FreeformCaptureUtilities.BuildGeometry(List<Point>)`
- **Purpose:** Ensures that generating vector geometry from a sequence of points produces a single, properly closed `PathGeometry` figure containing the expected number of line segments.
- **Workflow:**
  1. Defines a 3-point polygonal boundary:
     - `(0, 0)`
     - `(4, 0)`
     - `(4, 4)`
  2. Calls `FreeformCaptureUtilities.BuildGeometry(points)` to produce a `PathGeometry`.
  3. Asserts the following on the resulting geometry:
     - Contains exactly 1 `PathFigure` (`geometry.Figures`).
     - The figure's `StartPoint` equals `points[0]` (`(0, 0)`).
     - The figure's `IsClosed` property is `true`.
     - The figure contains exactly 2 segments connecting the remaining points.

---

### 3. `CreateMaskedBitmap_WhitensPixelsOutsideThePolygon()`

- **Attribute:** `[WpfFact]`
- **Target Method:** `FreeformCaptureUtilities.CreateMaskedBitmap(Bitmap, List<Point>)`
- **Purpose:** Validates that creating a masked bitmap from a source image and polygonal region correctly preserves pixel colors inside the polygon while modifying pixel colors outside the polygon.
- **Workflow:**
  1. Instantiates a 10x10 pixel source `Bitmap` filled completely with `System.Drawing.Color.Black`.
  2. Defines a rectangular region using points:
     - `(2, 2)`
     - `(7, 2)`
     - `(7, 7)`
     - `(2, 7)`
  3. Calls `FreeformCaptureUtilities.CreateMaskedBitmap(...)` passing the source bitmap and point list.
  4. Asserts the ARGB values of specific pixels on the resulting bitmap:
     - Pixel `(0, 0)` (outside the designated polygon) equals `System.Drawing.Color.Gray`.
     - Pixel `(4, 4)` (inside the designated polygon) remains `System.Drawing.Color.Black`.

---

## Execution Requirements

Because these unit tests instantiate WPF-specific objects (`PathGeometry`, `Point`, `Rect`), each test method is decorated with `[WpfFact]`. This attribute ensures the tests run on a thread configured with a Single-Threaded Apartment (STA) state required by WPF components.