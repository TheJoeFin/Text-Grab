# Detailed Technical Documentation: `Text-Grab/Services/LanguageService.cs`

## Overview

The `LanguageService` class in the `Text_Grab.Services` namespace provides cached, thread-safe management and lookup of language capabilities for Optical Character Recognition (OCR) and text processing operations. 

By holding static instances of custom pseudo-language models and caching resolved language objects, `LanguageService` minimizes garbage collector pressure and allocation overhead during frequent language checks and OCR operations.

---

## Architectural & Design Concepts

### Caching and Thread Safety
* **Lock Object:** A private read-only object (`_cacheLock`) synchronizes access to cached field members across different threads.
* **Cache Invalidation:** The class exposes granular and global cache invalidation methods (`InvalidateLanguagesCache`, `InvalidateOcrLanguageCache`, `InvalidateAllCaches`) to handle external state updates, such as changes to the operating system's input language or user settings.

### Static Pseudo-Language Instances
To reduce memory allocations, `LanguageService` maintains static instances for non-standard or custom OCR sources:
* `WindowsAiLang` (`_windowsAiLangInstance`)
* `WindowsAiDescriptionLang` (`_windowsAiDescriptionLangInstance`)
* `UiAutomationLang` (`_uiAutomationLangInstance`)

---

## Fields & Constants

| Field Name | Type | Description |
| :--- | :--- | :--- |
| `_cachedAllLanguages` | `IList<ILanguage>?` | Caches the combined list of all supported dynamic, static, and system OCR languages. |
| `_cachedCurrentInputLanguage` | `ILanguage?` | Holds the cached instance representing the current input language. |
| `_cachedCurrentInputLanguageTag` | `string?` | Stores the language tag string associated with `_cachedCurrentInputLanguage`. |
| `_cachedSystemLanguageForTranslation` | `string?` | Stores a user-friendly language name derived from the input language for translation tasks. |
| `_cachedLastUsedLang` | `string?` | Stores the language tag string used in the previous OCR execution. |
| `_cachedOcrLanguage` | `ILanguage?` | Holds the resolved `ILanguage` instance to be used for the next OCR operation. |
| `_cacheLock` | `readonly object` | Thread lock object used to synchronize access to cached instances. |
| `_windowsAiLangInstance` | `static readonly WindowsAiLang` | Reusable singleton instance for Windows AI language capability. |
| `_windowsAiLangTag` | `static readonly string` | Cached string tag derived from `_windowsAiLangInstance.LanguageTag`. |
| `_windowsAiDescriptionLangInstance` | `static readonly WindowsAiDescriptionLang` | Reusable singleton instance for Windows AI Description capability. |
| `_windowsAiDescriptionLangTag` | `static readonly string` | Cached string tag derived from `_windowsAiDescriptionLangInstance.LanguageTag`. |
| `_uiAutomationLangInstance` | `static readonly UiAutomationLang` | Reusable singleton instance for UI Automation text extraction. |
| `_uiAutomationLangTag` | `static readonly string` | Cached string tag derived from `_uiAutomationLangInstance.LanguageTag`. |

---

## Public Methods

### Language Retrieval Methods

#### `GetCurrentInputLanguage()`
* **Return Type:** `ILanguage`
* **Description:** Resolves the current system input language tag via `GetCurrentInputLanguageTag()`. If the cached input language tag matches the active tag, it returns the cached `ILanguage` instance. Otherwise, it instantiates a new `GlobalLang` object, updates the cache, and returns it under a lock.

#### `GetAllLanguages()`
* **Return Type:** `IList<ILanguage>`
* **Description:** Retrieves all available languages that can perform OCR or text retrieval. Results are populated on demand and cached.
* **Inclusion Logic:**
  1. Checks `AppUtilities.TextGrabSettings.UiAutomationEnabled`; if `true`, adds `_uiAutomationLangInstance`.
  2. Checks `WindowsAiUtilities.CanDeviceUseWinAI()`; if `true`, adds `_windowsAiLangInstance`.
  3. Checks `AppUtilities.TextGrabSettings.WindowsAiDescriptionEnabled` and `WindowsAiUtilities.CanDeviceDescribeImagesWithWinAI()`; if both are `true`, adds `_windowsAiDescriptionLangInstance`.
  4. Iterates through `OcrEngine.AvailableRecognizerLanguages` and wraps each Windows `Language` object in a `GlobalLang` instance.

