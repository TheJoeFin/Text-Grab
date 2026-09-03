# Technical Documentation: `NotifyIconUtilities.cs`

## Overview

The `NotifyIconUtilities` static class in the `Text_Grab.Utilities` namespace provides helper methods to manage Text-Grab's system tray (notification icon) window lifecycle and global hotkey registrations. It handles initializing the tray icon, resetting hotkeys/tray icons, listening for global keypress events, and dispatching corresponding UI or OCR actions based on user settings.

---

## Class Signature

```csharp
namespace Text_Grab.Utilities;

public static class NotifyIconUtilities
```

---

## Public Methods

### `SetupNotifyIcon()`
Initializes and displays the system tray icon for Text-Grab if system integration is allowed and conditions are met.

* **Behavior**:
  1. Checks if system integration is disabled (`AutomationProfile.Current.AllowsSystemIntegration == false`). If disabled, returns early.
  2. Retrieves the current `App` instance.
  3. Returns early if `app.TextGrabIcon` already exists or if there is more than one running instance (`app.NumberOfRunningInstances > 1`).
  4. Registers global hotkeys by calling `RegisterHotKeys(app)`.
  5. Instantiates and shows the notify icon window by calling `CreateNotifyIconWindow()` and assigns it to `app.TextGrabIcon`.

---

### `ResetNotifyIcon()`
Asynchronously resets and recreates the system tray icon and re-registers global hotkeys.

* **Signature**: `public static async Task ResetNotifyIcon()`
* **Behavior**:
  1. Sets `app.TextGrabIcon` to `null`.
  2. Unregisters existing hotkeys via `UnregisterHotkeys(app)`.
  3. Finds any existing `NotifyIconWindow` via `GetExistingNotifyIconWindow()` and closes it.
  4. Calls `RegisterHotKeys(app)` to re-register hotkeys.
  5. Creates a new tray icon window via `CreateNotifyIconWindow()` and assigns it to `app.TextGrabIcon`.

---

### `RegisterHotKeys(App app)`
Reads configured shortcut keys from settings and registers them with the application's `HotKeyManager`.

* **Parameters**:
  * `app`: The current `App` context containing `HotKeyIds`.
* **Behavior**:
  1. Returns early if `AutomationProfile.Current.AllowsSystemIntegration` is `false`.
  2. Retrieves `ShortcutKeySet` items from `ShortcutKeysUtilities.GetShortcutKeySetsFromSettings()`.
  3. Loops through each `ShortcutKeySet`:
     * Ignores disabled shortcut key sets (`keySet.IsEnabled == false`).
     * Registers the hotkey using `HotKeyManager.RegisterHotKey(keySet)`.
     * On success, adds the returned hotkey ID integer to `app.HotKeyIds`.
     * On failure, logs a diagnostic record via `AutomationDiagnostics.Record("hotkey-registration-failed", ...)`.
  4. Unsubscribes and resubscribes `HotKeyManager_HotKeyPressed` to the `HotKeyManager.HotKeyPressed` event to prevent duplicate subscriptions.

---

### `UnregisterHotkeys(App app)`
Unsubscribes event handlers and removes registered hotkeys from the operating system.

* **Parameters**:
  * `app`: The current `App` context containing `HotKeyIds`.
* **Behavior**:
  1. Detaches `HotKeyManager_HotKeyPressed` from `HotKeyManager.HotKeyPressed`.
  2. Iterates through `app.HotKeyIds` and invokes `HotKeyManager.UnregisterHotKey(hotKeyId)` for each ID.

---

## Private Methods

### `trayIcon_Disposed(object? sender, EventArgs e)`
Event handler triggered when the tray icon is disposed. Calls `UnregisterHotkeys(app)`.

---

### `HotKeyManager_HotKeyPressed(object? sender, HotKeyEventArgs e)`
Event handler triggered when a registered global hotkey is pressed.

* **Behavior**:
  1. Verifies that global hotkeys are enabled (`AppUtilities.TextGrabSettings.GlobalHotkeysEnabled`). If `false`, execution halts.
  2. Matches the event `e` against user-configured shortcut key sets to determine the `ShortcutKeyActions`.
  3. Dispatches the action to the WPF Application Dispatcher UI thread based on the action type:

| `ShortcutKeyActions` | Executed Action / Logic |
| :--- | :--- |
| `None` / `Settings` | No action performed. |
| `Fullscreen` | Calls `WindowUtilities.LaunchFullScreenGrab()`. |
| `GrabFrame` | Instantiates and opens a new `GrabFrame` window. |
| `Lookup` | Instantiates and opens a new `QuickSimpleLookup` window. |
| `EditWindow` | Instantiates, displays, and activates a new `EditTextWindow`. |
| `PreviousRegionGrab` | Calls `OcrUtilities.GetCopyTextFromPreviousRegion()`. |
| `PreviousEditWindow` | Fetches the last history item from `Singleton<HistoryService>.Instance.GetEditWindows()`. If available, opens an `EditTextWindow` populated with `historyInfo`; otherwise, opens a blank `EditTextWindow`. |
| `PreviousGrabFrame` | Calls `Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame()`. |
| `OpenClipboardContent` | Inspects clipboard contents and acts accordingly (see detailed section below). |

#### `OpenClipboardContent` Dispatch Logic:
1. **Text in Clipboard**: If `Clipboard.ContainsText()` is true, retrieves the text and opens `EditTextWindow(text, false)`.
2. **File List in Clipboard**: If `Clipboard.ContainsFileDropList()` is true, checks for the first file path matching an image file type using `IoUtilities.IsImageFile()`. If found, opens `GrabFrame(imagePath)`.
3. **Image in Clipboard**: Attempts to retrieve an image using `ClipboardUtilities.TryGetImageFromClipboard()`.
   * If the returned image is an `InteropBitmap`, converts it to a `Bitmap`, converts that to an `ImageSource`, and disposes the bitmap resource.
   * If it is a `BitmapSource`, uses it directly.
   * Saves the bitmap source as a PNG file to `AutomationProfile.GetTemporaryDirectory()` with a GUID-based filename (`TextGrab_Clipboard_{Guid}.png`).
   * Opens `GrabFrame` initialized with the saved temporary file path.

---

### `CreateNotifyIconWindow()`
Helper method to create or retrieve the notification icon window.

* **Return Type**: `NotifyIconWindow`
* **Behavior**:
  1. Checks for an existing `NotifyIconWindow` using `GetExistingNotifyIconWindow()`. If found, returns it.
  2. Otherwise, instantiates a new `NotifyIconWindow`, calls `.Show()`, and returns the instance.

---

### `GetExistingNotifyIconWindow()`
Searches open application windows for an instance of `NotifyIconWindow`.

* **Return Type**: `NotifyIconWindow?`
* **Behavior**: Returns `Application.Current.Windows.OfType<NotifyIconWindow>().FirstOrDefault()`.

---

## External & Internal Dependencies

* **Application Models & Views**:
  * `NotifyIconWindow`
  * `GrabFrame`
  * `QuickSimpleLookup`
  * `EditTextWindow`
  * `ShortcutKeySet`
  * `ShortcutKeyActions`
  * `HistoryInfo`
* **Services & Utilities**:
  * `AutomationProfile`
  * `AutomationDiagnostics`
  * `ShortcutKeysUtilities`
  * `HotKeyManager` / `HotKeyEventArgs`
  * `WindowUtilities`
  * `OcrUtilities`
  * `IoUtilities`
  * `ClipboardUtilities`
  * `ImageMethods`
  * `Singleton<HistoryService>`
  * `AppUtilities`