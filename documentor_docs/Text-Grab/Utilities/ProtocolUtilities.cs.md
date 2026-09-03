# Technical Documentation Guide: `ProtocolUtilities.cs`

**File Path:** `Text-Grab/Utilities/ProtocolUtilities.cs`  
**Namespace:** `Text_Grab.Utilities`  
**Access Modifier:** `internal static`

---

## 1. Overview

`ProtocolUtilities` is an internal static utility class responsible for handling custom `text-grab://` URI protocol commands. The URI protocol serves as a command channel between external companion applications (such as the Text Grab browser extension) and Text Grab. 

The primary responsibilities of `ProtocolUtilities` include:
* Checking startup arguments for `text-grab://` URIs.
* Parsing URI strings into commands and key-value parameter pairs.
* Validating input file paths supplied via URI parameters through strict security gates.
* Registering the `text-grab://` URI scheme in the Windows Registry for unpackaged installations.

---

## 2. Supported Protocol Commands

As specified in the class documentation, the `text-grab://` protocol supports the following command endpoints (payload data is expected to travel via the system clipboard):

| URI Format | Description |
| :--- | :--- |
| `text-grab://paste-spreadsheet` | Opens the Edit Text window in spreadsheet mode and pastes the clipboard content. |
| `text-grab://edit-text` | Opens the Edit Text window loaded with clipboard text. |
| `text-grab://grab-frame[?path=...]` | Triggers Grab Frame, optionally opening a local image or PDF file. |
| `text-grab://grab-text?path=...` | Runs OCR directly on a local image/PDF file to clipboard (without opening a window). |
| `text-grab://fullscreen` | Triggers a Fullscreen grab. |
| `text-grab://quick-lookup` | Opens Quick Simple Lookup. |
| `text-grab://settings` | Opens the Settings window. |

---

## 3. Constants

* `internal const string Scheme = "text-grab"`  
  Defines the primary URI protocol scheme name (`text-grab`).
* `private const string ProtocolKeyPath = @"Software\Classes\" + Scheme`  
  Defines the Windows Registry subkey path where protocol handler details are stored for the current user (`HKCU\Software\Classes\text-grab`).

---

## 4. Key Methods & Detailed Logic

### 4.1 Protocol Detection & Parsing

#### `IsProtocolUri(string? argument)`
```csharp
internal static bool IsProtocolUri(string? argument)
```
* **Purpose:** Determines if a command-line startup argument is formatted as a `text-grab://` protocol URI.
* **Logic:** Checks if `argument` is non-null and starts with `text-grab:` using ordinal case-insensitive comparison (`StringComparison.OrdinalIgnoreCase`).

---

#### `TryParseProtocolUri(string uriString, out string command, out Dictionary<string, string> parameters)`
```csharp
internal static bool TryParseProtocolUri(string uriString, out string command, out Dictionary<string, string> parameters)
```
* **Purpose:** Parses a URI string into a normalized command string and a dictionary of query parameters.
* **Parameters:**
  * `uriString`: The raw URI string to parse.
  * `command` *(out)*: Returns the lowercased command name (e.g., `grab-text` or `paste-spreadsheet`).
  * `parameters` *(out)*: Returns a `Dictionary<string, string>` (case-insensitive keys) containing query parameters.
* **Logic:**
  1. Validates `uriString` as an absolute URI using `Uri.TryCreate` and verifies `uri.Scheme` matches `"text-grab"`.
  2. Extracts the raw command from either `uri.Host` (for formats like `text-grab://command`) or `uri.AbsolutePath` (for formats like `text-grab:command`).
  3. Trims leading/trailing slashes and normalizes the command to lowercase via `.ToLowerInvariant()`.
  4. Splits `uri.Query` by `&`, separating key-value pairs at `=`.
  5. Unescapes key and value strings using `Uri.UnescapeDataString` and populates the `parameters` dictionary.
  6. Returns `true` if a valid non-empty command was extracted; otherwise `false`.

---

### 4.2 Security & Path Validation

URIs invoked from web browsers or external apps are inherently untrusted. The `TryGetSafeProtocolFilePath` method applies rigorous validation filters to sanitize any target path passed in a `path=` query parameter.

