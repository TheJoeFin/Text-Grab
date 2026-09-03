# Technical Documentation: `TemplateRecognizerMatch.cs`

**File Location:** `Text-Grab/Models/TemplateRecognizerMatch.cs`  
**Namespace:** `Text_Grab.Models`  
**Dependencies:** `Text_Grab.Utilities`

---

## Overview

The `TemplateRecognizerMatch` class is a data model representing a reference to a built-in recognizer configuration within a `GrabTemplate`. It stores rules and options determining how matches extracted by a recognizer should be selected, separated, and formatted (resolved value vs. matched raw text) when building output from source text.

---

## Class Definition

```csharp
public class TemplateRecognizerMatch
```

This is a public, lightweight data class (DTO/Model) with automatic properties and two constructors. It holds configuration data and contains no processing logic of its own.

---

## Placeholder Syntax

As documented in the class XML comments, instances of this class correspond to placeholder expressions used within template outputs using the `{r:...}` syntax pattern:

| Placeholder Syntax Example | Description |
| :--- | :--- |
| `{r:RecognizerName:first}` | Selects the first match emitted by the recognizer, outputting the resolved value. |
| `{r:RecognizerName:last}` | Selects the last match emitted by the recognizer. |
| `{r:RecognizerName:all}` | Selects all matches using the default separator. |
| `{r:RecognizerName:all:text}` | Selects all matches, outputting raw matched text instead of the resolved value. |
| `{r:RecognizerName:all:value:; }` | Selects all matches, outputting resolved values joined by custom separator `"; "`. |
| `{r:RecognizerName:2}` | Selects the 2nd match (1-based index). |
| `{r:RecognizerName:1,3}` | Selects the 1st and 3rd matches joined by the configured separator. |

---

## Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `RecognizerId` | `string` | `string.Empty` | Identifies the underlying recognizer by matching `BuiltInRecognizer.Id`. |
| `RecognizerName` | `string` | `string.Empty` | Display name mirroring `BuiltInRecognizer.Name`. Used as the identifier inside `{r:RecognizerName:...}` placeholder tags. |
| `MatchMode` | `string` | `"first"` | Dictates match selection behavior. Accepted values include `"first"`, `"last"`, `"all"`, single 1-based index (e.g., `"2"`), or comma-separated indices (e.g., `"1,3,5"`). |
| `Separator` | `string` | `", "` | The delimiter inserted between multiple matched elements when `MatchMode` is `"all"` or specifies multiple indices. |
| `OutputKind` | `RecognizerOutputKind` | `RecognizerOutputKind.ResolvedValue` | Enum value specifying whether to output the normalized/resolved value or the raw matched text. |

---

## Constructors

### 1. Parameterless Constructor
```csharp
public TemplateRecognizerMatch() { }
```
Initializes a new instance of `TemplateRecognizerMatch` with standard default property values (`RecognizerId` = `""`, `RecognizerName` = `""`, `MatchMode` = `"first"`, `Separator` = `", "`, `OutputKind` = `RecognizerOutputKind.ResolvedValue`).

### 2. Parameterized Constructor
```csharp
public TemplateRecognizerMatch(
    string recognizerId, 
    string recognizerName,
    string matchMode = "first", 
    string separator = ", ",
    RecognizerOutputKind outputKind = RecognizerOutputKind.ResolvedValue)
```

Initializes a new instance of `TemplateRecognizerMatch` allowing explicit assignment of configuration values with default fallback arguments for optional parameters.

#### Parameters:
* **`recognizerId`** (`string`): Sets `RecognizerId`.
* **`recognizerName`** (`string`): Sets `RecognizerName`.
* **`matchMode`** (`string`, optional): Sets `MatchMode` (defaults to `"first"`).
* **`separator`** (`string`, optional): Sets `Separator` (defaults to `", "`).
* **`outputKind`** (`RecognizerOutputKind`, optional): Sets `OutputKind` (defaults to `RecognizerOutputKind.ResolvedValue`).