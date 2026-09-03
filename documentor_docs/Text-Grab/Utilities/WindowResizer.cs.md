# Technical Documentation: `WindowResizer.cs`

## Overview

The `WindowResizer` class (located in the `Fasetto.Word` namespace) provides native window management capabilities for WPF (Windows Presentation Foundation) applications. Its primary functions are:
1. **Fixing Taskbar Overlap**: Resolves the standard WPF issue where borderless windows (`WindowStyle.None`) cover the Windows taskbar when maximized.
2. **Edge Dock Position Tracking**: Detects when a window is docked to the left or right edges of the screen (Aero Snap functionality) and notifies listeners via an event.
3. **Monitor Transformation Handling**: Converts WPF device-independent units (DIPs) to physical screen pixels based on display DPI scale factors.

---

## Class Architecture & Types

### 1. Enums

#### `WindowDockPosition`
Represents the current edge dock state of the window on the screen.

| Value | Description |
| :--- | :--- |
| `Undocked` | The window is not docked to any vertical edge. |
| `Left` | The window is snapped/docked to the left edge of the screen. |
| `Right` | The window is snapped/docked to the right edge of the screen. |

---

### 2. Classes and Structs

#### `WindowResizer`
* **Interfaces**: `IDisposable`
* **Pattern**: Partial class utilizing `LibraryImport` and `DllImport` P/Invoke methods.
* **Purpose**: Encapsulates window message hooks (`WindowProc`) and bounds-calculation logic for a target WPF `Window`.

#### Native P/Invoke Structures (`System.Runtime.InteropServices`)

* **`MONITORINFO`** (`class`): Receives information about a display monitor (monitor bounds and work area bounds).
* **`Rectangle`** (`struct`): Native Win32 `RECT` layout containing `Left`, `Top`, `Right`, and `Bottom` values.
* **`MINMAXINFO`** (`struct`): Native Win32 `MINMAXINFO` structure containing coordinates for maximum position, size, and minimum/maximum tracking limits.
* **`POINT`** (`struct`): Native Win32 2D point structure (`X`, `Y`).
* **`MonitorOptions`** (`enum`): Flags used with `MonitorFromPoint` (`MONITOR_DEFAULTTONULL`, `MONITOR_DEFAULTTOPRIMARY`, `MONITOR_DEFAULTTONEAREST`).

---

## Key Components & Internal Logic

### 1. Field Summary

| Field | Type | Description |
| :--- | :--- | :--- |
| `mWindow` | `Window?` | Reference to the managed WPF window being resized. |
| `mHookedSource` | `HwndSource?` | The `HwndSource` used to hook into Win32 messages; stored for unhooking during disposal. |
| `mDisposed` | `bool` | Indicates whether the instance has already been disposed. |
| `mScreenSize` | `Rect` | Stores the work area bounds calculated during `WM_GETMINMAXINFO`. |
| `mEdgeTolerance` | `int` | Pixel tolerance threshold (default: `2`) for detecting if window edges touch the monitor boundaries. |
| `mTransformToDevice` | `Matrix` | Visual transform matrix used to convert WPF DIPs to device pixels. |
| `mLastScreen` | `IntPtr` | Native monitor handle (`HMONITOR`) used to track monitor change events. |
| `mLastDock` | `WindowDockPosition` | Tracks the last known dock state to avoid redundant event triggers. |

---

### 2. Event Handlers & Hooks

#### `WindowDockChanged`
* **Type**: `public event Action<WindowDockPosition>`
* **Description**: Raised whenever the dock state changes (e.g., from `Undocked` to `Left`).

---

### 3. Core Methods

#### `WindowResizer(Window window)`
* **Parameters**: `window` - Target WPF window to attach to.
* **Behavior**:
  1. Stores target window reference.
  2. Calls `GetTransform()` to calculate initial device pixel matrices.
  3. Registers `Window_SourceInitialized` to hook into Win32 messages when the HWND becomes available.
  4. Registers `Window_SizeChanged` to handle dock detection during resizes.

