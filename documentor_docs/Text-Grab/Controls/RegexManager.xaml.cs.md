# Technical Documentation: `RegexManager.xaml.cs`

## Overview

The `RegexManager` class is a WPF `FluentWindow` control within the `Text_Grab.Controls` namespace. It serves as the primary user interface for viewing, managing, testing, and applying regular expression patterns and built-in text recognizers in Text-Grab.

Key capabilities provided by `RegexManager`:
- Storing and loading saved regular expressions (`StoredRegex`) and managing built-in text recognizers (`BuiltInRecognizer`).
- Adding, editing, deleting, and testing custom regex patterns.
- Hiding or unhiding built-in recognizers.
- Passing selected regex patterns directly into the `FindAndReplaceWindow`.
- Generating explanations for regular expression patterns.

---

## Class Architecture & State

### Namespace
`namespace Text_Grab.Controls`

### Hierarchy
`public partial class RegexManager : FluentWindow`

### Properties and Fields

| Name | Type | Access | Purpose |
| :--- | :--- | :--- | :--- |
| `SourceEditTextWindow` | `EditTextWindow?` | `public` | Optional reference to a source `EditTextWindow` initiating actions in this control. |
| `RegexPatterns` | `ObservableCollection<StoredRegex>` | `private` | The working in-memory collection of custom saved regular expressions. |
| `DisplayedPatterns` | `ObservableCollection<PatternItem>` | `private` | UI-bound collection representing the merged view of user regexes and built-in recognizers. |
| `HiddenRecognizerIds` | `HashSet<string>` | `private` | Set of recognizer IDs hidden by the user, initialized with case-insensitive string comparison (`StringComparer.OrdinalIgnoreCase`). |

---

## Life Cycle Methods

### `RegexManager()`
Constructor that calls `InitializeComponent()` to set up WPF XAML components.

### `Window_Loaded(object sender, RoutedEventArgs e)`
Executes when the window is loaded into the UI.
- Calls `LoadRegexPatterns()` to load persisted custom regex patterns.
- Loads hidden smart pattern IDs into `HiddenRecognizerIds` via `AppUtilities.TextGrabSettingsService.LoadHiddenSmartPatternIds()`.
- Executes `RebuildDisplayedPatterns()` to populate `DisplayedPatterns`.
- Sets `RegexDataGrid.ItemsSource` to `DisplayedPatterns`.
- Configures grouping on `RegexDataGrid` based on `PatternItem.GroupLabel`.

### `FluentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)`
Executes when the window is closing. Automatically triggers `SaveRegexPatterns()` to persist any pending changes.

---

## Pattern Persistence & Data Management

### `LoadRegexPatterns()`
Clears `RegexPatterns` and populates it from `AppUtilities.TextGrabSettingsService.LoadStoredRegexes()`. If no stored regexes exist, it populates `RegexPatterns` with default patterns from `StoredRegex.GetDefaultPatterns()` and immediately saves them.

### `SaveRegexPatterns()`
Persists the current `RegexPatterns` collection using `AppUtilities.TextGrabSettingsService.SaveStoredRegexes()`.

### `SaveHiddenRecognizerIds()`
Persists `HiddenRecognizerIds` using `AppUtilities.TextGrabSettingsService.SaveHiddenSmartPatternIds()`.

### `RebuildDisplayedPatterns()`
Reconstructs the `DisplayedPatterns` collection:
1. Clears `DisplayedPatterns`.
2. Adds a new `PatternItem` wrapper for each `StoredRegex` in `RegexPatterns`.
3. Adds a `PatternItem` wrapper for each `BuiltInRecognizer` returned by `BuiltInRecognizer.GetAll()`, passing `isHidden` based on whether `HiddenRecognizerIds` contains its ID.

### `SelectPatternById(string id)`
Finds a item in `DisplayedPatterns` matching the given `id` and sets it as the `SelectedItem` in `RegexDataGrid`.

---

## Public Methods

### `AddPatternFromText(string pattern, string sourceText, EditTextWindow? source = null)`
Opens the `RegexManager` prefilled with a specific pattern to create a new regex item.
- Sets `SourceEditTextWindow` to `source`.
- Instantiates a `RegexEditorDialog`.
- Sets `dialog.PatternTextBox.Text` to the provided `pattern`.
- Formats `dialog.NameTextBox.Text` as `"Pattern from '{sourceText}'"`, processing `sourceText` to a single line (`MakeStringSingleLine()`) and truncating it to 30 characters (`Truncate(30)`).
- If the dialog is saved (`ShowDialog() == true`), adds the newly created `EditedRegex` to `RegexPatterns`, saves changes, rebuilds the list, and selects the new item.

---

## UI Interactions & Command Handlers

### Grid Selection Logic (`RegexDataGrid_SelectionChanged`)
Evaluates the currently selected `PatternItem` in `RegexDataGrid` and toggles UI element states:
- **`SavedRegex` selected**:
  - `EditButton`, `UseButton`, and `DeleteButton` are enabled.
  - `DeleteButton` is visible.
  - `HideButton` is collapsed.
