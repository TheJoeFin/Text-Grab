# Technical Documentation: `HotKeyManager.cs`

## Overview

The `Text-Grab/Utilities/HotKeyManager.cs` file provides mechanism for registering, unregistering, and listening to system-wide global hotkeys in a Windows environment. 

It abstracts native Windows API calls (`user32.dll`) by managing a dedicated background thread running a hidden WinForms `Form` (`MessageWindow`). This window processes incoming Windows messages (specifically `WM_HOTKEY`) and raises C# events when registered hotkeys are pressed.

---

## File Identification & Dependencies

* **Namespace:** `Text_Grab.Utilities`
* **Dependencies:**
  * `System`
  * `System.Linq`
  * `System.Runtime.InteropServices`
  * `System.Threading`
  * `System.Windows.Forms`
  * `Text_Grab.Models`

---

## Types Defined

### 1. `public static partial class HotKeyManager`

The primary static manager class responsible for hotkey registration lifecycle and message handling coordination.

#### Fields & Threading Control
* `private static volatile MessageWindow? _wnd`: Reference to the hidden WinForms message window running on the dedicated message thread.
* `private static volatile IntPtr _hwnd`: Window handle (`HWND`) of the message window.
* `private static readonly ManualResetEvent? _windowReadyEvent`: Signal used to block registration calls until the background message loop thread and `MessageWindow` handle are fully initialized.
* `private static int _id`: Incrementing integer counter used to generate unique IDs for each registered hotkey.

#### Events
* `public static event EventHandler<HotKeyEventArgs>? HotKeyPressed`: Raised whenever a registered hotkey is detected by the message loop.

#### Static Constructor
The static constructor creates and starts a dedicated background thread named `"MessageLoopThread"` (`IsBackground = true`). This thread executes `Application.Run(new MessageWindow())` to establish a standard Windows message pump required to receive native hotkey messages.

#### Public Methods
* `public static int? RegisterHotKey(ShortcutKeySet keySet)`
  * Converts the non-modifier key from `keySet` into a `System.Windows.Forms.Keys` enum using `Enum.TryParse`.
  * Combines modifier flags using `Aggregate` with a bitwise OR (`|`).
  * Forwards the parsed parameters to `RegisterHotKey(Keys, KeyModifiers)`. Returns `null` if parsing fails.
* `public static int? RegisterHotKey(Keys key, KeyModifiers modifiers)`
  * Waits for `_windowReadyEvent` to ensure the target window handle exists.
  * Generates a unique hotkey ID via `Interlocked.Increment(ref _id)`.
  * Invokes `RegisterHotKeyInternal` on `_wnd`'s thread context using `_wnd.Invoke()`.
  * Returns the integer ID if successful; otherwise, returns `null`.
* `public static void UnregisterHotKey(int id)`
  * Calls `_wnd.Invoke()` to execute `UnRegisterHotKeyInternal` on the message loop thread to unregister the specified hotkey ID.

#### Private Methods & Delegates
* `private delegate bool RegisterHotKeyDelegate(IntPtr hwnd, int id, uint modifiers, uint key)`
* `private delegate void UnRegisterHotKeyDelegate(IntPtr hwnd, int id)`
* `private static bool RegisterHotKeyInternal(IntPtr hwnd, int id, uint modifiers, uint key)`: Calls the P/Invoke `RegisterHotKey` function.
* `private static void UnRegisterHotKeyInternal(IntPtr hwnd, int id)`: Calls the P/Invoke `UnregisterHotKey` function.
* `private static void OnHotKeyPressed(HotKeyEventArgs e)`: Triggers the `HotKeyPressed` event subscriber invocations.

#### Native Interop Methods (P/Invoke)
Uses Source Generated P/Invoke (`LibraryImport`) from `user32.dll`:
* `RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk)`: Maps to Win32 `RegisterHotKey`.
* `UnregisterHotKey(IntPtr hWnd, int id)`: Maps to Win32 `UnregisterHotKey`.

