# Technical Documentation: `NotifyIconWindow.xaml.cs`

## Overview

The `NotifyIconWindow` class (`Text_Grab.Controls.NotifyIconWindow`) is a partial WPF `Window` responsible for managing Text-Grab’s system tray (notification area) icon, context menu interactions, native Windows messages, and application launch routines.

Although derived from `System.Windows.Window`, this window remains hidden from the taskbar and Alt+Tab application switcher, serving primarily as an invisible anchor for the system tray icon (`Wpf.Ui.Tray.Controls.NotifyIcon`) and dispatching actions across the application.

---

## Class Architecture

- **Namespace**: `Text_Grab.Controls`
- **Base Class**: `System.Windows.Window`
- **Dependencies & Interop**:
  - `Wpf.Ui.Tray.Controls.NotifyIcon` for system tray management.
  - Native Windows API hooks (`User32.dll` interop via `NativeMethods`) for window style modification and taskbar restoration hooks.
  - Internal Text-Grab services and utilities: `AppUtilities`, `NativeMethods`, `WindowUtilities`, `OcrUtilities`, `ClipboardUtilities`, `IoUtilities`, `ImageMethods`, `AutomationProfile`, and `HistoryService`.

---

## Fields & Initialization

### Private Fields

| Field | Type | Description |
| :--- | :--- | :--- |
| `DefaultSettings` | `Settings` | Readonly reference to application settings retrieved via `AppUtilities.TextGrabSettings`. |
| `windowSource` | `HwndSource?` | Reference to the Win32 window source handle used to register and unregister native message hooks. |

### Constructor

```csharp
public NotifyIconWindow()
```
Calls `InitializeComponent()` to load the associated XAML layout and controls.

---

## Lifecycle & Native Interop Methods

### `OnSourceInitialized(EventArgs e)`
Overrides `Window.OnSourceInitialized`. Obtains the window handle (`HWND`) using `WindowInteropHelper`, retrieves its `HwndSource`, and hooks the `NotifyIconWindowMessageHook` message processing callback.

### `OnClosed(EventArgs e)`
Overrides `Window.OnClosed`. Unhooks `NotifyIconWindowMessageHook` from `windowSource`, nullifies the `windowSource` variable, and completes base window closing cleanup.

### `Window_Loaded(object sender, RoutedEventArgs e)`
Handles the window's `Loaded` event:
1. Calls `Hide()` to ensure the WPF window remains hidden.
2. Executes `HideFromAltTab()` to alter the window extended styles.
3. Sets `NotifyIcon.Visibility = Visibility.Visible`.
4. Reads `DefaultSettings.DefaultLaunch`, parses it into a `TextGrabMode` enum, and appends a corresponding suffix (`- Fullscreen Grab`, `- Grab Frame`, `- Edit Text`, `- Quick Lookup`) to `NotifyIcon.TooltipText`.

### `HideFromAltTab()`
Retrieves the window handle and applies Win32 window styles using `NativeMethods.GetWindowLong` and `NativeMethods.SetWindowLong`:
- Adds `WS_EX_TOOLWINDOW` flag.
- Removes `WS_EX_APPWINDOW` flag.
This removes the window from the Windows Alt+Tab task switcher.

### `NotifyIconWindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)`
A Win32 message loop callback. Monitors incoming native messages. If the message equals `NativeMethods.WM_TASKBARCREATED` (triggered when `explorer.exe` restarts), it executes `RestoreNotifyIconAfterExplorerRestart()`.

### `RestoreNotifyIconAfterExplorerRestart()`
Re-registers the tray icon after Windows Explorer restarts:
- Checks if `NotifyIcon.IsRegistered` is `true`; if so, unregisters it.
- Calls `NotifyIcon.Register()` to re-add the icon to the system tray.

---

## Tray Icon Interaction & Execution Dispatching

### `NotifyIcon_LeftClick(NotifyIcon sender, RoutedEventArgs e)`
Handles primary left-click actions on the notification icon. Marks the event as handled (`e.Handled = true`) and invokes `RunAfterTrayIconInteraction` passing `App.DefaultLaunch`.

### `RunAfterTrayIconInteraction(Action action)`
```csharp
private void RunAfterTrayIconInteraction(Action action)
```
Executes the provided `Action` asynchronously using `Dispatcher.BeginInvoke` at `DispatcherPriority.Background`. This allows the Windows shell tray interaction to release foreground focus before executing the action.

