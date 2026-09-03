# Documentation Guide: `Text-Grab/Utilities/LanguageUtilities.cs`

## Overview

The `LanguageUtilities` class is a `public static` utility class located within the `Text-Grab.Utilities` namespace. It serves as a facade and helper interface for language-related operations across the Text-Grab application. 

Instead of implementing business logic directly, `LanguageUtilities` delegates its calls to the `LanguageService`—either through static methods or via the `Singleton<LanguageService>.Instance` pattern. This design enables central caching of language operations to minimize memory allocations and improve execution efficiency.

---

## Class Information

- **Namespace:** `Text-Grab.Utilities`
- **Class Name:** `LanguageUtilities`
- **Modifiers:** `public static`
- **Dependencies / Imports:**
  - `System.Collections.Generic`
  - `Text-Grab.Interfaces`
  - `Text-Grab.Models`
  - `Text-Grab.Services`

---

## Architecture & Design Pattern

`LanguageUtilities` follows the **Facade Pattern**. It simplifies access to language routines by wrapping method calls to `LanguageService`:

1. **Singleton Instance Delegation:** Methods requiring state or cached lookup call `Singleton<LanguageService>.Instance.<Method>()`.
2. **Static Delegation:** Stateless/utility methods pass parameters directly to static methods on `LanguageService`.

---

## Method Reference

### Language Retrieval Methods

#### `GetCurrentInputLanguage()`
* **Signature:** `public static ILanguage GetCurrentInputLanguage()`
* **Description:** Retrieves the current system input language.
* **Return Value:** `ILanguage` object representing the active input language.
* **Delegation:** `Singleton<LanguageService>.Instance.GetCurrentInputLanguage()`

#### `GetAllLanguages()`
* **Signature:** `public static IList<ILanguage> GetAllLanguages()`
* **Description:** Obtains a collection of all available OCR languages using cached values from the service.
* **Return Value:** `IList<ILanguage>` containing available language instances.
* **Delegation:** `Singleton<LanguageService>.Instance.GetAllLanguages()`

#### `GetOCRLanguage()`
* **Signature:** `public static ILanguage GetOCRLanguage()`
* **Description:** Determines the appropriate OCR language based on application settings and available system languages. Utilizes cached values when settings remain unchanged.
* **Return Value:** `ILanguage` representing the active OCR language.
* **Delegation:** `Singleton<LanguageService>.Instance.GetOCRLanguage()`

#### `GetSystemLanguageForTranslation()`
* **Signature:** `public static string GetSystemLanguageForTranslation()`
* **Description:** Retrieves the system language name formatted for Windows AI translation (e.g., `"English"`, `"Spanish"`).
* **Return Value:** `string` language name. Defaults to `"English"` if unable to determine.
* **Delegation:** `Singleton<LanguageService>.Instance.GetSystemLanguageForTranslation()`

---

### Language Identity and Inspection Methods

#### `GetLanguageTag(object language)`
* **Signature:** `public static string GetLanguageTag(object language)`
* **Parameters:**
  - `language` (`object`): The language object to inspect.
* **Description:** Extracts the language tag (e.g., BCP-47 tag) from a given language object.
* **Return Value:** `string` representing the language tag.
* **Delegation:** `LanguageService.GetLanguageTag(language)`

#### `GetLanguageKind(object language)`
* **Signature:** `public static LanguageKind GetLanguageKind(object language)`
* **Parameters:**
  - `language` (`object`): The language object to evaluate.
* **Description:** Identifies the specific `LanguageKind` enum type of the provided language object.
* **Return Value:** `LanguageKind` enum value.
* **Delegation:** `LanguageService.GetLanguageKind(language)`

#### `GetPersistedLanguageIdentity(object language)`
* **Signature:** `public static (string LanguageTag, LanguageKind LanguageKind, bool UsedUiAutomation) GetPersistedLanguageIdentity(object language)`
* **Parameters:**
  - `language` (`object`): The language instance to evaluate.
* **Description:** Returns the persisted identity information for a given language object as a tuple.
* **Return Value:** A tuple containing:
  - `LanguageTag` (`string`)
  - `LanguageKind` (`LanguageKind`)
  - `UsedUiAutomation` (`bool`)
* **Delegation:** `LanguageService.GetPersistedLanguageIdentity(language)`

#### `NormalizePersistedLanguageIdentity(LanguageKind languageKind, string languageTag, bool usedUiAutomation = false)`
* **Signature:** `public static (string LanguageTag, LanguageKind LanguageKind, bool UsedUiAutomation) NormalizePersistedLanguageIdentity(LanguageKind languageKind, string languageTag, bool usedUiAutomation = false)`
* **Parameters:**
  - `languageKind` (`LanguageKind`): The kind of language.
  - `languageTag` (`string`): The associated language string tag.
  - `usedUiAutomation` (`bool`, optional): Flag indicating UI Automation usage (default: `false`).
* **Description:** Normalizes language identity parameters into a standardized tuple format suitable for persistence.
* **Return Value:** Normalized tuple containing `(LanguageTag, LanguageKind, UsedUiAutomation)`.
* **Delegation:** `LanguageService.NormalizePersistedLanguageIdentity(languageKind, languageTag, usedUiAutomation)`

#### `IsCurrentLanguageLatinBased()`
* **Signature:** `public static bool IsCurrentLanguageLatinBased()`
* **Description:** Checks whether the current input language uses a Latin-based script.
* **Return Value:** `bool` (`true` if Latin-based; otherwise `false`).
* **Delegation:** `Singleton<LanguageService>.Instance.IsCurrentLanguageLatinBased()`

---

### Cache Invalidation Methods

#### `InvalidateLanguagesCache()`
* **Signature:** `public static void InvalidateLanguagesCache()`
* **Description:** Clears the cached list of available languages. Should be invoked when new languages are installed on the system.
* **Delegation:** `Singleton<LanguageService>.Instance.InvalidateLanguagesCache()`

#### `InvalidateOcrLanguageCache()`
* **Signature:** `public static void InvalidateOcrLanguageCache()`
* **Description:** Clears the OCR-specific language cache. Should be invoked when settings such as `LastUsedLang` change.
* **Delegation:** `Singleton<LanguageService>.Instance.InvalidateOcrLanguageCache()`

#### `InvalidateAllCaches()`
* **Signature:** `public static void InvalidateAllCaches()`
* **Description:** Flushes all cached language data maintained by the service. Should be invoked when system input language changes occur.
* **Delegation:** `Singleton<LanguageService>.Instance.InvalidateAllCaches()`