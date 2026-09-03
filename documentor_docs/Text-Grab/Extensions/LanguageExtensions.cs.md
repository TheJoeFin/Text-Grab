# Technical Documentation: `LanguageExtensions.cs`

## Overview

The `LanguageExtensions.cs` file defines a static C# class (`LanguageExtensions`) within the `Text_Grab` namespace. It provides a set of extension methods for formatting, checking characteristics (such as text layout direction, script type, and word-spacing rules), and converting between Windows native language objects (`Windows.Globalization.Language`) and custom application language interfaces (`Text_Grab.Interfaces.ILanguage`).

---

## File Details

- **File Path:** `Text-Grab/Extensions/LanguageExtensions.cs`
- **Namespace:** `Text_Grab`
- **Class Type:** `public static class LanguageExtensions`

---

## Dependencies

The file relies on the following namespaces:

- `System`: Provides core system classes and exception handling (`Exception`, `StringComparison`).
- `System.Globalization`: Provides culture-related information (`CultureInfo`).
- `System.Windows.Markup`: Provides XML language utility features (`XmlLanguage`).
- `Text_Grab.Interfaces`: Contains the `ILanguage` abstraction.
- `Text_Grab.Models`: Contains concrete language representations (e.g., `GlobalLang`).
- `Windows.Globalization`: Provides the native `Language` class from the Windows SDK.

---

## Method Documentation

### 1. `IsSpaceJoining(this Language selectedLanguage)`

Evaluates whether words in the specified `Windows.Globalization.Language` are typically separated by spaces.

* **Signature:**
  ```csharp
  public static bool IsSpaceJoining(this Language selectedLanguage)
  ```
* **Parameters:**
  * `selectedLanguage` (`Language`): The Windows native language object instance.
* **Return Value:** `bool`
  * Returns `false` if the language tag begins with `"zh"` (Chinese) or equals `"ja"` (Japanese), ignoring case.
  * Returns `true` for all other languages.
* **Logic:**
  1. Checks if `selectedLanguage.LanguageTag` starts with `"zh"` using `StringComparison.InvariantCultureIgnoreCase`.
  2. Checks if `selectedLanguage.LanguageTag` equals `"ja"` using `StringComparison.InvariantCultureIgnoreCase`.
  3. Returns `false` for matching Chinese/Japanese language tags; otherwise, returns `true`.

---

### 2. `IsSpaceJoining(this ILanguage selectedLanguage)`

Evaluates whether words in the specified `ILanguage` instance are typically separated by spaces.

* **Signature:**
  ```csharp
  public static bool IsSpaceJoining(this ILanguage selectedLanguage)
  ```
* **Parameters:**
  * `selectedLanguage` (`ILanguage`): The interface instance representing a language.
* **Return Value:** `bool`
  * Returns `false` if the language tag begins with `"zh"` (Chinese) or equals `"ja"` (Japanese), ignoring case.
  * Returns `true` for all other languages.
* **Logic:**
  Identical logic to the `Language` extension overload: checks `selectedLanguage.LanguageTag` against `"zh"` and `"ja"`.

---

### 3. `IsRightToLeft(this Language language)`

Determines if the text flow for a given `Windows.Globalization.Language` is right-to-left (RTL).

* **Signature:**
  ```csharp
  public static bool IsRightToLeft(this Language language)
  ```
* **Parameters:**
  * `language` (`Language`): The target `Windows.Globalization.Language` instance.
* **Return Value:** `bool`
  * Returns `true` if the underlying culture's text layout direction is Right-to-Left; otherwise, `false`.
* **Logic:**
  1. Resolves an `XmlLanguage` instance via `XmlLanguage.GetLanguage(language.LanguageTag)`.
  2. Obtains the equivalent `CultureInfo` object from the `XmlLanguage` using `GetEquivalentCulture()`.
  3. Returns the value of `culture.TextInfo.IsRightToLeft`.

---

### 4. `IsRightToLeft(this ILanguage selectedLanguage)`

Determines if the text flow for an `ILanguage` implementation is right-to-left (RTL).

* **Signature:**
  ```csharp
  public static bool IsRightToLeft(this ILanguage selectedLanguage)
  ```
* **Parameters:**
  * `selectedLanguage` (`ILanguage`): The interface instance representing a language.
* **Return Value:** `bool`
  * Returns `true` if the language uses RTL orientation; otherwise, `false`.
* **Logic:**
  1. Checks if `selectedLanguage` can be pattern-matched as a `GlobalLang` model.
  2. If it is a `GlobalLang`, calls `OriginalLanguage.IsRightToLeft()` (delegating to the `Language` extension method).
  3. For all other `ILanguage` types, checks if `selectedLanguage.LayoutDirection` is equal to `LanguageLayoutDirection.Rtl`.

---

### 5. `IsLatinBased(this ILanguage selectedLanguage)`

Determines whether the script of an `ILanguage` object is Latin-based.

* **Signature:**
  ```csharp
  public static bool IsLatinBased(this ILanguage selectedLanguage)
  ```
* **Parameters:**
  * `selectedLanguage` (`ILanguage`): The interface instance representing a language.
* **Return Value:** `bool`
  * Returns `true` if the `Script` property equals `"Latn"` (case-insensitive); otherwise, `false`.
* **Logic:**
  Uses `string.Equals` with `StringComparison.OrdinalIgnoreCase` to compare `selectedLanguage.Script` against the string literal `"Latn"`.

---

### 6. `AsLanguage(this ILanguage iLanguage)`

Converts an `ILanguage` object into a `Windows.Globalization.Language` object, if possible.

* **Signature:**
  ```csharp
  public static Language? AsLanguage(this ILanguage iLanguage)
  ```
* **Parameters:**
  * `iLanguage` (`ILanguage`): The `ILanguage` instance to convert.
* **Return Value:** `Language?`
  * Returns the corresponding `Language` instance, or `null` if conversion or construction fails.
* **Logic:**
  1. If `iLanguage` is of type `GlobalLang`, returns its underlying `OriginalLanguage` property.
  2. Retrieves `iLanguage.LanguageTag`.
  3. Attempts to instantiate `new Language(tag)`.
  4. If an `Exception` is thrown during instantiation, catches it and returns `null`.

---

### 7. `AsILanguage(this Language language)`

Converts a `Windows.Globalization.Language` object into an `ILanguage` interface instance by wrapping it in a `GlobalLang` model.

* **Signature:**
  ```csharp
  public static ILanguage? AsILanguage(this Language language)
  ```
* **Parameters:**
  * `language` (`Language`): The native Windows language object to convert.
* **Return Value:** `ILanguage?`
  * Returns `null` if the input `language` is `null`; otherwise, returns a new instance of `GlobalLang(language)`.
* **Logic:**
  1. Checks if `language` is `null`. If so, returns `null`.
  2. Constructs and returns a new `GlobalLang` wrapper passing `language` to the constructor.