# Technical Documentation: `Text-Grab/Models/FindResult.cs`

## Overview

The `FindResult` class is a data model within the `Text_Grab.Models` namespace. It represents the outcome of a text search or "find" operation in the Text-Grab application. The class captures information about found text matches, including the text itself, surrounding preview contexts, match length, positional indices, and optional table/grid cell coordinates for structured data documents.

---

## Class Signature

```csharp
namespace Text_Grab.Models;

public class FindResult
```

---

## Properties

### Data Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Text` | `string` | `""` | The matched or processed text string. |
| `RawText` | `string` | `""` | The original or unformatted text string of the match. |
| `Count` | `int` | `0` | The total count or occurrence number associated with the match result. |
| `Index` | `int` | `0` | The zero-based character index where the match starts within the target text. |
| `PreviewLeft` | `string` | `""` | Text context immediately preceding (to the left of) the found match. |
| `PreviewRight` | `string` | `""` | Text context immediately following (to the right of) the found match. |
| `Length` | `int` | `0` | The length (character count) of the matched text. |
| `RowIndex` | `int?` | `null` | Optional zero-based row index if the search result originated from a tabular structure. |
| `ColumnIndex` | `int?` | `null` | Optional zero-based column index if the search result originated from a tabular structure. |

---

### Computed / Derived Properties

#### `CellAddress`

```csharp
public string CellAddress
```

* **Type**: `string` (Read-only getter)
* **Description**: Returns a user-friendly spreadsheet cell coordinate string (e.g., `"Cell: A1"` or `"Cell: B3"`) if both `RowIndex` and `ColumnIndex` are defined.
* **Logic**:
  1. Checks if `RowIndex` or `ColumnIndex` is `null`. If either is `null`, returns `string.Empty`.
  2. Converts the zero-based `ColumnIndex` to a spreadsheet column letter standard by calling `EditTextTableDocument.GetSpreadsheetColumnLabel(ColumnIndex.Value)`.
  3. Formats the output string as `$"Cell: {colLabel}{RowIndex.Value + 1}"` (converting the zero-based row index to a 1-based display value).

#### `LocationDisplay`

```csharp
public string LocationDisplay =>
    CellAddress.Length > 0 ? CellAddress : $"At index: {Index}";
```

* **Type**: `string` (Read-only getter expression)
* **Description**: Provides a contextual string describing where the match was found.
* **Logic**:
  * Evaluates whether `CellAddress` is non-empty (`CellAddress.Length > 0`).
  * If `CellAddress` exists, returns the `CellAddress` value.
  * If `CellAddress` is empty (e.g., when `RowIndex` or `ColumnIndex` is `null`), returns `$"At index: {Index}"`.

---

## Dependencies & External Calls

* **`EditTextTableDocument.GetSpreadsheetColumnLabel(int columnIndex)`**: External helper method referenced within the `CellAddress` property getter to translate numeric column indices into standard spreadsheet column letters (e.g., column index `0` becomes `"A"`).