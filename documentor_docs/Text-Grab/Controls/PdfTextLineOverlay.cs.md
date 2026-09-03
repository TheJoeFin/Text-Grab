# Technical Documentation: `PdfTextLineOverlay.cs`

## Overview

The `PdfTextLineOverlay` class is an `internal sealed` WPF custom control derived from `System.Windows.Controls.Border`. It represents a selectable textual line visual overlay designed to be rendered on a canvas.

The overlay hosts a child `TextBlock` containing text (rendered with a transparent brush) and provides visual highlighting state management (selected vs. unselected), bounds/layout positioning, and rectangle intersection checks.

---

## Class Definition & Inheritance

```csharp
namespace Text_Grab.Controls;

internal sealed class PdfTextLineOverlay : Border
```

* **Inherits from:** `System.Windows.Controls.Border`
* **Access Modifier:** `internal sealed` (cannot be instantiated outside its assembly or subclassed).

---

## Static Fields

The class defines three private static `Brush` instances used for visual styling:

| Field Name | Type | Value / Description |
| :--- | :--- | :--- |
| `DefaultBorderBrush` | `SolidColorBrush` | Color `ARGB(0x90, 0x00, 0x78, 0xD7)` – Used for the border when the overlay is selected. |
| `DefaultHighlightBrush` | `SolidColorBrush` | Color `ARGB(0x50, 0x00, 0x78, 0xD7)` – Used for the background highlight when the overlay is selected. |
| `TransparentTextBrush` | `SolidColorBrush` | `Colors.Transparent` – Used as the foreground brush for the contained `TextBlock`. |

---

## Properties

### `Text`
* **Type:** `string`
* **Access:** `public string Text { get; }`
* **Description:** Represents the textual content assigned to this overlay element during instantiation.

### `IsSelected`
* **Type:** `bool`
* **Access:** `public bool IsSelected { get; private set; }`
* **Description:** Indicates whether the overlay is currently in a selected state. Modified via `Select()` and `Deselect()`.

### `WasRegionSelected`
* **Type:** `bool`
* **Access:** `public bool WasRegionSelected { get; set; }`
* **Description:** Gets or sets a boolean flag indicating whether the element was part of a region selection.

### `Left`
* **Type:** `double`
* **Access:** `public double Left { get; private set; }`
* **Description:** Wraps the `Canvas.GetLeft` and `Canvas.SetLeft` attached WPF properties to control the horizontal positioning of this control within a `Canvas`.

### `Top`
* **Type:** `double`
* **Access:** `public double Top { get; private set; }`
* **Description:** Wraps the `Canvas.GetTop` and `Canvas.SetTop` attached WPF properties to control the vertical positioning of this control within a `Canvas`.

---

## Constructor

```csharp
public PdfTextLineOverlay(string text)
```

### Initialization Steps
1. Sets the `Text` property to the provided `text` string.
2. Initializes a child `TextBlock` instance with the following properties:
   * `Text`: Set to `text`.
   * `Foreground`: `TransparentTextBrush` (invisible text rendering).
   * `TextWrapping`: `TextWrapping.NoWrap`
   * `TextTrimming`: `TextTrimming.CharacterEllipsis`
   * `VerticalAlignment`: `VerticalAlignment.Center`
   * `Margin`: `Thickness(1, 0, 1, 0)`
   * `IsHitTestVisible`: `false`
3. Configures default properties on the parent `Border` (`this`):
   * `Background`: `Brushes.Transparent`
   * `BorderBrush`: `Brushes.Transparent`
   * `BorderThickness`: `Thickness(0)`
   * `ClipToBounds`: `true`
   * `IsHitTestVisible`: `true`
   * `SnapsToDevicePixels`: `true`

---

## Methods

### `ApplyLayout`

```csharp
public void ApplyLayout(Rect bounds)
```

Calculates and updates the element's dimensions, `Canvas` position, and child `TextBlock` font sizing based on a target bounding box (`Rect`).

* **Width:** `Math.Max(1, bounds.Width + 2)`
* **Height:** `Math.Max(1, bounds.Height + 2)`
* **Left Position:** `Math.Max(0, bounds.X - 1)`
* **Top Position:** `Math.Max(0, bounds.Y - 1)`
* **Child `TextBlock` Adjustments:**
  * `textBlock.FontSize` = `Math.Max(1, bounds.Height * 0.75)`
  * `textBlock.LineHeight` = `Math.Max(1, bounds.Height)`

---

### `Select`

```csharp
public void Select()
```

Sets the control to its selected visual state:
* Sets `IsSelected` to `true`.
* Sets `Background` to `DefaultHighlightBrush`.
* Sets `BorderBrush` to `DefaultBorderBrush`.
* Sets `BorderThickness` to `Thickness(1)`.

---

### `Deselect`

```csharp
public void Deselect()
```

Resets the control to its default (unselected) transparent visual state:
* Sets `IsSelected` to `false`.
* Sets `Background` to `Brushes.Transparent`.
* Sets `BorderBrush` to `Brushes.Transparent`.
* Sets `BorderThickness` to `Thickness(0)`.

---

### `IntersectsWith`

```csharp
public bool IntersectsWith(Rect rectToCheck)
```

Determines whether a target rectangle overlaps with the current visual boundaries of this overlay control.

* **Parameters:** `rectToCheck` (`Rect`) - The rectangle to test against.
* **Returns:** `bool` - `true` if `rectToCheck` intersects with a `Rect` defined at `(Left, Top, Width, Height)`; otherwise `false`.