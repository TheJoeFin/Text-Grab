# Technical Documentation: `Text-Grab/Models/BuiltInRecognizer.cs`

## Overview

The `BuiltInRecognizer` class in the `Text_Grab.Models` namespace represents a wrapper around individual text recognition methods provided by the `Microsoft.Recognizers.Text` library suite.

Unlike user-editable regex models (e.g., `StoredRegex`), `BuiltInRecognizer` provides a fixed, immutable catalog of culture-aware entities (such as numbers, dates, currencies, phone numbers, and emails). Each instance maps an identification string, display properties, and an execution delegate (`Func<string, string, List<ModelResult>>`) to a specific `Microsoft.Recognizers.Text` recognition function.

---

## Class Definition

```csharp
namespace Text_Grab.Models;

public class BuiltInRecognizer
```

### Namespace Imports
* `System`
* `System.Collections.Generic`
* `System.Linq`
* `Microsoft.Recognizers.Text`
* `Microsoft.Recognizers.Text.Choice`
* `Microsoft.Recognizers.Text.DateTime`
* `Microsoft.Recognizers.Text.Number`
* `Microsoft.Recognizers.Text.NumberWithUnit`
* `Microsoft.Recognizers.Text.Sequence`

---

## Properties

| Property | Type | Access | Description |
| :--- | :--- | :--- | :--- |
| `Id` | `string` | `get;` | Stable identifier used for serialization (e.g., in templates using format `{r:Name:mode}`). |
| `Name` | `string` | `get;` | Display name formatted for user interfaces, menus, and pickers. |
| `Description` | `string` | `get;` | Brief description explaining what entity type the recognizer targets. |
| `Recognize` | `Func<string, string, List<ModelResult>>` | `get;` | A delegate invoking the underlying `Microsoft.Recognizers.Text` method. Accepts `(text, culture)` and returns a list of `ModelResult` matches. |

---

## Constructor

### `private BuiltInRecognizer(string id, string name, string description, Func<string, string, List<ModelResult>> recognize)`

The class constructor is `private` to enforce the fixed-catalog model. New instances cannot be instantiated outside of the static initializer within this class.

* **Parameters:**
  * `id`: Unique string key for internal/serialized identification.
  * `name`: User-facing name.
  * `description`: Descriptive explanation of matched text patterns.
  * `recognize`: The recognition delegate function taking `(string text, string culture)` and returning `List<ModelResult>`.

---

## Built-In Recognizer Catalog (`All`)

`BuiltInRecognizer` maintains a static, read-only list (`All`) containing 14 predefined recognizer instances:

| Id | Name | Description | Delegate Implementation |
| :--- | :--- | :--- | :--- |
| `number` | Number | Numbers like 25 or 3.5 | `NumberRecognizer.RecognizeNumber` |
| `ordinal` | Ordinal | Ordinal numbers like 1st, 2nd, 3rd | `NumberRecognizer.RecognizeOrdinal` |
| `percentage` | Percentage | Percentages like 50% | `NumberRecognizer.RecognizePercentage` |
| `age` | Age | Ages like 25 years old | `NumberWithUnitRecognizer.RecognizeAge` |
| `currency` | Currency | Currency amounts like $5 or 10 dollars | `NumberWithUnitRecognizer.RecognizeCurrency` |
| `dimension` | Dimension | Dimensions like 3 miles or 5 kg | `NumberWithUnitRecognizer.RecognizeDimension` |
| `temperature` | Temperature | Temperatures like 90 degrees fahrenheit | `NumberWithUnitRecognizer.RecognizeTemperature` |
| `datetime` | Date / Time | Dates, times, durations and ranges like next tuesday at 3pm | `DateTimeRecognizer.RecognizeDateTime` |
| `phonenumber` | Phone Number | Phone numbers like (212) 555-0182 | `SequenceRecognizer.RecognizePhoneNumber` |
| `email` | Email | Email addresses | `SequenceRecognizer.RecognizeEmail` |
| `url` | URL | Web URLs | `SequenceRecognizer.RecognizeURL` |
| `ip` | IP Address | IPv4 and IPv6 addresses | `SequenceRecognizer.RecognizeIpAddress` |
| `guid` | GUID | GUIDs / UUIDs | `SequenceRecognizer.RecognizeGUID` |
| `boolean` | Boolean | Yes / no style boolean values | `ChoiceRecognizer.RecognizeBoolean` |

---

## Static Methods

### `GetAll()`
```csharp
public static IReadOnlyList<BuiltInRecognizer> GetAll()
```
* **Returns:** `IReadOnlyList<BuiltInRecognizer>`
* **Description:** Returns the complete static catalog of 14 built-in recognizers.

---

### `GetById(string id)`
```csharp
public static BuiltInRecognizer? GetById(string id)
```
* **Parameters:**
  * `id` (`string`): The stable string identifier to search for.
* **Returns:** `BuiltInRecognizer?` — The matching recognizer instance if found; otherwise, `null`.
* **Behavior:** Performs a case-insensitive lookup (`StringComparison.OrdinalIgnoreCase`) against the `Id` property using LINQ's `FirstOrDefault`.

---

### `GetByName(string name)`
```csharp
public static BuiltInRecognizer? GetByName(string name)
```
* **Parameters:**
  * `name` (`string`): The user-facing display name to search for.
* **Returns:** `BuiltInRecognizer?` — The matching recognizer instance if found; otherwise, `null`.
* **Behavior:** Performs a case-insensitive lookup (`StringComparison.OrdinalIgnoreCase`) against the `Name` property using LINQ's `FirstOrDefault`.

---

## Execution & Application Context

Based on the class specifications and code documentation:

1. **Templates:** Evaluated in Grab Templates via `{r:Name:mode}` placeholder structures using `Id` or `Name`.
2. **Recognition Output:** When invoked via the `Recognize(text, culture)` delegate, each recognizer delegates directly to `Microsoft.Recognizers.Text` assemblies and outputs a `List<ModelResult>`. Each `ModelResult` contains the raw matched text (`ModelResult.Text`) alongside structured resolution details.
3. **Immutability:** Recognizers are static and non-editable, ensuring predictable behavior across serializations.