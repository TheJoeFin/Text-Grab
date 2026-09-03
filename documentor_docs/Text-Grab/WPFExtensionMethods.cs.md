# Technical Documentation: `WPFExtensionMethods.cs`

## Overview

The `WPFExtensionMethods.cs` file defines an internal static C# class containing extension methods for WPF (`System.Windows.Window`) objects. Its primary purpose is to calculate and return the true absolute screen coordinates (`Point`) of a target WPF window, taking into account whether the window is in a normal state or maximized, and whether the system uses a single-monitor or multi-monitor setup.

---

## File Details

* **File Path:** `Text-Grab/WPFExtensionMethods.cs`
* **Namespace:** Global (No explicit namespace defined)
* **Access Modifier:** `internal`
* **Class Type:** `static`

---

## Class Architecture

### `WPFExtensionMethods`

```csharp
internal static class WPFExtensionMethods
```

An internal static class that acts as a container for WPF-related extension methods.

---

## Extension Methods

### `GetAbsolutePosition`

Calculates the absolute $(X, Y)$ screen position of a `System.Windows.Window`.

#### Signature
```csharp
public static Point GetAbsolutePosition(this Window w)
```

#### Parameters
| Parameter | Type | Description |
| :--- | :--- | :--- |
| `w` | `System.Windows.Window` | The WPF window instance whose absolute position is being requested. |

#### Return Value
* **Type:** `System.Windows.Point`
* **Description:** A point representing the absolute top-left coordinates $(X, Y)$ of the window on the screen monitor grid.

---

## Detailed Execution Logic

When `GetAbsolutePosition` is invoked on a `Window` object `w`, the method follows these logical steps:

1. **Check Window State:**
   * It checks if `w.WindowState != WindowState.Maximized`.
   * **If True (Window is not maximized):** Returns a `Point` constructed directly from the window's standard properties: `new Point(w.Left, w.Top)`.

2. **Handle Maximized Window State:**
   * If the window *is* maximized, standard `Left` and `Top` properties may not reflect the actual monitor bounds due to OS window margin offsets. The method determines monitor positioning via OS Interop.
   * It checks multi-monitor support by calling `OSInterop.GetSystemMetrics(OSInterop.SM_CMONITORS) != 0`.

3. **Single-Monitor Fallback (`multimonSupported == false`):**
   * Instantiates an `OSInterop.RECT` structure (`rc`).
   * Calls `OSInterop.SystemParametersInfo(48, 0, ref rc, 0)` to retrieve system work area dimensions.
   * Constructs an `Int32Rect` `r` using `rc.left`, `rc.top`, `rc.width`, and `rc.height`.

4. **Multi-Monitor Logic (`multimonSupported == true`):**
   * Creates a `WindowInteropHelper` wrapping window `w`.
   * Obtains the monitor handle (`hmonitor`) associated with the window's handle using `OSInterop.MonitorFromWindow(new HandleRef(null, helper.EnsureHandle()), 2)`.
   * Instantiates an `OSInterop.MONITORINFOEX` object (`info`).
   * Populates `info` by calling `OSInterop.GetMonitorInfo(new HandleRef(null, hmonitor), info)`.
   * Constructs an `Int32Rect` `r` using `info.rcMonitor.left`, `info.rcMonitor.top`, `info.rcMonitor.width`, and `info.rcMonitor.height`.

5. **Return Result:**
   * Returns `new Point(r.X, r.Y)` derived from the computed bounds rect `r`.

---

## Dependencies & External References

The method relies on external Win32 API wrappers provided by an `OSInterop` class (defined elsewhere in the project) and standard WPF namespaces:

* **System Namespaces:**
  * `System`
  * `System.Runtime.InteropServices` (`HandleRef`)
  * `System.Windows` (`Point`, `Window`, `WindowState`, `Int32Rect`)
  * `System.Windows.Interop` (`WindowInteropHelper`)
* **`OSInterop` Members Used:**
  * `OSInterop.GetSystemMetrics(...)`
  * `OSInterop.SM_CMONITORS`
  * `OSInterop.RECT`
  * `OSInterop.SystemParametersInfo(...)`
  * `OSInterop.MonitorFromWindow(...)`
  * `OSInterop.MONITORINFOEX`
  * `OSInterop.GetMonitorInfo(...)`