# `EditTextTableDocumentTests.cs` Documentation Guide

## Overview

The `EditTextTableDocumentTests` class is a unit test suite written in xUnit for testing the `EditTextTableDocument` model class located in the `Text_Grab.Models` namespace. 

The primary objective of this test suite is to verify the core capabilities of `EditTextTableDocument`, including:
- Parsing and round-trip text serialization across various structured text formats (TSV, CSV, XML, Plain Text).
- Row and column manipulation (insert, move, delete, and transpose operations).
- Dimension and format tracking (line endings, column widths, row heights, and cell wrapping).
- View metric alignment across document modifications.
- JSON serialization and deserialization state restoration.

---

## File Details

- **File Path**: `Tests/EditTextTableDocumentTests.cs`
- **Namespace**: `Tests`
- **Testing Framework**: xUnit (`[Fact]`, `Assert`)
- **Dependencies**:
  - `System.Text.Json`
  - `Text_Grab.Models`

---

## Key Concepts & Tested Functionality

### 1. Format Detection and Serialization Round-Trips
Verifies that text parsed from supported formats (`Tsv`, `Csv`, `Xml`, `PlainText`) accurately determines format types, retains newline styles (`\r\n` vs `\n`), handles quoting rules, flattens hierarchical XML data, and reconstructs the original or expected text output without data corruption.

### 2. Table Structural Operations
Tests the ability to dynamically modify table dimensions using methods like:
- `InsertRow(index)`
- `InsertColumn(index)`
- `DeleteRow(index)`
- `DeleteColumn(index)`
- `MoveRow(from, to)`
- `MoveColumn(from, to)`
- `Transpose()`

### 3. View Metrics & Formatting Management
Validates metadata management linked to table indices, such as:
- Column widths (`SetColumnWidth`, `ColumnWidths`)
- Row heights (`SetRowHeight`, `RowHeights`)
- Cell wrapping state (`SetCellWrap`, `IsCellWrapped`, `WrappedCells`)
- Copying formatting properties across documents (`ApplyViewMetricsFrom`)

### 4. JSON Serialization & Restoration
Ensures full document state—including row/column counts, content, column widths, row heights, and cell text wrapping—can be serialized to JSON and deserialized back to an equivalent document via `SerializeToJson()` and `TryDeserialize()`.

---

## Detailed Test Method Breakdown

### 1. `Tsv_RoundTrips_WithoutMinimumGridPadding()`
* **Purpose**: Tests parsing tab-separated values (TSV) and verifying round-trip serialization.
* **Input**: `"Name\tValue\r\nAlpha\t42"`
* **Assertions**:
  * Format resolves to `EtwStructuredTextFormat.Tsv`.
  * New line sequence matches `"\r\n"`.
  * `SerializeToText()` yields the exact input string.

### 2. `Csv_QuotedFields_RoundTrip()`
* **Purpose**: Verifies that comma-separated values (CSV) containing quoted strings and escaped double quotes round-trip correctly.
* **Input**: `"Name,Notes\r\nJoe,\"Hello, \"\"world\"\"\""`
* **Assertions**:
  * Format resolves to `EtwStructuredTextFormat.Csv`.
  * Output of `SerializeToText()` matches the original quoted string input.

### 3. `Xml_FlattensRows_AndSerializesAttributesAndChildren()`
* **Purpose**: Checks XML parsing into a flattened tabular structure and its ability to capture attributes and child elements.
* **Input**: `<items><item id="1"><name>Alpha</name><value>42</value></item><item id="2"><name>Beta</name><value>99</value></item></items>`
* **Assertions**:
  * Format resolves to `EtwStructuredTextFormat.Xml`.
  * Column names start with `["@id", "name", "value"]`.
  * Attributes (e.g., `"@id"`) map to cells (`Rows[0][0]` is `"1"`).
  * Child nodes map to cells (`Rows[0][1]` is `"Alpha"`).
  * Serialized output contains `id="1"` and `<name>Alpha</name>`.

### 4. `PlainText_PreservesNewLineStyle()`
* **Purpose**: Ensures plain text inputs retain their native newline character convention (`\n`).
* **Input**: `"first\nsecond\nthird"`
* **Assertions**:
  * Format resolves to `EtwStructuredTextFormat.PlainText`.
  * `NewLineSequence` equals `"\n"`.
  * `SerializeToText()` round-trips correctly.

### 5. `AddedRowsAndColumns_ExpandSerializedDocument_NotMinimumCapacity()`
* **Purpose**: Confirms that inserting rows and columns expands the logical document structure during text serialization.
* **Input**: `"A\tB"`
* **Operations**: Insert column at index 2, insert row at index 1, populate cells `[0][2]="C"`, `[1][0]="D"`, `[1][1]="E"`, `[1][2]="F"`.
* **Assertions**:
  * `SerializeToText()` matches `"A\tB\tC\r\nD\tE\tF"`.