- **`Recognizer` selected**:
  - `EditButton`, `UseButton`, and `DeleteButton` are disabled.
  - `DeleteButton` is collapsed.
  - `HideButton` is visible and enabled.
  - Updates `HideButton` text and icon based on `selected.IsHidden`:
    - Hidden: Displays `"Unhide"` with `SymbolRegular.Eye24`.
    - Visible: Displays `"Hide"` with `SymbolRegular.EyeOff24`.
- **General selection state**:
  - `ExplainButton` is enabled if `selected` is not null.
  - Calls `TestPattern()` to update testing outcomes.

### Button Click Handlers

#### `AddButton_Click`
Opens a new `RegexEditorDialog`. If submitted successfully:
- Adds `dialog.EditedRegex` to `RegexPatterns`.
- Saves patterns, rebuilds displayed patterns, and highlights the newly added item.

#### `EditButton_Click`
Checks if the selected grid item is a `SavedRegex`. If valid:
- Opens `RegexEditorDialog` pre-loaded with the selected `StoredRegex`.
- Replaces the original regex in `RegexPatterns` with `dialog.EditedRegex` upon dialog confirmation.
- Saves patterns, rebuilds displayed patterns, and re-selects the edited item.

#### `DeleteButton_Click`
Prompts a standard message box confirmation (`Wpf.Ui.Controls.MessageBox`) to confirm deletion of the selected `StoredRegex`.
- If confirmed (`MessageBoxResult.Primary`), removes the pattern from `RegexPatterns`, saves state, and rebuilds displayed patterns.

#### `HideButton_Click`
Toggles the visibility state of a `BuiltInRecognizer`:
- If already hidden, removes its ID from `HiddenRecognizerIds`.
- If visible, adds its ID to `HiddenRecognizerIds`.
- Calls `SaveHiddenRecognizerIds()`, rebuilds displayed patterns, and re-selects the item.

#### `UseButton_Click`
Executes action using a selected `SavedRegex`:
1. Updates `selectedRegex.LastUsedDate` to `DateTimeOffset.Now` and saves patterns.
2. Retrieves or opens a `FindAndReplaceWindow` using `WindowUtilities.OpenOrActivateWindow<FindAndReplaceWindow>()`.
3. Links `SourceEditTextWindow` to `findWindow.TextEditWindow` if unset.
4. Populates the search text with `selectedRegex.Pattern` and enables regex mode (`useRegex: true`).
5. Activates the find window, executes `findWindow.SearchForText()`, and closes `RegexManager`.

#### `ExplainButton_Click`
Displays explanation details for the selected pattern inside a `MessageBox`:
- If `SavedRegex`: Computes an explanation string using `StringMethods.ExplainRegexPattern(pattern)`.
- If `Recognizer`: Uses the recognizer's `Description`.

#### `ShowTestToggle_Click`
Toggles visibility of `TestPanel`:
- If checked: Displays `TestPanel` (`Visibility.Visible`) and sets button content to `"Hide Test"`.
- If unchecked: Collapses `TestPanel` (`Visibility.Collapsed`) and sets button content to `"Show Test"`.

---

## Pattern Testing Logic

### `TestTextBox_TextChanged`
- Manages placeholder text visibility (`TestTextPlaceholder`) based on `TestTextBox.Text` length.
- Calls `TestPattern()`.

### `TestPattern()`
Calculates real-time regex match count on user input:
1. Returns early if window is not loaded (`!IsLoaded`).
2. Resets `MatchCountText.Text` to `"0"` if no pattern is selected or if `TestTextBox.Text` is empty.
3. Validates pattern syntax for `SavedRegex` items using `IsValidRegexPattern()`. If invalid, updates `MatchCountText.Text` to `"Invalid Pattern"`.
4. Calculates matches using `PatternExecutor.GetMatches(selected, testText)` and displays the count in `MatchCountText.Text`.

### `IsValidRegexPattern(string? pattern)`
Static helper method that validates regular expression syntax.
- Returns `true` if `pattern` is null or empty.
- Attempts to construct `System.Text.RegularExpressions.Regex(pattern)`. Returns `false` if an `ArgumentException` is thrown.

---

## Dependencies & External Utilities

- **UI Controls**: `FluentWindow`, `SymbolIcon`, `SymbolRegular`, `Wpf.Ui.Controls.MessageBox` (from `Wpf.Ui.Controls`).
- **Models**: `StoredRegex`, `PatternItem`, `PatternKind`, `BuiltInRecognizer` (from `Text_Grab.Models`).
- **Utilities**:
  - `AppUtilities.TextGrabSettingsService` (Persistence for regex patterns and hidden IDs).
  - `WindowUtilities` (Window management for opening/activating `FindAndReplaceWindow`).
  - `PatternExecutor` (Executes matches for testing).
  - `StringMethods` (`ExplainRegexPattern`, `MakeStringSingleLine`).
- **External Frameworks**: `Humanizer` (`Truncate` method extension).