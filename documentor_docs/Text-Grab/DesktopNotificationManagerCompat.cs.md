# Documentation Guide: `Text-Grab/DesktopNotificationManagerCompat.cs`

## Overview

The `DesktopNotificationManagerCompat.cs` file provides a compatibility layer for handling Windows Desktop Toast Notifications in standard Win32 and Desktop Bridge (.NET / MSIX) applications. It manages:

1. **Application User Model ID (AUMID) and COM Server Registration**: Registers registry entries required for Win32 apps to be launched or activated by Windows Action Center.
2. **COM Class Factory Activation**: Registers local COM class objects (`CoRegisterClassObject`) so Windows can invoke notification activation callbacks in .NET Core / .NET.
3. **Toast Creation and History Management**: Wraps Windows Runtime (`Windows.UI.Notifications`) APIs for creating toast notifications and managing history (clearing, removing, or retrieving active toasts).
4. **User Input Parsing**: Exposes typed wrappers (`NotificationUserInput`) for key/value input data captured from interactive notifications.

---

## Key Components

### 1. `DesktopNotificationManagerCompat`
A static manager class that serves as the primary API for initializing notification capability, instantiating `ToastNotifier` instances, and accessing toast notification history.

#### Public Constants
* **`TOAST_ACTIVATED_LAUNCH_ARG`** (`"-ToastActivated"`): Command-line argument appended to the application path when launched via toast activation.

#### Key Methods & Properties

##### `RegisterAumidAndComServer<T>(string? aumid)`
* **Type Parameter**: `T` (Must inherit from `NotificationActivator`).
* **Parameters**: `aumid` - The unique Application User Model ID for the app.
* **Behavior**:
  * Throws `ArgumentException` if `aumid` is null or whitespace.
  * Checks if the app is running with UWP package identity via `DesktopBridgeHelpers.IsRunningAsUwp()`.
    * If running under Desktop Bridge, sets internal AUMID state to `null` (since AUMID and COM local servers are defined in the package manifest) and marks registration complete.
  * If running as an unpackaged Win32 app, obtains the process's main module executable path and calls `RegisterComServer<T>(exePath)`.

##### `RegisterActivator<T>()`
* **Type Parameter**: `T` (Must inherit from `NotificationActivator` and have a parameterless constructor).
* **Behavior**:
  * Registers the COM class factory for type `T` using the `CoRegisterClassObject` native API.
  * Creates a `NotificationActivatorClassFactory<T>` passing `T`'s GUID to expose the `INotificationActivationCallback` interface to COM.

##### `CreateToastNotifier()`
* **Returns**: `Windows.UI.Notifications.ToastNotifier`
* **Behavior**:
  * Verifies registration via `EnsureRegistered()`.
  * If unpackaged (`_aumid` is set), calls `ToastNotificationManager.CreateToastNotifier(_aumid)`.
  * If packaged (Desktop Bridge), calls `ToastNotificationManager.CreateToastNotifier()`.

##### `History`
* **Type**: `DesktopNotificationHistoryCompat` (Get-only property)
* **Behavior**:
  * Verifies registration via `EnsureRegistered()`.
  * Returns an instance of `DesktopNotificationHistoryCompat` initialized with the app's AUMID or an empty string.

##### `CanUseHttpImages`
* **Type**: `bool` (Get-only property)
* **Behavior**: Returns `true` if the app is running with package identity (Desktop Bridge / MSIX), permitting HTTP image URIs in toast notifications.

---

### 2. Internal COM Infrastructure

#### `IClassFactory` Interface
An internal COM interface definition for standard `IUnknown`/`IClassFactory` (`00000001-0000-0000-C000-000000000046`).
* `CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)`
* `LockServer(bool fLock)`

#### `NotificationActivatorClassFactory<T>`
An internal implementation of `IClassFactory`:
* Validates aggregation requests (`CLASS_E_NOAGGREGATION` if `pUnkOuter != IntPtr.Zero`).
* Verifies requested interface GUID against `typeof(T).GUID` or `IUnknownGuid`.
* Instantiates a new instance of `T` and returns a COM interface pointer for `INotificationActivationCallback` via `Marshal.GetComInterfaceForObject`.

#### Registry Helper (`RegisterComServer<T>`)
Configures COM activation registry entries under `HKCU\SOFTWARE\Classes\CLSID\{GUID}`:
* Sets `LocalServer32` key to `"<ExePath>" -ToastActivated`.
* If running with elevated privileges (`IsElevated`), additionally configures `HKLM\SOFTWARE\Classes\CLSID\{GUID}` and sets `HKLM\SOFTWARE\Classes\AppID\{GUID}` with `RunAs = "Interactive User"`.

