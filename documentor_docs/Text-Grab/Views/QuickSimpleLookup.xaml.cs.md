# Technical Documentation: `QuickSimpleLookup.xaml.cs`

## Overview

The `QuickSimpleLookup` class is a WPF view derived from `Wpf.Ui.Controls.FluentWindow` located in the `Text_Grab.Views` namespace. It provides a quick search, lookup, and action dashboard for saved key-value text pairs, application history, grab templates, links, dynamic commands, and PowerShell CLI operations.

Users can quickly filter stored data, edit rows within a WPF `DataGrid`, save custom lookup entries to CSV files, copy values to the clipboard, insert text directly into other controls, or trigger native operations like launching URLs or full-screen screen grabs.

---

## Class Hierarchy & Declaration

```csharp
namespace Text_Grab.Views;

public partial class QuickSimpleLookup : Wpf.Ui.Controls.FluentWindow
```

---

## Fields and Properties

### Fields

| Field | Type | Description |
| :--- | :--- | :--- |
| `DestinationTextBox` | `TextBox?` | An optional destination `TextBox` used to insert text into when triggered from another view. |
| `cacheFilename` | `string` | File name for default local lookup cache storage (`"QuickSimpleLookupCache.csv"`). |
| `isPuttingValueIn` | `bool` | Flag indicating whether a selection execution/insertion process is currently running. |
| `lastSelection` | `LookupItem?` | Stores the previously selected `LookupItem` prior to search field updates. |
| `rowCount` | `int` | Tracks the count of displayed items in the data grid. |
| `valueUnderEdit` | `string` | Captures cell text before editing begins to track changes. |
| `itemUnderEdit` | `LookupItem?` | Tracks the specific `LookupItem` instance being edited. |
| `DefaultSettings` | `Settings` | Static reference to application settings obtained via `AppUtilities.TextGrabSettings`. |
| `lookupItems` | `List<LookupItem>` | Private backing collection of lookup items loaded from CSV storage. |

### Properties

| Property | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `IsEditingDataGrid` | `bool` | `false` | Tracks whether the user is actively editing a row within the `MainDataGrid`. |
| `IsFromETW` | `bool` | `false` | Indicates if `QuickSimpleLookup` was launched from an `EditTextWindow` context. |
| `ItemsDictionary` | `List<LookupItem>` | `[]` | Holds the complete collection of active lookup items, history items, and templates. |

---

## Item Parsing and Data Processing

### `ParseStringToLookupItem`
Static method that parses a raw string row into a `LookupItem` object based on prefix syntax and delimiter character:

```csharp
private static LookupItem ParseStringToLookupItem(char splitChar, string row)
```

- **Kind Determination Rules**:
  - `>` prefix $\rightarrow$ `LookupItemKind.Command`
  - `http` or `🔗` prefix $\rightarrow$ `LookupItemKind.Link`
  - `⚡` prefix $\rightarrow$ `LookupItemKind.Dynamic`
  - Default $\rightarrow$ `LookupItemKind.Simple`
- **Cell Extraction**: Splits input by `splitChar` (`\t` or `,`). The first token becomes `ShortValue`, and remaining tokens are combined to form `LongValue`.

### `ParseStringToRows`
Splits multi-line text by newline characters and yields parsed `LookupItem` elements based on whether the string source is CSV (`,`) or tab-delimited (`\t`).

---

## Core Operations & Methods

### Data Loading & Population

#### `LoadDataGridContent`
```csharp
private async Task LoadDataGridContent(string csvToOpenPath)
```
- Reads text from the specified CSV file path or default cache file using `FileUtilities.GetTextFileAsync`.
- If the file content is empty or whitespace, calls `PopulateSampleData()` to pre-fill basic usage examples.
- Parses CSV lines into `ItemsDictionary` and initializes `lookupItems`.
- Conditionally appends history items (`AddHistoryItemsToItemsDictionary`) and grab templates (`AddGrabTemplatesToItemsDictionary`).
- Binds the result set to `MainDataGrid.ItemsSource`.

#### `AddHistoryItemsToItemsDictionary` & `AddGrabTemplatesToItemsDictionary`
- Fetches text, recent image grabs, and recent PDF document items from `Singleton<HistoryService>.Instance` and converts them to `LookupItem` objects.
- Fetches templates from `GrabTemplateManager.GetAllTemplates()` and adds them as `LookupItemKind.GrabTemplate`.

---

### Search and Filtering

`SearchBar_SearchChanged` triggers `ReSearch(string searchString)` on user input.

#### Search Modes in `ReSearch`

1. **Pattern Search (`PatternSearch`)**:
   Uses `SearchBar.SelectedPattern` with `PatternExecutor.HasMatch` to evaluate items.
2. **Regex Search (`RegexSearch`)**:
   Evaluates search strings as `Regex` patterns (case-insensitive). Updates regex validity UI indicators via `SearchBar.SetRegexValidity()`. Matches item string representation or initial-letter abbreviations (`FirstLettersString`).
