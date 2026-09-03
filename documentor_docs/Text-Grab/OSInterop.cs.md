# Developer Documentation: `Text-Grab/OSInterop.cs`

## Overview

The `OSInterop` class is an `internal static partial class` that serves as a bridge for Native Interoperability (P/Invoke) between managed C# code and unmanaged Windows Win32 APIs. It imports native functions, defines underlying C/C++ data structures, enumerations, delegates, and constants from `user32.dll`, `kernel32.dll`, and `dwmapi.dll`.

---

## Imported Native Dynamic Link Libraries (DLLs)

1. **`user32.dll`**: Handles window management, input processing, display/monitor metrics, cursor clipping, and low-level keyboard/mouse hooks.
2. **`kernel32.dll`**: Provides core OS-level functions for loading and freeing unmanaged modules/DLLs.
3. **`dwmapi.dll`**: Provides Desktop Window Manager (DWM) functionality for retrieving extended window attributes.

---

## Native API Method Import Declarations

### System & Monitor Operations (`user32.dll`)

* **`GetSystemMetrics(int smIndex)`** (`LibraryImport`)  
  Retrieves various system metrics and configuration settings.
* **`SystemParametersInfo(int nAction, int nParam, ref RECT rc, int nUpdate)`** (`LibraryImport`)  
  Queries or sets system-wide parameters.
* **`GetMonitorInfo(HandleRef hmonitor, [In, Out] MONITORINFOEX info)`** (`DllImport`)  
  Retrieves information about a display monitor.
* **`MonitorFromWindow(HandleRef handle, int flags)`** (`DllImport`)  
  Retrieves a handle to the display monitor that has the largest area of intersection with a specified window.
* **`ClipCursor(ref RECT lpRect)`** (`LibraryImport`) / **`ClipCursor([In()] IntPtr lpRect)`** (`DllImport`)  
  Confines the cursor to a specified rectangle on the screen.

### Window Management & Enumeration (`user32.dll` & `dwmapi.dll`)

* **`EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam)`** (`DllImport`)  
  Enumerates all top-level windows on the screen by passing the handle to each window to an application-defined callback function.
* **`GetWindowRect(IntPtr hWnd, out RECT lpRect)`** (`DllImport`)  
  Retrieves the dimensions of the bounding rectangle of the specified window.
* **`GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount)`** (`DllImport`)  
  Copies the text of the specified window's title bar into a buffer.
* **`GetWindowTextLength(IntPtr hWnd)`** (`DllImport`)  
  Retrieves the length, in characters, of the specified window's title bar text.
* **`IsIconic(IntPtr hWnd)`** (`DllImport`)  
  Determines whether the specified window is minimized (iconic).
* **`IsWindowVisible(IntPtr hWnd)`** (`DllImport`)  
  Determines the visibility state of the specified window.
* **`GetShellWindow()`** (`DllImport`)  
  Retrieves a handle to the Shell's desktop window.
* **`GetWindowLong(IntPtr hWnd, int nIndex)`** (`DllImport`)  
  Retrieves information about the specified window.
* **`GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId)`** (`DllImport`)  
  Retrieves the identifier of the thread and process that created the specified window.
* **`DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute)`** (`DllImport`)  
  Retrieves the current value of a specified Desktop Window Manager (DWM) attribute applied to a window (output as `RECT`).
* **`DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute)`** (`DllImport`)  
  Overload for retrieving integer DWM attribute values.

### Library & Module Management (`kernel32.dll`)

* **`LoadLibrary(string lpFileName)`** (`LibraryImport`)  
  Loads the specified module into the address space of the calling process.
* **`FreeLibrary(IntPtr hModule)`** (`LibraryImport`)  
  Frees the loaded dynamic-link library (DLL) module.

### Low-Level Hooks & Input State (`user32.dll`)

* **`SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId)`** (`LibraryImport`)  
  Installs an application-defined hook procedure into a hook chain.
* **`UnhookWindowsHookEx(IntPtr idHook)`** (`LibraryImport`)  
  Removes a hook procedure installed in a hook chain by `SetWindowsHookEx`.
* **`CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam)`** (`LibraryImport`)  
  Passes the hook information to the next hook procedure in the current hook chain.
* **`GetAsyncKeyState(int vKey)`** (`LibraryImport`)  
  Determines whether a key is up or down at the time the function is called.
* **`GetAsyncKeyState(System.Windows.Forms.Keys vKey)`** (`LibraryImport`)  
  Overload accepting WinForms `Keys` enum directly.
* **`SendInput(uint nInputs, INPUT[] pInputs, int cbSize)`** (`LibraryImport`)  
  Synthesizes keystrokes, mouse motions, and button clicks.

