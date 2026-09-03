# Technical Documentation: `ZoomBorder.cs`

**Namespace:** `Text_Grab.Controls`  
**Inherits from:** `System.Windows.Controls.Border`  
**Source File:** `Text-Grab/Controls/ZoomBorder.cs`

---

## Overview

The `ZoomBorder` class is a custom WPF `Border` control that provides interactive zoom and pan functionality for its contained child element (`UIElement`). It applies scale and translation transformations directly to the child control using a WPF `TransformGroup`.

### Primary Capabilities
* **Interactive Zooming:** Scales the child element using the mouse scroll wheel relative to the cursor position.
* **Interactive Panning:** Allows dragging the child element when scaled beyond its default size.
* **Pan Gesture Controls:** Supports conditional panning based on modifier keys (Space bar) or source controls (e.g., ignoring pan triggers on `TextBox` or managing overlays).
* **Reset Functions:** Resets scale and translation to defaults on middle-mouse button click or explicit method calls.

---

## Public Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Child` | `UIElement` | `null` | Overrides `Border.Child`. When set to a new non-null element, calls `Initialize()` to attach transform groups and event handlers. |
| `CanPan` | `bool` | `false` | Indicates whether panning is currently permitted. Automatically enabled when zoomed in. |
| `CanZoom` | `bool` | `true` | Gets or sets whether mouse wheel zooming is enabled. |
| `IsSpacePanModifierPressed` | `bool` | `false` | Manual flag to indicate whether the space key modifier requirement for panning is satisfied. |
| `RequireSpaceToPan` | `bool` | `false` | When `true`, panning requires the Space key to be held down or `IsSpacePanModifierPressed` to be `true`. |

---

## Events

| Event | Delegate Type | Description |
| :--- | :--- | :--- |
| `ResetRequested` | `EventHandler?` | Invoked when the user middle-clicks on the control, indicating a request to reset zoom and pan settings. |

---

## Public Methods

### `GetScale()`
```csharp
public double GetScale()
```
* **Returns:** `double` – The current horizontal scale (`ScaleX`) of the child element's `ScaleTransform`. Returns `1.0` if `child` is `null`.

### `Initialize(UIElement element)`
```csharp
public void Initialize(UIElement element)
```
* **Parameters:** `element` (`UIElement`) – The child element to initialize for zooming and panning.
* **Behavior:**
  1. Stores the target element as `child`.
  2. Constructs a `TransformGroup` containing a `ScaleTransform` and a `TranslateTransform`.
  3. Assigns this group to `child.RenderTransform` and sets `child.RenderTransformOrigin` to `(0.0, 0.0)`.
  4. Hooks up `MouseWheel` and preview mouse event handlers (`PreviewMouseDown`, `PreviewMouseUp`, `PreviewMouseMove`).

### `Reset()`
```csharp
public void Reset()
```
* **Behavior:**
  * Resets scale (`ScaleX`, `ScaleY`) back to `1.0`.
  * Resets translation (`X`, `Y`) back to `0.0`.
  * Clears panning state (`isPanning = false`), releases mouse capture, sets cursor to `Cursors.Arrow`, and sets `CanPan = false`.

### `SetScale(double scale)`
```csharp
public void SetScale(double scale)
```
* **Parameters:** `scale` (`double`) – The target scale factor.
* **Behavior:**
  * Validates the scale factor; falls back to `1.0` if non-finite or $\le 0$.
  * Updates `ScaleX` and `ScaleY` of the child element.
  * Centers the content horizontally and vertically based on child bounds (`RenderSize` or `ActualWidth`/`ActualHeight`).
  * Clears active panning state, releases mouse capture, sets cursor to `Cursors.Arrow`, and updates `CanPan` to `true` if `scale > 1.0`.

---

## Internal & Private Helper Methods

### `GetTranslateTransform(UIElement element)`
* **Returns:** `TranslateTransform`
* Searches the `TransformGroup` attached to `element.RenderTransform` and retrieves the first `TranslateTransform`.

### `GetScaleTransform(UIElement element)`
* **Returns:** `ScaleTransform`
* Searches the `TransformGroup` attached to `element.RenderTransform` and retrieves the first `ScaleTransform`.

### `IsPanGestureActive()`
* **Returns:** `bool`
* Evaluates whether the criteria for panning are satisfied. Returns `true` if `RequireSpaceToPan` is `false`, `IsSpacePanModifierPressed` is `true`, or the `Space` key is currently pressed.

### `BlocksPanFromSource(object? originalSource)`
* **Parameters:** `originalSource` (`object?`) – The source object from a mouse event.
* **Returns:** `bool`
* Traverses up the visual tree (`Visual` or `Visual3D`) starting from `originalSource`:
  * Returns `true` (blocks pan) if the source or any parent is a `TextBox`.
  * Returns `!IsPanGestureActive()` if the element is a `PdfTextLineOverlay`.
  * Returns `false` if tree traversal completes without matching a blocking source.

---

## Event Handlers & Core Interactions

### 1. Mouse Wheel Zoom (`Child_MouseWheel`)
* Validates `child != null` and `CanZoom == true`.
* Adjusts scale by increments/decrements of `0.2` based on `e.Delta`.
* Prevents scaling down below `0.4`.
* Calculates relative mouse position to adjust `TranslateTransform.X` and `TranslateTransform.Y`, ensuring zoom is centered on the current cursor position.
* Sets `CanPan = true`.

### 2. Mouse Down (`Child_PreviewMouseDown`)
* **Middle Mouse Button:** Triggers `ResetRequested` event, executes `Reset()`, and marks the event as handled (`e.Handled = true`).
* **Left Mouse Button:**
  * Checks if panning can start: requires `child` to exist, scale $> 1.0$, `CanPan == true`, `IsPanGestureActive() == true`, and `BlocksPanFromSource() == false`.
  * Captures initial mouse coordinates and transform origin.
  * Captures the mouse, sets `isPanning = true`, sets cursor to `Cursors.Hand`, and marks the event handled.

### 3. Mouse Up (`Child_PreviewMouseUp`)
* Releases panning state when the left mouse button is released.
* Resets cursor to `Cursors.Arrow` and releases mouse capture.

### 4. Mouse Move (`Child_MouseMove`)
* If panning is inactive and source blocks panning, exits immediately.
* Aborts active panning if child is null, scale is `1.0`, `CanPan` is false, or `Shift` or `Ctrl` modifier keys are pressed.
* Updates `TranslateTransform` offsets (`X` and `Y`) relative to mouse drag displacement vector `v = start - e.GetPosition(this)`.
* Marks event handled while active.