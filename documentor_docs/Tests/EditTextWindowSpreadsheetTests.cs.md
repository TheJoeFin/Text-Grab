# Technical Documentation: `Tests/EditTextWindowSpreadsheetTests.cs`

## Overview

The `EditTextWindowSpreadsheetTests` class contains unit tests for the spreadsheet-related static methods of `EditTextWindow` in the `Text_Grab` project. The tests cover cell value clearing, cut operations, selection extraction (plain text, Markdown, and numbers), cell pattern searching, row height manipulation, text formatting/wrapping, and coordinate fallback logic.

The test suite is written using the **xUnit** framework and operates on .NET data structures, primarily `System.Data.DataTable` and custom domain models like `EditTextTableDocument`.

---

## Dependencies & Imports

- **`System.Data`**: Provides the `DataTable` structure used to mock spreadsheet data grids.
- **`Text_Grab`**: Contains the target class under test (`EditTextWindow`).
- **`Text_Grab.Models`**: Provides data models used by spreadsheet operations, including `EditTextTableDocument`, `PatternItem`, `BuiltInRecognizer`, and `FindResult`.
- **`Xunit`**: Provides testing attributes (`[Fact]`, `[Theory]`, `[InlineData]`) and assertion methods (`Assert`).

---

## Test Categories & Detailed Coverage

### 1. Cell Data Clearing and Cut Operations

#### `ClearSpreadsheetCellValues_ClearsOnlyRequestedCells`
- **Purpose**: Verifies that `EditTextWindow.ClearSpreadsheetCellValues` clears only the specified cell coordinates within a `DataTable`.
- **Behavior Tested**:
  - Handles valid cell coordinates and resets their values to `string.Empty`.
  - Ignores duplicate coordinates without throwing errors.
  - Safely ignores out-of-bounds coordinates (e.g., negative indices like `(-1, 1)` or indices exceeding row/column limits like `(5, 0)` or `(0, 5)`).

#### `TryCutSpreadsheetCellValues_CopiesThenClearsRequestedCells`
- **Purpose**: Tests cutting cell values—copying cell content to a clipboard callback and subsequently clearing those cells from the `DataTable`.
- **Behavior Tested**:
  - Passes formatted text (tab-separated columns and newline-separated rows) to a clipboard handler.
  - Clears specified cells only after the clipboard handler succeeds (returns `true`).
  - Ignores out-of-bounds and duplicate coordinates.

#### `TryCutSpreadsheetCellValues_DoesNotClearWhenClipboardCopyFails`
- **Purpose**: Ensures transaction safety when a clipboard operation fails during a cut sequence.
- **Behavior Tested**:
  - If the provided clipboard action returns `false`, `EditTextWindow.TryCutSpreadsheetCellValues` aborts the operation, returning `false` and leaving the `DataTable` contents unmodified.

---

### 2. Selection Building & Markdown Generation

#### `BuildSpreadsheetSelectionText_IncludesOnlySelectedCells`
- **Purpose**: Tests building a plain-text TSV (tab-separated values) representation of selected cells.
- **Behavior Tested**:
  - Formats selected cell values into tab-delimited columns and newline-delimited rows.
  - Filters out duplicate and out-of-bounds coordinates.

#### `BuildSpreadsheetSelectionMarkdown_BuildsTableFromSelectedCells`
- **Purpose**: Verifies Markdown table syntax generation from selected cell coordinates.
- **Behavior Tested**:
  - Constructs a valid Markdown table with pipe separators (`|`) and a header delimiter row (`| --- | --- |`).

#### `BuildSpreadsheetSelectionMarkdown_EscapesPipesAndNewlines`
- **Purpose**: Ensures special Markdown characters within cell text are properly escaped.
- **Behavior Tested**:
  - Replaces pipe characters (`|`) with `\|`.
  - Replaces newlines (`\r\n`) with HTML break tags (`<br />`).

#### `BuildSpreadsheetSelectionMarkdown_ReturnsEmptyWhenNoValidCells`
- **Purpose**: Tests Markdown generator response when given only invalid coordinates.
- **Behavior Tested**:
  - Returns `string.Empty` when no coordinates fall within valid bounds of the `DataTable`.

---

### 3. Number Extraction & Preview

#### `ExtractSpreadsheetSelectionNumbers_PullsNumericValuesFromSelectedCells`
- **Purpose**: Tests extraction of numeric values from spreadsheet cells containing formatted text.
- **Behavior Tested**:
  - Successfully parses plain integers, currency formats (e.g., `$20.50`), numbers with thousands separators (e.g., `1,234`), and embedded numbers (e.g., `Total: 3,5`).
  - Ignores non-numeric text (`n/a`, `abc`).
  - Deduplicates incoming coordinates.

#### `ExtractSpreadsheetSelectionNumbers_IgnoresNonNumericSelectedCells`
- **Purpose**: Confirms empty results when selected cells contain no parseable numbers.
- **Behavior Tested**: Returns an empty list when cells contain purely non-numeric text or empty strings.

