# Technical Documentation: `ColorHelper.cs`

## Overview

The `ColorHelper` class in `Text-Grab.Utilities` is a static utility class that provides helper methods to convert color types between the GDI+ namespace (`System.Drawing`) and the WPF media namespace (`System.Windows.Media`). 

It serves as a bridge for interoperability when UI components or underlying logic require WPF color structures (`Color` or `SolidColorBrush`) created from standard GDI+ `System.Drawing.Color` instances.

---

## File Information

* **File Path:** `Text-Grab/Utilities/ColorHelper.cs`
* **Namespace:** `Text_Grab.Utilities`
* **Class Name:** `ColorHelper`
* **Access Modifier:** `public static`

---

## Class Methods

### 1. `MediaColorFromDrawingColor`

Converts a `System.Drawing.Color` structure to a `System.Windows.Media.Color` structure.

#### Declaration
```csharp
public static System.Windows.Media.Color MediaColorFromDrawingColor(System.Drawing.Color drawingColor)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `drawingColor` | `System.Drawing.Color` | The GDI+ color instance to be converted. |

#### Return Value
* **Type:** `System.Windows.Media.Color`
* **Description:** A WPF `Color` containing the identical Alpha (A), Red (R), Green (G), and Blue (B) color channel values from the input `drawingColor`.

#### Internal Logic
Extracts the ARGB channels (`drawingColor.A`, `drawingColor.R`, `drawingColor.G`, `drawingColor.B`) and invokes `System.Windows.Media.Color.FromArgb(...)` to construct the new WPF color object.

---

### 2. `SolidColorBrushFromDrawingColor`

Creates a WPF `SolidColorBrush` directly from a `System.Drawing.Color`.

#### Declaration
```csharp
public static SolidColorBrush SolidColorBrushFromDrawingColor(System.Drawing.Color drawingColor)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `drawingColor` | `System.Drawing.Color` | The GDI+ color instance used to construct the brush color. |

#### Return Value
* **Type:** `System.Windows.Media.SolidColorBrush`
* **Description:** A new `SolidColorBrush` initialized with the converted WPF color.

#### Internal Logic
1. Calls `MediaColorFromDrawingColor(drawingColor)` to convert the input into a `System.Windows.Media.Color`.
2. Instantiates and returns a new `SolidColorBrush` using the converted WPF color.

---

## Dependencies

* `System.Windows.Media`: Provides the target WPF `Color` and `SolidColorBrush` types.
* `System.Drawing`: Provides the source `System.Drawing.Color` type passed into the conversion methods.