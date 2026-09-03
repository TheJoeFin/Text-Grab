# Developer Documentation: `Tests/CaptureLanguageUtilitiesTests.cs`

## Overview

The `CaptureLanguageUtilitiesTests` class is a unit test suite within the `Tests` namespace. Its primary purpose is to test the utility functions provided by `CaptureLanguageUtilities` (located in `Text_Grab.Utilities`). 

These tests verify operations related to:
* Matching persisted language configuration strings against runtime language instances.
* Determining the preferred language index from available language options.
* Fetching available capture languages asynchronously based on feature settings and hardware support.
* Validating language capability flags (such as table output support).
* Evaluating whether capture modes require a live UI Automation source.

---

## Class Setup & Lifecycle Management

`CaptureLanguageUtilitiesTests` implements `IDisposable` and is decorated with the `[Collection("Settings isolation")]` attribute to prevent race conditions or settings corruption during parallel test execution.

### Fields

* `private readonly bool _originalUiAutomationEnabled`: Backing field storing the initial state of `Settings.Default.UiAutomationEnabled` prior to test execution.
* `private readonly bool _originalWindowsAiDescriptionEnabled`: Backing field storing the initial state of `Settings.Default.WindowsAiDescriptionEnabled` prior to test execution.

### Constructor & Disposal (`IDisposable`)

* **`CaptureLanguageUtilitiesTests()`**: Captures the initial user setting states (`UiAutomationEnabled` and `WindowsAiDescriptionEnabled`) from `Settings.Default`.
* **`Dispose()`**: Restores the modified settings to their original values, calls `Settings.Default.Save()`, and invalidates all language caches via `LanguageUtilities.InvalidateAllCaches()`. This guarantees test isolation across execution cycles.

---

## Tested Methods & Test Breakdown

### 1. Language Matching (`MatchesPersistedLanguage`)

Evaluates `CaptureLanguageUtilities.MatchesPersistedLanguage` against various implementation types of `ILanguage`.

| Test Method | Attribute | Description | Assertions |
| :--- | :--- | :--- | :--- |
| `MatchesPersistedLanguage_MatchesByLanguageTag` | `[Fact]` | Verifies that a `UiAutomationLang` instance matches its persisted language tag string (`UiAutomationLang.Tag`). | `Assert.True(matches)` |
| `MatchesPersistedLanguage_MatchesLegacyTesseractDisplayName` | `[Fact]` | Verifies that a `TessLang` instance matches its culture display name string (`CultureDisplayName`). | `Assert.True(matches)` |
| `MatchesPersistedLanguage_MatchesWindowsAiDescriptionTag` | `[Fact]` | Verifies that a `WindowsAiDescriptionLang` instance matches its language tag string (`WindowsAiDescriptionLang.Tag`). | `Assert.True(matches)` |

---

### 2. Preferred Language Selection (`FindPreferredLanguageIndex`)

Evaluates the logic used to identify the default or preferred language index from a list of available languages.

| Test Method | Attribute | Description | Assertions |
| :--- | :--- | :--- | :--- |
| `FindPreferredLanguageIndex_PrefersPersistedMatchBeforeFallbackLanguage` | `[Fact]` | Passes a list containing `UiAutomationLang`, `WindowsAiLang`, and `GlobalLang("en-US")`. Passes `UiAutomationLang.Tag` as the persisted match and `GlobalLang("en-US")` as the fallback. | Asserts that index `0` (`UiAutomationLang`) is selected over the fallback language. |

---

### 3. Capture Languages Retrieval (`GetCaptureLanguagesAsync`)

Evaluates `CaptureLanguageUtilities.GetCaptureLanguagesAsync` under different configuration states and platform capabilities. These tests use `[WpfFact]` to execute on WPF-compatible UI threads.

