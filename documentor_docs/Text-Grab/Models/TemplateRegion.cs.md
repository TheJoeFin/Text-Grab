# Technical Documentation: `TemplateRegion.cs`

**File Location:** `Text-Grab/Models/TemplateRegion.cs`  
**Namespace:** `Text_Grab.Models`  

---

## Overview

The `TemplateRegion` class represents a specific target capture area within a template (`GrabTemplate`). 

Instead of storing hardcoded pixel coordinates, bounding box positions and dimensions are saved as normalized ratios ranging from `0.0` to `1.0` relative to the reference image size. This design allows regions to automatically scale across different screen resolutions, display sizes, and DPI settings.

---

## Class Definition

```csharp
public class TemplateRegion
```

### Dependencies
* **`System.Windows`**: Provides the `Rect` struct used for representing standard pixel-based bounding boxes (X, Y, Width, Height).

---

## Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `RegionNumber` | `int` | `1` | A 1-based numeric identifier for the region. Displayed on UI borders and referenced as the `{RegionNumber}` placeholder in template output. |
| `Label` | `string` | `string.Empty` | An optional user-friendly label (e.g., `"Name"`, `"Email"`). Displayed on the region's border in the template designer. |
| `RatioLeft` | `double` | `0` | Normalized horizontal position (left offset) of the region as a ratio (`0.0` to `1.0`) of the full reference image width. |
| `RatioTop` | `double` | `0` | Normalized vertical position (top offset) of the region as a ratio (`0.0` to `1.0`) of the full reference image height. |
| `RatioWidth` | `double` | `0` | Normalized width of the region as a ratio (`0.0` to `1.0`) of the full reference image width. |
| `RatioHeight` | `double` | `0` | Normalized height of the region as a ratio (`0.0` to `1.0`) of the full reference image height. |
| `DefaultValue` | `string` | `string.Empty` | An optional fallback string returned if an OCR operation yields no text for this region. |

---

## Constructors

### `TemplateRegion()`
```csharp
public TemplateRegion()
```
Parameterless constructor that initializes a default instance with standard default property values (`RegionNumber = 1`, all ratios set to `0`, empty strings for `Label` and `DefaultValue`).

---

## Methods

### `ToAbsoluteRect`

Converts the relative ratio coordinates of the region into an absolute pixel-based `System.Windows.Rect` using explicit image/canvas dimensions.

```csharp
public Rect ToAbsoluteRect(double imageWidth, double imageHeight)
```

#### Parameters:
* **`imageWidth`** (`double`): The target image or canvas width in pixels.
* **`imageHeight`** (`double`): The target image or canvas height in pixels.

#### Returns:
* **`Rect`**: A `System.Windows.Rect` calculated as:
  * `X = RatioLeft * imageWidth`
  * `Y = RatioTop * imageHeight`
  * `Width = RatioWidth * imageWidth`
  * `Height = RatioHeight * imageHeight`

---

### `FromAbsoluteRect` *(Static)*

Factory method that creates and returns a new `TemplateRegion` instance by converting pixel dimensions (`Rect`) into normalized ratio coordinates (`0.0`–`1.0`).

```csharp
public static TemplateRegion FromAbsoluteRect(
    Rect rect, 
    double imageWidth, 
    double imageHeight, 
    int regionNumber, 
    string label = "")
```

#### Parameters:
* **`rect`** (`Rect`): The absolute pixel bounding rectangle (`X`, `Y`, `Width`, `Height`).
* **`imageWidth`** (`double`): The reference image or canvas width in pixels.
* **`imageHeight`** (`double`): The reference image or canvas height in pixels.
* **`regionNumber`** (`int`): The numeric index to assign to the new region.
* **`label`** (`string`, optional): Friendly label assigned to the region. Defaults to `""`.

#### Returns:
* **`TemplateRegion`**: A populated `TemplateRegion` instance with calculated relative ratio values.

#### Calculation & Safety Logic:
To prevent division-by-zero errors when `imageWidth` or `imageHeight` is zero or negative:
* `RatioLeft`: `imageWidth > 0 ? rect.X / imageWidth : 0`
* `RatioTop`: `imageHeight > 0 ? rect.Y / imageHeight : 0`
* `RatioWidth`: `imageWidth > 0 ? rect.Width / imageWidth : 0`
* `RatioHeight`: `imageHeight > 0 ? rect.Height / imageHeight : 0`