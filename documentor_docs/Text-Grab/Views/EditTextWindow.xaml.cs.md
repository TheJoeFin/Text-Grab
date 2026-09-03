# Text-Grab Technical Documentation: `EditTextWindow.xaml.cs`

## Overview

The `EditTextWindow` class (namespace `Text_Grab`, declared as partial with `Wpf.Ui.Controls.FluentWindow`) represents the primary text editing and manipulation window in Text-Grab. It provides an environment for editing raw text, structured spreadsheet data, and Markdown documents.

Key features implemented in this file include:
- Multi-mode text editing (**Text**, **Spreadsheet**, and **Markdown**).
- Advanced text transformation utilities (case conversion, regex operations, line manipulation, template execution, character analysis).
- Dynamic spreadsheet cell/row/column management with custom tab/CSV handling, HTML table pasting, transposing, splitting, and undo/redo support.
- Integrated live calculation pane and numeric aggregate reporting (Sum, Average, Median, Count, Min, Max, Product).
- Windows AI integration for summarizing, rewriting, translating, converting text to tables, and extracting regex patterns.
- Automated bulk OCR processing over entire directories of visual documents.
- Window management, clipboard synchronization, persistent user settings, and history tracking.

---

## Class Architecture & State Management

### Fields and Properties

#### Routed Commands
`EditTextWindow` defines several static `RoutedCommand` fields exposed via `GetRoutedCommands()` for binding shortcuts and UI triggers:
- Selection Commands: `DeleteAllSelectionCmd`, `DeleteAllSelectionPatternCmd`, `InsertSelectionOnEveryLineCmd`, `IsolateSelectionCmd`, `SingleLineCmd`, `SplitOnSelectionCmd`, `SplitAfterSelectionCmd`, `ToggleCaseCmd`, `ReplaceReservedCmd`, `UnstackCmd`, `UnstackGroupCmd`.
- Tool & Feature Commands: `LaunchCmd`, `MakeQrCodeCmd`, `OcrPasteCommand`, `TransposeTableCmd`, `WebSearchCmd`, `DefaultWebSearchCmd`.

#### Editor Modes & State Flags
- `editorMode`: Managed via `EtwEditorMode` (`Text`, `Spreadsheet`, or `Markdown`).
- `spellCheckMode`: Managed via `SpellCheckMode` (`Auto`, `AlwaysOn`, `Off`).
- Document Sync Flags: `isSyncingTextFromSpreadsheet`, `isSyncingTextFromMarkdown`, `isApplyingSpreadsheetLayout`, `isApplyingMarkdownDocument`, `isLoadingOpenedFile`.
- File State: `OpenedFilePath`, `savedFileText`, `hasPendingFileEdits`, `isShowingPendingFileClosePrompt`, `allowCloseAfterPendingFilePrompt`.

#### Document & Data Containers
- **Raw Text**: `PassedTextControl` (`TextBox`).
- **Spreadsheet**: `SpreadsheetDataGrid` (`DataGrid`), `tableDocument` (`EditTextTableDocument`), `spreadsheetTable` (`DataTable`), `spreadsheetUndoHistory` (`SpreadsheetUndoHistory`), and `trackedSpreadsheetColumns` (`List<DataGridColumn>`).
- **Markdown**: `MarkdownEditorControl` (`RichTextBox`).

#### Calculation & Aggregate State
- `_calculationService`: Instance of `CalculationService` for evaluating math expressions in text asynchronously.
- `_debounceTimer`: A `DispatcherTimer` (300 ms) used to throttle calculation updates during text typing.
- `_selectedAggregate`: An `AggregateType` enum (`None`, `Sum`, `Average`, `Median`, `Count`, `Min`, `Max`, `Product`).

---

## Key Functional Modules

### 1. Multi-Mode Editor Architecture (`SetEditorMode`)

`EditTextWindow` supports three distinct editing modes. When `SetEditorMode(EtwEditorMode mode)` is called:
1. **Spreadsheet Mode (`EtwEditorMode.Spreadsheet`)**:
   - Ensures an `EditTextTableDocument` is instantiated from the current text content.
   - Detaches previous controls and builds a WPF `DataTable` to bind to `SpreadsheetDataGrid`.
   - Manages custom cell wrapping styles (`SpreadsheetCellTextWrappingConverter`), column widths, and row heights.
2. **Markdown Mode (`EtwEditorMode.Markdown`)**:
   - Converts raw text to a WPF `FlowDocument` using `MarkdownDocumentUtilities.CreateFlowDocument`.
   - Applies active theme rules (light/dark mode) and text-wrapping configurations.
   - Features real-time promotion of inline/block Markdown syntax as the user types or pastes formatted content.