#### `GetTransform()`
* Obtains `PresentationSource.FromVisual(mWindow)`.
* Retrieves `CompositionTarget.TransformToDevice` matrix to map WPF DIPs to physical pixels.

#### `Window_SourceInitialized(object? sender, EventArgs e)`
* Obtains the handle (`HWND`) from `WindowInteropHelper`.
* Acquires the corresponding `HwndSource`.
* Hooks `WindowProc` into the Win32 message pump and assigns `mHookedSource`.

#### `WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)`
* Intercepts native Win32 window messages.
* **Handled Message**: `WM_GETMINMAXINFO` (`0x0024`).
* Invokes `WmGetMinMaxInfo(hwnd, lParam)` and sets `handled = true`.

#### `WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)`
Executes the native screen measurement to limit maximum window boundaries to the work area (excluding the taskbar):
1. Calls `GetCursorPos` to retrieve the current cursor location.
2. Identifies the primary monitor via `MonitorFromPoint` (`MONITOR_DEFAULTTOPRIMARY`) and retrieves its `MONITORINFO`.
3. Checks current monitor via cursor location (`MONITOR_DEFAULTTONEAREST`). Recomputes display transform if the window has changed screens.
4. Marshals `lParam` into a `MINMAXINFO` structure.
5. Populates `ptMaxPosition` and `ptMaxSize` using `lPrimaryScreenInfo.rcWork` dimensions.
6. Converts WPF `MinWidth` and `MinHeight` into physical pixels via `mTransformToDevice` and sets `ptMinTrackSize`.
7. Updates `mScreenSize` and marshals the modified structure back to `lParam`.

#### `Window_SizeChanged(object sender, SizeChangedEventArgs e)`
Performs dock state calculations:
1. Ensures `mTransformToDevice` and `mWindow` are valid.
2. Transforms top-left and bottom-right points from WPF coordinates into physical screen pixel coordinates.
3. Compares the transformed coordinates against `mScreenSize` work boundaries using `mEdgeTolerance`.
4. Evaluates whether the window is vertically spanning (`edgedTop` and `edgedBottom`) and touching either `edgedLeft` or `edgedRight`.
5. Updates `mLastDock` and triggers `WindowDockChanged` if a state transition occurs.

#### `Dispose()`
* Detaches handlers from `mWindow.SourceInitialized` and `mWindow.SizeChanged`.
* Removes `WindowProc` hook from `mHookedSource`.
* Clears the `WindowDockChanged` event listeners.
* Calls `GC.SuppressFinalize(this)`.

---

## Native Interop API Bindings

The class imports three native user32 functions:

```csharp
[LibraryImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static partial bool GetCursorPos(out POINT lpPoint);

[DllImport("user32.dll")]
private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

[LibraryImport("user32.dll", SetLastError = true)]
private static partial IntPtr MonitorFromPoint(POINT pt, MonitorOptions dwFlags);
```

---

## Lifecycle Flow

```
1. Instantiation (WindowResizer)
   │
   ├──> Call GetTransform()
   ├──> Attach Window.SourceInitialized
   └──> Attach Window.SizeChanged
   
2. Window Source Initialized
   │
   ├──> Obtain HWND via WindowInteropHelper
   └──> Add WindowProc hook to HwndSource

3. Message Loop Hook (WM_GETMINMAXINFO)
   │
   ├──> Query Monitor Work Area (excluding taskbar)
   ├──> Calculate DPI Scale Factor via mTransformToDevice
   ├──> Set Max Bounds / Min Track Size in MINMAXINFO
   └──> Update mScreenSize

4. Window Resized (Window_SizeChanged)
   │
   ├──> Transform bounds to native device pixels
   ├──> Perform edge-tolerance boundary check against mScreenSize
   └──> Fire WindowDockChanged event if docking status changes

5. Disposal (Dispose)
   │
   ├──> Unhook WindowProc from HwndSource
   └──> Unsubscribe from WPF Window events
```