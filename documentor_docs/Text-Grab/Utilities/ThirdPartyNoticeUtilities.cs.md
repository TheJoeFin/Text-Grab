# Documentation: `ThirdPartyNoticeUtilities.cs`

## Overview

The `ThirdPartyNoticeUtilities` static class in the `Text_Grab.Utilities` namespace provides centralized management, retrieval, and access logic for third-party software dependencies, licenses, and legal notices used within the application.

It maintains a list of third-party packages (both runtime application dependencies and test dependencies), resolves file system paths to attribution files (such as local license text files and `BUILT-WITH.md`), and provides methods to open project URLs, local notice files, or directories in the system's default viewer/shell.

---

## Constants

| Constant Name | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `BuiltWithFileName` | `string` | `"BUILT-WITH.md"` | Filename for the markdown document detailing built-with dependencies. |
| `NoticesDirectoryName` | `string` | `"ThirdPartyNotices"` | Directory name where third-party notice and license files reside. |
| `MarkdigNoticePath` | `string` | `@"ThirdPartyNotices\licenses\Markdig-license.txt"` | Relative file path to the local Markdig license file. |
| `WindowsAppSdkNoticePath` | `string` | `@"ThirdPartyNotices\licenses\Microsoft.WindowsAppSDK-license.txt"` | Relative file path to the local Microsoft Windows App SDK license file. |
| `DiagnosticsHubNoticePath` | `string` | `@"ThirdPartyNotices\licenses\Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers-LICENSE.md"` | Relative file path to the local Visual Studio DiagnosticsHub license file. |

---

## Static Properties

### `Packages`
* **Type:** `IReadOnlyList<ThirdPartyPackageInfo>`
* **Description:** A static, read-only collection containing metadata for all third-party libraries and packages utilized by the application and test suites.

Each entry is instantiated as a `ThirdPartyPackageInfo` model with parameters defining:
* **Package Name** (e.g., `"CliWrap"`, `"Markdig"`, `"WPF-UI"`)
* **Version Number**
* **Usage Context** (`"App"`, `"Tests"`, or `"App, Tests"`)
* **License Name** (e.g., `"MIT"`, `"Apache-2.0"`, `"BSD-2-Clause"`, `"Microsoft license terms"`)
* **Project URL**
* **Notice Target** (Remote URL or local relative path)
* **`NoticeIsLocal` flag** (Boolean: `true` if notice is stored locally, default is `false`)
* **Comments / Notes** (Optional description regarding the license or package role)

#### Included Package Inventory

1. **CliWrap** (3.10.1) - App [MIT]
2. **Dapplo.Windows.User32** (2.0.89) - App [MIT]
3. **Humanizer.Core** (3.0.10) - App [MIT]
4. **Magick.NET-Q16-AnyCPU** (14.12.0) - App [Apache-2.0]
5. **Magick.NET.SystemDrawing** (8.0.20) - App [Apache-2.0]
6. **Magick.NET.SystemWindowsMedia** (8.0.20) - App [Apache-2.0]
7. **Markdig** (1.1.3) - App [BSD-2-Clause] *(Local notice file)*
8. **Microsoft.Toolkit.Uwp.Notifications** (7.1.3) - App [MIT]
9. **Microsoft.WindowsAppSDK.AI** (1.8.70) - App [Microsoft license terms] *(Local notice file)*
10. **Microsoft.WindowsAppSDK.Foundation** (1.8.260415000) - App [Microsoft license terms] *(Local notice file)*
11. **Microsoft.WindowsAppSDK.Runtime** (1.8.260416003) - App [Microsoft license terms] *(Local notice file)*
12. **Microsoft.WindowsAppSDK.WinUI** (1.8.260415005) - App [Microsoft license terms] *(Local notice file)*
13. **NCalcAsync** (5.12.0) - App, Tests [MIT]
14. **PdfPig** (0.1.14) - App [Apache-2.0]
15. **UnitsNet** (5.75.0) - App [MIT-0]
16. **WPF-UI** (4.2.1) - App [MIT]
17. **WPF-UI.Tray** (4.2.1) - App [MIT]
18. **ZXing.Net** (0.16.11) - App [Apache-2.0]
19. **ZXing.Net.Bindings.Windows.Compatibility** (0.16.14) - App [Apache-2.0]
20. **BenchmarkDotNet** (0.15.8) - Tests [MIT]
21. **coverlet.collector** (10.0.0) - Tests [MIT]
22. **Microsoft.NET.Test.Sdk** (18.4.0) - Tests [MIT]
23. **Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers** (18.7.37220.1) - Tests [Microsoft license terms] *(Local notice file)*
24. **xunit.runner.visualstudio** (3.1.5) - Tests [Apache-2.0]
25. **Xunit.StaFact** (3.0.13) - Tests [MS-PL]
26. **xunit.v3** (3.2.2) - Tests [Apache-2.0]

