# Technical Documentation: `StringBuilderExtensions.cs`

## Overview

The `StringBuilderExtensions` static class provides a set of C# extension methods for `System.Text.StringBuilder`. These methods facilitate common text processing operations within the `Text-Grab` application, including:
- In-place dictionary-based character replacement.
- End-of-string trailing newline cleanup.
- Standardizing Greek/Cyrillic characters to Latin equivalents.
- Fixing OCR-related digit and letter substitution errors.
- Word-level string reordering for Right-to-Left (RTL) reading flows.

---

## File Details

* **File Path:** `Text-Grab/Extensions/StringBuilderExtensions.cs`
* **Namespace:** `Text_Grab`
* **Class Visibility:** `public static`
* **External Dependencies:**
  * `System`
  * `System.Collections.Generic`
  * `System.Linq`
  * `System.Text`
  * `System.Text.RegularExpressions`
  * `Text_Grab.Utilities` (specifically referencing `StringMethods`)

---

## Method Documentation

### 1. `CharDictionaryReplace`

```csharp
public static void CharDictionaryReplace(this StringBuilder stringBuilder, Dictionary<char, char> charDictionary)
```

* **Purpose:** Performs in-place replacement of characters in the `StringBuilder` based on a mapping dictionary.
* **Parameters:**
  * `stringBuilder` (`StringBuilder`): The target `StringBuilder` instance to modify.
  * `charDictionary` (`Dictionary<char, char>`): A dictionary where keys represent characters to be replaced, and values represent their replacements.
* **Return Value:** `void`
* **How It Works:**
  Iterates through each key in `charDictionary.Keys` and calls `stringBuilder.Replace(key, charDictionary[key])`.

---

### 2. `RemoveTrailingNewlines`

```csharp
public static void RemoveTrailingNewlines(this StringBuilder text)
```

* **Purpose:** Trims trailing carriage return (`\r`) and line feed (`\n`) characters from the end of the `StringBuilder`.
* **Parameters:**
  * `text` (`StringBuilder`): The target `StringBuilder` instance to clean up.
* **Return Value:** `void`
* **How It Works:**
  Executes a `while` loop that checks if `text.Length > 0` and whether the last character (`text[^1]`) is either `\n` or `\r`. If true, it decrements `text.Length` by 1 until no trailing newline characters remain.

---

### 3. `ReplaceGreekOrCyrillicWithLatin`

```csharp
public static void ReplaceGreekOrCyrillicWithLatin(this StringBuilder stringBuilder)
```

* **Purpose:** Replaces Greek or Cyrillic characters with matching Latin equivalents.
* **Parameters:**
  * `stringBuilder` (`StringBuilder`): The `StringBuilder` instance to process.
* **Return Value:** `void`
* **How It Works:**
  Delegates execution to `CharDictionaryReplace`, passing `StringMethods.GreekCyrillicLatinMap` as the mapping dictionary.

---

### 4. `TryFixToLetters`

```csharp
public static void TryFixToLetters(this StringBuilder stringBuilder)
```

* **Purpose:** Converts number characters that resemble letters back to their corresponding letter forms.
* **Parameters:**
  * `stringBuilder` (`StringBuilder`): The `StringBuilder` instance to process.
* **Return Value:** `void`
* **How It Works:**
  Delegates execution to `CharDictionaryReplace`, passing `StringMethods.NumbersToLetters` as the mapping dictionary.

---

### 5. `TryToFixToNumbers`

```csharp
public static void TryToFixToNumbers(this StringBuilder stringBuilder)
```

* **Purpose:** Converts letter characters that resemble numbers back to their corresponding numeric forms.
* **Parameters:**
  * `stringBuilder` (`StringBuilder`): The `StringBuilder` instance to process.
* **Return Value:** `void`
* **How It Works:**
  Delegates execution to `CharDictionaryReplace`, passing `StringMethods.LettersToNumbers` as the mapping dictionary.

---

### 6. `TryFixEveryWordLetterNumberErrors`

```csharp
public static string TryFixEveryWordLetterNumberErrors(this StringBuilder stringToFix)
```

* **Purpose:** Evaluates individual space-separated words to correct mixed character/digit recognition errors and cleans up formatting spaces around tab and newline characters.
* **Parameters:**
  * `stringToFix` (`StringBuilder`): The source `StringBuilder` containing text to fix.
* **Return Value:** `string` — A new string with corrected words and standardized spacing.
* **How It Works:**
  1. Converts `stringToFix` to a string and splits it by space characters (`' '`).
  2. Iterates over each word and applies the `TryFixNumberLetterErrors()` string extension method.
  3. Joins the resulting array of fixed words back into a single string using space delimiters.
  4. Removes extra spaces appended after escape sequences (`"\t "`, `"\r "`, `"\n "`) by replacing them with `"\t"`, `"\r"`, and `"\n"`.
  5. Returns the trimmed result (`joinedString.Trim()`).

---

### 7. `ReverseWordsForRightToLeft`

```csharp
public static void ReverseWordsForRightToLeft(this StringBuilder text)
```

* **Purpose:** Reverses the word order on a line-by-line basis to assist in processing Right-to-Left (RTL) text layouts.
* **Parameters:**
  * `text` (`StringBuilder`): The target `StringBuilder` instance. This object is cleared and rebuilt in-place.
* **Return Value:** `void`
* **How It Works:**
  1. Converts `text` to a string and splits it into lines using `\n` and `\r`.
  2. Initializes a `Regex` with pattern `@"(^[\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}"` to detect joining space conditions based on character categories (letters excluding uncategorized letters at start, decimal digits at end, or strings of length 2 or more).
  3. Clears the contents of `text` (`text.Clear()`).
  4. For each line:
     - Splits the line into words, converts them to a list, and reverses the list order.
     - Iterates through the reversed words.
     - Determines whether a space needs to precede the appended word based on regex match criteria and whether it is the first word on the line.
     - Appends `Environment.NewLine` at the end of each non-empty line.

---

## Method Summary Table

| Method Name | Return Type | In-Place Mutation? | Summary |
| :--- | :--- | :--- | :--- |
| `CharDictionaryReplace` | `void` | Yes | Replaces characters using a dictionary lookup. |
| `RemoveTrailingNewlines` | `void` | Yes | Trims trailing `\r` and `\n` characters from the end. |
| `ReplaceGreekOrCyrillicWithLatin` | `void` | Yes | Maps Greek/Cyrillic characters to Latin equivalents via `StringMethods.GreekCyrillicLatinMap`. |
| `TryFixToLetters` | `void` | Yes | Replaces numeric characters with letter equivalents via `StringMethods.NumbersToLetters`. |
| `TryToFixToNumbers` | `void` | Yes | Replaces letter characters with numeric equivalents via `StringMethods.LettersToNumbers`. |
| `TryFixEveryWordLetterNumberErrors` | `string` | No | Processes words individually to resolve OCR digit/letter errors and cleans spacing around control characters. |
| `ReverseWordsForRightToLeft` | `void` | Yes | Rebuilds the buffer with reversed word sequences per line for RTL formatting. |