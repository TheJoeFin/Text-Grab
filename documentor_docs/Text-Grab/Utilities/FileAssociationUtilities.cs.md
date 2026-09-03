# Technical Documentation: `FileAssociationUtilities.cs`

## Overview

The `FileAssociationUtilities` class provides functionality for managing per-user Windows Registry file associations for unpackaged installations of Text Grab. Its primary purpose is to associate the `.tggf` (Grab Frame) file extension with the Text Grab executable, allowing users to open saved Grab Frames by double-clicking them in Windows File Explorer.

> **Note:** For packaged installs (e.g., MSIX), file associations are declared directly in the application manifest. Therefore, this utility operates exclusively when the application is running unpackaged.

---

## Class Architecture & Metadata

* **Namespace:** `Text_Grab.Utilities`
* **Access Modifier:** `internal`
* **Class Modifier:** `static`

---

## Constants

The class defines several private constants used to construct Windows Registry key paths and values:

| Constant | Type | Value / Expression | Description |
| :--- | :--- | :--- | :--- |
| `GrabFrameProgId` | `string` | `"TextGrab.GrabFrame"` | The Programmatic Identifier (ProgID) assigned to the Grab Frame file type. |
| `GrabFrameProgIdDescription` | `string` | `"Text Grab Frame"` | The human-readable description associated with the ProgID. |
| `ClassesRoot` | `string` | `@"Software\Classes\"` | The relative path under `HKEY_CURRENT_USER` where user-level file associations are stored. |
| `GrabFrameExtensionKeyPath` | `string` | `ClassesRoot + GrabFrameFileUtilities.GrabFrameFileExtension` | The registry key path for the `.tggf` extension (e.g., `Software\Classes\.tggf`). |
| `GrabFrameProgIdKeyPath` | `string` | `ClassesRoot + GrabFrameProgId` | The registry key path for the ProgID definition (`Software\Classes\TextGrab.GrabFrame`). |

---

## Methods

### `EnsureGrabFrameFileAssociation()`

```csharp
internal static void EnsureGrabFrameFileAssociation()
```

#### Purpose
Ensures that the `.tggf` file extension is correctly mapped in the Windows Registry (`HKEY_CURRENT_USER`) to open with the current executable path of Text Grab. This method is safe to execute on every application startup because it checks for existing valid entries before writing to the registry.

#### Logic Flow

1. **Package Check:**
   * Invokes `AppUtilities.IsPackaged()`.
   * If the application is packaged, execution terminates immediately.

2. **Executable Path Validation:**
   * Invokes `FileUtilities.GetExePath()` to retrieve the current application executable path.
   * If the returned string is `null` or empty, execution terminates.

3. **Command String Formatting:**
   * Constructs the shell execution command: `"\"{executablePath}\" \"%1\""`.

4. **Registry Verification:**
   * Opens subkeys under `Registry.CurrentUser`:
     * `Software\Classes\TextGrab.GrabFrame\shell\open\command`
     * Extension key path (e.g., `Software\Classes\.tggf`)
   * Reads default values (`string.Empty` value name) for both keys.
   * **Early Return Condition:** If the extension key points to `GrabFrameProgId` **and** the open command matches `expectedCommand`, no action is taken and the method exits.

5. **Registry Registration / Repair:**
   If the association is missing or stale, the method creates/updates subkeys under `Registry.CurrentUser`:
   * **Extension Key** (`Software\Classes\.tggf`): Sets default value to `"TextGrab.GrabFrame"`.
   * **ProgID Key** (`Software\Classes\TextGrab.GrabFrame`): Sets default value to `"Text Grab Frame"`.
   * **Icon Subkey** (`Software\Classes\TextGrab.GrabFrame\DefaultIcon`): Sets default value to `"\"{executablePath}\",0"`.
   * **Command Subkey** (`Software\Classes\TextGrab.GrabFrame\shell\open\command`): Sets default value to `expectedCommand`.

6. **Error Handling:**
   * Wraps registry read and write operations inside a `try-catch` block.
   * Any `Exception` encountered during execution is caught and written to debug output via `Debug.WriteLine`.

---

## Registry Layout Generated

When executed, `EnsureGrabFrameFileAssociation()` configures the following hierarchy under `HKEY_CURRENT_USER`:

```text
HKEY_CURRENT_USER\Software\Classes\
├── <GrabFrameFileExtension>            (Default) = "TextGrab.GrabFrame"
└── TextGrab.GrabFrame                  (Default) = "Text Grab Frame"
    ├── DefaultIcon                     (Default) = "\"<path-to-exe>\",0"
    └── shell
        └── open
            └── command                 (Default) = "\"<path-to-exe>\" \"%1\""
```

---

## Dependencies

* **External Framework Assemblies:**
  * `Microsoft.Win32` (Registry operations)
  * `System` (Exception handling)
  * `System.Diagnostics` (`Debug.WriteLine`)
* **Internal Text Grab Utilities:**
  * `AppUtilities.IsPackaged()`: Determines deployment type (packaged vs unpackaged).
  * `FileUtilities.GetExePath()`: Obtains the path to the running executable.
  * `GrabFrameFileUtilities.GrabFrameFileExtension`: Provides the string extension for Grab Frame files.