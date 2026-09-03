# Technical Documentation: `WindowUtilities.cs`

## Overview

The `WindowUtilities` class located in `Text-Grab.Utilities` is a `public static partial` utility class providing core window management, input simulation, UI calculations, positioning, screen handling, and application lifecycle logic for the Text-Grab application.

It manages multi-monitor fullscreen OCR capture routines (`FullscreenGrab`), controls window positioning and center calculation across different DPI displays, handles automatic text paste simulation via Native Win32 APIs (`SendInput`), and manages background run/shutdown behavior.

---

## Class Declaration & Dependencies

```csharp
namespace Text_Grab.Utilities;

public static partial class WindowUtilities
```

### Dependencies
- **Win32 / Interop**: `Dapplo.Windows.User32`, `System.Runtime.InteropServices`, `OSInterop` (`GetCursorPos`, `SendInput`, `GetAsyncKeyState`, `INPUT`, `KEYEVENTF`, `VirtualKeyShort`).
- **WPF Core**: `System.Windows`, `System.Windows.Controls`, `System.Windows.Input`, `System.Windows.Media`.
- **Application Logic**: `Text_Grab.Extensions`, `Text_Grab.Services`, `Text_Grab.Views`, `Fasetto.Word`.

---

## Private State

| Field | Type | Description |
| :--- | :--- | :--- |
| `fullscreenPostGrabActionStates` | `Dictionary<string, bool>?` | Caches post-grab action toggle states across active `FullscreenGrab` instances to keep multi-monitor windows synchronized. |

---

## Key Method Groups & Technical Specifications

### 1. Window Creation, Retrieval, and Activation

#### `OpenOrActivateWindow<T>()`
```csharp
internal static T OpenOrActivateWindow<T>() where T : Window, new()
```
- **Purpose**: Ensures single-instance window management for window type `T`.
- **Behavior**:
  1. Searches `Application.Current.Windows` for an existing instance of type `T`.
  2. If found, calls `Activate()` on the existing instance and returns it.
  3. If not found, instantiates a new instance of `T`, attempts to display it via `Show()`, and handles exceptions by displaying a WPF UI `MessageBox`.

#### `OpenOrActivateEditTextWindow(bool isTableModeSelected = false)`
```csharp
internal static EditTextWindow OpenOrActivateEditTextWindow(bool isTableModeSelected = false)
```
- **Purpose**: Activates an existing `EditTextWindow` or instantiates a new one.
- **Behavior**:
  1. Activates and returns an existing `EditTextWindow` if present.
  2. If no window exists, creates a new `EditTextWindow`.
  3. Checks `ShouldOpenNewEtwInSpreadsheetMode` to conditionally put the new window into spreadsheet mode via `EnterSpreadsheetMode()`.
  4. Calls `Show()` wrapped in error handling.

#### `AddTextToOpenWindow(string textToAdd)`
```csharp
public static void AddTextToOpenWindow(string textToAdd)
```
- Iterates through all open windows in `Application.Current.Windows`.
- Finds every active instance of `EditTextWindow` and invokes `AddThisText(textToAdd)`.

---

### 2. Fullscreen Grab Management

#### `LaunchFullScreenGrab(TextBox? destinationTextBox = null)`
#### `LaunchFullScreenGrab(TextBox? destinationTextBox, string? preselectedTemplateId)`
```csharp
public static void LaunchFullScreenGrab(TextBox? destinationTextBox = null)
public static void LaunchFullScreenGrab(TextBox? destinationTextBox, string? preselectedTemplateId)
```
- **Purpose**: Launches centered `FullscreenGrab` windows on every available monitor.
- **Behavior**:
  1. Identifies all screens via `DisplayInfo.AllDisplayInfos`.
  2. Scans open windows for active `FullscreenGrab` instances.
  3. Clears saved post-grab states if no `FullscreenGrab` windows exist.
  4. Instantiates additional `FullscreenGrab` instances until the count equals the number of connected monitors.
  5. For each monitor:
     - Sets window position mode to `WindowStartupLocation.Manual`.
     - Initial dimensions set to a fixed $40 \times 40$ rectangle (`sideLength = 40`).
     - Positions the initial rectangle directly over the screen center returned by `screen.ScaledCenterPoint()`.
     - Assigns target `destinationTextBox` and `preselectedTemplateId`.
     - Displays (`Show()`) and focuses (`Activate()`) the window.

#### `CloseAllFullscreenGrabs()`
```csharp
internal static async void CloseAllFullscreenGrabs()
```
- Clears stored post-grab action states.
- Iterates over all active `FullscreenGrab` windows:
  - Captures `TextFromOCR` string if present.
  - Checks if launched from an edit window (`DestinationTextBox is not null`).
  - Calls `Close()` on the window instance.
- If `AppUtilities.TextGrabSettings.TryInsert` is enabled, string from OCR is non-empty, and action was not invoked directly from an edit window, calls `TryInsertString(stringFromOCR)`.
- Invokes Garbage Collection (`GC.Collect()`) and evaluates shutdown conditions via `ShouldShutDown()`.

