# Technical Documentation: `Tests/LanguageTests.cs`

## Overview

The `LanguageTests.cs` file contains a unit test suite within the `Tests` namespace. Its primary purpose is to validate language parsing, display name mapping, and Latin-script detection logic across various language models in the Text Grab application—specifically `GlobalLang`, `TessLang`, `WindowsAiLang`, and the standard .NET `CultureInfo`.

The tests are written using the xUnit testing framework (`[Theory]`, `[Fact]`, `[InlineData]`, `Assert`).

---

## Dependencies & Imports

- **`System.Globalization`**: Provides the `CultureInfo` class used for standard .NET language tag parsing.
- **`Text_Grab`**: Top-level application namespace.
- **`Text_Grab.Models`**: Contains the language domain models being tested (`GlobalLang`, `TessLang`, `WindowsAiLang`).

---

## Tested Components & Interfaces

The test suite exercises the following types and members:

1. **`System.Globalization.CultureInfo`**
   - Constructor: `CultureInfo(string name)`
2. **`Text_Grab.Models.TessLang`**
   - Constructor: `TessLang(string languageTag)`
   - Property: `CultureDisplayName` (string)
   - Method: `IsLatinBased()` (returns `bool`)
3. **`Text_Grab.Models.GlobalLang`**
   - Constructor: `GlobalLang(string languageTag)`
   - Method: `IsLatinBased()` (returns `bool`)
4. **`Text_Grab.Models.WindowsAiLang`**
   - Constructor: `WindowsAiLang()`
   - Method: `IsLatinBased()` (returns `bool`)

---

## Test Methods Summary

### 1. `CanParseEveryLanguageTag(string langTag)`
- **Type**: Data-driven test (`[Theory]`)
- **Data Sets**:
  - `"zh-Hant"` (Chinese Traditional)
  - `"zh-Hans"` (Chinese Simplified)
- **Purpose**: Verifies that standard BCP-47 language tags for Chinese scripts can be instantiated as valid `.NET` `CultureInfo` objects without throwing exceptions.
- **Assertion**: `Assert.NotNull(culture)`

---

### 2. `CanParseChineseLanguageTag(string langTag, string expectedDisplayName)`
- **Type**: Data-driven test (`[Theory]`)
- **Data Sets**:
  | Input `langTag` | Expected `CultureDisplayName` |
  | :--- | :--- |
  | `"chi_sim"` | `"Chinese (Simplified)"` |
  | `"chi_tra"` | `"Chinese (Traditional)"` |
  | `"chi_sim_vert"` | `"Chinese (Simplified) Vertical"` |
  | `"chi_tra_vert"` | `"Chinese (Traditional) Vertical"` |
- **Purpose**: Validates that Tesseract-formatted language tags (`chi_sim`, `chi_tra`, and their vertical orientation variants) correctly resolve to their expected human-readable display names when wrapped in a `TessLang` model.
- **Assertion**: `Assert.Equal(expectedDisplayName, tessLang.CultureDisplayName)`

---

### 3. `IsLatinBased_WithLatinLanguages_ReturnsTrue(string languageTag)`
- **Type**: Data-driven test (`[Theory]`)
- **Data Sets**: `"en-US"`, `"es-ES"`, `"fr-FR"`, `"it-IT"`, `"ro-RO"`, `"pt-BR"`, `"de-DE"`, `"nl-NL"`
- **Purpose**: Verifies that standard standard Latin-script language tags evaluated via both `GlobalLang` and `TessLang` return `true` when calling `IsLatinBased()`.
- **Assertions**: 
  - `Assert.True(language.IsLatinBased())`
  - `Assert.True(tessLang.IsLatinBased())`

---

### 4. `IsLatinBased_WithTesseractLanguages_UsesResolvedScript(string languageTag, bool expected)`
- **Type**: Data-driven test (`[Theory]`)
- **Data Sets**:
  | Input `languageTag` | Expected Result |
  | :--- | :--- |
  | `"deu"` (German) | `true` |
  | `"nld"` (Dutch) | `true` |
  | `"rus"` (Russian) | `false` |
  | `"ara"` (Arabic) | `false` |
- **Purpose**: Validates script resolution for 3-letter Tesseract language codes using `TessLang.IsLatinBased()`.
- **Assertion**: `Assert.Equal(expected, new TessLang(languageTag).IsLatinBased())`

---

### 5. `IsLatinBased_WithNonLatinLanguages_ReturnsFalse(string languageTag)`
- **Type**: Data-driven test (`[Theory]`)
- **Data Sets**: `"zh-CN"`, `"ja-JP"`, `"ar-SA"`, `"ru-RU"`, `"hi-IN"`
- **Purpose**: Ensures that non-Latin language tags (Chinese, Japanese, Arabic, Russian, Hindi) evaluated by `GlobalLang.IsLatinBased()` correctly evaluate to `false`.
- **Assertion**: `Assert.False(result)`

---

### 6. `IsLatinBased_WithLatinLanguageVariants_ReturnsTrue(string languageTag)`
- **Type**: Data-driven test (`[Theory]`)
- **Data Sets**: `"en-GB"`, `"en-CA"`, `"es-MX"`, `"fr-CA"`, `"pt-PT"`
- **Purpose**: Confirms that regional variants of Latin-script languages evaluated by `GlobalLang.IsLatinBased()` return `true`.
- **Assertion**: `Assert.True(result)`

---

### 7. `IsLatinBased_WithMixedCaseLanguageTag_WorksCorrectly()`
- **Type**: Single test case (`[Fact]`)
- **Input**: `"En-us"`
- **Purpose**: Ensures that case variations in language tags (e.g., non-canonical casing like `"En-us"`) do not break Latin script detection logic in `GlobalLang`.
- **Assertion**: `Assert.True(result)`

---

### 8. `IsLatinBased_WithWindowsAiLang_ReturnsFalse()`
- **Type**: Single test case (`[Fact]`)
- **Purpose**: Tests the default initialization behavior of `WindowsAiLang` to verify its `IsLatinBased()` implementation returns `false`.
- **Assertion**: `Assert.False(result)`