### 6. `SerializedJson_RestoresLogicalDimensions()`
* **Purpose**: Verifies that serializing to JSON preserves and restores custom view properties and logical dimensions.
* **Operations**:
  * Create document from `"left\tright"`, add column 2 with value `"extra"`.
  * Set column width at `0` to `180`.
  * Set row height at `0` to `36`.
  * Set cell wrapping at `(0, 1)` to `true`.
  * Serialize to JSON via `SerializeToJson()` and deserialize via `EditTextTableDocument.TryDeserialize()`.
* **Assertions**:
  * Restored object is not null.
  * `RowCount` and `ColumnCount` match original.
  * Restored text, column width (`180`), row height (`36`), and cell wrapping (`[0, 1]`) match.
  * JSON payload explicitly includes property `"ColumnCount"`.

### 7. `MoveAndDeleteRow_UpdateLogicalOrdering()`
* **Purpose**: Tests row reordering and deletion on logical text output.
* **Input**: `"A\t1\r\nB\t2\r\nC\t3"`
* **Operations**: Move row 2 to 0, then delete row 1.
* **Assertions**:
  * Serialized output reflects updated row order: `"C\t3\r\nB\t2"`.

### 8. `MoveAndDeleteColumn_UpdateLogicalOrdering()`
* **Purpose**: Tests column reordering and deletion on logical text output.
* **Input**: `"A\tB\tC"`
* **Operations**: Move column 2 to 0, then delete column 1.
* **Assertions**:
  * Serialized output reflects updated column order: `"C\tB"`.

### 9. `ViewMetrics_MoveWithRowsAndColumns()`
* **Purpose**: Asserts that column widths and row heights remain associated with their corresponding rows and columns when moved.
* **Input**: 2x2 grid (`"A\tB\r\nC\tD"`)
* **Operations**: Set width col 0 (140), col 1 (220); set height row 0 (30), row 1 (44). Swap column 1 to 0 and row 1 to 0.
* **Assertions**:
  * Column 0 width becomes `220`, column 1 width becomes `140`.
  * Row 0 height becomes `44`, row 1 height becomes `30`.

### 10. `ApplyViewMetricsFrom_PreservesExistingSizing()`
* **Purpose**: Validates applying view metrics (column widths, row heights, wrapped cells) from a source document to a target document.
* **Operations**: Copy view metrics from a 2x2 source document with custom dimensions to a 3x2 target document via `target.ApplyViewMetricsFrom(source)`.
* **Assertions**:
  * Target document inherits widths `160`, `240` and heights `28`, `40`.
  * Unspecified target rows (row 2) remain `null`.
  * Cell wrap setting at `(1, 1)` is preserved on the target document.

### 11. `Transpose_SwapsRowsAndColumns_AndResetsViewMetrics()`
* **Purpose**: Validates that transposing a table swaps row and column data/capacities, adjusts wrapped cell coordinates, and resets custom width and height metrics.
* **Input**: 2x3 matrix `"A\tB\tC\r\n1\t2\t3"` with `minimumRowCount: 2`, `minimumColumnCount: 3`.
* **Operations**: Set column 0 width (180), row 0 height (36), cell wrap at `(0, 2)`. Perform `Transpose()`.
* **Assertions**:
  * Serialized output is `"A\t1\r\nB\t2\r\nC\t3"`.
  * `RowCount` becomes `3`, `ColumnCount` becomes `2`.
  * `MinimumRowCount` becomes `3`, `MinimumColumnCount` becomes `2`.
  * Column widths and row heights are reset to `null`.
  * Cell wrap coordinate `(0, 2)` shifts to `(2, 0)`.

### 12. `WrappedCells_MoveWithInsertedMovedAndDeletedRowsAndColumns()`
* **Purpose**: Confirms that wrapped cell coordinates accurately track location updates across row/column insertions, moves, and deletions.
* **Input**: 3x3 matrix with wrapped cell at `(1, 1)`.
* **Operations & Assertions**:
  1. Insert row 1, insert column 1 $\rightarrow$ Wrapped cell shifts to `(2, 2)`.
  2. Move row 2 to 0, move column 2 to 0 $\rightarrow$ Wrapped cell shifts to `(0, 0)`.
  3. Delete row 0, delete column 0 $\rightarrow$ Wrapped cell is removed; `WrappedCells` is empty.

---

## Summary of Exercised API Methods

The test suite directly exercises the following members of `EditTextTableDocument`:

| Category | API Members Tested |
| :--- | :--- |
| **Factory Methods** | `CreateFromText()`, `TryDeserialize()` |
| **Serialization** | `SerializeToText()`, `SerializeToJson()` |
| **Document Structure** | `InsertRow()`, `InsertColumn()`, `DeleteRow()`, `DeleteColumn()`, `MoveRow()`, `MoveColumn()`, `Transpose()` |
| **Properties** | `Format`, `NewLineSequence`, `ColumnNames`, `Rows`, `RowCount`, `ColumnCount`, `MinimumRowCount`, `MinimumColumnCount`, `ColumnWidths`, `RowHeights`, `WrappedCells` |
| **View Formatting** | `SetColumnWidth()`, `SetRowHeight()`, `SetCellWrap()`, `IsCellWrapped()`, `ApplyViewMetricsFrom()` |