# GlobalLang Class Documentation

**File Path:** `Text-Grab/Models/GlobalLang.cs`  
**Namespace:** `Text_Grab.Models`  
**Implemented Interface:** `ILanguage`

---

## Overview

The `GlobalLang` class serves as a model wrapper for the UWP/WinRT `Windows.Globalization.Language` object. It implements the `ILanguage` interface to expose language attributes—such as display names, script types, layout direction, and language tags—to the application. It also provides construction logic that handles language tag conversions and offers fallback mechanisms when an invalid language tag is provided.

---

## Properties

| Property Name | Type | Description |
| :--- | :--- | :--- |
| `OriginalLanguage` | `Windows.Globalization.Language` | Gets or sets the underlying `Windows.Globalization.Language` instance. |
| `AbbreviatedName` | `string` | Gets or sets the abbreviated name of the language (e.g., "en"). |
| `CurrentInputMethodLanguageTag` | `string` | Gets or sets the language tag for the current input method. Initialized to `string.Empty` by default. |
| `CultureDisplayName` | `string` | Gets or sets the localized display name of the language culture. |
| `LanguageTag` | `string` | Gets or sets the BCP-47 language tag (e.g., "en-US"). |
| `LayoutDirection` | `Windows.Globalization.LanguageLayoutDirection` | Gets or sets the text layout direction (e.g., left-to-right or right-to-left) of the language. |
| `NativeName` | `string` | Gets or sets the name of the language expressed in its native tongue. |
| `Script` | `string` | Gets or sets the script or writing system used by the language (e.g., "Latn"). |
| `DisplayName` | `string` | Read-only computed property that returns the value of `CultureDisplayName`. |

---

## Constructors

### 1. `GlobalLang(Windows.Globalization.Language lang)`

Initializes a new instance of `GlobalLang` using an existing `Windows.Globalization.Language` object.

```csharp
public GlobalLang(Windows.Globalization.Language lang)
```

#### Behavior
Populates all class properties directly from the provided `lang` instance:
* `AbbreviatedName` $\leftarrow$ `lang.AbbreviatedName`
* `CultureDisplayName` $\leftarrow$ `lang.DisplayName`
* `LanguageTag` $\leftarrow$ `lang.LanguageTag`
* `LayoutDirection` $\leftarrow$ `lang.LayoutDirection`
* `NativeName` $\leftarrow$ `lang.NativeName`
* `Script` $\leftarrow$ `lang.Script`
* `OriginalLanguage` $\leftarrow$ `lang`

---

### 2. `GlobalLang(string inputLangTag)`

Initializes a new instance of `GlobalLang` by parsing a string representation of a language tag.

```csharp
public GlobalLang(string inputLangTag)
```

#### Behavior & Execution Flow
1. **Special Handling for "English":**  
   If `inputLangTag` is equal to `"English"`, it is automatically converted to `"en-US"`.

2. **Initialization with Fallback Support:**  
   * Pre-initializes a fallback `Windows.Globalization.Language` object using the system's current culture (`System.Globalization.CultureInfo.CurrentCulture.Name`).
   * Attempts to construct a `Windows.Globalization.Language` object using `inputLangTag`.

3. **Exception Handling:**  
   * Catch block targets `System.ArgumentException`.
   * If parsing fails, a debug message is written to `System.Diagnostics.Debug.WriteLine`:
     ```
     Failed to initialize language '{inputLangTag}': {ex.Message}
     ```
   * Falls back to constructing `Windows.Globalization.Language` using `System.Globalization.CultureInfo.CurrentCulture.Name`.

4. **Property Assignment:**  
   Assigns `AbbreviatedName`, `CultureDisplayName`, `LanguageTag`, `LayoutDirection`, `NativeName`, `Script`, and `OriginalLanguage` from the resulting `Windows.Globalization.Language` instance.