---

### 2. `private partial class MessageWindow : Form`

A private, nested WinForms `Form` class contained within `HotKeyManager`.

* **Constructor**: Stores its reference in `_wnd`, captures `this.Handle` into `_hwnd`, and signals `_windowReadyEvent.Set()` to unblock pending registration requests.
* **`SetVisibleCore(bool value)`**: Overridden to force visibility to `false` at all times, keeping the window hidden.
* **`WndProc(ref Message m)`**: Overrides window message processing:
  * Constant: `WM_HOTKEY = 0x312`
  * Intercepts `WM_HOTKEY` messages.
  * Converts `m.LParam` into a `HotKeyEventArgs` instance.
  * Calls `HotKeyManager.OnHotKeyPressed(e)`.
  * Delegates all messages back to `base.WndProc(ref m)`.

---

### 3. `public class HotKeyEventArgs : EventArgs`

Event argument wrapper sent when `HotKeyPressed` is raised.

#### Public Fields
* `public readonly Keys Key`
* `public readonly KeyModifiers Modifiers`

#### Constructors
* `public HotKeyEventArgs(Keys key, KeyModifiers modifiers)`: Direct initialization constructor.
* `public HotKeyEventArgs(IntPtr hotKeyParam)`: Extracts key and modifier data directly from the `LParam` pointer of a `WM_HOTKEY` message using bitwise masking:
  * `Key`: Extracted from the high word `(param & 0xffff0000) >> 16`.
  * `Modifiers`: Extracted from the low word `param & 0x0000ffff`.

---

### 4. `[Flags] public enum KeyModifiers`

Bitfield flags mapping to Win32 modifier key constants.

| Enum Value | Hex / Decimal Value | Description |
| :--- | :--- | :--- |
| `Alt` | `1` (`0x0001`) | ALT key |
| `Control` | `2` (`0x0002`) | CTRL key |
| `Shift` | `4` (`0x0004`) | SHIFT key |
| `Windows` | `8` (`0x0008`) | Windows key |
| `NoRepeat` | `0x4000` | Prevents auto-repeat `WM_HOTKEY` notifications when held down |

---

## Execution & Operational Flow

```
+-------------------------------------------------------------------+
|                         Initialization                            |
+-------------------------------------------------------------------+
  HotKeyManager static constructor starts "MessageLoopThread"
    └─> MessageLoopThread creates new MessageWindow (Form)
          └─> Constructor saves HWND & signals _windowReadyEvent
          └─> Application.Run begins Windows Message Pump

+-------------------------------------------------------------------+
|                       HotKey Registration                         |
+-------------------------------------------------------------------+
  Caller -> HotKeyManager.RegisterHotKey(keySet)
    └─> Parses keys & flags
    └─> Waits for _windowReadyEvent
    └─> Thread-safely increments unique ID (_id)
    └─> Invokes RegisterHotKeyInternal on MessageWindow Thread
          └─> user32.dll RegisterHotKey() called with HWND, ID, keys
          └─> Returns hotkey ID (or null if registration failed)

+-------------------------------------------------------------------+
|                        HotKey Triggered                           |
+-------------------------------------------------------------------+
  User presses HotKey combination
    └─> Windows posts WM_HOTKEY (0x312) to MessageWindow HWND
    └─> MessageWindow.WndProc receives message
          └─> Parses LParam into HotKeyEventArgs (Key + Modifiers)
          └─> HotKeyManager.HotKeyPressed event is fired to subscribers

+-------------------------------------------------------------------+
|                      HotKey Unregistration                        |
+-------------------------------------------------------------------+
  Caller -> HotKeyManager.UnregisterHotKey(id)
    └─> Invokes UnRegisterHotKeyInternal on MessageWindow Thread
          └─> user32.dll UnregisterHotKey() called with HWND and ID
```