#### `TryGetSafeProtocolFilePath(string? rawPath, out string fullPath)`
```csharp
internal static bool TryGetSafeProtocolFilePath(string? rawPath, out string fullPath)
```
* **Purpose:** Validates that a file path provided via a URI parameter points to a safe, allowed local image or PDF file without exposing network vulnerabilities or unauthorized local file access.
* **Security Sequence & Validation Pipeline:**
  1. **Null/Whitespace Check:** Rejects empty or whitespace inputs.
  2. **UNC & Device Path Gate:** Rejects paths starting with `\\` or `//`. Checking this *before* touching the file system prevents outbound SMB connections that could leak the user's NTLM authentication hashes.
  3. **Canonicalization:** Obtains the absolute path via `Path.GetFullPath(rawPath)`. Re-validates that the resulting path does not start with `\\`.
  4. **Drive Letter Validation:** Verifies the path is rooted with a valid drive letter (e.g., `C:\`).
  5. **Drive Type Check:** Instantiates a `DriveInfo` object and rejects the path if the drive type is `Network`, `NoRootDirectory`, or `Unknown`.
  6. **Allowed Root Directory Check:** Verifies that the canonicalized path resides inside an allowed root directory via `IsUnderAllowedRoot`.
  7. **Visual Document Format Check:** Calls `IoUtilities.IsVisualDocumentFile(candidate)` to confirm the file is a recognized image/PDF format.
  8. If all checks pass, returns `true` with `fullPath` assigned; otherwise, returns `false`.

---

#### Private Security Helpers

#### `AllowedFileRoots()`
```csharp
private static IEnumerable<string> AllowedFileRoots()
```
Returns an enumerable of authorized root directories where incoming protocol files are permitted to reside:
* `AutomationProfile.Current.TemporaryDirectory` (if `AutomationProfile.Current` is defined).
* System Temp Directory (`Path.GetTempPath()`).
* User Downloads Directory (`UserProfile/Downloads`).
* User Pictures Directory (`Environment.SpecialFolder.MyPictures`).

#### `IsUnderAllowedRoot(string fullPath)`
```csharp
private static bool IsUnderAllowedRoot(string fullPath)
```
* Ensures `fullPath` resides inside one of the folders returned by `AllowedFileRoots()`.
* Normalizes root paths via `Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))`.
* Verifies exact path equality or checks if `fullPath` starts with `normalizedRoot + Path.DirectorySeparatorChar` (preventing partial path matches, such as matching `C:\DownloadsEvil` when `C:\Downloads` is the root).

---

### 4.3 Windows Registry Integration

#### `EnsureProtocolRegistration()`
```csharp
internal static void EnsureProtocolRegistration()
```
* **Purpose:** Registers the `text-grab://` URI scheme in the Windows Registry for unpackaged application execution.
* **Logic:**
  1. Checks if the application is running packaged (`AppUtilities.IsPackaged()`); if true, exits immediately (packaged apps register URIs via `AppxManifest.xml`).
  2. Acquires the current application executable path via `FileUtilities.GetExePath()`.
  3. Formats the expected command string as `"{exePath}" "%1"`.
  4. Inspects the registry at `HKCU\Software\Classes\text-grab\shell\open\command`. If the current registry value already matches `expectedCommand`, execution halts (idempotent design).
  5. If missing or stale, creates/updates the registry keys under `HKCU\Software\Classes\text-grab`:
     * Set `(Default)` value to `"URL:Text Grab Protocol"`.
     * Set `"URL Protocol"` value to `""`.
     * Create `DefaultIcon` subkey set to `"{exePath}",0`.
     * Create `shell\open\command` subkey set to `"{exePath}" "%1"`.
  6. Catches and logs any registry operation exceptions to `Debug.WriteLine`.

---

## 5. Security Summary Matrix

| Risk | Mitigation Mechanism in `ProtocolUtilities` |
| :--- | :--- |
| **NTLM Credential Leakage** | Explicitly rejects paths starting with `\\` or `//` before performing path resolution or filesystem access. |
| **Directory Traversal (`../`)** | Forces canonicalization through `Path.GetFullPath`. |
| **Arbitrary File Access** | Restricts allowable target file paths to specific directories (`Temp`, `Downloads`, `Pictures`, or active `AutomationProfile`). |
| **Network Drive Access** | Queries `DriveInfo.DriveType` and explicitly rejects `DriveType.Network`. |
| **Invalid File Processing** | Verifies file types via `IoUtilities.IsVisualDocumentFile()`. |