---

## Delegates

* **`public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam)`**  
  Callback delegate used with `EnumWindows` to iterate over top-level window handles.
* **`public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)`**  
  Callback delegate used with Windows hooks (`SetWindowsHookEx`).

---

## Data Structures & Classes

### `RECT` (Struct)
Defines a rectangle by the coordinates of its upper-left and lower-right corners.
* **Fields**: `left`, `top`, `right`, `bottom` (`int`)
* **Computed Properties**:
  * `width`: Returns `right - left`
  * `height`: Returns `bottom - top`

### `MONITORINFOEX` (Class)
Sequential memory layout structure (`Pack = 4`, `CharSet = Auto`) containing monitor information.
* **Fields**:
  * `cbSize` (`int`): Structure size initialized to `Marshal.SizeOf(typeof(MONITORINFOEX))`.
  * `rcMonitor` (`RECT`): Monitor rectangle coordinates.
  * `rcWork` (`RECT`): Work area rectangle coordinates.
  * `szDevice` (`char[]`): Fixed array of size 32 containing the device name.
  * `dwFlags` (`int`): Monitor flags.

### Input Structures for `SendInput`

* **`INPUT` (Struct)**: Sequential layout containing `InputType Type` and `InputUnion U`. Has a static helper property `Size` returning its unmanaged size.
* **`InputUnion` (Explicit Struct)**: `[StructLayout(LayoutKind.Explicit)]` union structure with `FieldOffset(0)` mapping input types:
  * `MI` (`MOUSEINPUT`)
  * `Ki` (`KEYBDINPUT`)
  * `Hi` (`HARDWAREINPUT`)
* **`MOUSEINPUT` (Struct)**: Fields for mouse events (`Dx`, `Dy`, `MouseData`, `DwFlags` (`MOUSEEVENTF`), `Time`, `DwExtraInfo`).
* **`KEYBDINPUT` (Struct)**: Fields for keyboard events (`WVk` (`VirtualKeyShort`), `WScan` (`ScanCodeShort`), `DwFlags` (`KEYEVENTF`), `Time`, `DwExtraInfo`).
* **`HARDWAREINPUT` (Struct)**: Fields for non-keyboard/mouse hardware events (`UMsg`, `WParamL`, `WParamH`).
* **`LowLevelKeyboardInputEvent` (Struct)**: Contains event data passed to a low-level keyboard hook procedure:
  * `VirtualCode`: Virtual key code.
  * `HardwareScanCode`: Hardware scan code.
  * `Flags`: Event flags (e.g., transition state, injection status).
  * `TimeStamp`: Event timestamp.
  * `AdditionalInformation`: Extra info handle.

---

## Enumerations

* **`InputType` (`uint`)**: `INPUT_MOUSE`, `INPUT_KEYBOARD`, `INPUT_HARDWARE`.
* **`MOUSEEVENTF` (`uint`, `[Flags]`)**: Flags controlling mouse motions and click actions (e.g., `MOVE`, `LEFTDOWN`, `LEFTUP`, `ABSOLUTE`, `VIRTUALDESK`, `WHEEL`).
* **`KEYEVENTF` (`uint`, `[Flags]`)**: Flags controlling keyboard input actions (`EXTENDEDKEY`, `KEYUP`, `SCANCODE`, `UNICODE`).
* **`VirtualKeyShort` (`short`)**: Comprehensive enum mapping virtual key constants (e.g., standard keys, modifier keys, media controls, OEM keys, IME keys, and function keys `F1`-`F24`).
* **`ScanCodeShort` (`short`)**: Enumeration mapping physical keyboard hardware scan codes to corresponding key values.

---

## Constants

* **System Metrics Constants**:
  * `SM_CMONITORS` = `80`
* **Hook Constants**:
  * `WH_KEYBOARD_LL` = `13` (Low-Level Keyboard Hook)
* **Virtual Key Byte Constants**:
  * `VK_SHIFT` = `0x10`
  * `VK_CONTROL` = `0x11`
  * `VK_MENU` = `0x12`
  * `VK_ESCAPE` = `0x1B`
  * `VK_LWIN` = `0x5B`
  * `VK_RWIN` = `0x5C`
* **Window Message Constants**:
  * `WM_KEYDOWN` = `0x0100`
  * `WM_KEYUP` = `0x0101`
  * `WM_HOTKEY` = `0x0312`

---

## Managed Helper Methods

### `IsWindows10()`
```csharp
public static bool IsWindows10()
```
* **Purpose**: Checks if the host operating system version build number is less than `22000`.
* **Return Value**: Returns `true` if the OS build is less than `22000` (indicating Windows 10 or earlier); otherwise returns `false` (indicating Windows 11 or newer).