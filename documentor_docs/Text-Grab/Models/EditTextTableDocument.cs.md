# EditTextTableDocument Technical Documentation

## Overview

The `EditTextTableDocument` class (located in the `Text_Grab.Models` namespace) serves as a data model for representing, manipulating, and serializing structured tabular text data. It supports standard text formats including Plain Text, CSV, TSV, custom-delimited text, and XML. 

In addition to maintaining tabular row and column data, the class tracks display/view-related properties such as column widths, row heights, minimum grid dimensions, and individual cell wrap states.

---

## Associated Types

### Enums

#### `EtwEditorMode`
Defines the editing mode environment:
* `Text`: Plain text editing mode.
* `Markdown`: Markdown formatting mode.
* `Spreadsheet`: Spreadsheet-style grid mode.

#### `EtwStructuredTextFormat`
Defines the format of the structured text document:
* `PlainText`: Standard unformatted text.
* `DelimitedText`: Custom-delimited text (e.g., `;`, `|`, `:`).
* `Csv`: Comma-Separated Values.
* `Tsv`: Tab-Separated Values.
* `Xml`: Extensible Markup Language structure.

### Records

#### `EditTextTableWrappedCell`
A record representing the position of a cell configured with text wrapping:
* `RowIndex` (`int`): The zero-based row index of the cell.
* `ColumnIndex` (`int`): The zero-based column index of the cell.

---

## Class Constants and Properties

### Constants
* `DefaultMinimumRowCount` (`int = 25`): Default minimum number of rows maintained in the grid.
* `DefaultMinimumColumnCount` (`int = 8`): Default minimum number of columns maintained in the grid.

### Document Configuration Properties
* `Format` (`EtwStructuredTextFormat`): Specifies the document format type. Defaults to `EtwStructuredTextFormat.PlainText`.
* `NewLineSequence` (`string`): The line ending sequence used during text serialization. Defaults to `Environment.NewLine`.
* `Delimiter` (`string`): The delimiter character sequence for delimited formats. Defaults to `"\t"`.

### XML Format Properties
* `XmlRootElementName` (`string`): Name of the root element when serializing to XML. Defaults to `"rows"`.
* `XmlContainerElementName` (`string?`): Optional intermediate container element surrounding row elements in XML serialization.
* `XmlRowElementName` (`string`): Name of row elements when serializing to XML. Defaults to `"row"`.

### Table Data and Dimensions
* `ColumnNames` (`List<string>`): List of header names for columns.
* `Rows` (`List<List<string>>`): Two-dimensional list containing table row and column cell values.
* `RowCount` (`int`): Active logical count of rows in the document.
* `ColumnCount` (`int`): Active logical count of columns in the document.
* `MinimumRowCount` (`int`): Minimum number of rows enforced. Defaults to `DefaultMinimumRowCount` (25).
* `MinimumColumnCount` (`int`): Minimum number of columns enforced. Defaults to `DefaultMinimumColumnCount` (8).

### View Metrics Properties
* `ColumnWidths` (`List<double?>`): List of optional width dimensions for each column.
* `RowHeights` (`List<double?>`): List of optional height dimensions for each row.
* `WrappedCells` (`List<EditTextTableWrappedCell>`): List of cell locations that have text wrapping enabled.

---

## Document Creation and Deserialization

### Static Factory Methods

#### `CreateFromText(string? text, int minimumRowCount, int minimumColumnCount)`
Parses input raw text and constructs an instance of `EditTextTableDocument`. It attempts format detection in the following sequential order:
1. **TSV**: Tries parsing with `\t` delimiter.
2. **CSV**: Tries parsing with `,` delimiter.
3. **XML**: Tries parsing as XML structure.
4. **Heuristic Delimited**: Tries parsing with alternate delimiters (`|`, `;`, `:`).
5. **Plain Text**: Fallback format if structured parsing fails or text is unstructured.

After format detection, `EnsureMinimumSize()` is called before returning the document.

#### `TryDeserialize(string? json)`
Attempts to deserialize a JSON string into an `EditTextTableDocument` using `JsonSerializer`.
* Returns `null` if the input is null/whitespace or if a `JsonException` occurs.
* Calls `EnsureMinimumSize()` on successfully deserialized instances.

#### `GetSpreadsheetColumnLabel(int index)`
Static helper that converts zero-based integer column indices into spreadsheet column letters (e.g., `0` -> `"A"`, `25` -> `"Z"`, `26` -> `"AA"`).

---

## Serialization Methods

### `SerializeToJson()`
Converts the current `EditTextTableDocument` instance into its JSON string representation.

### `SerializeToText()`
Serializes document grid data into a raw text string formatted according to the current `Format` property:
* **XML**: Calls `SerializeToXml()`. Maps attributes (`@`-prefixed column names) and child elements.
* **CSV**: Calls `SerializeDelimitedText(',')`. Encloses values containing quotes, line breaks, or commas in double quotes.
* **TSV**: Calls `SerializeDelimitedText('\t')`.
* **DelimitedText**: Calls `SerializeDelimitedText(delimiter)`. Uses the first character of `Delimiter`.
* **PlainText**: Serializes first column row values concatenated by `NewLineSequence` if single-column, or falls back to delimited serialization if multi-column.

---

## Table Structure Operations

