# FreeformCaptureUtilities Technical Documentation

## Overview

The `FreeformCaptureUtilities` class in the `Text_Grab.Utilities` namespace is a static utility class that provides helper methods for processing freeform (polygonal/custom region) screen capture operations. It handles bounding box calculations, creation of WPF vector geometries, and bitmap masking using `System.Drawing` graphics operations.

---

## Class Signature

```csharp
namespace Text_Grab.Utilities;

public static class FreeformCaptureUtilities
```

---

## Dependencies & Type Aliases

The file relies on the following namespaces:
* `System`
* `System.Collections.Generic`
* `System.Drawing` (used for standard GDI+ bitmap and graphics operations)
* `System.Drawing.Drawing2D` (used for antialiasing and path geometries in GDI+)
* `System.Linq`
* `System.Windows`
* `System.Windows.Media` (used for WPF vector path geometries)

### Type Alias
```csharp
using Point = System.Windows.Point;
```
*Note: Throughout this class, `Point` explicitly refers to `System.Windows.Point` rather than `System.Drawing.Point`.*

---

## Public Methods

### 1. `GetBounds`

Calculates a bounding box (`Rect`) that encompasses a given set of points.

#### Method Signature
```csharp
public static Rect GetBounds(IReadOnlyList<Point> points)
```

#### Parameters
* **`points`** (`IReadOnlyList<Point>`): A list of WPF `Point` structures defining a shape or captured path.

#### Returns
* **`Rect`**: The smallest axis-aligned bounding rectangle that encloses all points in the list. Returns `Rect.Empty` if the input list is `null` or contains no elements.

#### Logic & Workflow
1. Checks if `points` is `null` or empty (`points.Count == 0`). If true, returns `Rect.Empty`.
2. Computes the minimum X (`left`), minimum Y (`top`), maximum X (`right`), and maximum Y (`bottom`) values using LINQ.
3. Constructs and returns a new `Rect` using `Math.Floor` for the top-left coordinate `(left, top)` and `Math.Ceiling` for the bottom-right coordinate `(right, bottom)`.

---

### 2. `BuildGeometry`

Constructs a frozen WPF `PathGeometry` object representing a closed polygon based on a collection of points.

#### Method Signature
```csharp
public static PathGeometry BuildGeometry(IReadOnlyList<Point> points)
```

#### Parameters
* **`points`** (`IReadOnlyList<Point>`): A sequence of WPF `Point` structures representing the vertices of a shape.

#### Returns
* **`PathGeometry`**: A frozen WPF `PathGeometry` object. If `points` is `null` or contains fewer than 2 points, returns an empty `PathGeometry`.

#### Logic & Workflow
1. Instantiates a new `PathGeometry`.
2. Validates if `points` is non-null and has a count of at least 2. If not, returns the empty `PathGeometry`.
3. Creates a `PathFigure` with:
   * `StartPoint` set to `points[0]`
   * `IsClosed = true`
   * `IsFilled = true`
4. Iterates over remaining points (skipping the first point) and appends a `LineSegment` for each point to `figure.Segments`.
5. Adds the `PathFigure` to `geometry.Figures`.
6. Freezes the geometry via `geometry.Freeze()` to make it immutable and thread-safe.
7. Returns the frozen `PathGeometry`.

---

### 3. `CreateMaskedBitmap`

Generates a masked `Bitmap` where content outside the defined polygon is cleared to a solid gray background.

#### Method Signature
```csharp
public static Bitmap CreateMaskedBitmap(Bitmap sourceBitmap, IReadOnlyList<Point> pointsRelativeToBounds)
```

#### Parameters
* **`sourceBitmap`** (`Bitmap`): The original image to be masked. Cannot be `null`.
* **`pointsRelativeToBounds`** (`IReadOnlyList<Point>`): A list of points defining the polygon mask, relative to the coordinates of `sourceBitmap`.

#### Returns
* **`Bitmap`**: A new 32-bit ARGB `Bitmap` containing the masked region drawn over a gray background. If `pointsRelativeToBounds` is `null` or has fewer than 3 points, a full copy of `sourceBitmap` is returned.

#### Exceptions
* **`ArgumentNullException`**: Thrown if `sourceBitmap` is `null`.

#### Logic & Workflow
1. Verifies `sourceBitmap` is not `null` using `ArgumentNullException.ThrowIfNull(sourceBitmap)`.
2. Checks if `pointsRelativeToBounds` is `null` or has fewer than 3 points. If so, returns a new copy of `sourceBitmap` (`new Bitmap(sourceBitmap)`).
3. Instantiates `maskedBitmap` with dimensions matching `sourceBitmap` and format `PixelFormat.Format32bppArgb`.
4. Initializes a `Graphics` context from `maskedBitmap` and a `GraphicsPath` object within `using` statements for memory management.
5. Configures `SmoothingMode = SmoothingMode.AntiAlias` on the `Graphics` object.
6. Clears the background of `maskedBitmap` to `System.Drawing.Color.Gray`.
7. Converts the WPF `Point` list into an array of `System.Drawing.PointF` structures and adds it as a polygon to `graphicsPath`.
8. Sets the clipping region of the `Graphics` object to `graphicsPath` using `graphics.SetClip(graphicsPath)`.
9. Draws `sourceBitmap` onto the clipped area.
10. Returns the resulting `maskedBitmap`.

---

## Error & Edge Case Handling Summary

| Scenario | Handled By | Outcome |
| :--- | :--- | :--- |
| `points` is `null` or empty in `GetBounds` | `GetBounds` | Returns `Rect.Empty` |
| `points` count < 2 in `BuildGeometry` | `BuildGeometry` | Returns empty `PathGeometry` |
| `sourceBitmap` is `null` in `CreateMaskedBitmap` | `CreateMaskedBitmap` | Throws `ArgumentNullException` |
| `pointsRelativeToBounds` is `null` or count < 3 in `CreateMaskedBitmap` | `CreateMaskedBitmap` | Returns a copy of `sourceBitmap` |