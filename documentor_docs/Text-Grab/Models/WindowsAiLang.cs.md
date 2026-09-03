# Technical Documentation: `WindowsAiLang.cs`

## Overview

The `WindowsAiLang` class is part of the `Text_Grab.Models` namespace. It implements the `ILanguage` interface to represent "Windows AI OCR" as a pseudo-language or engine option within the Text-Grab application language model framework.

By implementing `ILanguage`, this class allows the application to handle Windows AI OCR seamlessly alongside traditional system/globalization languages, supplying read-only properties for display labels, tags, and layout preferences.

---

## Class Metadata

* **Namespace**: `Text_Grab.Models`
* **File Location**: `Text-Grab/Models/WindowsAiLang.cs`
* **Implemented Interfaces**: `ILanguage` (from `Text_Grab.Interfaces`)
* **Dependencies**: `Windows.Globalization`

---

## Key Components & Properties

All properties in `WindowsAiLang` are public read-only properties implemented with expression-bodied members (`=>`).

| Property | Type | Value / Return | Description |
| :--- | :--- | :--- | :--- |
| `AbbreviatedName` | `string` | `"WinAI"` | Shortened name/abbreviation for the Windows AI language entry. |
| `DisplayName` | `string` | `"Windows AI OCR"` | User-facing display name. |
| `CurrentInputMethodLanguageTag` | `string` | `string.Empty` | Returns an empty string indicating no specific input method language tag. |
| `CultureDisplayName` | `string` | `"Windows AI OCR"` | Culture display name string. |
| `LanguageTag` | `string` | `"WinAI"` | Identifier tag used for language selection/matching. |
| `LayoutDirection` | `LanguageLayoutDirection` | `LanguageLayoutDirection.Ltr` | Layout direction set to Left-to-Right using the `Windows.Globalization.LanguageLayoutDirection` enum. |
| `NativeName` | `string` | `"Windows AI OCR"` | Native display name for the language object. |
| `Script` | `string` | `string.Empty` | Returns an empty string indicating no specific script identifier. |

---

## How It Works

1. **Interface Compliance**: `WindowsAiLang` implements the `ILanguage` contract defined in `Text_Grab.Interfaces`. This allows any code operating on `ILanguage` instances to query metadata about the Windows AI OCR option.
2. **Static Metadata**: The class returns predefined hardcoded constants for text labels (`"WinAI"`, `"Windows AI OCR"`), uses `LanguageLayoutDirection.Ltr` for layout direction, and returns empty strings for fields that do not apply to Windows AI OCR (such as `CurrentInputMethodLanguageTag` and `Script`).