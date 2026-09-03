# Technical Documentation: `Tests/EditTextWindowFileStateTests.cs`

## Overview

The `EditTextWindowFileStateTests` class contains unit tests written in xUnit to validate file-state-related helper methods in the `EditTextWindow` class of the `Text_Grab` application.

This test suite focuses on verifying how `EditTextWindow` manages:
- Window title updates based on open file paths and pending edits.
- Pending file edit detection logic.
- Default file extension resolution during save operations.
- Save file dialog filter index selection.

---

## Namespace and Dependencies

**Namespace:** `Tests`

**Imports:**
- `Text_Grab` — Contains the `EditTextWindow` class being tested.
- `Text_Grab.Models` — Contains model types such as `EtwEditorMode`, `EtwStructuredTextFormat`, and `EditTextTableDocument`.

**Testing Framework:** xUnit (`[Theory]`, `[InlineData]`, `Assert.Equal`)

---

## Class Architecture

```
Tests.EditTextWindowFileStateTests
 ├── GetWindowTitle_ReflectsTrackedFileAndPendingEdits(...)
 ├── ShouldShowPendingFileEdits_RequiresTrackedFileAndChangedText(...)
 ├── GetDefaultSaveExtension_MatchesEditorMode(...)
 └── GetSaveDocumentFilterIndex_MatchesEditorMode(...)
```

---

## Unit Test Methods

### 1. `GetWindowTitle_ReflectsTrackedFileAndPendingEdits`

#### Signature
```csharp
[Theory]
[InlineData(null, false, "Edit Text")]
[InlineData("", true, "Edit Text")]
[InlineData(@"C:\Temp\notes.md", false, "Edit Text | notes.md")]
[InlineData(@"C:\Temp\notes.md", true, "Edit Text | *notes.md")]
public void GetWindowTitle_ReflectsTrackedFileAndPendingEdits(string? path, bool hasPendingEdits, string expectedTitle)
```

#### Purpose
Verifies that `EditTextWindow.GetWindowTitle(path, hasPendingEdits)` produces the correct window title string based on whether a file is tracked and whether there are unsaved changes.

#### Tested Behavior
- **Null or Empty File Path:** Defaults to `"Edit Text"`, regardless of `hasPendingEdits`.
- **Tracked File Path Without Pending Edits:** Appends ` | <filename>` to `"Edit Text"` (e.g., `"Edit Text | notes.md"`).
- **Tracked File Path With Pending Edits:** Prefixes the filename with an asterisk `*` (e.g., `"Edit Text | *notes.md"`).

---

### 2. `ShouldShowPendingFileEdits_RequiresTrackedFileAndChangedText`

#### Signature
```csharp
[Theory]
[InlineData(null, "saved", "changed", false)]
[InlineData("", "saved", "changed", false)]
[InlineData(@"C:\Temp\notes.md", "same", "same", false)]
[InlineData(@"C:\Temp\notes.md", "same", "changed", true)]
public void ShouldShowPendingFileEdits_RequiresTrackedFileAndChangedText(string? path, string savedText, string currentText, bool expected)
```

#### Purpose
Verifies `EditTextWindow.ShouldShowPendingFileEdits(path, savedText, currentText)` to ensure the pending edit state is only active when a valid file path exists and the current text differs from the saved text.

#### Tested Behavior
- **Null or Empty Path:** Returns `false` even if current text differs from saved text.
- **Identical Saved and Current Text:** Returns `false` even if a valid file path is provided.
- **Valid File Path and Different Text:** Returns `true`.

---

### 3. `GetDefaultSaveExtension_MatchesEditorMode`

