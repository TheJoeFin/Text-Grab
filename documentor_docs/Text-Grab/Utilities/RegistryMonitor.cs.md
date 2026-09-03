# Technical Documentation: `Text-Grab/Utilities/RegistryMonitor.cs`

## Overview

The `RegistryMonitor` class (located in the `RegistryUtils` namespace) provides a managed wrapper around native Windows APIs to monitor changes to specific Windows Registry keys. 

Standard .NET `Microsoft.Win32.RegistryKey` classes do not natively expose asynchronous change notifications. `RegistryMonitor` bridges this gap by P/Invoking `advapi32.dll` functions (specifically `RegNotifyChangeKeyValue`) inside a background thread. When changes match configured criteria, it fires events to subscriber applications.

---

## Architecture & How It Works

### High-Level Workflow

1. **Initialization:** The user instantiates `RegistryMonitor` using a registry hive handle and subkey string, a full path string, or a `RegistryKey` instance.
2. **Execution:** Calling `.Start()` creates and starts a background thread (`MonitorThread`).
3. **Monitoring Loop (`ThreadLoop`):**
   - Opens the registry key handle using native `RegOpenKeyEx`.
   - Creates an `AutoResetEvent` handle for Windows change notifications.
   - Registers for change notifications via native `RegNotifyChangeKeyValue`.
   - Blocks on `WaitHandle.WaitAny`, waiting for either a registry change notification or a termination signal (`_eventTerminate`).
   - Raises the `RegChanged` event when a registry change occurs.
4. **Shutdown / Cleanup:** Calling `.Stop()` or `.Dispose()` signals `_eventTerminate`, unblocks the thread, closes the native registry handle with `RegCloseKey`, and joins the background thread.

---

## Class Reference: `RegistryMonitor`

**Namespace:** `RegistryUtils`  
**Interfaces:** `System.IDisposable`

### Public Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| `IsMonitoring` | `bool` | Returns `true` if the monitoring thread is currently active (`_thread != null`); otherwise `false`. |
| `RegChangeNotifyFilter` | `RegChangeNotifyFilter` | Gets or sets the filter flags determining what registry change events trigger a notification. **Note:** Throws `InvalidOperationException` if modified while `IsMonitoring` is `true`. |

### Public Constructors

| Constructor | Parameters | Description |
| :--- | :--- | :--- |
| `RegistryMonitor(RegistryKey registryKey)` | `RegistryKey registryKey` | Initializes the monitor using the full name of an existing `RegistryKey` instance. |
| `RegistryMonitor(string name)` | `string name` | Initializes the monitor using a string path (e.g., `"HKEY_CURRENT_USER\\Environment"` or `"HKCU\\Software"`). Throws `ArgumentNullException` if `name` is null/empty or `ArgumentException` if the hive prefix is unsupported. |
| `RegistryMonitor(RegistryHive registryHive, string subKey)` | `RegistryHive registryHive`, `string subKey` | Initializes the monitor using a structured `RegistryHive` enum value and a subkey path string. |

### Public Methods

#### `Start()`
- **Return Type:** `void`
- **Description:** Resets the termination event and starts a new background thread executing `MonitorThread`.
- **Exceptions:** `ObjectDisposedException` if the instance has already been disposed.

#### `Stop()`
- **Return Type:** `void`
- **Description:** Signals the background thread to terminate via `_eventTerminate` and blocks until the worker thread exits (`thread.Join()`).
- **Exceptions:** `ObjectDisposedException` if the instance has already been disposed.

#### `Dispose()`
- **Return Type:** `void`
- **Description:** Implements `IDisposable.Dispose()`. Stops monitoring, sets the disposed flag to `true`, and suppresses finalization via `GC.SuppressFinalize(this)`.

### Events

| Event | Type | Description |
| :--- | :--- | :--- |
| `RegChanged` | `EventHandler?` | Raised when a registry change matching `RegChangeNotifyFilter` is detected. |
| `Error` | `ErrorEventHandler?` | Raised when an exception occurs inside the monitoring background thread loop. |

---

## Enum Reference: `RegChangeNotifyFilter`

A bitwise `[Flags]` enum used to specify which registry change events to monitor.

```csharp
[Flags]
public enum RegChangeNotifyFilter
{
    Key = 1,        // Notify if a subkey is added or deleted
    Attribute = 2,  // Notify of attribute changes (e.g., security descriptor)
    Value = 4,      // Notify of value changes (add, remove, or modify)
    Security = 8,   // Notify of security descriptor changes
}
```

*Default filter set by `RegistryMonitor`:* `Key | Attribute | Value | Security` (value `15`).

---

## Native P/Invoke Interop Details

The class imports three functions from `advapi32.dll`:

1. **`RegOpenKeyEx`**
   - Opens the specified registry key.
   - Requested Access Mask: `STANDARD_RIGHTS_READ | KEY_QUERY_VALUE | KEY_NOTIFY` (`0x00020000 | 0x0001 | 0x0010`).
2. **`RegNotifyChangeKeyValue`**
   - Listens for notifications on the opened registry key.
   - Accepts parameters for subtree monitoring (`bWatchSubtree = true`), notification filter (`_regFilter`), event handle, and asynchronous flag (`fAsynchronous = true`).
3. **`RegCloseKey`**
   - Closes the handle to the open registry key upon exiting `ThreadLoop`.

### Native Registry Hive Handles

Native handles used internally for hive mapping:
- `HKEY_CLASSES_ROOT` (`0x80000000`) / Supported string prefixes: `HKEY_CLASSES_ROOT`, `HKCR`
- `HKEY_CURRENT_USER` (`0x80000001`) / Supported string prefixes: `HKEY_CURRENT_USER`, `HKCU`
- `HKEY_LOCAL_MACHINE` (`0x80000002`) / Supported string prefixes: `HKEY_LOCAL_MACHINE`, `HKLM`
- `HKEY_USERS` (`0x80000003`) / Supported string prefix: `HKEY_USERS`
- `HKEY_PERFORMANCE_DATA` (`0x80000004`)
- `HKEY_CURRENT_CONFIG` (`0x80000005`) / Supported string prefix: `HKEY_CURRENT_CONFIG`

---

## Threading & Synchronization

- **Thread Safety:** Key operations (`Start`, `Stop`, and property updates for `RegChangeNotifyFilter`) are synchronized using a private object lock (`_threadLock`).
- **Background Execution:** Worker thread `_thread` is configured with `IsBackground = true` to prevent blocking the host process termination.
- **Thread Signal Control:** Uses `ManualResetEvent` (`_eventTerminate`) to stop execution gracefully and `AutoResetEvent` (`_eventNotify`) to await native registry change signals.

---

## Usage Example

```csharp
using System;
using Microsoft.Win32;
using RegistryUtils;

public class Program
{
    public static void Main()
    {
        // Monitor the environment variables subkey in HKEY_CURRENT_USER
        using RegistryMonitor monitor = new RegistryMonitor(RegistryHive.CurrentUser, "Environment");

        // Subscribe to events
        monitor.RegChanged += OnRegistryChanged;
        monitor.Error += OnRegistryError;

        // Start listening
        monitor.Start();

        Console.WriteLine("Monitoring HKEY_CURRENT_USER\\Environment. Press Enter to stop...");
        Console.ReadLine();

        // Stop monitoring
        monitor.Stop();
    }

    private static void OnRegistryChanged(object? sender, EventArgs e)
    {
        Console.WriteLine("Registry key changed!");
    }

    private static void OnRegistryError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine($"Error monitored: {e.GetException().Message}");
    }
}
```