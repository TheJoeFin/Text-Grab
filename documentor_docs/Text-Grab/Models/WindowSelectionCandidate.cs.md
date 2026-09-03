# Technical Documentation: `WindowSelectionCandidate.cs`

**File Path:** `Text-Grab/Models/WindowSelectionCandidate.cs`  
**Namespace:** `Text_Grab.Models`  
**Type:** `record`

---

## Overview

The `WindowSelectionCandidate` record represents a candidate window on the user's screen that can be selected or targeted within Text-Grab. It encapsulates metadata about an open window—such as its OS window handle, bounding coordinates, title, process identifier, and application name—and provides helper utilities for spatial hit-testing and user-interface display fallbacks.

---

## Declaration & Constructor

```csharp
public record WindowSelectionCandidate(
    IntPtr Handle, 
    Rect Bounds, 
    string Title, 
    int ProcessId, 
    string AppName = ""
)
```

As an immutable C# positional record, `WindowSelectionCandidate` automatically generates value-based equality semantics and standard positional constructor properties.

### Primary Constructor Parameters / Record Properties

| Parameter / Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Handle` | `IntPtr` | *Required* | The native window handle (`HWND`) referencing the window in the operating system. |
| `Bounds` | `System.Windows.Rect` | *Required* | The screen coordinates and dimensions defining the bounding rectangle of the window. |
| `Title` | `string` | *Required* | The title string associated with the window. |
| `ProcessId` | `int` | *Required* | The Process Identifier (PID) of the process running the window. |
| `AppName` | `string` | `""` | Optional name of the underlying application associated with the window. Defaults to an empty string. |

---

## Members

### Computed Properties

#### `DisplayAppName`
```csharp
public string DisplayAppName => string.IsNullOrWhiteSpace(AppName) ? "Application" : AppName;
```
* **Type:** `string`
* **Access:** Read-only (`get`)
* **Behavior:** Returns `AppName` if it contains non-whitespace characters. If `AppName` is `null`, empty, or composed entirely of whitespace, it defaults to `"Application"`.

#### `DisplayTitle`
```csharp
public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Untitled window" : Title;
```
* **Type:** `string`
* **Access:** Read-only (`get`)
* **Behavior:** Returns `Title` if it contains non-whitespace characters. If `Title` is `null`, empty, or composed entirely of whitespace, it defaults to `"Untitled window"`.

---

### Methods

#### `Contains(Point)`
```csharp
public bool Contains(Point point) => Bounds.Contains(point);
```
* **Parameters:** 
  * `point` (`System.Windows.Point`): The 2D coordinate point to test.
* **Return Type:** `bool`
* **Behavior:** Performs a spatial hit-test determining whether the specified `Point` falls within the area defined by the `Bounds` property. Delegates directly to `Rect.Contains(Point)`.