#### `FullscreenKeyDown(Key key, bool? isActive = null)`
```csharp
internal static void FullscreenKeyDown(Key key, bool? isActive = null)
```
- Routes keypress events across active `FullscreenGrab` instances.
- If `key == Key.Escape`, invokes `CloseAllFullscreenGrabs()`.
- Broadcasts `fsg.KeyPressed(key, isActive)` to all open `FullscreenGrab` windows.

#### `SyncFullscreenPostGrabActionStates(...)`, `GetFullscreenPostGrabActionStates()`, `ClearFullscreenPostGrabActionStates()`
- State synchronization helpers that maintain post-grab action states across multi-screen setup windows.

---

### 3. Window Positioning & Visual Tree Extensions

#### `SetWindowPosition(Window passedWindow)`
```csharp
public static void SetWindowPosition(Window passedWindow)
```
- **Purpose**: Restores persistent window coordinates and size from settings (`EditTextWindowSizeAndPosition` or `GrabFrameWindowSizeAndPosition`).
- **Validation**:
  1. Parses stored string into X, Y, Width, and Height doubles.
  2. Ignores restoration if parsed height or width is less than 10 units.
  3. Uses `VisualTreeHelper.GetDpi(passedWindow)` to adjust screen bounds.
  4. Verifies `storedSize` intersects with at least one active screen in `DisplayInfo.AllDisplayInfos`.
  5. Applies `Left`, `Top`, `Width`, and `Height` to `passedWindow` if validation succeeds.

#### `CenterOverThisWindow(this Window newWindow, Window bottomWindow)`
```csharp
public static void CenterOverThisWindow(this Window newWindow, Window bottomWindow)
```
- Computes relative center coordinates using `GetWindowCenter()` for both windows and updates `newWindow.Top` and `newWindow.Left` to center `newWindow` relative to `bottomWindow`.

#### `GetCenterPoint(this DisplayInfo screen)` / `GetWindowCenter(this Window window)`
- Helper methods returning `Point` structures representing center points of display bounds or window dimensions.

#### `IsMouseInWindow(this Window window)`
```csharp
public static bool IsMouseInWindow(this Window window)
```
- Converts the window's screen position and dimensions scaled by system DPI (`DpiScaleX`, `DpiScaleY`).
- Compares the calculated absolute bounding `Rect` against the current cursor position obtained via `GetMousePosition(...)`.

#### `GetScrollViewer(DependencyObject obj)`
```csharp
public static ScrollViewer? GetScrollViewer(DependencyObject obj)
```
- Recursive algorithm searching WPF's `VisualTreeHelper` hierarchy to find and return the first child `ScrollViewer`.

---

### 4. Text Insertion & Win32 Input Simulation

#### `TryInsertString(string stringToInsert)`
```csharp
internal static async Task TryInsertString(string stringToInsert)
```
- **Purpose**: Simulates a standard paste operation (`Ctrl + V`) via low-level OS input injection.
- **Workflow**:
  1. Delays execution for `InsertDelay` seconds specified in `AppUtilities.TextGrabSettings`.
  2. Clears stuck modifier keys (Control, Windows, Shift) by adding key-up `INPUT` structs via `TryInjectModifierKeyUp`.
  3. Constructs `INPUT` structures corresponding to:
     - `Ctrl` Down
     - `V` Down
     - `V` Up
     - `Ctrl` Up
  4. Invokes Win32 P/Invoke function `SendInput` to submit key events to the OS input queue.

#### `TryInjectModifierKeyUp(ref List<INPUT> inputs, VirtualKeyShort modifier)`
- Queries key state using `GetAsyncKeyState`.
- If the high-order bit is set (`& 0x8000 != 0`), the key is currently held down; appends a `KEYEVENTF.KEYUP` event to `inputs` to prevent stuck modifier keys.

---

### 5. Application Lifecycle & System Interop

#### `ShouldShutDown()`
```csharp
public static void ShouldShutDown()
```
- Determines if the application should terminate based on window count and system tray settings.
- Checks if open windows equal 0 (`Application.Current.Windows.Count < 1`).
- Assesses background execution settings (`RunInTheBackground`) and tray icon status (`TextGrabIcon`).
- If shutdown conditions are met, queues application shutdown via `TtsService.RunWhenIdle(...)`:
  ```csharp
  Singleton<TtsService>.Instance.RunWhenIdle(
      () => Application.Current.Dispatcher.Invoke(Application.Current.Shutdown));
  ```
  This guarantees active text-to-speech tasks drain before `Shutdown` is invoked on the WPF Dispatcher.

#### `GetMousePosition(out Point mousePosition)`
```csharp
public static bool GetMousePosition(out Point mousePosition)
```
- Executes P/Invoke `GetCursorPos(out POINT point)` to retrieve absolute screen coordinates of the system cursor.

#### Native Imports

```csharp
[LibraryImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
private static partial bool GetCursorPos(out POINT lpPoint);
```
- Imports `GetCursorPos` from `user32.dll` via source-generated P/Invoke (`LibraryImport`).