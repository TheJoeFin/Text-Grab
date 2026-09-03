# CharacterUtilities Documentation

**File Path:** `Text-Grab/Utilities/CharacterUtilities.cs`  
**Namespace:** `Text_Grab.Utilities`

---

## Overview

The `CharacterUtilities` class is a static utility class in the `Text_Grab.Utilities` namespace. It provides helper methods for inspecting individual character (`char`) properties, retrieving user-friendly Unicode category names, identifying and formatting HTML entities, and generating formatted character metadata text strings.

---

## Class Details

```csharp
namespace Text_Grab.Utilities;

public static class CharacterUtilities
```

### Dependencies
- `System.Globalization`: Provides `UnicodeCategory` enumeration and culture-specific operations.
- `System.Linq`: Used for array/enumerable mapping formatting (e.g., `Select`).
- `System.Text`: Provides `Encoding.UTF8` and `StringBuilder`.

---

## Public Static Methods

### 1. `GetUnicodeCategory(char c)`

Converts the standard .NET `UnicodeCategory` enum for a given character into a human-readable string description.

#### Signature
```csharp
public static string GetUnicodeCategory(char c)
```

#### Parameters
- `c` (`char`): The target character to evaluate.

#### Returns
- `string`: A user-friendly string representation of the character's Unicode category. Returns `"Unknown"` if the category does not match any specified switch arm.

#### Logic Mapping Table
| `UnicodeCategory` Enum Value | Output String |
| :--- | :--- |
| `UppercaseLetter` | `"Uppercase Letter"` |
| `LowercaseLetter` | `"Lowercase Letter"` |
| `TitlecaseLetter` | `"Titlecase Letter"` |
| `ModifierLetter` | `"Modifier Letter"` |
| `OtherLetter` | `"Other Letter"` |
| `NonSpacingMark` | `"Non-Spacing Mark"` |
| `SpacingCombiningMark` | `"Spacing Mark"` |
| `EnclosingMark` | `"Enclosing Mark"` |
| `DecimalDigitNumber` | `"Decimal Digit"` |
| `LetterNumber` | `"Letter Number"` |
| `OtherNumber` | `"Other Number"` |
| `SpaceSeparator` | `"Space Separator"` |
| `LineSeparator` | `"Line Separator"` |
| `ParagraphSeparator` | `"Paragraph Separator"` |
| `Control` | `"Control Character"` |
| `Format` | `"Format Character"` |
| `Surrogate` | `"Surrogate"` |
| `PrivateUse` | `"Private Use"` |
| `ConnectorPunctuation` | `"Connector Punctuation"` |
| `DashPunctuation` | `"Dash Punctuation"` |
| `OpenPunctuation` | `"Open Punctuation"` |
| `ClosePunctuation` | `"Close Punctuation"` |
| `InitialQuotePunctuation` | `"Initial Quote"` |
| `FinalQuotePunctuation` | `"Final Quote"` |
| `OtherPunctuation` | `"Other Punctuation"` |
| `MathSymbol` | `"Math Symbol"` |
| `CurrencySymbol` | `"Currency Symbol"` |
| `ModifierSymbol` | `"Modifier Symbol"` |
| `OtherSymbol` | `"Other Symbol"` |
| `OtherNotAssigned` | `"Not Assigned"` |
| *(Default)* | `"Unknown"` |

---

### 2. `IsCommonHtmlEntity(char c)`

Determines if a character is one of six defined common HTML entity characters.

#### Signature
```csharp
public static bool IsCommonHtmlEntity(char c)
```

#### Parameters
- `c` (`char`): The character to check.

#### Returns
- `bool`: `true` if `c` is one of `'<'`, `'>'`, `'&'`, `'"'`, `'\''`, or `' '`; otherwise, `false`.

---

### 3. `GetHtmlEntity(char c, int codePoint)`

Generates an HTML entity string representation for a given character and its decimal Unicode code point.

#### Signature
```csharp
public static string GetHtmlEntity(char c, int codePoint)
```

#### Parameters
- `c` (`char`): The character to format.
- `codePoint` (`int`): The decimal Unicode code point of the character.

#### Returns
- `string`: The corresponding named/numeric HTML entity string, formatted as follows:
  - `'<'` $\rightarrow$ `"&lt; or &#60;"`
  - `'>'` $\rightarrow$ `"&gt; or &#62;"`
  - `'&'` $\rightarrow$ `"&amp; or &#38;"`
  - `'"'` $\rightarrow$ `"&quot; or &#34;"`
  - `'\''` $\rightarrow$ `"&apos; or &#39;"`
  - `' '` *(when `codePoint == 160`)* $\rightarrow$ `"&nbsp; or &#160;"`
  - *All other characters* $\rightarrow$ `$"&#{codePoint};"`

---

### 4. `GetCharacterDetailsText(char c)`

Aggregates multiple metadata properties of a given character into a multi-line formatted string.

#### Signature
```csharp
public static string GetCharacterDetailsText(char c)
```

#### Parameters
- `c` (`char`): The character whose details need to be extracted and formatted.

#### Execution Logic
1. Computes the UTF-32 code point using `char.ConvertToUtf32(c.ToString(), 0)`.
2. Formats the code point into a hexadecimal Unicode string (`U+XXXX`, padded to at least 4 hex digits).
3. Resolves the human-readable Unicode category using `GetUnicodeCategory(c)`.
4. Encodes the character into UTF-8 byte array using `Encoding.UTF8.GetBytes(c.ToString())` and formats each byte into space-separated uppercase hex strings prefixed with `0x` (e.g., `0x41`).
5. Appends the following lines to a `StringBuilder`:
   - `Character: '{c}'`
   - `Unicode: U+{codePoint:X4} (decimal: {codePoint})`
   - `Category: {category}`
   - `UTF-8: {utf8Hex}`
6. If the character's code point is less than `128` **or** `IsCommonHtmlEntity(c)` returns `true`:
   - Retrieves the HTML entity string using `GetHtmlEntity(c, codePoint)`.
   - If the resulting HTML entity string is not null or empty, appends: `HTML: {htmlEntity}`.
7. Returns the accumulated text with trailing whitespace trimmed via `.TrimEnd()`.

#### Output Format Example
For input character `'A'`:
```text
Character: 'A'
Unicode: U+0041 (decimal: 65)
Category: Uppercase Letter
UTF-8: 0x41
HTML: &#65;
```

For input character `'<'`:
```text
Character: '<'
Unicode: U+003C (decimal: 60)
Category: Open Punctuation
UTF-8: 0x3C
HTML: &lt; or &#60;
```