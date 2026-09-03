# Documentation: `UiTests/TextGrab.SystemIntegrationHelper/Program.cs`

## Overview

The `TextGrab.SystemIntegrationHelper` program is a command-line helper utility designed to perform low-level Windows desktop interactions and system integration operations for UI testing. It provides a set of CLI commands to simulate mouse and keyboard input, manipulate the clipboard, manage system hotkeys, test file drag-and-drop operations, and verify system desktop state.

The application relies on Win32 API calls (P/Invoke) and WPF libraries (`System.Windows`).

---

## Program Entry & Initialization

### Threading Model
The `Main` method is decorated with the `[STAThread]` attribute, which is required for WPF window execution and Windows Clipboard operations.

### DPI Awareness
Before processing command-line arguments or retrieving screen metrics, the application sets its DPI awareness context:
```csharp
SetProcessDpiAwarenessContext(PerMonitorAwareV2); // PerMonitorAwareV2 = -4
```
Setting `DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2` ensures that:
- Screen metric calculations (`GetSystemMetrics`) reflect physical pixels.
- Mouse coordinate normalization for `SendInput` aligns directly with physical display pixel coordinates used by Windows Automation framework reports (UIA).

---

## Command-Line Interface (CLI)

The application accepts command-line arguments parsed in `Main`. If invalid arguments are provided or required argument counts are not met, the process writes an error message to `Console.Error` and exits with code `2`. Unhandled exceptions are caught in `Main`, logged to `Console.Error`, and cause the process to exit with code `1`.

### Exit Codes
- `0`: Operation succeeded (or preflight environment is interactive/available).
- `1`: Process failed with an unhandled exception.
- `2`: Invalid argument, missing argument, non-existent file path, or failed preflight check.

---

## Commands Reference

### 1. `--preflight`
Inspects the current system session and desktop state to ensure automated input can be injected.

- **Parameters**: None
- **Behavior**:
  - Attempts to open the input desktop (`OpenInputDesktop`).
  - Retrieves the desktop object name using `GetUserObjectInformationW`.
  - Captures the current foreground window handle (`GetForegroundWindow`).
  - Outputs a JSON object to standard output containing:
    - `userInteractive`: Boolean indicating if `Environment.UserInteractive` is true.
    - `inputDesktopAvailable`: Boolean indicating whether `OpenInputDesktop` succeeded.
    - `inputDesktop`: The name of the input desktop (or `null`).
    - `error`: Win32 error code if desktop access failed.
    - `foregroundWindow`: Handle of the foreground window as an integer.
- **Return Code**: Returns `0` if `Environment.UserInteractive` is `true` AND `inputDesktopAvailable` is `true`. Otherwise returns `2`.

---

### 2. `--click`
Simulates a single left mouse click at specified display coordinates.

- **Parameters**: `x` (int), `y` (int)
- **Example Usage**: `--click 100 200`
- **Behavior**: Calls `Move(x, y)` and sends mouse down (`MouseLeftDown`) followed by mouse up (`MouseLeftUp`).

---

### 3. `--right-click`
Simulates a single right mouse click at specified display coordinates.

- **Parameters**: `x` (int), `y` (int)
- **Example Usage**: `--right-click 100 200`
- **Behavior**: Calls `Move(x, y)` and sends right mouse down (`MouseRightDown`) followed by right mouse up (`MouseRightUp`).

---

### 4. `--drag`
Simulates a mouse drag operation from a starting coordinate to an ending coordinate.

- **Parameters**: `startX` (int), `startY` (int), `endX` (int), `endY` (int)
- **Example Usage**: `--drag 100 200 300 400`
- **Behavior**:
  1. Moves cursor to (`startX`, `startY`).
  2. Sends `MouseLeftDown`.
  3. Pauses for 75 ms.
  4. Moves cursor to (`endX`, `endY`).
  5. Pauses for 75 ms.
  6. Sends `MouseLeftUp`.

---

### 5. `--move`
Moves the mouse cursor to a specific coordinate.

- **Parameters**: `x` (int), `y` (int)
- **Example Usage**: `--move 100 200`
- **Behavior**:
  - Performs an initial absolute movement to `(x + 3, y + 3)`.
  - Pauses for 40 ms.
  - Performs a second absolute movement to `(x, y)`.
  - *Note*: Two distinct absolute movements trigger a `WM_MOUSEMOVE` delta to ensure auto-hiding UI components reveal themselves.

---

### 6. `--escape`
Simulates pressing and releasing the Escape key.

- **Parameters**: None
- **Behavior**: Sends a `KEYBDINPUT` down event for virtual key `0x1b` (Escape), followed immediately by a `KEYBDINPUT` up event (`KeyUp`).

---

### 7. `--set-text`
Sets text data onto the system clipboard.

- **Parameters**: `text` (string)
- **Example Usage**: `--set-text "Sample clipboard text"`
- **Behavior**: Calls `Clipboard.SetText(args[1])`.

