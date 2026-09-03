# Technical Documentation: `Text-Grab/Models/LookupItem.cs`

## Overview

The `LookupItem.cs` file is located in the `Text_Grab.Models` namespace. It defines data structures used for lookup and quick-search items within the Text-Grab application. 

This file contains two primary types:
1. `LookupItemKind`: An enumeration defining the category or type of a lookup item.
2. `LookupItem`: A class representing a lookup entry, holding descriptive text, type classification, associated icons, optional history information, and equality logic.

---

## Enum: `LookupItemKind`

The `LookupItemKind` enum specifies the type of lookup item, which determines the UI symbol displayed for the item.

```csharp
public enum LookupItemKind
{
    Simple = 0,
    EditWindow = 1,
    GrabFrame = 2,
    Link = 3,
    Command = 4,
    Dynamic = 5,
    GrabTemplate = 6,
    PdfDocument = 7,
}
```

### Enumeration Members

| Name | Underlying Value | Description |
| :--- | :--- | :--- |
| `Simple` | `0` | Represents a standard text or simple lookup item. |
| `EditWindow` | `1` | Represents an item originating from or targeting an Edit Window. |
| `GrabFrame` | `2` | Represents a captured frame/region item. |
| `Link` | `3` | Represents a hyperlink lookup item. |
| `Command` | `4` | Represents an executable command or action. |
| `Dynamic` | `5` | Represents a dynamic/generated lookup item. |
| `GrabTemplate` | `6` | Represents a template item for grabs. |
| `PdfDocument` | `7` | Represents a PDF document lookup item. |

---

## Class: `LookupItem`

The `LookupItem` class models individual lookup entries and implements `IEquatable<LookupItem>` for value comparison based on string representations.

### Properties

#### `ShortValue`
* **Type:** `string`
* **Access:** `get`, `set`
* **Default Value:** `string.Empty`
* **Description:** Represents the primary or abbreviated string representation of the lookup item.

#### `LongValue`
* **Type:** `string`
* **Access:** `get`, `set`
* **Default Value:** `string.Empty`
* **Description:** Represents the detailed or extended string content associated with the lookup item.

#### `Kind`
* **Type:** `LookupItemKind`
* **Access:** `get`, `set`
* **Default Value:** `LookupItemKind.Simple`
* **Description:** Specifies the type of the lookup item.

#### `HistoryItem`
* **Type:** `HistoryInfo?` (Nullable)
* **Access:** `get`, `set`
* **Default Value:** `null`
* **Description:** Stores a reference to an associated `HistoryInfo` object if the lookup item was instantiated from history data.

#### `TemplateId`
* **Type:** `string?` (Nullable)
* **Access:** `get`, `set`
* **Default Value:** `null`
* **Description:** Stores an optional identifier string for template reference.

#### `UiSymbol` (Read-only)
* **Type:** `Wpf.Ui.Controls.SymbolRegular`
* **Access:** `get`
* **Description:** A computed property that returns a specific WPF UI icon control symbol based on the item's `Kind` property using switch pattern matching:

| `Kind` Value | Returned `SymbolRegular` Icon |
| :--- | :--- |
| `LookupItemKind.Simple` | `SymbolRegular.Copy20` |
| `LookupItemKind.EditWindow` | `SymbolRegular.Window24` |
| `LookupItemKind.GrabFrame` | `SymbolRegular.PanelBottom20` |
| `LookupItemKind.Link` | `SymbolRegular.Link24` |
| `LookupItemKind.Command` | `SymbolRegular.WindowConsole20` |
| `LookupItemKind.Dynamic` | `SymbolRegular.Flash24` |
| `LookupItemKind.GrabTemplate` | `SymbolRegular.DocumentTableSearch24` |
| `LookupItemKind.PdfDocument` | `SymbolRegular.DocumentSearch24` |
| *Default / Fallback* | `SymbolRegular.Copy20` |

#### `FirstLettersString` (Read-only)
* **Type:** `string`
* **Access:** `get`
* **Description:** Computes a lowercased string consisting of the first character of every word in `ShortValue`. Splits `ShortValue` by space (`' '`), removes empty entries, extracts the character at index `0` of each word, joins them together, and converts the result to lowercase.

---

### Constructors

#### `LookupItem()`
Parameterless constructor initializing default values for all properties.

```csharp
public LookupItem()
```

---

#### `LookupItem(string sv, string lv)`
Initializes a `LookupItem` instance with explicitly provided short and long string values.

```csharp
public LookupItem(string sv, string lv)
```

* **Parameters:**
  * `sv` (`string`): Assigned to `ShortValue`.
  * `lv` (`string`): Assigned to `LongValue`.

---

#### `LookupItem(HistoryInfo historyInfo)`
Initializes a `LookupItem` instance populated from a `HistoryInfo` object.

```csharp
public LookupItem(HistoryInfo historyInfo)
```

* **Execution Logic:**
  1. **`ShortValue` Assignment:** Concatenates the humanized relative time of capture (`historyInfo.CaptureDateTime.Humanize()`), a newline (`Environment.NewLine`), and the full formatted date time (`historyInfo.CaptureDateTime.ToString("F")`).
  2. **`LongValue` Assignment:** Takes the trimmed `historyInfo.TextContent`. If the text content length exceeds 100 characters, it truncates the string to the first 100 characters, trims trailing whitespace, and appends the ellipsis character (`"…"`).
  3. **`HistoryItem` Assignment:** Stores the provided `historyInfo` reference.
  4. **`Kind` Determination:**
     * Sets `Kind` to `LookupItemKind.PdfDocument` if `historyInfo.IsPdfDocument` is `true`.
     * Else, sets `Kind` to `LookupItemKind.EditWindow` if `historyInfo.ImagePath` is `null` or `string.Empty`.
     * Otherwise, sets `Kind` to `LookupItemKind.GrabFrame`.

---

### Methods

#### `ToString()`
Provides a string representation of the lookup item.

```csharp
public override string ToString()
```

* **Returns:**
  * If `HistoryItem` is not `null`: `"{HistoryItem.CaptureDateTime:F} {HistoryItem.TextContent}"`
  * If `HistoryItem` is `null`: `"{ShortValue} {LongValue}"`

---

#### `ToCSVString()`
Formats the item into a comma-separated values string format.

```csharp
public string ToCSVString()
```

* **Returns:** A string formatted as `"{ShortValue},{LongValue}"`.

---

#### `Equals(LookupItem? other)`
Implementation of the `IEquatable<LookupItem>` interface. Evaluates equality between the current instance and another `LookupItem`.

```csharp
public bool Equals(LookupItem? other)
```

* **Parameters:**
  * `other` (`LookupItem?`): The target item to compare against.
* **Returns:**
  * `false` if `other` is `null`.
  * `true` if `other.ToString()` is equal to `this.ToString()`.
  * `false` otherwise.