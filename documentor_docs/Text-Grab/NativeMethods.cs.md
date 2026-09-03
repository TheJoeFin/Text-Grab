# Technical Documentation: `Text-Grab/NativeMethods.cs`

## Overview

The `NativeMethods.cs` file defines an internal static partial class named `NativeMethods`. It serves as a central P/Invoke (Platform Invoke) interop wrapper for accessing unmanaged Windows API functions, constants, and handle definitions from native system libraries such as `user32.dll`, `gdi32.dll`, `shcore.dll`, and `shell32.dll`.

---

## Class Declaration

```csharp
internal static partial class NativeMethods
```

- **Scope:** `internal` — Accessible only within the containing assembly.
- **Modifiers:** `static` (cannot be instantiated) and `partial` (allows definition across multiple files and enables source generation for `[LibraryImport]`).

---

## Constants and Fields

### Windows Messages & Window Handles

| Name | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `WM_CLIPBOARDUPDATE` | `const int` | `0x031D` | Windows message identifier sent when the contents of the clipboard change. |
| `WM_TASKBARCREATED` | `static readonly uint` | Result of `RegisterWindowMessage("TaskbarCreated")` | Dynamically registered message ID received when the Windows taskbar is created/restarted. |
| `HWND_MESSAGE` | `static IntPtr` | `new(-3)` | Special handle used to create a message-only window or pass as a parent handle. |

### Extended Window Styles

| Name | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `GWL_EX_STYLE` | `const int` | `-20` | Index offset used with `GetWindowLong`/`SetWindowLong` to retrieve or set extended window styles. |
| `WS_EX_APPWINDOW` | `const int` | `0x00040000` | Extended window style that forces a top-level window onto the taskbar when visible. |
| `WS_EX_TOOLWINDOW` | `const int` | `0x00000080` | Extended window style intended for tool windows; prevents the window from appearing on the taskbar. |

---

## Native API Method Signatures

The file uses both modern C# `[LibraryImport]` (source-generated P/Invoke) and traditional `[DllImport]` attribute declarations.

### 1. `AddClipboardFormatListener`
```csharp
[LibraryImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
public static partial bool AddClipboardFormatListener(IntPtr hwnd);
```
- **DLL Source:** `user32.dll`
- **Purpose:** Registers the specified window handle (`hwnd`) to receive clipboard update notification messages (`WM_CLIPBOARDUPDATE`).
- **Parameters:**
  - `hwnd` (`IntPtr`): Handle to the window to register.
- **Return Value:** `bool` (`true` if successful, `false` otherwise).

---

### 2. `RegisterWindowMessage`
```csharp
[LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
public static partial uint RegisterWindowMessage(string lpString);
```
- **DLL Source:** `user32.dll` (Entry point: `RegisterWindowMessageW`)
- **Purpose:** Registers a new window message using UTF-16 string marshalling. The message name is guaranteed to be unique throughout the system.
- **Parameters:**
  - `lpString` (`string`): The message string to be registered.
- **Return Value:** `uint` (A message identifier in the range `0xC000` through `0xFFFF`, or `0` if failure occurs).

---

### 3. `DeleteObject`
```csharp
[LibraryImport("gdi32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
internal static partial bool DeleteObject(IntPtr hObject);
```
- **DLL Source:** `gdi32.dll`
- **Purpose:** Deletes a logical pen, brush, font, bitmap, region, or palette, freeing all system resources associated with the object.
- **Parameters:**
  - `hObject` (`IntPtr`): Handle to a logical GDI graphics object.
- **Return Value:** `bool` (`true` if successful, `false` if handle is invalid or in use).

---

### 4. `GetKeyboardState`
```csharp
[LibraryImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
internal static partial bool GetKeyboardState(byte[] keyState);
```
- **DLL Source:** `user32.dll`
- **Purpose:** Copies the status of the 256 virtual keys to the specified buffer.
- **Parameters:**
  - `keyState` (`byte[]`): A 256-byte array that receives the status data for each virtual key.
- **Return Value:** `bool` (`true` if successful, `false` otherwise).

---

### 5. `GetScaleFactorForMonitor`
```csharp
[LibraryImport("shcore.dll")]
public static partial void GetScaleFactorForMonitor(IntPtr hMon, out uint pScale);
```
- **DLL Source:** `shcore.dll`
- **Purpose:** Retrieves the scale factor for a specified monitor handle.
- **Parameters:**
  - `hMon` (`IntPtr`): Handle to the monitor.
  - `pScale` (`out uint`): Output variable that receives the monitor scale factor value.
- **Return Value:** `void`

---

### 6. `SHChangeNotify`
```csharp
[LibraryImport("shell32.dll")]
public static partial void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
```
- **DLL Source:** `shell32.dll`
- **Purpose:** Notifies the system of an event that an application has performed (such as file creation, deletion, or association changes).
- **Parameters:**
  - `wEventId` (`int`): Event type identifier.
  - `uFlags` (`uint`): Flags indicating the meaning of `dwItem1` and `dwItem2`.
  - `dwItem1` (`IntPtr`): First event-dependent item handle/pointer.
  - `dwItem2` (`IntPtr`): Second event-dependent item handle/pointer.
- **Return Value:** `void`

---

### 7. `GetWindowLong`
```csharp
[DllImport("user32.dll")]
public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
```
- **DLL Source:** `user32.dll`
- **Purpose:** Retrieves information about the specified window. It can retrieve attributes such as window styles.
- **Parameters:**
  - `hWnd` (`IntPtr`): Handle to the window.
  - `nIndex` (`int`): The zero-based offset to the value to be retrieved (e.g., `GWL_EX_STYLE`).
- **Return Value:** `int` (The requested value, or `0` if the function fails).

---

### 8. `SetWindowLong`
```csharp
[DllImport("user32.dll")]
public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
```
- **DLL Source:** `user32.dll`
- **Purpose:** Changes an attribute of the specified window.
- **Parameters:**
  - `hWnd` (`IntPtr`): Handle to the window.
  - `nIndex` (`int`): The zero-based offset to the value to be set (e.g., `GWL_EX_STYLE`).
  - `dwNewLong` (`int`): The replacement value.
- **Return Value:** `int` (The previous value of the specified integer, or `0` if the function fails).