#### Signature
```csharp
[Theory]
[InlineData(null, EtwEditorMode.Text, null, null, ".txt")]
[InlineData(null, EtwEditorMode.Markdown, null, null, ".md")]
[InlineData(null, EtwEditorMode.Spreadsheet, null, null, ".tsv")]
[InlineData(null, EtwEditorMode.Spreadsheet, EtwStructuredTextFormat.Csv, ",", ".csv")]
[InlineData(null, EtwEditorMode.Spreadsheet, EtwStructuredTextFormat.Tsv, "\t", ".tsv")]
[InlineData(null, EtwEditorMode.Spreadsheet, EtwStructuredTextFormat.DelimitedText, ",", ".csv")]
[InlineData(null, EtwEditorMode.Spreadsheet, EtwStructuredTextFormat.DelimitedText, "|", ".tsv")]
[InlineData(@"C:\Temp\notes.markdown", EtwEditorMode.Text, null, null, ".markdown")]
[InlineData(@"C:\Temp\data.json", EtwEditorMode.Markdown, null, null, ".json")]
public void GetDefaultSaveExtension_MatchesEditorMode(
    string? openedFilePath,
    EtwEditorMode editorMode,
    EtwStructuredTextFormat? format,
    string? delimiter,
    string expectedExtension)
```

#### Purpose
Verifies `EditTextWindow.GetDefaultSaveExtension(openedFilePath, editorMode, tableDocument)` to ensure the correct file extension is selected based on the editor mode, structured text settings, and opened file path.

#### How It Works
1. If `format` is provided (`format.HasValue`), an `EditTextTableDocument` instance is constructed with `Format = format.Value` and `Delimiter = delimiter ?? "\t"`. Otherwise, `tableDocument` is passed as `null`.
2. Calls `EditTextWindow.GetDefaultSaveExtension(openedFilePath, editorMode, tableDocument)` and compares the result against `expectedExtension`.

#### Tested Scenarios
- **No File Path Provided (`null`):**
  - Mode `Text` $\rightarrow$ `.txt`
  - Mode `Markdown` $\rightarrow$ `.md`
  - Mode `Spreadsheet` (no format specified) $\rightarrow$ `.tsv`
  - Mode `Spreadsheet` with CSV format / `,` delimiter $\rightarrow$ `.csv`
  - Mode `Spreadsheet` with TSV format / `\t` delimiter $\rightarrow$ `.tsv`
  - Mode `Spreadsheet` with DelimitedText format / `,` delimiter $\rightarrow$ `.csv`
  - Mode `Spreadsheet` with DelimitedText format / `|` delimiter $\rightarrow$ `.tsv`
- **File Path Already Exists:** Preserve the original extension (e.g., `.markdown` stays `.markdown`, `.json` stays `.json`).

---

### 4. `GetSaveDocumentFilterIndex_MatchesEditorMode`

#### Signature
```csharp
[Theory]
[InlineData(null, EtwEditorMode.Spreadsheet, 1)]
[InlineData(null, EtwEditorMode.Markdown, 2)]
[InlineData(null, EtwEditorMode.Text, 3)]
[InlineData(@"C:\Temp\sheet.csv", EtwEditorMode.Markdown, 1)]
[InlineData(@"C:\Temp\notes.md", EtwEditorMode.Text, 2)]
[InlineData(@"C:\Temp\notes.txt", EtwEditorMode.Markdown, 3)]
[InlineData(@"C:\Temp\data.json", EtwEditorMode.Text, 4)]
public void GetSaveDocumentFilterIndex_MatchesEditorMode(string? openedFilePath, EtwEditorMode editorMode, int expectedFilterIndex)
```

#### Purpose
Verifies `EditTextWindow.GetSaveDocumentFilterIndex(openedFilePath, editorMode)` to ensure the 1-based index corresponding to the file dialog filter dropdown is selected correctly.

#### Tested Scenarios
- Resolves appropriate 1-based integer indices based on combination of `openedFilePath` extension and active `EtwEditorMode`.

---

## Types and Enums Used in Tests

- **`EtwEditorMode`**: Enum controlling the active mode (`Text`, `Markdown`, `Spreadsheet`).
- **`EtwStructuredTextFormat`**: Enum controlling structured format options (`Csv`, `Tsv`, `DelimitedText`).
- **`EditTextTableDocument`**: Model object containing document table attributes (`Format`, `Delimiter`).