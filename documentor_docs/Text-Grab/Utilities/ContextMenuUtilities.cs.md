# Technical Documentation: `ContextMenuUtilities.cs`

## Overview

The `ContextMenuUtilities` class is an `internal static` utility class within the `Text_Grab.Utilities` namespace. Its primary purpose is to manage Windows Shell context menu integration for supported visual document file extensions (images and PDFs). 

By modifying the current user's Windows Registry (`HKEY_CURRENT_USER`), this utility allows users to right-click supported files in File Explorer and perform actions via Text Grab:
1. **"Grab text with Text Grab"** – Opens the default text extraction workflow for the target file.
2. **"Open in Grab Frame"** – Opens the selected file directly inside Text Grab's Grab Frame interface using the `--grabframe` command-line argument.

---

## Class Signature & Dependencies

```csharp
namespace Text_Grab.Utilities;

internal static class ContextMenuUtilities
```

### Namespace Dependencies
* `Microsoft.Win32`: Provides access to the Windows Registry via `Registry` and `RegistryKey`.
* `System`: Core types and exception types (`UnauthorizedAccessException`, `InvalidOperationException`, `Exception`).
* `System.Diagnostics`: Diagnostic logging via `Debug.WriteLine`.

---

## Constants & Fields

### Constants

| Constant Name | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `GrabTextRegistryKeyName` | `string` | `"Text-Grab.GrabText"` | Registry key name used for the main text grab option. |
| `GrabTextDisplayText` | `string` | `"Grab text with Text Grab"` | Display label shown in the Windows right-click context menu for text grabbing. |
| `GrabFrameRegistryKeyName` | `string` | `"Text-Grab.OpenInGrabFrame"` | Registry key name used for the Grab Frame option. |
| `GrabFrameDisplayText` | `string` | `"Open in Grab Frame"` | Display label shown in the Windows right-click context menu for Grab Frame. |

### Private Fields

* **`VisualDocumentExtensions`** (`static readonly string[]`):
  Combines image file extensions (`IoUtilities.ImageExtensions`) and PDF file extensions (`IoUtilities.PdfExtensions`) into a single array using collection expressions (`[.. IoUtilities.ImageExtensions, .. IoUtilities.PdfExtensions]`). Represents all file types supported for context menu registration.

---

## Public Methods

### 1. `AddToContextMenu(out string? errorMessage)`

Registers context menu entries for all supported extensions defined in `VisualDocumentExtensions`.

```csharp
public static bool AddToContextMenu(out string? errorMessage)
```

* **Parameters:**
  * `errorMessage` (`out string?`): Populated with an explanation if registration fails; set to `null` on success.
* **Returns:** `bool` — `true` if registration succeeded for all extensions; `false` otherwise.
* **Execution Flow:**
  1. Checks `AutomationProfile.Current`. If `AllowsSystemIntegration` is `false`, returns `false` with `errorMessage = "System integration is disabled for this automation profile."`.
  2. Resolves executable path via `FileUtilities.GetExePath()`. If empty or null, returns `false` with an error message.
  3. Iterates over `VisualDocumentExtensions` and calls `RegisterGrabTextContextMenu` and `RegisterGrabFrameContextMenu` for each extension.
  4. **Exception Handling:**
     * Catches `UnauthorizedAccessException`: Sets a user-friendly permission error message and logs debug output.
     * Catches generic `Exception`: Captures the exception message, logs debug output, and returns `false`.

---

### 2. `RemoveFromContextMenu(out string? errorMessage)`

Unregisters Text Grab context menu entries for all supported extensions.

```csharp
public static bool RemoveFromContextMenu(out string? errorMessage)
```

* **Parameters:**
  * `errorMessage` (`out string?`): Populated with an error message if removal fails; set to `null` on success.
* **Returns:** `bool` — `true` if removal process completed successfully; `false` if blocked or failed.
* **Execution Flow:**
  1. Checks `AutomationProfile.Current`. If `AllowsSystemIntegration` is `false`, returns `false` with an error message.
  2. Iterates over `VisualDocumentExtensions` and calls `UnregisterContextMenuForExtension` for both `GrabTextRegistryKeyName` and `GrabFrameRegistryKeyName`.
  3. **Exception Handling:**
     * Catches `UnauthorizedAccessException`: Returns `false` with a permission failure message.
     * Catches generic `Exception`: Returns `false` with a generic failure message.

---

### 3. `IsRegisteredInContextMenu()`

Checks whether Text Grab is currently registered in the Windows Context Menu for at least one supported file extension.

