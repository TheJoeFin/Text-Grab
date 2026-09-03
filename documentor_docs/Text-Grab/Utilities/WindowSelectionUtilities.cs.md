# Technical Documentation: `WindowSelectionUtilities.cs`

## Overview

The `WindowSelectionUtilities` class is a static utility class in the `Text_Grab.Utilities` namespace. Its primary purpose is to enumerate, filter, inspect, and select desktop windows that are eligible for screen capture. 

By interfacing with Win32 APIs and Desktop Window Manager (DWM) attributes via an `OSInterop` helper class, `WindowSelectionUtilities` filters out system shell windows, invisible or minimized windows, cloaked windows, tool windows, and non-activating windows to return a list of valid `WindowSelectionCandidate` objects.

---

## Class Signature

```csharp
namespace Text_Grab.Utilities;

public static class WindowSelectionUtilities
```

---

## Constants

The class defines several private constants used for Win32 window styles and DWM (Desktop Window Manager) attributes:

| Constant | Value | Description |
| :--- | :--- | :--- |
| `DwmwaExtendedFrameBounds` | `9` | DWM attribute index (`DWMWA_EXTENDED_FRAME_BOUNDS`) to retrieve extended window frame bounds in screen coordinates. |
| `DwmwaCloaked` | `14` | DWM attribute index (`DWMWA_CLOAKED`) to check if a window is cloaked (e.g., on a non-active virtual desktop). |
| `GwlExStyle` | `-20` | `GetWindowLong` index (`GWL_EXSTYLE`) to retrieve extended window styles. |
| `WsExToolWindow` | `0x00000080` | Extended window style (`WS_EX_TOOLWINDOW`) designating a tool window that does not appear in the taskbar. |
| `WsExNoActivate` | `0x08000000` | Extended window style (`WS_EX_NOACTIVATE`) designating a window that should not become active when clicked. |

---

## Public Methods

### `GetCapturableWindows`

```csharp
public static List<WindowSelectionCandidate> GetCapturableWindows(
    IReadOnlyCollection<IntPtr>? excludedHandles = null)
```

Enumerates all top-level windows on the screen and returns a list of candidate windows suitable for capture.

* **Parameters:**
  * `excludedHandles` (`IReadOnlyCollection<IntPtr>?`): Optional collection of window handles (`IntPtr`) to exclude from the resulting candidate list.
* **Returns:**
  * `List<WindowSelectionCandidate>`: A list of candidate objects representing capturable windows.
* **How it works:**
  1. Initializes a hash set of handles to exclude.
  2. Fetches the handle of the Windows desktop shell window via `OSInterop.GetShellWindow()`.
  3. Uses `OSInterop.EnumWindows` to iterate over all top-level windows.
  4. Passes each window handle to `CreateCandidate(...)` for filtering and instantiation.
  5. Appends valid candidates to the list and returns it.

---

### `FindWindowAtPoint`

```csharp
public static WindowSelectionCandidate? FindWindowAtPoint(
    IEnumerable<WindowSelectionCandidate> candidates, 
    Point screenPoint)
```

Finds the first candidate window containing a given screen coordinate point.

* **Parameters:**
  * `candidates` (`IEnumerable<WindowSelectionCandidate>`): A collection of candidate windows to search.
  * `screenPoint` (`Point`): The screen coordinate point (`System.Windows.Point`) to check against.
* **Returns:**
  * `WindowSelectionCandidate?`: The first matching candidate whose bounds contain `screenPoint`, or `null` if no match is found.

---

## Internal Methods

### `IsValidWindowBounds`

```csharp
internal static bool IsValidWindowBounds(Rect bounds)
```

Validates whether a window's bounding rectangle meets the minimum size requirements.

* **Parameters:**
  * `bounds` (`Rect`): The bounding rectangle to evaluate.
* **Returns:**
  * `bool`: `true` if `bounds` is not equal to `Rect.Empty` and both `Width` and `Height` are strictly greater than 20 pixels; otherwise, `false`.

---

## Private Helper Methods

### `CreateCandidate`

```csharp
private static WindowSelectionCandidate? CreateCandidate(
    IntPtr windowHandle, 
    IntPtr shellWindow, 
    ISet<IntPtr> excludedHandles)
```

Evaluates a specific window handle against a series of exclusion criteria. If the window passes all checks, it creates and returns a `WindowSelectionCandidate` instance.

* **Exclusion Criteria (Returns `null` if any condition is met):**
  * `windowHandle` is `IntPtr.Zero`.
  * `windowHandle` matches `shellWindow`.
  * `windowHandle` is contained in `excludedHandles`.
  * The window is not visible (`!OSInterop.IsWindowVisible`).
  * The window is minimized (`OSInterop.IsIconic`).
  * The window is cloaked (`IsCloaked`).
  * Extended style includes `WS_EX_TOOLWINDOW` or `WS_EX_NOACTIVATE`.
  * The window's calculated bounds fail `IsValidWindowBounds`.

* **Instantiation:**
  * Retrieves process ID via `OSInterop.GetWindowThreadProcessId`.
  * Resolves window title via `GetWindowTitle`.
  * Resolves process name via `GetProcessName`.
  * Instantiates and returns a `WindowSelectionCandidate` object with the handle, bounds, title, process ID, and process name.

---

### `GetWindowBounds`

```csharp
private static Rect GetWindowBounds(IntPtr windowHandle)
```

Calculates the actual visible screen bounding rectangle of a window.

* **Logic:**
  1. Attempts to fetch DWM extended frame bounds using `OSInterop.DwmGetWindowAttribute` with `DwmwaExtendedFrameBounds` (index `9`). If successful and valid, returns these bounds.
  2. If DWM bounds fail or are invalid, falls back to `OSInterop.GetWindowRect`.
  3. If both attempts fail or yield invalid bounds, returns `Rect.Empty`.

---

### `GetWindowTitle`

```csharp
private static string GetWindowTitle(IntPtr windowHandle)
```

Retrieves the text title associated with a window handle.

* **Logic:**
  1. Gets the length of the window title using `OSInterop.GetWindowTextLength`.
  2. Returns `string.Empty` if the length is $\le 0$.
  3. Allocates a `StringBuilder` with capacity `titleLength + 1`.
  4. Fills the buffer using `OSInterop.GetWindowText` and returns the resulting string.

---

### `IsCloaked`

```csharp
private static bool IsCloaked(IntPtr windowHandle)
```

Checks if a window is marked as "cloaked" by DWM (e.g., hidden on an inactive virtual desktop).

* **Returns:**
  * `bool`: `true` if `DwmGetWindowAttribute` with `DwmwaCloaked` (index `14`) succeeds and returns a non-zero cloaked state value; otherwise, `false`.

---

### `GetProcessName`

```csharp
private static string GetProcessName(int processId)
```

Retrieves the process name corresponding to a given process ID.

* **Exception Handling:**
  * Safely attempts `Process.GetProcessById(processId).ProcessName`.
  * Catches `ArgumentException`, `InvalidOperationException`, and `Win32Exception` (e.g., if the process terminated or permission is denied), returning `string.Empty` on failure.

---

## Summary Flow of Window Candidate Filtering

```
Window Handle
   │
   ├──> Handle == Zero / Shell Window / Excluded Handle? ──> (Discard)
   ├──> Hidden or Minimized?                            ──> (Discard)
   ├──> Cloaked by DWM?                                 ──> (Discard)
   ├──> Tool Window or No-Activate Style?              ──> (Discard)
   ├──> Bounds <= 20x20 or Empty?                        ──> (Discard)
   └──> [Valid] Create WindowSelectionCandidate
```