#### `BuildSpreadsheetSelectionNumbersPreviewText_FormatsExtractedNumbersForCalcPane`
- **Purpose**: Verifies preview text generation for calculation displays.
- **Behavior Tested**:
  - Extracts numeric values from selected coordinates and returns them as a newline-separated string preview.

---

### 4. Cell Searching & Pattern Recognition

#### `SearchSpreadsheetDocumentCells_SmartPatternFindsAndNarrowsCellMatches`
- **Purpose**: Tests pattern matching and narrowing search filters within an `EditTextTableDocument`.
- **Behavior Tested**:
  - Searches an `EditTextTableDocument` using a `PatternItem` (e.g., built-in `email` recognizer).
  - Matches pattern items across cells and populates `FindResult` objects with zero-based `RowIndex`, `ColumnIndex`, matching `RawText`, and occurrence count.
  - Narrows pattern search results when a text filter argument (e.g., `"C@D"`) is provided.

---

### 5. Input & Shortcut Handling

#### `ShouldHandleSpreadsheetDeleteKey_RequiresSelectionAndNoInlineEditor`
- **Purpose**: Tests conditional logic determining whether the spreadsheet key handler should intercept the `Delete` key.
- **Parameters Tested**:
  | `selectedCellCount` | `isCellEditorFocused` | Expected Result | Reason |
  | :--- | :--- | :--- | :--- |
  | `1` | `false` | `true` | Has selection, cell editor not focused. |
  | `3` | `false` | `true` | Multiple cells selected, cell editor not focused. |
  | `0` | `false` | `false` | No selection. |
  | `1` | `true` | `false` | Cell editor is active/focused. |

---

### 6. Coordinate Resolution

#### `GetSelectedOrPopulatedSpreadsheetCellCoordinates_PrefersValidSelection`
- **Purpose**: Ensures selected cell coordinates are prioritized when valid selections exist.
- **Behavior Tested**:
  - Filters and returns only valid, bounded, non-duplicate coordinates from the provided list.

#### `GetSelectedOrPopulatedSpreadsheetCellCoordinates_FallsBackToPopulatedCells`
- **Purpose**: Verifies fallback behavior when no valid selected coordinates are supplied.
- **Behavior Tested**:
  - When provided coordinates are all invalid or out-of-bounds, falls back to returning all non-empty, non-whitespace cell coordinates from the `DataTable`.

---

### 7. Document Transformations & Content Manipulation

#### `TransformSpreadsheetDocumentCellValues_TransformsOnlyRequestedCells`
- **Purpose**: Tests updating cell values in an `EditTextTableDocument` via a transformation function.
- **Behavior Tested**:
  - Applies a `Func<string, string>` delegate (e.g., wrapping text in brackets `[value]`) specifically to targeted valid cell coordinates.

#### `SetSpreadsheetDocumentCellValues_SetsOnlyRequestedCells`
- **Purpose**: Tests explicitly assigning values to specific cells in an `EditTextTableDocument`.
- **Behavior Tested**:
  - Takes tuples of `(RowIndex, ColumnIndex, Value)` and updates only matching, valid cells within the document.

---

### 8. Text Wrapping State Management

#### `SetSpreadsheetDocumentCellWrapState_UpdatesOnlyRequestedCells`
- **Purpose**: Verifies updating text-wrap settings on individual cells within an `EditTextTableDocument`.
- **Behavior Tested**:
  - Toggles the wrapping state (`shouldWrap: true/false`) on target coordinates without altering non-targeted cells.

#### `AreSpreadsheetDocumentCellsWrapped_ReturnsTrueOnlyWhenAllValidTargetsAreWrapped`
- **Purpose**: Checks whether all specified valid cells are wrapped.
- **Behavior Tested**:
  - Returns `true` only if every valid targeted cell has wrapping enabled.
  - Returns `false` if any targeted cell is unwrapped.

---

### 9. Row Height Management

#### `ClearSpreadsheetDocumentRowHeights_ClearsOnlyRequestedRows`
- **Purpose**: Verifies resetting explicit row height definitions on an `EditTextTableDocument`.
- **Behavior Tested**:
  - Clears explicit height values (`null`) for specified row indices while retaining heights on unselected rows.
  - Safely ignores out-of-bounds row indices.

#### `GetSpreadsheetPersistedRowHeight_PersistsOnlyExplicitPositiveHeights`
- **Purpose**: Validates row height filtering before persisting state.
- **Parameters Tested**:
  | Input (`double`) | Expected Output (`double?`) | Rule |
  | :--- | :--- | :--- |
  | `24d` | `24d` | Valid positive height |
  | `36.5` | `36.5` | Valid positive decimal height |
  | `double.NaN` | `null` | Non-numeric / invalid height |
  | `double.PositiveInfinity` | `null` | Non-finite height |
  | `0d` | `null` | Zero height |
  | `-10d` | `null` | Negative height |