#### `GetOCRLanguage()`
* **Return Type:** `ILanguage`
* **Description:** Determines the optimal `ILanguage` to use for OCR based on user settings, fallback rules, and available engines.
* **Resolution Steps:**
  1. Checks if the setting `AppUtilities.TextGrabSettings.LastUsedLang` matches `_cachedLastUsedLang` and a valid `_cachedOcrLanguage` exists. If so, returns `_cachedOcrLanguage`.
  2. Attempts to match `LastUsedLang` against known language tags:
     * **Windows AI Language (`_windowsAiLangTag`):** Uses `_windowsAiLangInstance` if `WindowsAiUtilities.CanDeviceUseWinAI()` is `true`.
     * **Windows AI Description (`_windowsAiDescriptionLangTag`):** Uses `_windowsAiDescriptionLangInstance` if feature setting and hardware capabilities allow.
     * **UI Automation (`_uiAutomationLangTag`):** Uses `_uiAutomationLangInstance` if `UiAutomationEnabled` is `true`. If disabled, falls back to `CaptureLanguageUtilities.GetUiAutomationFallbackLanguage()`.
     * **Standard Language Tag:** Attempts to instantiate a `GlobalLang(lastUsedLang)`. On exception/failure, reverts to `GetCurrentInputLanguage()`.
  3. Validates the resolved language against `GetAllLanguages()`:
     * If no languages are available at all, returns `GlobalLang("en-US")`.
     * If the selected tag does not strictly match any available language, performs substring matching (`Contains`) to find similar languages.
     * If a similar language is found, instantiates a `GlobalLang` with that tag.
     * If no similar languages match, defaults to the first available language in `GetAllLanguages()`.

---

### Identity & Classification Methods

#### `GetLanguageTag(object language)`
* **Parameters:** `object language`
* **Return Type:** `string`
* **Description:** Static pattern-matching helper that extracts the string language tag from various supported language object types.
* **Supported Pattern Matches:**
  * `Language lang` $\rightarrow$ `lang.LanguageTag`
  * `WindowsAiLang` $\rightarrow$ `_windowsAiLangTag`
  * `WindowsAiDescriptionLang` $\rightarrow$ `_windowsAiDescriptionLangTag`
  * `UiAutomationLang` $\rightarrow$ `_uiAutomationLangTag`
  * `TessLang tessLang` $\rightarrow$ `tessLang.RawTag`
  * `GlobalLang gLang` $\rightarrow$ `gLang.LanguageTag`
  * **Fallback:** Throws `ArgumentException` for unsupported types.

#### `GetLanguageKind(object language)`
* **Parameters:** `object language`
* **Return Type:** `LanguageKind`
* **Description:** Identifies the `LanguageKind` enum type corresponding to a provided language object.
* **Supported Pattern Matches:**
  * `Language` $\rightarrow$ `LanguageKind.Global`
  * `WindowsAiLang` $\rightarrow$ `LanguageKind.WindowsAi`
  * `WindowsAiDescriptionLang` $\rightarrow$ `LanguageKind.WindowsAiDescription`
  * `UiAutomationLang` $\rightarrow$ `LanguageKind.UiAutomation`
  * `TessLang` $\rightarrow$ `LanguageKind.Tesseract`
  * **Fallback:** Returns `LanguageKind.Global`.

#### `GetPersistedLanguageIdentity(object language)`
* **Parameters:** `object language`
* **Return Type:** `(string LanguageTag, LanguageKind LanguageKind, bool UsedUiAutomation)`
* **Description:** Generates persistent identity attributes for a given language instance.
* **Behavior:**
  * If `language` is `UiAutomationLang`, obtains a fallback language from `CaptureLanguageUtilities.GetUiAutomationFallbackLanguage()` and returns its tag, `LanguageKind.Global`, and sets `UsedUiAutomation` to `true`.
  * Otherwise, returns the tag via `GetLanguageTag()`, the kind via `GetLanguageKind()`, and `UsedUiAutomation` as `false`.

