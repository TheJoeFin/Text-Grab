# Technical Documentation: `CaptureLanguageUtilities.cs`

## Overview

The `CaptureLanguageUtilities` static class in the `Text_Grab.Utilities` namespace provides helper methods to query, match, persist, and evaluate available text capture languages and engines in Text-Grab. It acts as a central utility for handling language selection, identifying engine features (e.g., UI Automation, Windows AI, Tesseract, and Windows OCR), and evaluating capture compatibility features (such as table output support or static image compatibility).

---

## Class Information

- **Namespace:** `Text_Grab.Utilities`
- **Class Name:** `CaptureLanguageUtilities`
- **Type:** `internal static class`

---

## Key Dependencies & References

- **System Libraries:** `System`, `System.Collections.Generic`, `System.Linq`, `System.Threading.Tasks`
- **Windows APIs:** `Windows.Media.Ocr` (`OcrEngine`), `Windows.Globalization.Language`
- **Internal Interfaces:** `Text_Grab.Interfaces.ILanguage`
- **Internal Models / Language Types:** `UiAutomationLang`, `WindowsAiLang`, `WindowsAiDescriptionLang`, `GlobalLang`, `TessLang`
- **Internal Utilities:** `AppUtilities`, `WindowsAiUtilities`, `TesseractHelper`, `LanguageUtilities`

---

## Methods Documentation

### 1. `GetCaptureLanguagesAsync`

```csharp
public static async Task<List<ILanguage>> GetCaptureLanguagesAsync(bool includeTesseract)
```

#### Purpose
Asynchronously aggregates and returns a list of all currently available capture languages/engines based on application settings and hardware capabilities.

#### Logic Flow
1. Initializes an empty list `List<ILanguage> languages`.
2. Checks if UI Automation is enabled via `AppUtilities.TextGrabSettings.UiAutomationEnabled`. If `true`, adds `new UiAutomationLang()`.
3. Checks if the device supports Windows AI via `WindowsAiUtilities.CanDeviceUseWinAI()`. If `true`, adds `new WindowsAiLang()`.
4. Checks if Windows AI image description is enabled (`AppUtilities.TextGrabSettings.WindowsAiDescriptionEnabled`) and supported (`WindowsAiUtilities.CanDeviceDescribeImagesWithWinAI()`). If both are `true`, adds `new WindowsAiDescriptionLang()`.
5. If `includeTesseract` is `true`, Tesseract is enabled in settings (`AppUtilities.TextGrabSettings.UseTesseract`), and the executable exists (`TesseractHelper.CanLocateTesseractExe()`), it fetches and adds available Tesseract languages via `await TesseractHelper.TesseractLanguages()`.
6. Iterates through system OCR languages available via `OcrEngine.AvailableRecognizerLanguages` and wraps each in a `GlobalLang` object, adding them to the list.
7. Returns the compiled list of `ILanguage` items.

#### Parameters
- `includeTesseract` (`bool`): Determines whether to check for and include Tesseract OCR languages.

#### Return Value
- `Task<List<ILanguage>>`: A task returning the list of available `ILanguage` implementations.

---

### 2. `MatchesPersistedLanguage`

```csharp
public static bool MatchesPersistedLanguage(ILanguage language, string persistedLanguage)
```

#### Purpose
Checks whether a given `ILanguage` object matches a saved/persisted language string value.

#### Logic Flow
1. Returns `false` immediately if `persistedLanguage` is `null`, empty, or whitespace.
2. Compares `persistedLanguage` against the following properties using `StringComparison.CurrentCultureIgnoreCase`:
   - `language.LanguageTag`
   - `language.CultureDisplayName`
   - `language.DisplayName`
3. Returns `true` if any of these properties match `persistedLanguage`.

#### Parameters
- `language` (`ILanguage`): The language instance to evaluate.
- `persistedLanguage` (`string`): The saved language string to compare against.

#### Return Value
- `bool`: `true` if a match is found; otherwise, `false`.

---

### 3. `FindPreferredLanguageIndex`

```csharp
public static int FindPreferredLanguageIndex(
    IReadOnlyList<ILanguage> languages, 
    string persistedLanguage, 
    ILanguage fallbackLanguage)
```