### `InsertRow(int rowIndex)`
Inserts an empty row at `rowIndex` (clamped between `0` and `RowCount`):
* Shifts row indices for items in `WrappedCells` where `RowIndex >= insertIndex`.
* Inserts empty cell strings in `Rows` and `null` into `RowHeights`.
* Increments `RowCount` and updates `MinimumRowCount`.

### `InsertColumn(int columnIndex, string? columnName = null)`
Inserts a column at `columnIndex` (clamped between `0` and `ColumnCount`):
* Generates a unique column name if `columnName` is omitted or conflicts with existing column names.
* Shifts column indices for items in `WrappedCells` where `ColumnIndex >= insertIndex`.
* Inserts the column name into `ColumnNames`, `null` into `ColumnWidths`, and an empty string into each row in `Rows`.
* Increments `ColumnCount` and updates `MinimumColumnCount`.

### `DeleteRow(int rowIndex)`
Deletes the row at `rowIndex`:
* Removes wrapped cell entries matching `rowIndex`, and decrements `RowIndex` for entries where `RowIndex > rowIndex`.
* Removes entry from `Rows` and `RowHeights`.
* Decrements `RowCount` (floored at `1`).

### `DeleteColumn(int columnIndex)`
Deletes the column at `columnIndex`:
* Removes wrapped cell entries matching `columnIndex`, and decrements `ColumnIndex` for entries where `ColumnIndex > columnIndex`.
* Removes entries from `ColumnNames`, `ColumnWidths`, and cell data across all elements in `Rows`.
* Decrements `ColumnCount` (floored at `1`).

### `MoveRow(int fromIndex, int toIndex)`
Relocates a row from `fromIndex` to `toIndex`:
* Updates affected `RowIndex` values in `WrappedCells` using internal index translation logic.
* Relocates elements within `Rows` and `RowHeights`.

### `MoveColumn(int fromIndex, int toIndex)`
Relocates a column from `fromIndex` to `toIndex`:
* Updates affected `ColumnIndex` values in `WrappedCells` using internal index translation logic.
* Relocates elements within `ColumnNames`, `ColumnWidths`, and column values within each row in `Rows`.

### `Transpose()`
Swaps rows and columns:
* Flips matrix data in `Rows` (rows become columns, columns become rows).
* Swaps `RowCount` and `ColumnCount` dimensions.
* Swaps original `MinimumRowCount` and `MinimumColumnCount`.
* Swaps `RowIndex` and `ColumnIndex` coordinates in `WrappedCells`.
* Resets `ColumnNames` to default generic spreadsheet names ("Column A", "Column B", etc.).
* Resets `ColumnWidths` and `RowHeights`.
* Calls `EnsureMinimumSize()`.

---

## Size Normalization & View Metrics Management

### `EnsureMinimumSize()`
Ensures structural consistency between grid lists, actual row/column counts, and minimum constraints:
* Validates `MinimumRowCount` and `MinimumColumnCount` are at least default constants if set below `1`.
* Infers logical column count if `ColumnCount` is `0`.
* Expands `ColumnNames`, `ColumnWidths`, `Rows`, and `RowHeights` to match the maximum necessary dimension bounds.
* Pads shortened row cell lists with empty strings.
* Invokes `NormalizeWrappedCells()`.

### `ApplyViewMetricsFrom(EditTextTableDocument source)`
Copies view-specific properties (`ColumnWidths`, `RowHeights`, `WrappedCells`) from a `source` document:
* Ensures minimum dimensions on both source and target documents.
* Copies available column widths and row heights up to the lower dimension boundary.
* Overwrites `WrappedCells` with source values and normalizes them.

### `SetColumnWidth(int columnIndex, double? width)`
Sets the width for a specified column index. Validates index boundaries and normalizes input width (discards `<= 0`, `NaN`, and `Infinity` values as `null`).

### `SetRowHeight(int rowIndex, double? height)`
Sets the height for a specified row index. Validates index boundaries and normalizes input height (discards `<= 0`, `NaN`, and `Infinity` values as `null`).

### `IsCellWrapped(int rowIndex, int columnIndex)` -> `bool`
Returns `true` if `WrappedCells` contains an entry matching `(rowIndex, columnIndex)`.

### `SetCellWrap(int rowIndex, int columnIndex, bool shouldWrap)`
Adds or removes a cell coordinate from `WrappedCells` based on `shouldWrap` boolean parameter and normalizes `WrappedCells`.

---

## Private Helper Logic

* **`ParseDelimitedText(text, delimiter)`**: State-machine parser handling field extraction, standard newline breaks (`\r`, `\n`, `\r\n`), and escaped quotes (`""`).
* **`TryCreateXmlDocument(...)`**: Scans XML node tree for repeated elements, infers attributes (prefixed with `@`) and child elements as columns, and constructs row data.
* **`LooksStructured(rows)`**: Heuristic algorithm requiring at least two non-empty rows matching the maximum column width (or a single row with 2+ columns) to qualify as structured text.
* **`TranslateMovedIndex(currentIndex, fromIndex, toIndex)`**: Internal index translation arithmetic used when shifting items during move operations.
* **`NormalizeWrappedCells()`**: Filters out wrapped cell coordinates outside current valid table dimensions, removes duplicates, and sorts remaining cells by row index then column index.
* **`CreateXmlName(rawName, fallbackPrefix, index)`**: Sanitizes input strings into standard XML-compliant element names (replaces spaces with underscores, encodes local names, prepends numeric-leading strings with underscores).