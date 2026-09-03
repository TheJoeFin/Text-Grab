# Technical Documentation Guide: `Tests/LanguageServiceTests.cs`

## Overview

The `LanguageServiceTests` class is a unit testing suite designed to validate the functionality of `LanguageService`, `LanguageUtilities`, and related models (`HistoryInfo`, `ILanguage` implementations) in the `Text_Grab` application.

This test class strictly targets language resolution, tag extraction, language kind identification, singleton behavior, feature fallback logic when specific features (like UI Automation or Windows AI Description) are disabled, and isolation of application settings during test execution.

---

## Class Architecture & Test Setup

### Namespace & Attributes
* **Namespace:** `Tests`
* **Collection Attribute:** `[Collection("Settings isolation")]`
  * Ensures that tests modifying application configuration settings (`Settings.Default`) run in an isolated environment to prevent inter-test state contamination.
* **Interface Implementation:** `IDisposable`

### State Isolation Lifecycle
To prevent settings modifications from leaking into other test execution paths, `LanguageServiceTests` captures the initial state of key settings upon instantiation and restores them during cleanup.

#### Constructor
Captures the original settings before any test execution:
* `_originalLastUsedLang`: Stores `Settings.Default.LastUsedLang`.
* `_originalUiAutomationEnabled`: Stores `Settings.Default.UiAutomationEnabled`.
* `_originalWindowsAiDescriptionEnabled`: Stores `Settings.Default.WindowsAiDescriptionEnabled`.

#### `Dispose()` Method
Restores the stored settings, saves them to persistent storage, and invalidates language caches:
1. Restores `Settings.Default.LastUsedLang`, `Settings.Default.UiAutomationEnabled`, and `Settings.Default.WindowsAiDescriptionEnabled`.
2. Invokes `Settings.Default.Save()`.
3. Invokes `LanguageUtilities.InvalidateAllCaches()`.

---

## Key Components & Tested Services

The tests target interactions between the following core types:
* **`LanguageService`**: Primary service for managing and resolving language objects, tags, kinds, and persistence identities.
* **`LanguageUtilities`**: Utility class delegating operations to `LanguageService`.
* **`HistoryInfo`**: Model class representing OCR execution history and language references.
* **Language Models / Interfaces**: `GlobalLang`, `WindowsAiLang`, `WindowsAiDescriptionLang`, `UiAutomationLang`, `TessLang`, `Windows.Globalization.Language`.
* **`LanguageKind`**: Enum classifying the source/type of language support (e.g., `Global`, `WindowsAi`, `WindowsAiDescription`, `UiAutomation`, `Tesseract`).

---

## Detailed Test Methods Breakdown

### 1. Language Tag Extraction (`GetLanguageTag`)

These tests verify that `LanguageService.GetLanguageTag(object)` correctly extracts string language tags across distinct language object types.

| Test Method | Input Object | Expected Output Tag |
| :--- | :--- | :--- |
| `GetLanguageTag_WithGlobalLang_ReturnsCorrectTag` | `GlobalLang("en-US")` | `"en-US"` |
| `GetLanguageTag_WithWindowsAiLang_ReturnsWinAI` | `WindowsAiLang()` | `"WinAI"` |
| `GetLanguageTag_WithWindowsAiDescriptionLang_ReturnsDescriptionTag` | `WindowsAiDescriptionLang()` | `WindowsAiDescriptionLang.Tag` |
| `GetLanguageTag_WithUiAutomationLang_ReturnsUiAutomationTag` | `UiAutomationLang()` | `UiAutomationLang.Tag` |
| `GetLanguageTag_WithTessLang_ReturnsRawTag` | `TessLang("eng")` | `"eng"` |
| `GetLanguageTag_WithLanguage_ReturnsLanguageTag` | `Windows.Globalization.Language("en-US")` | `"en-US"` |

---

### 2. Language Kind Resolution (`GetLanguageKind`)

These tests verify that `LanguageService.GetLanguageKind(object)` maps different language instances to their corresponding `LanguageKind` enum values.

