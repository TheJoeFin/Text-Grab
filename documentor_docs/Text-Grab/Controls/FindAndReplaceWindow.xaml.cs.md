# Developer Documentation: `FindAndReplaceWindow.xaml.cs`

## Overview

The `FindAndReplaceWindow` class (located in namespace `Text_Grab.Controls`) provides the code-behind and interaction logic for the Find and Replace dialog in the Text-Grab application. Inheriting from `Wpf.Ui.Controls.FluentWindow`, it provides robust text searching, regular expression evaluation, cell-based spreadsheet searching, match replacement/deletion, pattern extraction, template execution, and integration with an associated `EditTextWindow`.

---

## Class Architecture & Static Commands

### Static Routed Commands
The window exposes static `RoutedCommand` fields for binding UI actions:
* `CopyMatchesCmd` – Copies selected or all search match texts into a new `EditTextWindow`.
* `DeleteAllCmd` – Deletes matched instances from the target text or spreadsheet cells.
* `ExtractPatternCmd` – Generates regex patterns based on text selected in the target editor.
* `ReplaceAllCmd` – Replaces all matches (or selected matches) with specified replacement text.
* `ReplaceOneCmd` – Replaces a single selected match instance.
* `TextSearchCmd` – Executes a text search manually.

### Primary Fields & Timers
* `ChangeFindTextTimer` (`DispatcherTimer`): A 400ms debouncing timer used when find text changes.
* `PrecisionSliderTimer` (`DispatcherTimer`): A 300ms debouncing timer used when modifying pattern precision levels.
* `Matches` (`MatchCollection?`): Holds standard regex matches found in source text.
* `stringFromWindow` (`string`): Cached text extracted from the target editor.
* `textEditWindow` (`EditTextWindow?`): Reference to the parent/target text editor window.
* `extractedPattern` (`ExtractedPattern?`): Holds active pattern extraction data when tuning regex precision via the precision slider.

---

## Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| `IsSpreadsheetSearch` | `bool` (private) | Evaluates whether `textEditWindow` is in spreadsheet mode. |
| `IsSmartPatternSearch` | `bool` (private) | Returns `true` if `SearchBar.SelectedPattern` is a `PatternKind.Recognizer` pattern. |
| `FindResults` | `List<FindResult>` | Collection of search results formatted for UI display. |
| `StringFromWindow` | `string` | Gets or sets the cached target string. |
| `TextEditWindow` | `EditTextWindow?` | Associated editor window; updates event subscriptions (`TextChanged`, `EditorModeChanged`) upon assignment. |
| `Pattern` | `string?` (private) | Active pattern string evaluated during text searches. |

---

## Key Workflows & Execution Logic

### 1. Text Searching (`SearchForText`)
The core search dispatcher evaluates search parameters based on active search modes:

1. **Spreadsheet Search**: If `IsSpreadsheetSearch` is `true`, routes search execution to `SearchSpreadsheetCells()`.
2. **Recognizer Search**: If a recognizer pattern is selected (`IsSmartPatternSearch`), delegates search logic to `SearchByRecognizer()`.
3. **Regex Auto-Anchoring**: If the search string starts with `^` and ends with `$`, regex mode is automatically enabled and anchors are stripped.
4. **Special Character Escaping**: When regex mode is off, non-regex special characters are escaped using `TextSearchUtilities.EscapeSpecialRegexChars()`.
5. **Regex Execution & Exception Handling**:
   * Creates a regex query via `TextSearchUtilities.CreateFindAndReplaceSearchRegex()`.
   * Catches `RegexMatchTimeoutException` (>5 seconds) and displays a `Wpf.Ui.Controls.MessageBox`.
   * Catches generic exceptions and updates the match count display text with the exception details.
6. **Result Population**: Populates `FindResults` with `FindResult` instances containing match index, raw text, single-line left/right previews, and count indices. Pops selection into `textEditWindow` for the first match if the find window is focused.

### 2. Recognizer Searches (`SearchByRecognizer`)
Executes built-in entity recognition using `RecognizerExecutor.GetMatches()`:
* Optional narrowing: Keeps only matches containing `narrowText` (case-insensitive) if additional text is typed alongside the recognizer chip.
* Sets `Matches = null`, disabling standard regex operations while displaying formatted match results in `FindResults`.