3. **Standard Search (`StandardSearch`)**:
   Splits input into space-separated terms and requires all terms to be present in either `LookupItem.ToString()` or match `FirstLettersString`.

---

### Action Execution: `PutValueIntoClipboard`

```csharp
private async void PutValueIntoClipboard(KeyboardModifiersDown? keysDown = null)
```

Handles row selection execution based on keyboard modifier flags or current UI settings:

- **Keyboard Modifier Actions**:
  - **`Ctrl + Shift` / `Ctrl + Shift + Alt`**: Copies all currently filtered search results into the clipboard.
  - **`Ctrl + Alt`**: Tries parsing `LongValue` as an absolute URL and launches it in the system default browser via `Process.Start`.
  - **`Ctrl`**:
    - If a `GrabTemplate` item is selected, opens a new `GrabFrame` with that template.
    - Otherwise, copies `ShortValue` of selected items to clipboard.
  - **`Shift`**: Copies full `ToString()` representation of selected items to clipboard.
  - **Default (No modifiers)**:
    - `LookupItemKind.EditWindow`: Opens an `EditTextWindow` using the item's `HistoryInfo`.
    - `LookupItemKind.GrabFrame` / `LookupItemKind.PdfDocument`: Opens a `GrabFrame` loaded with historical capture data.
    - `LookupItemKind.Link`: Launches URL in default browser via `Process.Start`.
    - `LookupItemKind.Command`: Runs PowerShell CLI command via `RunCli(string longValue)`.
    - `LookupItemKind.GrabTemplate`: Hides window and launches `WindowUtilities.LaunchFullScreenGrab`.
    - Default/`Simple`: Appends `LongValue` to clipboard buffer.

- **Insertion & Output Dispatching**:
  - Direct insertion into `DestinationTextBox` if present and `EditWindowToggleButton` is checked.
  - Inserts string into active focused system input via `WindowUtilities.TryInsertString` if `PasteToggleButton` is checked.
  - Opens `EditTextWindow` if `EditWindowToggleButton` is checked.

---

### CLI Command Execution: `RunCli`

```csharp
private async Task<bool> RunCli(string longValue)
```
- Splits `longValue` string into shell arguments.
- Spawns PowerShell asynchronously using `CliWrap.Cli.Wrap("powershell")`.
- Captures standard output and standard error using `ExecuteBufferedAsync`.
- Displays execution output back in `MainDataGrid` as a newly wrapped `LookupItem`.

---

### Data Editing and Persistence

- **Cell Editing**: Managed via `MainDataGrid_BeginningEdit` and `MainDataGrid_CellEditEnding`. Updates backing `lookupItems` collection upon completion and toggles save button visibility (`SaveBTN.Visibility`).
- **Deleting Rows**: `RowDeleted()` removes selected rows from data sources (`MainDataGrid.ItemsSource`, `lookupItems`, `ItemsDictionary`) and calls `HistoryService` to remove stored items if applicable.
- **Saving to CSV (`WriteDataToCSV`)**:
  - Converts items in `lookupItems` to CSV format (`ToCSVString()`).
  - Writes to `DefaultSettings.LookupFileLocation` or fallback cache file using `FileUtilities.SaveTextFile`.

---

## Keyboard Shortcuts Summary

Keyboard inputs are captured via `QuickSimpleLookup_PreviewKeyDown`:

| Key Shortcut | Action |
| :--- | :--- |
| `Enter` | Trigger `PutValueIntoClipboard()` or add new tabbed row if input contains tab (`\t`). |
| `Escape` | Clears search bar if populated; closes window if empty (`ClearOrExit()`). |
| `Delete` | Deletes selected row(s) when focus is not in search bar or edit mode (`RowDeleted()`). |
| `Down Arrow` | Moves focus from search bar into `MainDataGrid`. |
| `Ctrl + Q` | Focuses the search input `SearchBar.TextBox`. |
| `Ctrl + S` | Asynchronously saves current lookup data to CSV file. |
| `Ctrl + F` | Launches full-screen text grab mode. |
| `Ctrl + I` | Toggles insert string mode (`PasteToggleButton`). |
| `Ctrl + E` | Toggles edit window output mode (`EditWindowToggleButton`). |
| `Ctrl + R` | Toggles Regex search mode on search bar. |
| `Home` | Moves selection to the first row in data grid. |
| `End` | Moves selection to the last row in data grid. |

---

## External & Internal Dependencies

- **Frameworks & UI**: WPF, `Wpf.Ui.Controls.FluentWindow`, `System.Windows.Controls.DataGrid`.
- **CliWrap**: Asynchronous CLI execution wrapper (`CliWrap`, `CliWrap.Buffered`).
- **Internal Services & Utilities**:
  - `Text_Grab.Services.HistoryService` (History retrieval and management)
  - `Text_Grab.Utilities.FileUtilities` (File I/O abstraction)
  - `Text_Grab.Utilities.WindowUtilities` (Window management, full-screen grab triggering, direct input insertion)
  - `Text_Grab.Utilities.GrabTemplateManager` (Template processing)
  - `Text_Grab.Models.LookupItem` / `LookupItemKind` (Data models)