3. **Raw Text Mode (`EtwEditorMode.Text`)**:
   - Syncs any changes from spreadsheet or Markdown representations back into `PassedTextControl`.
   - Displays `PassedTextControl` and hides grid/rich-text controls.

---

### 2. Spreadsheet Operations & Data Grid Handling

When operating in Spreadsheet Mode, `EditTextWindow` provides full structured tabular editing capabilities:

- **Row and Column Manipulation**: Methods like `AddSpreadsheetColumnMenuItem_Click`, `AddSpreadsheetRowMenuItem_Click`, `DeleteSpreadsheetColumnMenuItem_Click`, `DeleteSpreadsheetRowMenuItem_Click`, `MoveSpreadsheetColumn`, and `MoveSpreadsheetRow` modify the underlying `EditTextTableDocument` model and rebuild the grid.
- **Transposing**: `TransposeTableExecuted` transposes rows and columns using `EditTextTableDocument.Transpose()`.
- **Column Splitting**: `SplitSelectedSpreadsheetCells` splits column values based on custom rules defined via `SplitColumnWindow` and `ColumnSplitUtilities`.
- **Cell Selection and Editing**: Tracks selected coordinates in `selectedSpreadsheetCellCoordinates`. Directly handles editing lifecycle hooks (`BeginningEdit`, `CellEditEnding`, `PreparingCellForEdit`).
- **Clipboard Integration**:
  - `PasteIntoSpreadsheet()` extracts standard tab-separated values or HTML tables from the clipboard using `ClipboardUtilities.TryGetHtmlTableAsTabSeparated()`.
  - `CopySpreadsheetAsMarkdownMenuItem_Click()` exports selected cells into formatted Markdown table syntax via `BuildSpreadsheetSelectionMarkdown`.
- **Undo / Redo System**: Captures table snapshots via `SpreadsheetUndoState` serialized JSON payloads through `spreadsheetUndoHistory`.

---

### 3. Text Processing & Transformations

`EditTextWindow` contains extensive utility operations applied to `PassedTextControl` or cell selections:

- **Case Toggling**: `ToggleCase()` cycles selected text through Lower, Title/Camel, and Upper case using `CultureInfo.TextInfo`.
- **Line Manipulations**:
  - `MoveLineUp` / `MoveLineDown`: Moves selected lines up or down within the text body.
  - `DuplicateSelectedLine`: Clones the line at the current caret.
  - `AddedLineAboveCommand`: Inserts a new line above the current cursor line.
  - `TrimEachLineMenuItem_Click`, `RemoveDuplicateLines_Click`, `ShuffleLinesMenuItem_Click`, `SingleLineCmdExecuted`, `UnstackExecuted`, `UnstackGroupExecuted`.
- **Regex & Pattern Execution**:
  - `ApplyPatternItem_Click` and `ApplyPatternPerLineItem_Click`: Apply predefined or custom `PatternItem` definitions.
  - Pattern extraction and precision tweaking: `PatternButton_MouseWheel` dynamically adjusts regex precision levels (`currentPrecisionLevel`) on `ExtractedPattern` instances, offering live animation visual feedback (`AnimatePrecisionChange`).
- **Template Execution**: Executes text-only `GrabTemplate` items globally or per-line.
- **Character Inspector**: `CharDetailsButton_Click` opens a pop-up inspect window providing character details, codepoints, and Unicode category details via `CharacterUtilities`.

---

### 4. Live Calculation Pane & Aggregate Calculations

When enabled via `ShowCalcPaneMenuItem` or `ToggleCalcPaneExecuted`:
- The window displays `CalcResultsTextControl` side-by-side with the main editor, split by `TextBoxSplitter`.
- **Throttled Evaluation**: `DebounceTimer_Tick` calls `_calculationService.EvaluateExpressionsAsync(input)` after 300 ms of inactivity.
- **Vertical Scroll Mirroring**: `PassedTextControl_ScrollChanged` and `SyncCalcScrollToMain` synchronize vertical scrolling between the editor and the calculation results control.
- **Aggregate Calculations**: Evaluates numbers extracted from raw text or spreadsheet cell selections. Users can select aggregate metrics (Sum, Average, Median, Count, Min, Max, Product) via context menus (`UpdateCalcAggregates`), which update `CalcAggregateStatusText` and copy values to the clipboard when clicked.

---

### 5. Windows AI Integration

