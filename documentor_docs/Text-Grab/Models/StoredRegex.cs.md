# Technical Documentation: `StoredRegex.cs`

## Overview

**File Path:** `Text-Grab/Models/StoredRegex.cs`  
**Namespace:** `Text_Grab.Models`  
**Class:** `StoredRegex`

The `StoredRegex` model class represents a regular expression pattern saved within the Text-Grab application. It holds metadata associated with a regular expression, including unique identification, display naming, pattern text, descriptions, creation/usage timestamps, and flags identifying whether a pattern is a system default or user-created.

---

## Class Definition

```csharp
public class StoredRegex
```

---

## Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Id` | `string` | `Guid.NewGuid().ToString()` | A unique string identifier generated automatically upon instantiation. |
| `Name` | `string` | `string.Empty` | The display name for the regex pattern. |
| `Pattern` | `string` | `string.Empty` | The raw regular expression pattern string. |
| `IsDefault` | `bool` | `false` | A boolean flag indicating whether this is a built-in default pattern (`true`) or custom/user-defined (`false`). |
| `Description` | `string` | `string.Empty` | Additional details, notes, or descriptions explaining the pattern's purpose. |
| `CreatedDate` | `DateTimeOffset` | `DateTimeOffset.Now` | Timestamp recording when the pattern instance was created. |
| `LastUsedDate` | `DateTimeOffset?` | `null` | Nullable timestamp recording the date and time when the pattern was last executed or applied. |

---

## Constructors

### 1. Parameterless Constructor
```csharp
public StoredRegex()
```
Initializes a new instance of `StoredRegex` with property defaults.

### 2. Parameterized Constructor
```csharp
public StoredRegex(string name, string pattern, bool isDefault = false, string description = "")
```
Initializes a `StoredRegex` instance with specified properties.

* **Parameters:**
  * `name` (`string`): The name assigned to `Name`.
  * `pattern` (`string`): The pattern assigned to `Pattern`.
  * `isDefault` (`bool`, optional): Assigned to `IsDefault`. Defaults to `false`.
  * `description` (`string`, optional): Assigned to `Description`. Defaults to `""` (empty string).

---

## Static Methods

### `GetDefaultPatterns()`

```csharp
public static StoredRegex[] GetDefaultPatterns()
```

Returns an array of predefined `StoredRegex` objects supplied as built-in defaults by Text-Grab. 

#### Design Scope & Exclusions
As noted in the source documentation comments, this method deliberately excludes standard formats like emails, phone numbers, URLs, IP addresses, GUIDs, dates, times, currency, and plain numbers. Those categories are handled separately by the culture-aware `BuiltInRecognizer` catalog to prevent duplication.

#### Returned Patterns

The method returns an array containing the following 4 default patterns:

1. **Credit Card**
   * **Pattern:** `\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b`
   * **IsDefault:** `true`
   * **Description:** `"Matches credit card numbers"`

2. **Hex Color**
   * **Pattern:** `#[0-9a-fA-F]{6}\b`
   * **IsDefault:** `true`
   * **Description:** `"Matches hex color codes like #FFFFFF"`

3. **Social Security Number**
   * **Pattern:** `\b\d{3}-\d{2}-\d{4}\b`
   * **IsDefault:** `true`
   * **Description:** `"Matches SSN format XXX-XX-XXXX"`

4. **Zip Code (US)**
   * **Pattern:** `\b\d{5}(-\d{4})?\b`
   * **IsDefault:** `true`
   * **Description:** `"Matches US zip codes (5 or 9 digit)"`