### `NotifyIcon_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)`
Ensures `NotifyIcon.Visibility` stays set to `Visibility.Visible` if its visibility state changes to `false`.

---

## Context Menu Dynamic Handling & Action Events

### `ContextMenu_Opened(object sender, RoutedEventArgs e)`
Invoked when the context menu opens. Evaluates whether the Windows clipboard contains usable image data to enable or disable the `OpenClipboardImageGrabFrame` menu item:
1. Checks if clipboard has a file drop list (`Clipboard.ContainsFileDropList()`) and whether any file is an image (`IoUtilities.IsImageFile`).
2. If no image files are found, checks `Clipboard.ContainsImage()` or `ClipboardUtilities.TryGetImageFromClipboard()`.
3. Sets `OpenClipboardImageGrabFrame.IsEnabled` to `true` if an image is detected; otherwise, `false`. Any exception during evaluation defaults `hasClipboardImage` to `false`.

### Context Menu Click Handlers

| Event Handler | Functionality |
| :--- | :--- |
| `Exit_Click` | Calls `App.Current.Shutdown()` to terminate the application. |
| `EditWindowMenuItem_Click` | Instantiates, displays, and activates a new `EditTextWindow`. |
| `OpenFileMenuItem_Click` | Executes `await App.OpenFileWithPickerAsync()` to open an image file via file picker. |
| `GrabFrameMenuItem_Click` | Instantiates, displays, and activates a new `GrabFrame`. |
| `FullscreenGrabMenuItem_Click` | Invokes `WindowUtilities.LaunchFullScreenGrab()` via `RunAfterTrayIconInteraction`. |
| `PreviousRegionMenuItem_Click` | Executes `await OcrUtilities.GetTextFromPreviousFullscreenRegion()`. |
| `LookupMenuItem_Click` | Instantiates and displays a new `QuickSimpleLookup` window. |
| `LastGrabMenuItem_Click` | Calls `Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame()`. |
| `SettingsMenuItem_Click` | Instantiates and shows a new `SettingsWindow`. |
| `LastEditWindow_Click` | Retrieves the last `EditTextWindow` record from `Singleton<HistoryService>.Instance.GetEditWindows()`. Opens `EditTextWindow` initialized with the history item if found, or a default `EditTextWindow` if null. |

---

## Clipboard Image Processing

### `OpenClipboardImageGrabFrame_Click(object sender, RoutedEventArgs e)`
Processes image data from the clipboard and opens it inside a `GrabFrame` window using the following workflow:

```
[Click "Open Clipboard Image in Grab Frame"]
                       │
        Is Clipboard File Drop List containing an image?
                       ├─── YES ──> Open GrabFrame(imagePath) & Activate
                       │
                       NO
                       │
    Try ClipboardUtilities.TryGetImageFromClipboard()
                       │
            Success & Image Obtained?
                       ├─── NO ───> Return (Abort)
                       │
                       YES
                       │
         Convert Image to BitmapSource
 (Convert InteropBitmap -> Bitmap -> ImageSource if needed)
                       │
 Save BitmapSource as PNG in Temporary Directory (AutomationProfile)
                       │
          Open GrabFrame(tempPath) & Activate
```

1. **File Drop Handling**: Checks `Clipboard.ContainsFileDropList()`. If a file path matches `IoUtilities.IsImageFile`, opens `GrabFrame(imagePath)` immediately.
2. **Direct Image Extract**: If no file path is present, attempts to pull image data via `ClipboardUtilities.TryGetImageFromClipboard()`.
3. **Format Conversion**:
   - If the image is an `InteropBitmap`, converts it to `System.Drawing.Bitmap` via `ImageMethods.InteropBitmapToBitmap`, converts that to `BitmapSource` using `ImageMethods.BitmapToImageSource`, and disposes the temporary bitmap.
   - If the image is already a `BitmapSource`, casts it directly.
4. **Temporary File Persistence**: Encodes the `BitmapSource` into a PNG via `PngBitmapEncoder` and saves it to a file path within `AutomationProfile.GetTemporaryDirectory()`.
5. **Frame Launch**: Constructs and shows a new `GrabFrame` targeting the saved temporary image file path.