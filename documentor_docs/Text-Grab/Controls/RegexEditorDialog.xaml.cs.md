# `Text-Grab/Controls/RegexEditorDialog.xaml.cs` Technical Documentation

## Overview

The `RegexEditorDialog` class is a WPF dialog window (inheriting from `Wpf.Ui.Controls.FluentWindow`) designed for creating and editing stored regular expression (`StoredRegex`) patterns within the Text-Grab application. It handles user input validation, constructs or updates `StoredRegex` models, and provides interactive insertion of common regex syntax tokens via a built-in regex reference list.

---

## Class Architecture

- **Namespace:** `Text_Grab.Controls`
- **Base Class:** `Wpf.Ui.Controls.FluentWindow`
- **Class Modifier:** `public partial`

---

## Properties and Fields

### Public Properties
- **`EditedRegex`** (`StoredRegex?`): 
  - **Access:** `public StoredRegex? EditedRegex { get; private set; }`
  - **Purpose:** Holds the resulting `StoredRegex` object generated or modified when the user clicks the Save button. Returns `null` if the dialog is canceled or closed without saving.

### Private Fields
- **`_originalRegex`** (`StoredRegex?`): 
  - **Purpose:** Stores a reference to the existing `StoredRegex` object being edited. If `null`, the dialog operates in "Create" mode rather than "Edit" mode.

---

## Constructors

### 1. `RegexEditorDialog()`
```csharp
public RegexEditorDialog()
```
- **Purpose:** Initializes a new instance of `RegexEditorDialog` for **creating** a new regex pattern.
- **Behavior:**
  - Calls `InitializeComponent()`.
  - Sets `_originalRegex` to `null`.
  - Populates `RegexReferenceList.ItemsSource` by calling `BuildRegexReference()`.

### 2. `RegexEditorDialog(StoredRegex regexToEdit)`
```csharp
public RegexEditorDialog(StoredRegex regexToEdit)
```
- **Purpose:** Initializes a new instance of `RegexEditorDialog` for **editing** an existing regex pattern.
- **Parameters:**
  - `regexToEdit` (`StoredRegex`): The existing regex pattern model to modify.
- **Behavior:**
  - Calls `InitializeComponent()`.
  - Populates `RegexReferenceList.ItemsSource` by calling `BuildRegexReference()`.
  - Sets `_originalRegex` to the passed `regexToEdit` object.
  - Pre-fills input controls (`NameTextBox.Text`, `PatternTextBox.Text`, `DescriptionTextBox.Text`) using properties from `regexToEdit`.
  - Changes the window title (`Title`) to `"Edit Regex Pattern"`.
  - Triggers initial validation by calling `ValidateInput(null, null)`.

---

## Methods

### Event Handlers & Input Validation

#### `ValidateInput(object? sender, TextChangedEventArgs? e)`
- **Type:** `private void`
- **Purpose:** Validates the contents of `NameTextBox` and `PatternTextBox` whenever their text changes.
- **Validation Rules:**
  1. **Name:** Must not be null, empty, or consist entirely of whitespace. Error text: `"Name is required"`.
  2. **Pattern:** Must not be null, empty, or consist entirely of whitespace. Error text: `"Pattern is required"`.
  3. **Regex Syntax:** Attemps to instantiate `new Regex(PatternTextBox.Text)`. If an `ArgumentException` is thrown, the pattern is invalid. Error text: `"Invalid regular expression pattern"`.
- **UI State Updates:**
  - `SaveButton.IsEnabled` is set to `true` if valid, `false` if invalid.
  - If invalid, `ErrorText.Text` receives the error message and `ErrorText.Visibility` is set to `Visibility.Visible`.
  - If valid, `ErrorText.Visibility` is set to `Visibility.Collapsed`.

#### `SaveButton_Click(object sender, RoutedEventArgs e)`
- **Type:** `private void`
- **Purpose:** Handles the Save button click event.
- **Behavior:**
  - Checks if `_originalRegex` is not `null`:
    - **Edit Mode:** Creates a new `StoredRegex` assigned to `EditedRegex`, preserving the `Id`, `IsDefault`, `CreatedDate`, and `LastUsedDate` from `_originalRegex`, while updating `Name`, `Pattern`, and `Description` with trimmed values from the text boxes.
    - **Create Mode:** Creates a new `StoredRegex` assigned to `EditedRegex`, initializing `IsDefault` to `false`, and assigning trimmed values from text boxes to `Name`, `Pattern`, and `Description`.
  - Sets `DialogResult = true` and calls `Close()`.

#### `CancelButton_Click(object sender, RoutedEventArgs e)`
- **Type:** `private void`
- **Purpose:** Handles the Cancel button click event.
- **Behavior:**
  - Sets `DialogResult = false` and calls `Close()`.

#### `InsertToken_Click(object sender, RoutedEventArgs e)`
- **Type:** `private void`
- **Purpose:** Inserts a selected regular expression token (from the reference guide) into `PatternTextBox` at the current cursor or selection position.
- **Behavior:**
  - Verifies that `sender` is a `Wpf.Ui.Controls.Button` with a non-empty `Tag` property containing a `string` token.
  - Checks if text is currently selected in `PatternTextBox`:
    - If selected, replaces `PatternTextBox.SelectedText` with the token string.
    - If no text is selected, inserts the token into `PatternTextBox.Text` at `PatternTextBox.CaretIndex`.
  - Advances `PatternTextBox.CaretIndex` past the newly inserted token (`caret + token.Length`).
  - Restores keyboard focus to `PatternTextBox`.

---

### Reference Data Helper Methods

#### `BuildRegexReference()`
- **Type:** `private static List<RegexReferenceCategory>`
- **Purpose:** Constructs a predefined structure containing categorized regular expression reference tokens and descriptions.
- **Categories Included:**
  1. **Character classes** (`\d`, `\D`, `\w`, `\W`, `\s`, `\S`, `.`, `[abc]`, `[^abc]`, `[a-z]`)
  2. **Anchors & boundaries** (`^`, `$`, `\b`, `\B`)
  3. **Quantifiers** (`*`, `+`, `?`, `{3}`, `{2,}`, `{2,5}`, `*?`)
  4. **Groups & alternation** (`(...)`, `(?:...)`, `(?<name>...)`, `a|b`)
  5. **Lookaround** (`(?=...)`, `(?!...)`, `(?<=...)`, `(?<!...)`)
  6. **Escapes & literals** (`\.`, `\\`, `\n`, `\t`)

---

## Nested Types

### `RegexReferenceCategory`
- **Declaration:** `public sealed record RegexReferenceCategory(string CategoryName, IReadOnlyList<RegexReferenceItem> Items)`
- **Purpose:** Represents a named group of regex items displayed within the regex reference UI.

### `RegexReferenceItem`
- **Declaration:** `public sealed record RegexReferenceItem(string Token, string Description)`
- **Purpose:** Represents an individual regex token string along with its human-readable description.