#### `NormalizePersistedLanguageIdentity(LanguageKind languageKind, string languageTag, bool usedUiAutomation = false)`
* **Parameters:**
  * `LanguageKind languageKind`
  * `string languageTag`
  * `bool usedUiAutomation` (default: `false`)
* **Return Type:** `(string LanguageTag, LanguageKind LanguageKind, bool UsedUiAutomation)`
* **Description:** Normalizes identity metadata saved in persistent settings.
* **Behavior:** If `languageKind` is `LanguageKind.UiAutomation` or `languageTag` matches `_uiAutomationLangTag` (case-insensitive), it overrides the identity using `CaptureLanguageUtilities.GetUiAutomationFallbackLanguage()` and returns `LanguageKind.Global` with `UsedUiAutomation = true`. Otherwise, returns the input parameters unmodified.

#### `IsCurrentLanguageLatinBased()`
* **Return Type:** `bool`
* **Description:** Evaluates whether the active input language uses a Latin-based script by invoking `IsLatinBased()` on the result of `GetCurrentInputLanguage()`.

---

### Translation & Localization Methods

#### `GetSystemLanguageForTranslation()`
* **Return Type:** `string`
* **Description:** Obtains a simplified, human-readable system language name intended for Windows AI translation prompts (e.g., "English", "Spanish", "Japanese").
* **Behavior:**
  1. Checks cache against current input language tag.
  2. Strips region information inside parentheses from `DisplayName` (e.g., converts `"English (United States)"` to `"English"`).
  3. Evaluates BCP-47 tag prefixes using a switch expression:
     * `en*` $\rightarrow$ "English"
     * `es*` $\rightarrow$ "Spanish"
     * `fr*` $\rightarrow$ "French"
     * `de*` $\rightarrow$ "German"
     * `it*` $\rightarrow$ "Italian"
     * `pt*` $\rightarrow$ "Portuguese"
     * `ru*` $\rightarrow$ "Russian"
     * `ja*` $\rightarrow$ "Japanese"
     * `zh*` $\rightarrow$ "Chinese"
     * `ko*` $\rightarrow$ "Korean"
     * `ar*` $\rightarrow$ "Arabic"
     * `hi*` $\rightarrow$ "Hindi"
     * **Default:** Uses the stripped display name.
  4. On exception, catches the error and defaults to returning `"English"`.

---

### Cache Invalidation Methods

#### `InvalidateLanguagesCache()`
* **Return Type:** `void`
* **Description:** Resets `_cachedAllLanguages` and `_cachedOcrLanguage` to `null` under lock synchronization. Use when available system OCR languages are updated or installed.

#### `InvalidateOcrLanguageCache()`
* **Return Type:** `void`
* **Description:** Resets `_cachedOcrLanguage` and `_cachedLastUsedLang` to `null` under lock synchronization. Use when application settings alter the selected OCR language.

#### `InvalidateAllCaches()`
* **Return Type:** `void`
* **Description:** Resets all internal cache fields (`_cachedAllLanguages`, `_cachedCurrentInputLanguage`, `_cachedCurrentInputLanguageTag`, `_cachedSystemLanguageForTranslation`, `_cachedLastUsedLang`, `_cachedOcrLanguage`) to `null` under lock synchronization.

---

## Private Helper Methods

### `GetCurrentInputLanguageTag()`
* **Return Type:** `string`
* **Description:** Attempts to detect the operational system input language string through cascading fallbacks.
* **Resolution Pipeline:**
  1. Reads `InputLanguageManager.Current.CurrentInputLanguage.Name` (handles `NullReferenceException` gracefully).
  2. If null or whitespace, reads `CultureInfo.CurrentUICulture.Name`.
  3. If still null or whitespace, defaults to `"en-US"`.