# Technical Documentation: `UiAutomationLang.cs`

**File Path:** `Text-Grab/Models/UiAutomationLang.cs`  
**Namespace:** `Text-Grab.Models`

---

## Overview

The `UiAutomationLang` class represents a custom implementation of the `ILanguage` interface within the `Text-Grab` application. It acts as a specialized pseudo-language model representing "Direct Text (Beta)" (tagged internally as `"Direct-Txt"`). 

It provides metadata properties (such as language tags, display names, and text directions) required by the `ILanguage` contract using fixed, predefined values.

---

## Dependencies & Imports

* **`Text_Grab.Interfaces`**: Provides the `ILanguage` interface contract that `UiAutomationLang` implements.
* **`Windows.Globalization`**: Provides the `LanguageLayoutDirection` enum used to define text layout direction (e.g., Left-to-Right).

---

## Class Signature

```csharp
namespace Text_Grab.Models;

public class UiAutomationLang : ILanguage
```

---

## Constants

| Constant | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `Tag` | `string` | `"Direct-Txt"` | The internal identifier string for this language model. |
| `BetaDisplayName` | `string` | `"Direct Text (Beta)"` | The human-readable display string for this language option. |

---

## Property Implementations (`ILanguage`)

All properties are read-only expression-bodied members that return predetermined static values.

### 1. `AbbreviatedName`
* **Type:** `string`
* **Value:** `"DT"`
* **Description:** Provides a short, two-letter abbreviation representing "Direct Text".

### 2. `DisplayName`
* **Type:** `string`
* **Value:** `BetaDisplayName` (`"Direct Text (Beta)"`)
* **Description:** Gets the display name for the language suitable for UI presentation.

### 3. `CurrentInputMethodLanguageTag`
* **Type:** `string`
* **Value:** `string.Empty`
* **Description:** Returns an empty string as there is no associated input method language tag.

### 4. `CultureDisplayName`
* **Type:** `string`
* **Value:** `BetaDisplayName` (`"Direct Text (Beta)"`)
* **Description:** Returns the culture display name, set identically to `BetaDisplayName`.

### 5. `LanguageTag`
* **Type:** `string`
* **Value:** `Tag` (`"Direct-Txt"`)
* **Description:** Gets the tag string identifying this language representation.

### 6. `LayoutDirection`
* **Type:** `LanguageLayoutDirection`
* **Value:** `LanguageLayoutDirection.Ltr`
* **Description:** Specifies that the text layout direction is Left-to-Right.

### 7. `NativeName`
* **Type:** `string`
* **Value:** `BetaDisplayName` (`"Direct Text (Beta)"`)
* **Description:** Gets the native name of the language, returning the beta display name.

### 8. `Script`
* **Type:** `string`
* **Value:** `string.Empty`
* **Description:** Returns an empty string as no specific writing script is assigned.

---

## How It Works

1. **Interface Conformance:** `UiAutomationLang` implements `ILanguage` to allow system components that process language models to treat "Direct Text" interchangeably with standard language objects.
2. **Fixed Metadata:** Because "Direct Text" is a functional feature represented as a language option rather than a standard spoken language, all properties return constant or expression-bodied values (such as returning `LanguageLayoutDirection.Ltr` or `string.Empty` for culture-specific fields like `Script`).