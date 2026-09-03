# Documentation: `Text-Grab/Models/TessLang.cs`

## Overview

The `TessLang` class implements the `ILanguage` interface and serves as a model for representing Tesseract OCR language tags within the application. It parses Tesseract-specific language codes (such as vertical orientation variants, script tags, or specialized subsets like Fraktur and Old scripts) and maps them to standard .NET `CultureInfo` objects and Windows Globalization parameters.

---

## Class Declaration

```csharp
namespace Text_Grab.Models;

public class TessLang : ILanguage
```

* **Namespace:** `Text_Grab.Models`
* **Interfaces Implemented:** `ILanguage`

---

## Fields

| Field | Type | Description |
| :--- | :--- | :--- |
| `_tessLangTag` | `string` | Stores the primary Tesseract language tag passed during initialization. |
| `cultureInfo` | `CultureInfo` | The .NET `CultureInfo` object mapped from the Tesseract tag. |
| `isScript` | `bool` | Indicates whether the language tag represents a script variant (contains `"script"`). Defaults to `false`. |

---

## Constructors

### `TessLang(string tessLangTag)`

Initializes a new instance of `TessLang` by processing a raw Tesseract language tag string.

#### Workflow:
1. Assigns `RawTag = tessLangTag`.
2. Checks if `tessLangTag` contains `"script"`. If true, sets `isScript = true` and strips `"script\"` from the tag copy.
3. Checks if `tessLangTag` contains `"vert"`. If true, sets `IsVertical = true` and strips `"_vert"` from the tag copy.
4. Calls `GetCultureInfoFromTesseractTag()` using the cleaned tag copy to resolve the internal `cultureInfo` instance.
5. Saves `tessLangTag` into `_tessLangTag`.

---

## Properties

| Property | Type | Accessors | Description |
| :--- | :--- | :--- | :--- |
| `AbbreviatedName` | `string` | `get` | Returns `_tessLangTag`. |
| `IsVertical` | `bool` | `get`, `set` | Indicates whether the language is rendered/processed vertically. Defaults to `false`. |
| `CurrentInputMethodLanguageTag` | `string` | `get` | Returns an empty string (`string.Empty`). |
| `CultureDisplayName` | `string` | `get` | Formats the human-readable display name, adding descriptors for specific variants (e.g., Fraktur, Old, Latin, Vertical, Script). |
| `DisplayName` | `string` | `get` | Returns the formatted display name appended with `" with Tesseract"` (e.g., `"{CultureDisplayName} with Tesseract"`). |
| `LayoutDirection` | `Windows.Globalization.LanguageLayoutDirection` | `get` | Returns `TtbRtl` (Top-to-Bottom, Right-to-Left) if `_tessLangTag` contains `"vert"`, otherwise returns `Rtl` (Right-to-Left). |
| `NativeName` | `string` | `get` | Returns the native language name from `cultureInfo.NativeName`. |
| `Script` | `string` | `get` | Retrieves the script name using `Windows.Globalization.Language` instantiated with `cultureInfo.IetfLanguageTag`. |
| `LanguageTag` | `string` | `get` | Returns `RawTag`. |
| `RawTag` | `string` | `get`, `set` | Gets or sets the original raw language tag. Defaults to `string.Empty`. |

### `CultureDisplayName` Logic Details

The `CultureDisplayName` getter checks `_tessLangTag` against explicit variant patterns:
* `"dan_frak"` $\rightarrow$ `"{DisplayName} (Fraktur)"`
* `"deu_frak"` $\rightarrow$ `"{DisplayName} (Fraktur)"`
* `"ita_old"` $\rightarrow$ `"{DisplayName} (Old)"`
* `"kat_old"` $\rightarrow$ `"{DisplayName} (Old)"`
* `"slk_frak"` $\rightarrow$ `"{DisplayName} (Fraktur)"`
* `"spa_old"` $\rightarrow$ `"{DisplayName} (Old)"`
* `"srp_latn"` $\rightarrow$ `"{DisplayName} (Latin)"`

If none of the above match:
* If `IsVertical` is `true` $\rightarrow$ `"{DisplayName} Vertical"`
* If `isScript` is `true` $\rightarrow$ `"{DisplayName} Script"`
* Default $\rightarrow$ `"{DisplayName}"`

---

## Private Methods

### `GetCultureInfoFromTesseractTag(string tessLangTag)`

```csharp
private CultureInfo GetCultureInfoFromTesseractTag(string tessLangTag)
```

Maps a Tesseract-formatted language tag string into a standard .NET `CultureInfo` object.

#### Logic:
1. Strips variant suffixes `"_frak"`, `"_old"`, and `"_latn"` from `tessLangTag`.
2. If `isScript` is `true`, calls `getTagFromEnglishName()` to resolve the language tag.
3. Attempts to instantiate `CultureInfo`:
   * Matches explicit Chinese script variants:
     * `"chi_sim"` $\rightarrow$ `new CultureInfo("zh-Hans")`
     * `"chi_tra"` $\rightarrow$ `new CultureInfo("zh-Hant")`
   * Default case $\rightarrow$ `new CultureInfo(tessLangTag)`
4. If an exception occurs during `CultureInfo` creation, it falls back to looking up the tag via `getTagFromEnglishName(tessLangTag)` and returns a `CultureInfo` object based on that tag.

---

### `getTagFromEnglishName(string EnglishName)`

```csharp
private string getTagFromEnglishName(string EnglishName)
```

Searches available system neutral cultures to find an IETF language tag matching a given English culture name.

#### Logic:
1. Iterates through all neutral cultures obtained via `CultureInfo.GetCultures(CultureTypes.NeutralCultures)`.
2. Performs a case-insensitive, culture-invariant string comparison between `info.EnglishName` and `EnglishName`.
3. Returns `info.IetfLanguageTag` if a match is found.
4. Returns `string.Empty` if no match is found.