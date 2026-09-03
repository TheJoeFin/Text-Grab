# Technical Documentation: `ProtocolUtilitiesTests.cs`

## Overview

The `ProtocolUtilitiesTests.cs` file contains unit tests for the `ProtocolUtilities` class (located in `Text_Grab.Utilities`). Written using the xUnit testing framework, this suite verifies the behavior of custom protocol scheme handling (`text-grab://` or `text-grab:`), protocol URI command and parameter parsing, and path validation security rules.

---

## File Information

* **File Path:** `Tests/ProtocolUtilitiesTests.cs`
* **Namespace:** `Tests`
* **Target Class Under Test:** `Text_Grab.Utilities.ProtocolUtilities`
* **Dependencies:**
  * `System`
  * `System.IO`
  * `Text_Grab.Utilities`
  * xUnit (`Fact`, `Theory`, `InlineData`, `Assert`)

---

## Tested Functionality Summary

Based on the test cases, the underlying `ProtocolUtilities` class provides three core capabilities:

1. **Protocol URI Detection (`IsProtocolUri`)**: Checks whether a given string argument matches the `text-grab` custom protocol format.
2. **Protocol URI Parsing (`TryParseProtocolUri`)**: Extracts normalized commands and query parameters from protocol URIs, handling case sensitivity, URL decoding, trailing slashes, and malformed parameters.
3. **Safe File Path Resolution (`TryGetSafeProtocolFilePath`)**: Enforces file system security policies for files passed via protocol parameters, rejecting unsafe paths (e.g., UNC shares, path traversal attacks, non-allowed directories, unsupported extensions, or non-existent files).

---

## Detailed Test Specifications

### 1. Protocol URI Detection Tests (`IsProtocolUri`)

#### `IsProtocolUri_RecognizesProtocolArguments`
* **Type:** `[Theory]`
* **Purpose:** Verifies that valid `text-grab` scheme strings are correctly identified as protocol URIs.
* **Tested Inputs:**
  * `"text-grab://paste-spreadsheet"`
  * `"TEXT-GRAB://EDIT-TEXT"` (verifies case-insensitivity)
  * `"text-grab:grab-frame"` (verifies standard protocol format without double slashes)
* **Assertion:** `Assert.True(ProtocolUtilities.IsProtocolUri(argument))`

#### `IsProtocolUri_RejectsOtherArguments`
* **Type:** `[Theory]`
* **Purpose:** Verifies that non-protocol inputs, file paths, web links, command flags, and empty values are rejected.
* **Tested Inputs:** `null`, `""`, `"Settings"`, `@"C:\images\screenshot.png"`, `"https://example.com"`, `"--windowless"`
* **Assertion:** `Assert.False(ProtocolUtilities.IsProtocolUri(argument))`

---

### 2. Protocol URI Parsing Tests (`TryParseProtocolUri`)

#### `TryParseProtocolUri_ParsesCommands`
* **Type:** `[Theory]`
* **Purpose:** Verifies successful parsing of various protocol commands into expected standard command names.
* **Test Cases:**
  | Input URI | Expected Parsed Command |
  | :--- | :--- |
  | `"text-grab://paste-spreadsheet"` | `"paste-spreadsheet"` |
  | `"text-grab://edit-text"` | `"edit-text"` |
  | `"text-grab://grab-frame"` | `"grab-frame"` |
  | `"text-grab://grab-text"` | `"grab-text"` |
  | `"text-grab://fullscreen"` | `"fullscreen"` |
  | `"text-grab://quick-lookup"` | `"quick-lookup"` |
  | `"text-grab://settings"` | `"settings"` |

#### `TryParseProtocolUri_NormalizesCommandForms`
* **Type:** `[Theory]`
* **Purpose:** Ensures command names are normalized regardless of capitalization, trailing slashes, or missing `//` separators.
* **Tested Inputs:**
  * `"TEXT-GRAB://Paste-Spreadsheet"` $\rightarrow$ `"paste-spreadsheet"`
  * `"text-grab://paste-spreadsheet/"` $\rightarrow$ `"paste-spreadsheet"`
  * `"text-grab:paste-spreadsheet"` $\rightarrow$ `"paste-spreadsheet"`

#### `TryParseProtocolUri_ExtractsUrlEncodedPathParameter`
* **Type:** `[Fact]`
* **Purpose:** Ensures URL-encoded query parameters (e.g., `path=...`) are correctly decoded and added to the parameters dictionary.
* **Test Flow:**
  * Constructs a URI containing an encoded Windows file path: `text-grab://grab-frame?path=C%3A%5CUsers...`
  * Calls `TryParseProtocolUri`.
  * Verifies the output command is `"grab-frame"` and `parameters["path"]` matches the unencoded local path (`@"C:\Users\joe\Downloads\TextGrab\capture 2026-06-12.png"`).

