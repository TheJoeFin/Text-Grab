# Technical Documentation: `Text-Grab/App.xaml.cs`

## Overview

The `Text-Grab/App.xaml.cs` file contains the `App` partial class, which serves as the core application entry point and lifecycle manager for **Text-Grab**. Inheriting from `System.Windows.Application`, this class manages:

- Application startup and shutdown sequences.
- Command-line argument parsing and execution.
- Deep-link protocol handling (`text-grab://`).
- File open requests and drag-and-drop operations.
- Dynamic theme switching and registry monitoring.
- System tray notification icon management.
- Application-wide unhandled exception handling and diagnostics logging.

---

## Data Types and Structures

### `StartupArguments`
```csharp
internal readonly record struct StartupArguments(
    bool IsQuiet,
    bool OpenInGrabFrame,
    string? PrimaryArgument,
    string? GrabFramePath);
```
An internal record struct used to store parsed command-line parameters:
- `IsQuiet`: Set when `--windowless` is present.
- `OpenInGrabFrame`: Set when `--grabframe` is present.
- `PrimaryArgument`: The first non-flag startup argument provided.
- `GrabFramePath`: Validated absolute file path corresponding to a path argument intended for GrabFrame.

---

## Properties and Fields

### Fields
- `_automationProfile`: An instance of `AutomationProfile?` retrieved from `AutomationProfile.Current`.
- `_defaultSettings`: Reference to application settings (`AppUtilities.TextGrabSettings`).
- `_themeRegistryMonitor`: An instance of `RegistryMonitor?` used to watch system registry theme changes.
- `_themeRegistryKey`: An instance of `RegistryKey?` pointing to the system theme key path.

### Properties
- `HotKeyIds` (`List<int>`): Holds IDs of system-wide hotkeys registered by the application.
- `NumberOfRunningInstances` (`int`): Count of currently running `Text-Grab` processes.
- `TextGrabIcon` (`NotifyIconWindow?`): System tray icon controller.

---

## Application Lifecycle Management

### `appStartup(object sender, StartupEventArgs e)`
The primary entry point executed when the WPF application starts.
1. **Automation & Diagnostics**: Initializes `AutomationDiagnostics` if an automation profile exists, and binds unhandled exception handlers for non-UI domain and task scheduler exceptions.
2. **Instance Counting**: Counts running processes named `"Text-Grab"`.
3. **Dispatcher Exceptions**: Attaches `CurrentDispatcherUnhandledException` to `Current.DispatcherUnhandledException`.
4. **Protocols & File Associations**: Ensures registration of `text-grab://` URI protocols and `.tggf` file associations (if supported by the automation profile).
5. **Toast Activation Hook**: Registers `LaunchFromToast` callback to `ToastNotificationManagerCompat.OnActivated`.
6. **System Tray Integration**: Calls `HandleNotifyIcon()` to set up system tray features if configured to run in the background.
7. **Input Sources Check**:
   - Checks share target activations (`ShareTargetUtilities.HandleShareTargetActivationAsync`).
   - Parses startup arguments (`HandleStartupArgs`).
8. **Theme Setup**: Initializes real-time theme monitoring via `WatchTheme()`.
9. **Window Launch Strategy**:
   - If argument handling or background launch handled the startup, marks `FirstRun` as `false` and records readiness.
   - If `FirstRun` is true, configures language settings, opens `FirstRunWindow`, and returns.
   - Otherwise, invokes `DefaultLaunch()`.

### `appExit(object sender, ExitEventArgs e)`
Executed when the application closes.
- Records exit diagnostics.
- Closes `TextGrabIcon`.
- Unregisters registered global hotkeys via `NotifyIconUtilities.UnregisterHotkeys(this)`.
- Stops registry theme monitoring.
- Saves and disposes the `HistoryService` instance.

---

## Window & Theme Management

### `DefaultLaunch()`
Launches the initial window based on user preferences defined in `_defaultSettings.DefaultLaunch`:
- `TextGrabMode.Fullscreen`: Invokes `WindowUtilities.LaunchFullScreenGrab()`.
- `TextGrabMode.GrabFrame`: Creates and opens a new `GrabFrame`.
- `TextGrabMode.EditText`: Creates, opens, and activates `EditTextWindow`.
- `TextGrabMode.QuickLookup`: Opens `QuickSimpleLookup`.
- *Default*: Opens `EditTextWindow`.

Finally, calls `SetTheme()`.

### `LaunchStandardMode(TextGrabMode launchMode)`
Directly instantiates and opens a window corresponding to the specified `TextGrabMode`:
- `TextGrabMode.EditText` -> `EditTextWindow`
- `TextGrabMode.GrabFrame` -> `GrabFrame`
- `TextGrabMode.Fullscreen` -> `WindowUtilities.LaunchFullScreenGrab()`
- `TextGrabMode.QuickLookup` -> `QuickSimpleLookup`