#### Native P/Invoke References
* **`CoRegisterClassObject`** (from `ole32.dll`): Registers a COM class factory object with Windows.
* **`GetCurrentPackageFullName`** (from `kernel32.dll`): Checked within `DesktopBridgeHelpers` to detect if the current process runs with UWP/MSIX package identity (`APPMODEL_ERROR_NO_PACKAGE = 15700L`).

---

### 3. `DesktopNotificationHistoryCompat`

A wrapper around `Windows.UI.Notifications.ToastNotificationHistory` that accounts for unpackaged (AUMID-driven) vs. packaged execution contexts.

#### Public Methods
* **`Clear()`**: Clears all active notifications sent by the app from Action Center.
* **`GetHistory()`**: Returns an `IReadOnlyList<ToastNotification>` containing active notifications.
* **`Remove(string tag)`**: Removes a notification matching the given tag.
* **`Remove(string tag, string group)`**: Removes a notification matching the given tag and group.
* **`RemoveGroup(string group)`**: Removes all notifications belonging to a specific group.

*Note: If an AUMID is present, methods call the AUMID-explicit overloads of `ToastNotificationHistory`.*

---

### 4. `NotificationActivator`

An abstract class that application developers implement to handle notification interaction callbacks.

#### Abstract Method
```csharp
public abstract void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId);
```

#### COM Interop Interface (`INotificationActivationCallback`)
* GUID: `53E31837-6600-4A81-9395-75CFFE746F94`
* Implements `Activate(string appUserModelId, string invokedArgs, NOTIFICATION_USER_INPUT_DATA[] data, uint dataCount)` which parses user input data and delegates execution to `OnActivated`.

#### Nested Struct: `NOTIFICATION_USER_INPUT_DATA`
Sequential layout struct holding notification input entries:
* `string Key` (marshaled as `LPWStr`)
* `string Value` (marshaled as `LPWStr`)

---

### 5. `NotificationUserInput`

Implements `IReadOnlyDictionary<string, string>` to wrap user inputs passed to the toast callback.

* **Indexer `this[string key]`**: Retrieves the value corresponding to `key`.
* **`ContainsKey(string key)`**: Returns whether a given key exists in the input array.
* **`TryGetValue(string key, out string value)`**: Attempts to retrieve input value for `key`.
* **Properties**: `Keys`, `Values`, `Count`.

---

## Complete Setup & Execution Flow

```
+-----------------------------------------------------------------------+
| 1. Startup Initialization                                              |
|    - DesktopNotificationManagerCompat.RegisterAumidAndComServer<T>()  |
|      * Checks package identity via GetCurrentPackageFullName          |
|      * Configures LocalServer32 registry keys if unpackaged Win32     |
|    - DesktopNotificationManagerCompat.RegisterActivator<T>()          |
|      * Calls CoRegisterClassObject with NotificationActivatorClassFactory |
+-----------------------------------------------------------------------+
                                  |
                                  v
+-----------------------------------------------------------------------+
| 2. Toast Generation                                                   |
|    - ToastNotifier notifier = DesktopNotificationManagerCompat.CreateToastNotifier() |
|    - notifier.Show(toastNotification)                                 |
+-----------------------------------------------------------------------+
                                  |
                                  v
+-----------------------------------------------------------------------+
| 3. User Activation & Handling                                         |
|    - User interacts with Toast in Action Center                       |
|    - OS activates COM Server or calls registered Class Factory        |
|    - INotificationActivationCallback.Activate(...) triggered           |
|    - Converts NOTIFICATION_USER_INPUT_DATA[] to NotificationUserInput |
|    - Invokes overridden NotificationActivator.OnActivated(...)        |
+-----------------------------------------------------------------------+
```

---

## Class Architecture Summary

| Class / Component | Role / Purpose |
| :--- | :--- |
| `DesktopNotificationManagerCompat` | Main entry point for initialization, COM registration, notifier creation, and history retrieval. |
| `DesktopBridgeHelpers` | Helper class evaluating desktop package identity via `GetCurrentPackageFullName`. |
| `NotificationActivatorClassFactory<T>` | Custom COM `IClassFactory` implementation generating instances of `T`. |
| `DesktopNotificationHistoryCompat` | Wrapper over `ToastNotificationHistory` handling AUMID routing. |
| `NotificationActivator` | Abstract base class implemented by the host app to process notification clicks and inputs. |
| `NotificationUserInput` | `IReadOnlyDictionary<string, string>` view over raw COM user input arrays. |