### 3. Spreadsheet Operations (`SearchSpreadsheetCells`)
Performs searches across spreadsheet cells via the associated `EditTextWindow`:
* Commits active edits and syncs cell data prior to execution.
* Evaluates cells using regex patterns or recognizer patterns.
* Selects and navigates to target cell coordinates (`RowIndex`, `ColumnIndex`) in `EditTextWindow`.

---

## Manipulation Operations (Replace, Delete, Copy)

### Single & Bulk Replacement
* **`Replace_Executed`**: Selects current `FindResult` item or match in `textEditWindow` (or single cell in spreadsheet mode) and replaces it with `ReplaceTextBox.Text`.
* **`ReplaceAll_Executed`**: Replaces matches across all instances or multi-selected items in `ResultsListView`:
  * *Text Mode*: Replaces text in reverse order (iterating from last index to first) on a `StringBuilder` copy of the full document to preserve offsets during modification.
  * *Spreadsheet Mode*: Invokes `textEditWindow.ReplaceInSpreadsheetCells()`.

### Deletion (`DeleteAll_Executed`)
Deletes targeted or all matches:
* Iterates backward through selected/all `FindResult` instances and removes matched index ranges using `StringBuilder.Remove()`.
* In spreadsheet mode, substitutes target matches with `string.Empty`.

### Copying Matches (`CopyMatchesCmd_Executed`)
* Gathers raw match strings via internal helper `GetMatchTextForEditing()`.
* Opens a new `EditTextWindow` and populates it with newline-delimited match outputs.

---

## Pattern Extraction & Precision Slider

### Pattern Extraction (`ExtractPattern_Executed`)
1. Reads selected text from `textEditWindow.PassedTextControl.SelectedText`.
2. Constructs an `ExtractedPattern` instance configured for case sensitivity matching `SearchBar.ExactMatch`.
3. Generates a pattern based on `PrecisionSlider.Value`.
4. Enables regex mode, sets `SearchBar.SearchText`, and displays `PrecisionSliderPanel`.

### Precision Adjustments (`PrecisionSlider_ValueChanged`)
1. Checks that `extractedPattern` is present and `SearchBar.UseRegex` is enabled.
2. Retrieves pre-generated pattern levels using `extractedPattern.GetPattern(precisionLevel)`.
3. Starts `PrecisionSliderTimer` to debounce search updates.

---

## Text-Only Grab Template Execution

### Applying Templates to Matches (`ApplyTemplateToMatchesAsync`)
Applies custom text transformations using `GrabTemplate`:
1. Validates that text-only templates are selected from the dropdown menu (`ApplyTemplateButton_Click`).
2. Iterates backward over targeted `FindResult` items (`OrderByDescending(r => r.Index)`).
3. Evaluates template output per match using `GrabTemplateExecutor.ApplyTextOnlyTemplate()`.
4. Replaces original match text ranges in `textEditWindow` and updates usage metrics via `GrabTemplateManager.RecordUsage()`.

---

## Customization & Saved Regex Patterns

* **Save Pattern Logic (`SavePatternButton_Click`)**:
  * Opens `RegexManager` and sends pattern text alongside single-line preview source text.
  * `UpdateSaveButtonVisibility()` checks if regex mode is on, text is present, and `IsPatternAlreadySaved()` evaluates false against stored regexes loaded from `AppUtilities.TextGrabSettingsService.LoadStoredRegexes()`.
* **Inline Flag Toggling (`OptionsChangedRefresh`)**:
  * Toggles case-sensitivity flags (`(?i)` and `(?-i)`) dynamically within existing regex search patterns when exact match controls are toggled.

---

## Lifecycle & UI State Management

* **Window Initialization**: Configures debounce timer intervals and events.
* **Loading Overlay**: `SetWindowToLoading()` disables `MainContentGrid` and toggles `LoadingSpinner` visibility during background tasks.
* **Cleanup (`Window_Closed`)**: Unsubscribes from `DispatcherTimer` tick events and detaches `TextChanged` / `EditorModeChanged` event handlers from `textEditWindow`.
* **Keyboard Shortcuts (`Window_KeyUp` / `FindTextBox_KeyUp`)**:
  * `Escape`: Clears search bar text if present; closes the window if search text is already empty.
  * `Enter`: Immediately triggers `SearchForText()` without waiting for timer debounce. Typing clears active `extractedPattern` references.