### `SetTheme(object? sender = null, EventArgs? e = null)`
Applies the active application theme:
1. Parses `_defaultSettings.AppTheme` into `AppTheme`.
2. Calls `ThemeService.SetTheme()` matching `ApplicationTheme.Light` or `ApplicationTheme.Dark` (resolving `AppTheme.System` using `SystemThemeUtility.IsLightTheme()`).
3. Applies system accent colors via `ApplicationAccentColorManager.ApplySystemAccent()`.

### `WatchTheme()` / `StopWatchingTheme()`
- `WatchTheme()`: Subscribes to registry modification events on the Windows theme key using `RegistryMonitor`. Triggers `SetTheme()` automatically when system theme settings change.
- `StopWatchingTheme()`: Unsubscribes event listeners and disposes registry key monitoring objects.

---

## Startup Arguments & Protocol Handling

### `ParseStartupArguments(IEnumerable<string> args)`
Parses incoming command-line strings into a `StartupArguments` struct:
- Recognizes automation arguments (`--automation-profile`).
- Detects `--windowless` to set `IsQuiet = true`.
- Detects `--grabframe` to set `OpenInGrabFrame = true`.
- Identifies paths and sets `GrabFramePath` if valid files exist on disk.

### `HandleStartupArgs(string[] args)`
Executes actions based on startup arguments:
1. Checks if any argument is a custom protocol URI (`ProtocolUtilities.IsProtocolUri`) and routes to `HandleProtocolUri`.
2. Evaluates parsed `StartupArguments`:
   - Handles quiet mode flags.
   - Handles `--grabframe` launch requests.
   - Intercepts reserved strings like `"ToastActivated"` or `"Settings"`.
   - Attempts parsing command as a `TextGrabMode` standard mode launch.
   - Attempts to open arguments as file paths via `TryToOpenFilePathAsync`.
   - Checks if the path is a folder needing bulk OCR via `CheckForOcringFolder`.

### `HandleProtocolUri(string uriString)`
Processes protocol invocations using the `text-grab://` scheme:

| Command | Action |
| :--- | :--- |
| `paste-spreadsheet` | Opens `EditTextWindow`, switches to spreadsheet mode, and deferred-pastes clipboard contents onto the grid. |
| `edit-text` | Opens `EditTextWindow` and populates it with clipboard text. |
| `grab-frame` | Opens `GrabFrame`. Validates optional `path` parameter using `ProtocolUtilities.TryGetSafeProtocolFilePath`. |
| `grab-text` | Asynchronously OCRs a target file path straight to clipboard via `GrabTextFromFileAsync`. |
| `fullscreen` | Launches full screen capture mode. |
| `quick-lookup` | Opens `QuickSimpleLookup`. |
| `settings` | Opens `SettingsWindow`. |

### `GrabTextFromFileAsync(string path)`
Performs silent OCR on a specified local file path using `OcrUtilities.OcrAbsoluteFilePathAsync` and outputs the result via `OutputUtilities.HandleTextFromOcr`.

---

## File Handling & Drag and Drop

### Drag and Drop Support
- `GetDroppedFileEffect(IDataObject? dataObject)`: Returns `DragDropEffects.Copy` if dropped data contains existing files, otherwise `DragDropEffects.None`.
- `GetDroppedFilePaths(IDataObject? dataObject)`: Extracts and filters a list of existing file paths from a drop event payload.
- `TryToOpenDroppedFilesAsync(IDataObject? dataObject, bool isQuiet)`: Iterates over dropped file paths and invokes `TryToOpenFilePathAsync` on each.

### File Opening & Processing
- `OpenFileWithPickerAsync(bool isQuiet = false)`: Displays a WPF `OpenFileDialog` and opens the selected file.
- `TryToOpenFilePathAsync(string possiblePath, bool isQuiet = false)`:
  - If `.tggf` (Grab Frame File), invokes `TryOpenGrabFrameFileAsync`.
  - If `isQuiet` is true, extracts content via `IoUtilities.GetContentFromPath` and routes text directly to the system handling logic.
  - If the path points to a visual document file (`IoUtilities.IsVisualDocumentFile`), opens it in `GrabFrame`.
  - Otherwise, opens the document in `EditTextWindow`.
- `TryOpenGrabFrameFileAsync(string path, bool isQuiet)`: Restores a saved `.tggf` session path from `HistoryInfo`. Passes content straight to output utilities if `isQuiet` is `true`, or opens it within a new `GrabFrame` window.
- `CheckForOcringFolder(string currentArgument)`: If the path is a valid directory, opens `EditTextWindow` and runs `OcrAllImagesInFolder`.

---

## Exception Handling and Notifications

### UI Thread Exception Handling
`CurrentDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)`:
- Intercepts unhandled UI exceptions.
- Records the exception via `AutomationDiagnostics.RecordUnhandledException`.
- Marks `e.Handled = true` to prevent process crashes.
- If an automation profile is present, triggers asynchronous shutdown (`Shutdown(-1)`).

### Toast Activation
`LaunchFromToast(ToastNotificationActivatedEventArgsCompat toastArgs)`:
- Reads invoked arguments from toast notifications and opens an `EditTextWindow` on the UI thread populated with the invocation string.