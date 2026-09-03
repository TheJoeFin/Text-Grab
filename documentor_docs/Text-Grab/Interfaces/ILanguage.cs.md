# Technical Documentation: `Text-Grab/Interfaces/ILanguage.cs`

## Overview

The `ILanguage.cs` file defines the `ILanguage` interface within the `Text_Grab.Interfaces` namespace. This interface provides a contract for language metadata, globalization attributes, and related language manipulation methods within the Text-Grab application. 

It imports `Windows.Globalization` to utilize Windows platform globalization types (specifically `LanguageLayoutDirection`).

---

## File Details

* **File Path:** `Text-Grab/Interfaces/ILanguage.cs`
* **Namespace:** `Text_Grab.Interfaces`
* **Dependencies:**
  * `System.Collections.Generic`
  * `Windows.Globalization`

---

## Interface Definition: `ILanguage`

The `ILanguage` interface defines instance properties, default instance methods, and static interface methods related to language tags, display names, and text layout direction.

### Instance Properties

All properties are read-only (get-only) strings or enums that represent metadata about a specific language.

| Property | Type | Access | Description |
| :--- | :--- | :--- | :--- |
| `AbbreviatedName` | `string` | `{ get; }` | Represents the short/abbreviated form of the language name. |
| `CurrentInputMethodLanguageTag` | `string` | `{ get; }` | Gets the language tag representing the active input method for the language. |
| `CultureDisplayName` | `string` | `{ get; }` | Gets the display name of the language as formatted by the culture settings. |
| `LanguageTag` | `string` | `{ get; }` | Gets the standardized language tag (e.g., BCP-47 identifier). |
| `DisplayName` | `string` | `{ get; }` | Gets the human-readable display name of the language. |
| `LayoutDirection` | `LanguageLayoutDirection` | `{ get; }` | Gets the text reading and layout direction (e.g., LeftToRight, RightToLeft) using `Windows.Globalization.LanguageLayoutDirection`. |
| `NativeName` | `string` | `{ get; }` | Gets the language name as written in the native language itself. |
| `Script` | `string` | `{ get; }` | Gets the script/writing system associated with the language. |

---

### Methods

The interface provides default implementations for both instance and static methods.

#### 1. `TrySetInputMethodLanguageTag` (Static Method)
```csharp
public static bool TrySetInputMethodLanguageTag(string languageTag)
```
* **Parameters:**
  * `languageTag` (`string`): The target language tag string to set for the input method.
* **Return Value:** `bool` — Returns `false` by default.
* **Description:** A static interface method designed to attempt setting the current input method language tag.

#### 2. `IsWellFormed` (Static Method)
```csharp
public static bool IsWellFormed(string languageTag)
```
* **Parameters:**
  * `languageTag` (`string`): The language tag string to validate.
* **Return Value:** `bool` — Returns `true` by default.
* **Description:** A static interface method designed to check whether a given language tag string is well-formed.

#### 3. `GetMuiCompatibleLanguageListFromLanguageTags` (Static Method)
```csharp
public static IList<string> GetMuiCompatibleLanguageListFromLanguageTags(IEnumerable<string> languageTags)
```
* **Parameters:**
  * `languageTags` (`IEnumerable<string>`): A collection of language tag strings.
* **Return Value:** `IList<string>` — Returns an empty list (`[]`) by default.
* **Description:** A static interface method intended to convert or extract a list of MUI (Multilingual User Interface)-compatible language tags from an enumerable collection of language tags.

#### 4. `GetExtensionSubTags` (Instance Method)
```csharp
public IReadOnlyList<string> GetExtensionSubTags(string singleton)
```
* **Parameters:**
  * `singleton` (`string`): The extension singleton identifier.
* **Return Value:** `IReadOnlyList<string>` — Returns an empty list (`[]`) by default.
* **Description:** An instance method that retrieves extension sub-tags associated with a given singleton identifier.