```csharp
public static bool IsRegisteredInContextMenu()
```

* **Returns:** `bool` — `true` if at least one extension has a valid subkey for `GrabTextRegistryKeyName` under `Registry.CurrentUser`; `false` otherwise.
* **Behavior:** Checks extensions sequentially. As soon as `Registry.CurrentUser.OpenSubKey` finds an existing key for an extension, it returns `true`. If iteration finishes without finding any keys or if an exception occurs, it returns `false`.

---

## Internal Methods

### 1. `GetShellKeyPath(string extension, string registryKeyName)`

Generates the Windows Registry path string for a given file extension and context menu key name.

```csharp
internal static string GetShellKeyPath(string extension, string registryKeyName = GrabTextRegistryKeyName)
```

* **Parameters:**
  * `extension` (`string`): The file extension (e.g., `.png`, `.pdf`).
  * `registryKeyName` (`string`, optional): Defaults to `GrabTextRegistryKeyName` (`"Text-Grab.GrabText"`).
* **Returns:** `string` formatted as:  
  `Software\Classes\SystemFileAssociations\{extension}\shell\{registryKeyName}`

### 2. `GetShellKeyPath(string extension)`

Overload method provided for backward compatibility (e.g., unit tests).

```csharp
internal static string GetShellKeyPath(string extension)
```

* **Parameters:**
  * `extension` (`string`): File extension.
* **Returns:** Result of `GetShellKeyPath(extension, GrabTextRegistryKeyName)`.

---

## Private Methods

### 1. `RegisterGrabTextContextMenu(string extension, string executablePath)`

Registers the `"Grab text with Text Grab"` entry in the registry for a specific extension.

* **Key Path Created:** `HKCU\Software\Classes\SystemFileAssociations\{extension}\shell\Text-Grab.GrabText`
* **Values Set:**
  * `(Default)`: `"Grab text with Text Grab"`
  * `"Icon"`: `"\"{executablePath}\""`
* **Command Subkey Path:** `...\Text-Grab.GrabText\command`
* **Command Set:** `"\"{executablePath}\" \"%1\""` (where `%1` represents the right-clicked file path passed by Windows Explorer).

### 2. `RegisterGrabFrameContextMenu(string extension, string executablePath)`

Registers the `"Open in Grab Frame"` entry in the registry for a specific extension.

* **Key Path Created:** `HKCU\Software\Classes\SystemFileAssociations\{extension}\shell\Text-Grab.OpenInGrabFrame`
* **Values Set:**
  * `(Default)`: `"Open in Grab Frame"`
  * `"Icon"`: `"\"{executablePath}\""`
* **Command Subkey Path:** `...\Text-Grab.OpenInGrabFrame\command`
* **Command Set:** `"\"{executablePath}\" --grabframe \"%1\""` (passes the `--grabframe` argument along with the file path).

### 3. `UnregisterContextMenuForExtension(string extension, string registryKeyName)`

Deletes the registry subkey tree for a given extension and key name.

```csharp
private static void UnregisterContextMenuForExtension(string extension, string registryKeyName)
```

* Uses `Registry.CurrentUser.DeleteSubKeyTree(shellKeyPath, throwOnMissingSubKey: false)`.
* Catches and logs any exceptions to debug output if deletion fails for a specific extension.

---

## Registry Architecture Details

All entries are registered under the current user hive (`HKEY_CURRENT_USER`), avoiding mandatory administrator elevation for basic operations unless user-specific permissions are restricted.

```text
HKEY_CURRENT_USER
 └── Software
      └── Classes
           └── SystemFileAssociations
                └── .{extension}
                     └── shell
                          ├── Text-Grab.GrabText
                          │    ├── (Default) = "Grab text with Text Grab"
                          │    ├── Icon = "[Path to Text-Grab Executable]"
                          │    └── command
                          │         └── (Default) = "[Path to Text-Grab Executable] "%1""
                          │
                          └── Text-Grab.OpenInGrabFrame
                               ├── (Default) = "Open in Grab Frame"
                               ├── Icon = "[Path to Text-Grab Executable]"
                               └── command
                                    └── (Default) = "[Path to Text-Grab Executable] --grabframe "%1""
```

---

## Automation Profile Guard Logic

Methods modifying context menu registration (`AddToContextMenu` and `RemoveFromContextMenu`) query `AutomationProfile.Current`. If `AllowsSystemIntegration` evaluates to `false`, the methods short-circuit, set an explicit `errorMessage`, and return `false` without making any registry changes.