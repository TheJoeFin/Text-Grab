# Technical Documentation: `Text-Grab/Utilities/ImplementAppOptions.cs`

## Overview

The `ImplementAppOptions` class is an `internal` static utility class in the `Text_Grab.Utilities` namespace. It manages system-level integration settings for the Text-Grab application. Its responsibilities include:

1. **Windows Startup Configuration**: Enabling or disabling automatic application launch upon user login.
2. **Background System Tray Icon Management**: Toggling the application's background system tray notification icon.
3. **File Extensions & "Open With" Associations**: Registering or unregistering Text-Grab in the Windows Registry as a visual document/image viewer for supported image and PDF extensions.

---

## Class Architecture & Fields

### Signature
```csharp
namespace Text_Grab.Utilities;

internal class ImplementAppOptions
```

### Static Fields

* **`SupportedOpenWithExtensions`** (`private static readonly string[]`)
  * **Description**: Combines image and PDF file extension arrays derived from `IoUtilities.ImageExtensions` and `IoUtilities.PdfExtensions` using collection expressions.
  * **Usage**: Used during file association operations to bind Text-Grab to all supported file types in the Windows Registry.

---

## Public Methods

### `ImplementStartupOption(bool startupOnLogin)`

Enables or disables automatic startup when the user logs into Windows.

* **Parameters**:
  * `startupOnLogin` (`bool`): `true` to register Text-Grab for startup; `false` to remove it.
* **Return Type**: `Task`
* **Execution Flow**:
  1. Checks `AutomationProfile.Current`. If `AllowsSystemIntegration` is `false`, execution terminates immediately.
  2. If `startupOnLogin` is `true`, calls `await SetForStartup()`.
  3. Otherwise, calls `RemoveFromStartup()`.

---

### `ImplementBackgroundOption(bool runInBackground)`

Toggles the background execution mode via the system notification area (tray icon).

* **Parameters**:
  * `runInBackground` (`bool`): `true` to enable the notification icon; `false` to destroy it.
* **Return Type**: `void`
* **Execution Flow**:
  1. Checks `AutomationProfile.Current`. If `AllowsSystemIntegration` is `false`, execution terminates immediately.
  2. If `runInBackground` is `true`, calls `NotifyIconUtilities.SetupNotifyIcon()`.
  3. If `runInBackground` is `false`:
     * Casts `App.Current` to `App`.
     * Closes the active icon via `app.TextGrabIcon?.Close()`.
     * Nullifies the icon reference `app.TextGrabIcon = null`.

---

### `RegisterAsImageOpenWithApp()`

Registers Text-Grab as an "Open With" program handler in the Windows Registry for all file types specified in `SupportedOpenWithExtensions`.

* **Return Type**: `void`
* **Execution Flow**:
  1. **Guards**:
     * Returns if `AutomationProfile.Current.AllowsSystemIntegration` is `false`.
     * Returns if `AppUtilities.IsPackaged()` is `true` (packaged MSIX apps handle file associations via their `appxmanifest`).
     * Obtains executable path via `FileUtilities.GetExePath()`. Returns if `executablePath` is null or empty.
  2. **ProgID Creation**:
     * Creates/opens `HKCU\SOFTWARE\Classes\Text-Grab.Image`.
     * Sets default value to `"Text Grab - Image OCR"`.
     * Sets `"FriendlyTypeName"` to `"Text Grab Image"`.
     * Creates subkey `shell\open\command` and sets the default command to `"{executablePath}" "%1"`.
     * Creates subkey `DefaultIcon` and sets the default value to `"{executablePath}",0`.
  3. **Extension Mapping**:
     * Iterates over `SupportedOpenWithExtensions`.
     * Creates subkey `HKCU\SOFTWARE\Classes\{ext}\OpenWithProgids`.
     * Adds a value named `"Text-Grab.Image"` with `RegistryValueKind.None` and an empty byte array (`Array.Empty<byte>()`).
  4. **Applications Registry Key**:
     * Creates/opens `HKCU\SOFTWARE\Classes\Applications\Text-Grab.exe`.
     * Sets `"FriendlyAppName"` to `"Text Grab"`.
     * Creates `SupportedTypes` subkey and inserts empty strings for each supported extension.
     * Creates `shell\open\command` subkey set to `"{executablePath}" "%1"`.
  5. **Shell Notification**:
     * Calls `NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero)` to notify Windows Explorer of system registry changes.
  6. **Exception Handling**:
     * Catches exceptions during registry operations and logs the error via `Debug.WriteLine`.

---

### `UnregisterAsImageOpenWithApp()`

Removes Text-Grab file association entries from the Windows Registry.

* **Return Type**: `void`
* **Execution Flow**:
  1. **Guards**:
     * Returns if `AutomationProfile.Current.AllowsSystemIntegration` is `false`.
     * Returns if `AppUtilities.IsPackaged()` is `true`.
  2. **Registry Cleanup**:
     * Deletes key tree `HKCU\SOFTWARE\Classes\Text-Grab.Image` (suppressing missing key errors).
     * Iterates through `SupportedOpenWithExtensions`, opens `HKCU\SOFTWARE\Classes\{ext}\OpenWithProgids` as writable, and attempts to delete the `"Text-Grab.Image"` value name.
     * Deletes key tree `HKCU\SOFTWARE\Classes\Applications\Text-Grab.exe`.
  3. **Shell Notification**:
     * Calls `NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero)` to refresh file associations in Windows.
  4. **Exception Handling**:
     * Catches exceptions and writes error messages via `Debug.WriteLine`.

---

## Private Helper Methods

### `RemoveFromStartup()`

Removes the application from startup execution.

* **Signature**: `private static async void RemoveFromStartup()`
* **Execution Flow**:
  * **Packaged Context (`AppUtilities.IsPackaged() == true`)**:
    * Retrieves the task via `StartupTask.GetAsync("StartTextGrab")`.
    * Calls `startupTask.Disable()`.
  * **Unpackaged Context (`AppUtilities.IsPackaged() == false`)**:
    * Opens `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` with write access.
    * Deletes value `"Text-Grab"`.

---

### `SetForStartup()`

Registers the application to launch on user startup.

* **Signature**: `private static async Task SetForStartup()`
* **Execution Flow**:
  * **Packaged Context (`AppUtilities.IsPackaged() == true`)**:
    * Retrieves the task via `StartupTask.GetAsync("StartTextGrab")`.
    * Requests enablement via `await startupTask.RequestEnableAsync()`.
  * **Unpackaged Context (`AppUtilities.IsPackaged() == false`)**:
    * Opens `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` with write access.
    * Obtains `executablePath` via `FileUtilities.GetExePath()`.
    * Writes registry string value `"Text-Grab"` formatted as `"{executablePath}"`.
  * Completes task via `await Task.CompletedTask`.

---

## Summary of Integration Strategies

| Task | Packaged App (MSIX / AppX) | Unpackaged App |
| :--- | :--- | :--- |
| **Startup Control** | `Windows.ApplicationModel.StartupTask` (`"StartTextGrab"`) | Registry: `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` |
| **File Open With Associations** | Deferred to `appxmanifest` (method returns early) | Registry: `HKCU\SOFTWARE\Classes` (ProgID, OpenWithProgids, Applications) |
| **System Refresh** | Managed by UWP / Windows Application Model | Direct P/Invoke call: `NativeMethods.SHChangeNotify` |