#### Purpose
Determines the index of the most suitable language within a provided list based on a persisted setting, a fallback language, or list default.

#### Logic Flow
1. Iterates through `languages` and uses `MatchesPersistedLanguage` to check if any item matches `persistedLanguage`. If found, returns its index.
2. If no persisted match is found, iterates through `languages` and compares each item's `LanguageTag` against `fallbackLanguage.LanguageTag` using case-insensitive matching (`StringComparison.CurrentCultureIgnoreCase`). If found, returns its index.
3. If neither matches:
   - Returns `0` if `languages` contains one or more elements.
   - Returns `-1` if `languages` is empty.

#### Parameters
- `languages` (`IReadOnlyList<ILanguage>`): The list of available languages to search.
- `persistedLanguage` (`string`): The preferred saved language identifier.
- `fallbackLanguage` (`ILanguage`): The language to fall back to if the persisted setting yields no match.

#### Return Value
- `int`: Zero-based index of the preferred language, or `-1` if the list is empty.

---

### 4. `PersistSelectedLanguage`

```csharp
public static void PersistSelectedLanguage(ILanguage language)
```

#### Purpose
Saves the selected language tag to application settings and invalidates the cached OCR language configuration.

#### Logic Flow
1. Sets `AppUtilities.TextGrabSettings.LastUsedLang` to `language.LanguageTag`.
2. Calls `AppUtilities.TextGrabSettings.Save()` to persist changes.
3. Calls `LanguageUtilities.InvalidateOcrLanguageCache()` to clear any existing cached OCR language instances.

#### Parameters
- `language` (`ILanguage`): The language instance selected by the user.

---

### 5. `GetUiAutomationFallbackLanguage`

```csharp
public static ILanguage GetUiAutomationFallbackLanguage()
```

#### Purpose
Retrieves a fallback `ILanguage` object based on the system's current input language.

#### Logic Flow
1. Obtains the current input language via `LanguageUtilities.GetCurrentInputLanguage()`.
2. Casts `currentInputLanguage` to `GlobalLang`. If the cast yields `null`, creates and returns a `new GlobalLang(currentInputLanguage.LanguageTag)`.

#### Return Value
- `ILanguage`: A `GlobalLang` instance corresponding to the current input language.

---

### 6. `SupportsTableOutput`

```csharp
public static bool SupportsTableOutput(ILanguage language)
```

#### Purpose
Determines if a given language or engine supports table output formatting.

#### Logic Flow
Returns `true` unless the language is one of the following non-supporting types:
- `TessLang`
- `UiAutomationLang`
- `WindowsAiDescriptionLang`

#### Parameters
- `language` (`ILanguage`): The language or engine to check.

#### Return Value
- `bool`: `true` if the language supports table output; otherwise, `false`.

---

### 7. `IsStaticImageCompatible`

```csharp
public static bool IsStaticImageCompatible(ILanguage language)
```

#### Purpose
Checks if the language/engine can perform text capture on a static image source.

#### Logic Flow
Returns `true` if `language` is not `UiAutomationLang`.

#### Parameters
- `language` (`ILanguage`): The language/engine instance.

#### Return Value
- `bool`: `true` if compatible with static images; `false` if `language` is `UiAutomationLang`.

---

### 8. `RequiresLiveUiAutomationSource`

```csharp
public static bool RequiresLiveUiAutomationSource(
    ILanguage language, 
    bool isStaticImageSource, 
    bool hasFrozenUiAutomationSnapshot)
```

#### Purpose
Evaluates whether a text capture operation requires a live UI Automation source instead of a static image capture.

#### Logic Flow
Returns `true` if **all** of the following conditions are met:
1. `language` is `UiAutomationLang`.
2. `isStaticImageSource` is `true`.
3. `hasFrozenUiAutomationSnapshot` is `false`.

#### Parameters
- `language` (`ILanguage`): The current language/engine.
- `isStaticImageSource` (`bool`): Indicates if the current target is a static image source.
- `hasFrozenUiAutomationSnapshot` (`bool`): Indicates if a frozen UI Automation snapshot is available.

#### Return Value
- `bool`: `true` if a live UI Automation source is required; otherwise, `false`.