| Test Method | Attribute | Configuration / Condition | Assertions |
| :--- | :--- | :--- | :--- |
| `GetCaptureLanguagesAsync_ExcludesUiAutomationByDefault` | `[WpfFact]` | `Settings.Default.UiAutomationEnabled = false` | Asserts the returned list does **not** contain any `UiAutomationLang` instances. |
| `GetCaptureLanguagesAsync_IncludesUiAutomationWhenEnabled` | `[WpfFact]` | `Settings.Default.UiAutomationEnabled = true` | Asserts the returned list **contains** a `UiAutomationLang` instance. |
| `GetCaptureLanguagesAsync_ExcludesWindowsAiDescriptionByDefault` | `[WpfFact]` | `Settings.Default.WindowsAiDescriptionEnabled = false` | Asserts the returned list does **not** contain any `WindowsAiDescriptionLang` instances. |
| `GetCaptureLanguagesAsync_IncludesWindowsAiDescriptionOnlyWhenSupported` | `[WpfFact]` | `Settings.Default.WindowsAiDescriptionEnabled = true` | Checks `WindowsAiUtilities.CanDeviceDescribeImagesWithWinAI()`. If `true`, asserts `WindowsAiDescriptionLang` **is contained**; otherwise, asserts it is **not contained**. |

---

### 4. Feature Capabilities (`SupportsTableOutput`)

Evaluates whether specific capture language/mode objects support structured table output.

| Test Method | Attribute | Target Object | Assertions |
| :--- | :--- | :--- | :--- |
| `SupportsTableOutput_ReturnsFalseForUiAutomation` | `[Fact]` | `UiAutomationLang` | `Assert.False(...)` |
| `SupportsTableOutput_ReturnsFalseForWindowsAiDescription` | `[Fact]` | `WindowsAiDescriptionLang` | `Assert.False(...)` |

---

### 5. Live UI Automation Requirements (`RequiresLiveUiAutomationSource`)

Evaluates whether capture language modes require an active, live UI Automation source based on the source type and snapshot state.

| Test Method | Attribute | Parameters | Expected Result | Assertion |
| :--- | :--- | :--- | :--- | :--- |
| `RequiresLiveUiAutomationSource_ReturnsTrueForStaticUiAutomationWithoutSnapshot` | `[Fact]` | Language: `UiAutomationLang`<br>Static Image Source: `true`<br>Has Frozen Snapshot: `false` | `true` | `Assert.True(requiresLiveSource)` |
| `RequiresLiveUiAutomationSource_ReturnsFalseWhenFrozenSnapshotExists` | `[Fact]` | Language: `UiAutomationLang`<br>Static Image Source: `true`<br>Has Frozen Snapshot: `true` | `false` | `Assert.False(requiresLiveSource)` |
| `RequiresLiveUiAutomationSource_ReturnsFalseForOcrLanguageOnStaticImage` | `[Fact]` | Language: `GlobalLang("en-US")`<br>Static Image Source: `true`<br>Has Frozen Snapshot: `false` | `false` | `Assert.False(requiresLiveSource)` |

---

## Dependencies & Imports

The test file relies on the following namespaces and interfaces:

* **`Text_Grab.Interfaces`**:
  * `ILanguage`: Common interface for language and mode representations.
* **`Text_Grab.Models`**:
  * `UiAutomationLang`: Language wrapper for UI Automation modes.
  * `TessLang`: Language wrapper for Tesseract OCR.
  * `WindowsAiDescriptionLang`: Language wrapper for Windows AI image description.
  * `WindowsAiLang`: Language wrapper for Windows AI features.
  * `GlobalLang`: Represents standard system OCR languages (e.g., `"en-US"`).
* **`Text_Grab.Properties`**:
  * `Settings`: Application user settings manager (`Settings.Default`).
* **`Text_Grab.Utilities`**:
  * `CaptureLanguageUtilities`: The primary static utility class being tested.
  * `LanguageUtilities`: Utility class used to invalidate language caches (`InvalidateAllCaches()`).
  * `WindowsAiUtilities`: Utility class used to query system/device WinAI capabilities (`CanDeviceDescribeImagesWithWinAI()`).