| Test Method | Input Object | Expected `LanguageKind` |
| :--- | :--- | :--- |
| `GetLanguageKind_WithGlobalLang_ReturnsGlobal` | `GlobalLang("en-US")` | `LanguageKind.Global` |
| `GetLanguageKind_WithWindowsAiLang_ReturnsWindowsAi` | `WindowsAiLang()` | `LanguageKind.WindowsAi` |
| `GetLanguageKind_WithWindowsAiDescriptionLang_ReturnsWindowsAiDescription` | `WindowsAiDescriptionLang()` | `LanguageKind.WindowsAiDescription` |
| `GetLanguageKind_WithUiAutomationLang_ReturnsUiAutomation` | `UiAutomationLang()` | `LanguageKind.UiAutomation` |
| `GetLanguageKind_WithTessLang_ReturnsTesseract` | `TessLang("eng")` | `LanguageKind.Tesseract` |
| `GetLanguageKind_WithLanguage_ReturnsGlobal` | `Windows.Globalization.Language("en-US")` | `LanguageKind.Global` |
| `GetLanguageKind_WithUnknownType_ReturnsGlobal` | `"some string"` (unsupported `object`) | `LanguageKind.Global` |

---

### 3. Persistence Identity & Fallback Logic

Tests validating persistence identity handling and fallback behaviors when certain feature flags are disabled.

#### `GetPersistedLanguageIdentity_ForUiAutomationUsesRollbackSafeGlobalLanguage`
* **Purpose:** Verifies that persistence logic for `UiAutomationLang` produces a rollback-safe global language representation.
* **Assertions:**
  * `usedUiAutomation` flag is `true`.
  * Returned `languageKind` is `LanguageKind.Global`.
  * Returned `languageTag` does not equal `UiAutomationLang.Tag`.

#### `GetOCRLanguage_WhenUiAutomationWasLastUsedButFeatureIsDisabled_FallsBack`
* **Purpose:** Verifies fallback behavior when UI Automation was the last used language setting, but the feature (`UiAutomationEnabled`) is set to `false`.
* **Execution Flow:**
  1. Sets `Settings.Default.UiAutomationEnabled = false`.
  2. Sets `Settings.Default.LastUsedLang = UiAutomationLang.Tag`.
  3. Saves settings and invalidates caches via `LanguageUtilities.InvalidateAllCaches()`.
  4. Retrieves the OCR language via `Singleton<LanguageService>.Instance.GetOCRLanguage()`.
* **Assertion:** Asserts that the returned language object is **not** of type `UiAutomationLang`.

#### `GetOCRLanguage_WhenWindowsAiDescriptionWasLastUsedButFeatureIsDisabled_FallsBack`
* **Purpose:** Verifies fallback behavior when Windows AI Description was the last used language setting, but the feature (`WindowsAiDescriptionEnabled`) is set to `false`.
* **Execution Flow:**
  1. Sets `Settings.Default.WindowsAiDescriptionEnabled = false`.
  2. Sets `Settings.Default.LastUsedLang = WindowsAiDescriptionLang.Tag`.
  3. Saves settings and invalidates caches via `LanguageUtilities.InvalidateAllCaches()`.
  4. Retrieves the OCR language via `Singleton<LanguageService>.Instance.GetOCRLanguage()`.
* **Assertion:** Asserts that the returned language object is **not** of type `WindowsAiDescriptionLang`.

---

### 4. Singleton Pattern and Delegation Tests

#### `LanguageService_IsSingleton`
* **Purpose:** Confirms that `LanguageService` adheres to the singleton pattern managed via `Singleton<LanguageService>.Instance`.
* **Assertion:** `Assert.Same(instance1, instance2)` verifies that two calls return identical references in memory.

#### `LanguageUtilities_DelegatesTo_LanguageService`
* **Purpose:** Validates that `LanguageUtilities` static utility methods forward calls to `LanguageService`.
* **Execution:** Calls `LanguageUtilities.GetLanguageTag` and `LanguageUtilities.GetLanguageKind` using a `GlobalLang("en-US")` instance.
* **Assertions:** Confirms returned tag is `"en-US"` and kind is `LanguageKind.Global`.

---

### 5. `HistoryInfo` Model Integration Tests

Tests checking how `HistoryInfo` resolves `OcrLanguage` dynamically based on stored properties.

#### `HistoryInfo_OcrLanguage_FallsBackForUiAutomationPersistence`
* **Purpose:** Checks fallback logic for `HistoryInfo` instances configured with `UiAutomationLang.Tag` and `LanguageKind.UiAutomation`.
* **Assertion:** Verifies `historyInfo.OcrLanguage` is **not** of type `UiAutomationLang`.

#### `HistoryInfo_OcrLanguage_ReturnsWindowsAiDescriptionLanguage`
* **Purpose:** Confirms that `HistoryInfo` instances configured with `WindowsAiDescriptionLang.Tag` and `LanguageKind.WindowsAiDescription` correctly resolve to a `WindowsAiDescriptionLang` object.
* **Assertion:** Verifies `historyInfo.OcrLanguage` is of type `WindowsAiDescriptionLang`.