`EditTextWindow` leverages `WindowsAiUtilities` when `WindowsAiUtilities.CanDeviceUseWinAI()` returns true:
- **Summarize**: `SummarizeMenuItem_Click` calls `WindowsAiUtilities.SummarizeParagraph`.
- **Rewrite**: `RewriteMenuItem_Click` calls `WindowsAiUtilities.Rewrite`.
- **Table Generation**: `ConvertTableMenuItem_Click` calls `WindowsAiUtilities.TextToTable`.
- **Translation**: Supports multiple target languages (e.g., English, Spanish, French, German, Italian, Portuguese, Russian, Japanese, Chinese, Korean, Arabic, Hindi) as well as automatic translation to the system language via `TranslateToSystemLanguageMenuItem_Click`.
- **Regex Extraction**: `ExtractRegexMenuItem_Click` submits natural language descriptions to generate regex patterns via AI, displaying pattern explanations via `ExplainRegexPattern()`.

---

### 6. Directory OCR Engine

`EditTextWindow` can perform batch OCR on image and document files within a selected directory using `OcrAllImagesInFolder`:
- Recursively or non-recursively finds compatible visual files (`IoUtilities.IsVisualDocumentFileExtension`).
- Runs parallel OCR tasks via `Parallel.ForEachAsync` throttled by parallelism degree settings (single-threaded for Tesseract, multi-threaded for Windows OCR).
- Supports cancellation token handling tied to the `Escape` key (`cancellationTokenForDirOCR`).
- Can optionally append results to the main text box or write individual text files for each image.

---

### 7. File Management & Close Workflow

- **File Open / Drag & Drop**:
  - `OpenPath`: Handles reading content via `IoUtilities.GetContentFromPath`, setting file mode based on file extensions.
  - Drag-and-drop support (`ETWindow_DragOver`, `ETWindow_Drop`) handles files and folders dropped directly onto the window.
- **Dirty State Tracking**: `ShouldShowPendingFileEdits` checks if the current text matches `savedFileText`. If edits exist, an asterisk (`*`) is prepended to the title bar name via `GetWindowTitle`.
- **Close Interception**: `Window_Closing` intercepts closing when pending edits exist, triggering `PromptForPendingFileEditsAsync()` which presents a dialog (`ContentDialog`) offering:
  - **Save**: Saves back to `OpenedFilePath` or opens `SaveFileDialog`.
  - **Don't Save**: Discards changes.
  - **Save to History**: Writes the state into Text-Grab's history storage service (`HistoryService`).
  - **Cancel**: Aborts window close.

---

### 8. UI Commands, Context Menus & Dynamic Submenus

- **Routed Commands Binding**: `SetupRoutedCommands()` registers standard WPF application commands (`Undo`, `Redo`, `Cut`, `Copy`, `Paste`) along with custom shortcuts (e.g., `Ctrl+F` for fullscreen grab, `Ctrl+G` for grab frame, `Ctrl+L` for line selection, `Ctrl+S` for saving).
- **Dynamic Right-Click Context Menus**: `PassedTextControl_ContextMenuOpening` dynamically inserts context-sensitive items into the right-click menu:
  - Spelling suggestions (`AddPossibleSpellingErrorsToRightClickMenu`).
  - Web URLs (`AddPossibleURLToRightClickMenu`).
  - Email addresses (`AddPossibleMailToToRightClickMenu`).
- **Dynamic Submenu Population**: Submenus for recent text history, recent grabs, PDF history, OCR languages, and web search engines are populated dynamically on opening (`MenuItem_SubmenuOpened`, `CaptureMenuItem_SubmenuOpened`).

---

## Spell Check Logic

Spell checking performance is controlled via `ShouldEnableSpellCheck`:
```csharp
internal static bool ShouldEnableSpellCheck(string text)
```
- **Disabling Threshold**: Spell checking automatically turns off if document length exceeds **10,000 characters** (`SpellCheckDisableThreshold`).
- **Token Analysis**: Automatically disables spell checking if text contains **3 or more** unspaced tokens exceeding **25 characters** (`SpellCheckLongWordLength`), preventing UI hangs on large encoded strings, base64 blobs, or GUIDs.
- Override modes (`AlwaysOn`, `Off`, `Auto`) can be configured via settings.

---

## Summary of Window Lifecycle Events

| Event | Logic Executed |
| :--- | :--- |
| `Window_Initialized` | Binds font settings, mouse wheel zoom handlers, and window title formatting. |
| `Window_Loaded` | Binds routed commands, attaches Win32 message hooks (`WmMouseHWheel`), initializes calculation timers, restores window positions/settings, and registers clipboard listeners. |
| `Window_Activated` | Re-checks spell check mode, refreshes Markdown themes, and restores control focus based on `editorMode`. |
| `Window_Closing` | Syncs active editor state, checks pending file edit state, prompts user if unsaved, and records window state to history if needed. |
| `Window_Closed` | Detaches event handlers, removes window message hooks, cleans up dynamic menu items, updates persistent user settings (`EditTextWindowSizeAndPosition`, `CalcPaneWidth`), and triggers garbage collection. |