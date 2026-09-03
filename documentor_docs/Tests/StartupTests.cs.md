# Technical Documentation: `Tests/StartupTests.cs`

## Overview

The `StartupTests` class in `Tests/StartupTests.cs` is a unit test suite designed to validate path resolution logic and command-line argument parsing for the **Text-Grab** application. 

Specifically, this test file serves two primary functions:
1. **Path Calculation Regression Testing**: Verifies fixes for legacy path calculation bugs where using `Path.GetDirectoryName(...)` on trimmed directory paths incorrectly resolved to parent directories instead of target application base directories.
2. **Startup Argument Parsing Validation**: Ensures that `App.ParseStartupArguments(...)` correctly parses command-line flags (such as `--windowless` and case-insensitive `--GRABFRAME`) and resolves primary arguments and file paths.

---

## Class Information

- **Namespace**: `Tests`
- **Class Name**: `StartupTests`
- **Dependencies**: 
  - `System.IO`
  - `Text_Grab`

---

## Test Methods Summary

| Test Method | Description |
| :--- | :--- |
| `StartupPathCalculation_OldVsNewLogic()` | Demonstrates the discrepancy between legacy parent-directory path calculation logic and corrected direct path combination for a standard executable path. |
| `WindowsStartupPathCalculation_OldVsNewLogic()` | Tests path calculation logic under a simulated `C:\Program Files` path structure. |
| `FixedStartupPathCalculation_UsesCorrectBaseDirectory()` | Validates that the corrected base-directory logic properly formats executable paths with trailing quotes and proper folder nesting. |
| `FileUtilitiesPathCalculation_OldVsNewLogic()` | Demonstrates the fix for resolving application subdirectories (specifically the `history` folder). |
| `FileUtilitiesLocalFilePathCalculation_OldVsNewLogic()` | Tests local file path resolution logic (e.g., relative image paths like `images\logo.png`) against old vs. new path calculations. |
| `ParseStartupArguments_IgnoresFlagsWhenSelectingPrimaryArgument()` | Verifies that passing flags like `--windowless` correctly sets argument properties (`IsQuiet`) while identifying positional primary arguments (`Settings`). |
| `ParseStartupArguments_FindsGrabFramePathCaseInsensitive()` | Ensures `--GRABFRAME` is parsed case-insensitively, setting `OpenInGrabFrame` to `true` and correctly mapping the `GrabFramePath`. |

---

## Detailed Test Method Breakdown

### 1. Executable & File Path Calculation Tests

These tests evaluate legacy vs. corrected logic across various directory structures. 

#### The Bug Pattern (Old Logic)
In legacy code, path calculations executed logic equivalent to:
```csharp
string? parentDir = Path.GetDirectoryName(simulatedBaseDirectory.TrimEnd('\\'));
```
Calling `Path.GetDirectoryName()` on `C:\Apps\Text-Grab` (after trimming the trailing slash) yields `C:\Apps` (the parent directory), leading to incorrect file paths like `C:\Apps\Text-Grab.exe` or `C:\Apps\history`.

#### The Corrected Logic (New Logic)
The updated logic directly combines the base directory with the target file or subdirectory:
```csharp
string newLogicPath = $"\"{Path.Combine(simulatedBaseDirectory, "Text-Grab.exe")}\"";
```
This correctly retains the root application directory (e.g., `C:\Apps\Text-Grab\Text-Grab.exe`).

---

#### `StartupPathCalculation_OldVsNewLogic`
- **Simulated Base Directory**: `C:\Apps\Text-Grab\`
- **Evaluated Target**: `Text-Grab.exe`
- **Assertions**:
  - `oldLogicPath` (`"C:\Apps\Text-Grab.exe"`) is **not equal** to `newLogicPath` (`"C:\Apps\Text-Grab\Text-Grab.exe"`).
  - `oldLogicPath` contains `C:\Apps\Text-Grab.exe`.
  - `newLogicPath` contains `C:\Apps\Text-Grab\Text-Grab.exe`.

#### `WindowsStartupPathCalculation_OldVsNewLogic`
- **Simulated Base Directory**: `C:\Program Files\Text-Grab\`
- **Evaluated Target**: `Text-Grab.exe`
- **Assertions**:
  - Confirms inequality between old and new calculations.
  - `oldLogicPath` equals `"\"C:\\Program Files\\Text-Grab.exe\""`.
  - `newLogicPath` equals `"\"C:\\Program Files\\Text-Grab\\Text-Grab.exe\""`.

#### `FixedStartupPathCalculation_UsesCorrectBaseDirectory`
- **Simulated Base Directory**: `C:\MyApps\Text-Grab\`
- **Evaluated Target**: `Text-Grab.exe` using `Path.Combine` directly with `simulatedBaseDirectory`.
- **Assertions**:
  - `fixedLogicPath` equals `"\"C:\\MyApps\\Text-Grab\\Text-Grab.exe\""`.
  - Contains `C:\MyApps\Text-Grab`.
  - Ends with `Text-Grab.exe"`.

#### `FileUtilitiesPathCalculation_OldVsNewLogic`
- **Simulated Base Directory**: `C:\Apps\Text-Grab\`
- **Evaluated Target**: `history` directory
- **Assertions**:
  - `oldLogicHistoryPath` resolves to `C:\Apps\history` (incorrect parent path).
  - `newLogicHistoryPath` resolves to `C:\Apps\Text-Grab\history` (correct path).

#### `FileUtilitiesLocalFilePathCalculation_OldVsNewLogic`
- **Simulated Base Directory**: `C:\Program Files\Text-Grab\`
- **Relative Target File**: `images\logo.png`
- **Assertions**:
  - `oldLogicPath` resolves to `C:\Program Files\images\logo.png`.
  - `newLogicPath` resolves to `C:\Program Files\Text-Grab\images\logo.png`.

---

### 2. Startup Argument Parsing Tests

These tests validate the behavior of `App.ParseStartupArguments(string[] args)`.

#### `ParseStartupArguments_IgnoresFlagsWhenSelectingPrimaryArgument`
- **Input Arguments**: `["--windowless", "Settings"]`
- **Verifications**:
  - `startupArguments.IsQuiet` is set to `true` (triggered by `--windowless`).
  - `startupArguments.PrimaryArgument` correctly captures `"Settings"`, ignoring the preceding command flag.

#### `ParseStartupArguments_FindsGrabFramePathCaseInsensitive`
- **Setup**: Creates a temporary file via `Path.GetTempFileName()`.
- **Input Arguments**: `["--GRABFRAME", tempFilePath]` (testing uppercase flag handling).
- **Execution Lifecycle**: Wrapped inside a `try...finally` block to ensure the temporary file is deleted upon test completion.
- **Verifications**:
  - `startupArguments.OpenInGrabFrame` is set to `true`.
  - `startupArguments.PrimaryArgument` matches `tempFilePath`.
  - `startupArguments.GrabFramePath` matches `Path.GetFullPath(tempFilePath)`.