#### `TryParseProtocolUri_ParsesGrabTextWithPath`
* **Type:** `[Fact]`
* **Purpose:** Confirms parsing behavior specifically for the `grab-text` command with a URL-encoded `path` parameter.
* **Test Logic:** Evaluates `text-grab://grab-text?path={encodedPath}` and verifies the decoded path parameter output.

#### `TryParseProtocolUri_ParameterKeysAreCaseInsensitive`
* **Type:** `[Fact]`
* **Purpose:** Ensures parameter key lookups in the dictionary are case-insensitive.
* **Test Logic:** Parses `text-grab://grab-frame?PATH=C%3A%5Cimage.png` and verifies accessing `parameters["path"]` (lowercase) returns `@"C:\image.png"`.

#### `TryParseProtocolUri_RejectsInvalidUris`
* **Type:** `[Theory]`
* **Purpose:** Ensures malformed strings, web links, or empty protocol URIs fail to parse.
* **Tested Inputs:** `"https://example.com"`, `"not a uri"`, `"text-grab://"`, `""`
* **Assertion:** Returns `false` from `TryParseProtocolUri`.

#### `TryParseProtocolUri_IgnoresMalformedQueryPairs`
* **Type:** `[Fact]`
* **Purpose:** Ensures malformed query key-value pairs (e.g., missing key `?=novalue` or flags without values `&flag`) are ignored while preserving valid parameters.
* **Test Input:** `"text-grab://grab-frame?=novalue&path=C%3A%5Ca.png&flag"`
* **Assertions:**
  * Returns `true` for parsing.
  * Extracted command is `"grab-frame"`.
  * `parameters` dictionary contains exactly **1** entry (`parameters["path"]` equal to `@"C:\a.png"`).

---

### 3. Safe Protocol File Path Validation Tests (`TryGetSafeProtocolFilePath`)

#### `TryGetSafeProtocolFilePath_AcceptsImageInTempFolder`
* **Type:** `[Fact]`
* **Purpose:** Verifies that a valid image file created within the system Temp folder is accepted as safe.
* **Test Execution:**
  1. Generates a temporary `.png` file path inside `Path.GetTempPath()`.
  2. Creates the file using `File.WriteAllBytes`.
  3. Executes `TryGetSafeProtocolFilePath`.
  4. Asserts that the path is marked safe (`true`) and outputs the full canonical path.
  5. Deletes the temporary file in a `finally` block.

#### `TryGetSafeProtocolFilePath_RejectsUncDeviceAndEmptyPaths`
* **Type:** `[Theory]`
* **Purpose:** Rejects null, empty, whitespace, UNC (Universal Naming Convention) network paths (which could cause SMB credential leaks), extended-length paths, and physical device namespaces.
* **Tested Inputs:**
  * `null`
  * `""`
  * `"   "`
  * `@"\\server\share\image.png"` (UNC path)
  * `"//server/share/image.png"` (Forward-slash UNC path)
  * `@"\\?\C:\Windows\image.png"` (Extended-length device path)
  * `@"\\.\PhysicalDrive0"` (Device namespace)
* **Assertion:** Returns `false`.

#### `TryGetSafeProtocolFilePath_RejectsPathOutsideAllowedRoots`
* **Type:** `[Fact]`
* **Purpose:** Rejects paths located outside allowed system root directories (e.g., files in the Windows directory).
* **Test Execution:** Generates a path in `SpecialFolder.Windows` and verifies `TryGetSafeProtocolFilePath` returns `false`.

#### `TryGetSafeProtocolFilePath_RejectsTraversalEscapingAllowedRoot`
* **Type:** `[Fact]`
* **Purpose:** Prevents directory traversal attacks that attempt to escape allowed roots using relative parent directory sequences (`..`).
* **Test Input:** `Path.Combine(Path.GetTempPath(), "..", "..", "..", "Windows", "image.png")`
* **Assertion:** Returns `false`.

#### `TryGetSafeProtocolFilePath_RejectsNonImageExtensionInAllowedRoot`
* **Type:** `[Fact]`
* **Purpose:** Ensures files without an allowed image file extension are rejected even if present inside an allowed root (e.g., Temp folder).
* **Test Execution:**
  1. Creates a `.txt` file in the Temp directory.
  2. Asserts `TryGetSafeProtocolFilePath` returns `false`.
  3. Cleans up the temporary text file.

#### `TryGetSafeProtocolFilePath_RejectsNonexistentImageInAllowedRoot`
* **Type:** `[Fact]`
* **Purpose:** Ensures image files that do not actually exist on disk are rejected, even if specified within an allowed root directory.
* **Test Logic:** Generates a non-existent `.png` path in the Temp folder and asserts `TryGetSafeProtocolFilePath` returns `false`.