---

### 8. `--set-image`
Generates a dummy 16x16 white pixel image and places it on the system clipboard.

- **Parameters**: None
- **Behavior**: Creates a 16x16 pixel BGRA byte array (filled with value 255), converts it to a WPF `BitmapSource` (96 DPI, `PixelFormats.Bgra32`), and calls `Clipboard.SetImage()`.

---

### 9. `--set-files`
Sets a list of file paths onto the clipboard in `DataFormats.FileDrop` format.

- **Parameters**: `path1` [path2 ... pathN]
- **Example Usage**: `--set-files "C:\file1.txt" "C:\file2.txt"`
- **Behavior**: Validates that all supplied file paths exist using `File.Exists`. If any file is missing, execution fails with code `2`. Populates a `DataObject` with `DataFormats.FileDrop` and calls `Clipboard.SetDataObject(data, true)`.

---

### 10. `--drag-files`
Launches a UI window acting as an OLE Drag-and-Drop source for automated testing.

- **Parameters**: `readyFile` (string), `filePath1` [filePath2 ... filePathN]
- **Example Usage**: `--drag-files "C:\ready.tmp" "C:\file1.txt"`
- **Behavior**:
  1. Validates existence of all target file paths.
  2. Constructs a WPF application and a tool window (`WindowStyle.ToolWindow`, topmost, size 220x70, position 10,10, taskbar hidden).
  3. Attach a `PreviewMouseLeftButtonDown` event handler to the window's visual container (`Border`).
  4. When clicked, executes `DragDrop.DoDragDrop(..., DataFormats.FileDrop, DragDropEffects.Copy)`.
  5. Writes `"ready"` to the path specified by `readyFile` when the window finishes loading.
  6. Runs the WPF event loop until the window closes.

---

### 11. `--hold-hotkey`
Registers a global system hotkey and holds execution until terminated.

- **Parameters**: `modifiers` (uint), `key` (uint), `readyFile` (string)
- **Example Usage**: `--hold-hotkey 2 65 "C:\ready.tmp"`
- **Behavior**:
  1. Registers a system hotkey with ID `7001` using `RegisterHotKey`.
  2. Writes `"registered"` into the file path specified by `readyFile`.
  3. Writes `"registered"` to stdout and flushes the buffer.
  4. Enters an infinite sleep (`Thread.Sleep(Timeout.Infinite)`).
  5. Releases the hotkey via `UnregisterHotKey` in a `finally` block when the process closes.

---

## Low-Level Helper Methods

### Coordinate Normalization & Input Injection
- **`Move(int x, int y)`**:
  Calculates relative system metric bounds using `GetSystemMetrics(0)` (width) and `GetSystemMetrics(1)` (height). Converts absolute screen pixel coordinates `(x, y)` to normalized absolute coordinate values (ranging from `0` to `65535`) required by `MOUSEEVENTF_ABSOLUTE` (`0x8000`).

  Normalized formula:
  $$\text{dx} = \text{Round}\left(\frac{x \times 65535}{\text{screenWidth} - 1}\right)$$
  $$\text{dy} = \text{Round}\left(\frac{y \times 65535}{\text{screenHeight} - 1}\right)$$

- **`SendMouse(uint flags)`**:
  Constructs a `MOUSEINPUT` struct with specified flags and passes it to `Send`.

- **`Send(INPUT input)`**:
  Invokes the native `SendInput` Win32 function. Throws an `InvalidOperationException` if `SendInput` fails to inject the event.

---

## Win32 API Integration Details

The class uses `[LibraryImport]` for P/Invoke definitions interfacing with `user32.dll`:

| Native Function | Functionality |
| :--- | :--- |
| `OpenInputDesktop` | Opens the desktop that receives user input. |
| `CloseDesktop` | Closes an open desktop handle. |
| `GetUserObjectInformationW` | Retrieves desktop handle string properties. |
| `GetForegroundWindow` | Retrieves handle to the current active foreground window. |
| `GetSystemMetrics` | Retrieves screen dimensions in physical pixels. |
| `SetProcessDpiAwarenessContext` | Sets process DPI awareness. |
| `SendInput` | Synthesizes mouse and keyboard events. |
| `RegisterHotKey` | Defines a system-wide hot key. |
| `UnregisterHotKey` | Frees a previously registered system hot key. |

### Native Data Structures

```csharp
[StructLayout(LayoutKind.Sequential)]
private struct INPUT {
    public uint type; // 0 = Mouse, 1 = Keyboard
    public InputUnion union;
}

[StructLayout(LayoutKind.Explicit)]
private struct InputUnion {
    [FieldOffset(0)] public MOUSEINPUT mouse;
    [FieldOffset(0)] public KEYBDINPUT keyboard;
}

[StructLayout(LayoutKind.Sequential)]
private struct MOUSEINPUT {
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
private struct KEYBDINPUT {
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}
```