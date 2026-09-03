# Technical Documentation: `Tests/ThirdPartyNoticeUtilitiesTests.cs`

## Overview

The `ThirdPartyNoticeUtilitiesTests` class is a unit test suite located in the `Tests` namespace. Its primary purpose is to validate the integrity, completeness, and accuracy of third-party package disclosures maintained by `ThirdPartyNoticeUtilities.Packages` (from `Text_Grab.Utilities`).

It verifies that:
1. The catalog contains an exact list of expected direct package dependencies.
2. Every catalog entry provides a valid absolute URL for the project and a non-empty notice target.
3. Specific packages with local license notices (specifically `Markdig`) are correctly configured with local paths.

---

## Namespace and Dependencies

```csharp
using System.Linq;
using Text_Grab.Utilities;

namespace Tests;
```

* **`System.Linq`**: Used for collection querying and operations (`Select`, `OrderBy`, `ToArray`, `SingleOrDefault`).
* **`Text_Grab.Utilities`**: Provides the `ThirdPartyNoticeUtilities` class being tested.

---

## Class: `ThirdPartyNoticeUtilitiesTests`

### Test Methods

#### 1. `PackageCatalog_CoversAllDirectReferences()`

* **Attribute**: `[Fact]`
* **Purpose**: Ensures that `ThirdPartyNoticeUtilities.Packages` contains the exact expected list of third-party dependencies.

##### Logic and Implementation:
1. Defines a hardcoded array (`expectedPackageIds`) of 26 expected package IDs:
   * `BenchmarkDotNet`
   * `CliWrap`
   * `coverlet.collector`
   * `Dapplo.Windows.User32`
   * `Humanizer.Core`
   * `Magick.NET-Q16-AnyCPU`
   * `Magick.NET.SystemDrawing`
   * `Magick.NET.SystemWindowsMedia`
   * `Markdig`
   * `Microsoft.NET.Test.Sdk`
   * `Microsoft.Toolkit.Uwp.Notifications`
   * `Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers`
   * `Microsoft.WindowsAppSDK.AI`
   * `Microsoft.WindowsAppSDK.Foundation`
   * `Microsoft.WindowsAppSDK.Runtime`
   * `Microsoft.WindowsAppSDK.WinUI`
   * `NCalcAsync`
   * `PdfPig`
   * `UnitsNet`
   * `WPF-UI`
   * `WPF-UI.Tray`
   * `xunit.runner.visualstudio`
   * `Xunit.StaFact`
   * `xunit.v3`
   * `ZXing.Net`
   * `ZXing.Net.Bindings.Windows.Compatibility`
2. Queries `ThirdPartyNoticeUtilities.Packages`, selecting `PackageId` for each item, ordering them alphabetically, and materializing into an array `actualPackageIds`.
3. Compares `expectedPackageIds` (sorted) against `actualPackageIds` using `Assert.Equal`.

---

#### 2. `PackageCatalog_ProvidesProjectAndNoticeLinksForEveryEntry()`

* **Attribute**: `[Fact]`
* **Purpose**: Validates that every entry in `ThirdPartyNoticeUtilities.Packages` has populated and well-formed metadata for project links and notice targets.

##### Logic and Implementation:
Uses `Assert.All` to iterate through every package in `ThirdPartyNoticeUtilities.Packages` and validates two conditions:
1. **Project URL**: `Uri.IsWellFormedUriString(package.ProjectUrl, UriKind.Absolute)` must be `true`.
2. **Notice Target**: `string.IsNullOrWhiteSpace(package.NoticeTarget)` must be `false`.

If either condition fails, the failing package's `PackageId` is supplied as the user message for context.

---

#### 3. `PackageCatalog_UsesLocalNoticeForMarkdig()`

* **Attribute**: `[Fact]`
* **Purpose**: Validates specific local license settings for the `Markdig` package entry.

##### Logic and Implementation:
1. Searches `ThirdPartyNoticeUtilities.Packages` for an entry where `PackageId == "Markdig"` using `SingleOrDefault`.
2. Asserts that the package was found (`Assert.NotNull(package)`).
3. Asserts that `package.NoticeIsLocal` is `true`.
4. Asserts that `package.NoticeTarget` equals the relative file path: `@"ThirdPartyNotices\licenses\Markdig-license.txt"`.

---

## Inferred Data Contract of `ThirdPartyNoticeUtilities`

Based strictly on usage in this test file, the package items exposed by `ThirdPartyNoticeUtilities.Packages` possess the following properties:

| Property Name | Type | Description |
| :--- | :--- | :--- |
| `PackageId` | `string` | The identifier of the third-party NuGet package or dependency. |
| `ProjectUrl` | `string` | Absolute URL pointing to the project repository or home page. |
| `NoticeTarget` | `string` | Target URI or local file path containing the third-party notice/license text. |
| `NoticeIsLocal` | `bool` | Flag indicating whether the notice is stored locally on disk rather than remotely. |