---

## Methods

### Path Resolution Methods

#### `GetBuiltWithFilePath()`
```csharp
public static string? GetBuiltWithFilePath()
```
* **Description:** Computes the full file path to the `BUILT-WITH.md` file located in the application executable directory.
* **Returns:** The fully qualified file path string if the executable directory exists and is valid; otherwise, `null`.

#### `GetNoticesDirectoryPath()`
```csharp
public static string? GetNoticesDirectoryPath()
```
* **Description:** Computes the full path to the `ThirdPartyNotices` directory located in the application executable directory.
* **Returns:** The fully qualified directory path string if the executable directory exists and is valid; otherwise, `null`.

#### `GetNoticeTarget(ThirdPartyPackageInfo package)`
```csharp
public static string? GetNoticeTarget(ThirdPartyPackageInfo package)
```
* **Parameters:**
  * `package` (`ThirdPartyPackageInfo`): The package model whose notice path or URL is being retrieved.
* **Description:** Determines the target notice location for a given package:
  * If `package.NoticeIsLocal` is `false`, returns `package.NoticeTarget` directly (typically a web URL).
  * If `package.NoticeIsLocal` is `true`, combines the executable directory with `package.NoticeTarget` to form a full local path.
* **Returns:** A file path string, web URL string, or `null` if the executable directory cannot be determined for a local notice.

---

### External Execution Methods

#### `OpenBuiltWithFile()`
```csharp
public static void OpenBuiltWithFile()
```
* **Description:** Resolves the path to `BUILT-WITH.md` via `GetBuiltWithFilePath()` and opens it using the default system handler.

#### `OpenNoticesDirectory()`
```csharp
public static void OpenNoticesDirectory()
```
* **Description:** Resolves the path to the `ThirdPartyNotices` directory via `GetNoticesDirectoryPath()` and opens it in File Explorer.

#### `OpenNoticeFile(ThirdPartyPackageInfo package)`
```csharp
public static void OpenNoticeFile(ThirdPartyPackageInfo package)
```
* **Parameters:**
  * `package` (`ThirdPartyPackageInfo`): The package whose notice file or link should be opened.
* **Description:** Resolves the target via `GetNoticeTarget(package)` and opens it using the OS shell.

#### `OpenProjectUrl(ThirdPartyPackageInfo package)`
```csharp
public static void OpenProjectUrl(ThirdPartyPackageInfo package)
```
* **Parameters:**
  * `package` (`ThirdPartyPackageInfo`): The package whose repository/project web page should be opened.
* **Description:** Passes `package.ProjectUrl` to `OpenTarget(...)` to open the package URL in the system browser.

---

### Private Helper Methods

#### `OpenTarget(string? target)`
```csharp
private static void OpenTarget(string? target)
```
* **Parameters:**
  * `target` (`string?`): The target path, folder, or URL to launch.
* **Description:** Validates that `target` is not null, empty, or whitespace. If valid, launches a shell process using `Process.Start`:
  ```csharp
  Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
  ```

---

## External Dependencies

* `Text_Grab.Models.ThirdPartyPackageInfo`: Data model representing package attribution data.
* `Text_Grab.Utilities.FileUtilities`: Utility used to get the execution path via `FileUtilities.GetExePath()`.