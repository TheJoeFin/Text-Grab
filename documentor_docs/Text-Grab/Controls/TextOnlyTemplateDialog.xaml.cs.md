# Technical Documentation: `TextOnlyTemplateDialog.xaml.cs`

## Overview

The `TextOnlyTemplateDialog` class is a WPF dialog window inheriting from `Wpf.Ui.Controls.FluentWindow`. It provides a UI interface for creating a new text-only template (`GrabTemplate`) or editing an existing one within the **Text-Grab** application.

It allows users to specify a template name, design an output template using an interactive text box that supports embedded pattern pickers (regex patterns and recognizers), configure options for pattern matches, and save the resulting template configuration.

---

## Class Declaration & Inheritance

```csharp
namespace Text_Grab.Controls;

public partial class TextOnlyTemplateDialog : FluentWindow
```

- **Namespace**: `Text-Grab.Controls`
- **Base Class**: `Wpf.Ui.Controls.FluentWindow`

---

## Properties

### `EditingTemplate`
```csharp
public GrabTemplate? EditingTemplate { get; set; }
```
- **Type**: `GrabTemplate?`
- **Description**: Stores the template currently being edited. If set before the dialog is displayed, the window operates in "Edit Mode" (updating the existing template instead of creating a new one). If `null`, the dialog operates in "Creation Mode".

---

## Constructors

### `TextOnlyTemplateDialog()`
```csharp
public TextOnlyTemplateDialog()
```
- Calls `InitializeComponent()` to initialize XAML UI controls.
- Hooks event handlers to the window lifecycle:
  - `Loaded += OnLoaded;`
  - `Activated += OnActivated;`

---

## Methods & Functions

### Window Lifecycle & Focus Events

#### `OnLoaded(object sender, RoutedEventArgs e)`
*Private Event Handler*
- Configures title text to `"Edit Text-Only Template"` if `EditingTemplate` is not `null`.
- Sets keyboard focus to the `TemplateNameBox` control.
- Invokes `LoadPatternItems()` to populate available patterns.
- Hooks callbacks (`OnPatternItemSelected` and `OnRecognizerItemSelected`) to the `OutputTemplateBox`.

#### `OnActivated(object? sender, EventArgs e)`
*Private Event Handler*
- Fires when the dialog window regains focus.
- If `IsLoaded` is `true`, calls `LoadPatternItems()` to refresh the available pattern list. This ensures any regex/smart patterns added or edited in the external Patterns Manager window immediately reflect in the open dialog.

---

### Pattern Management & Selection Callbacks

#### `LoadPatternItems()`
*Private Method*
- Retrieves all available patterns via `PatternItem.GetAll()`.
- Maps each `PatternItem` through `InlinePickerItemFor()` and assigns the result array to `OutputTemplateBox.ItemsSource`.

#### `InlinePickerItemFor(PatternItem pattern)`
*Internal Static Method*
```csharp
internal static InlinePickerItem InlinePickerItemFor(PatternItem pattern)
```
- **Parameters**: `pattern` (`PatternItem`)
- **Returns**: `InlinePickerItem`
- Constructs an `InlinePickerItem` instance for insertion into the output template UI element.
- **Placeholder Generation**:
  - If `pattern.Kind == PatternKind.SavedRegex`, builds placeholder format: `{p:<pattern.Name>:first}`.
  - Otherwise, builds placeholder format: `{r:<pattern.Name>:first}`.
- Preserves `pattern.Kind` on the created `InlinePickerItem`.

#### `OnRecognizerItemSelected(InlinePickerItem item)`
*Private Method*
- Callback invoked when a user selects a recognizer pattern item inside `OutputTemplateBox`.
- Looks up a `BuiltInRecognizer` matching `item.DisplayName`.
- Displays a modal `PatternMatchModeDialog` configured for recognizers (`isRecognizer: true`).
- If confirmed (`dialog.ShowDialog() == true` with non-null result), returns a new `TemplateRecognizerMatch` containing:
  - Recognizer ID and name.
  - Match mode (`dialog.Result.MatchMode`).
  - Separator (`dialog.Result.Separator`).
  - Output kind (`dialog.SelectedOutputKind`).
- Returns `null` if canceled or invalid.

#### `OnPatternItemSelected(InlinePickerItem item)`
*Private Method*
- Callback invoked when a user selects a saved regex pattern item inside `OutputTemplateBox`.
- Loads stored regexes via `AppUtilities.TextGrabSettingsService.LoadStoredRegexes()`. Falls back to default patterns if empty.
- Finds the corresponding `StoredRegex` matching `item.DisplayName` (case-insensitive).
- Displays a modal `PatternMatchModeDialog` configured for saved regexes.
- Returns the resulting `TemplatePatternMatch` (`dialog.Result`) if the user confirms the dialog, or `null` if canceled.

#### `ManagePatternsButton_Click(object sender, RoutedEventArgs e)`
*Private Event Handler*
- Opens or brings into focus the `RegexManager` window using `WindowUtilities.OpenOrActivateWindow<RegexManager>()`.
- Allows users to manage or create regex patterns while keeping the dialog open.

---

### Input Validation & UI State Updates

#### `ValidateInput(object sender, TextChangedEventArgs e)`
*Private Event Handler*
- Event handler connected to text change events. Delegates to `UpdateSaveButton()`.

#### `OutputTemplateBox_TextChanged(object sender, TextChangedEventArgs e)`
*Private Event Handler*
- Event handler connected to text changes within `OutputTemplateBox`. Delegates to `UpdateSaveButton()`.

#### `UpdateSaveButton()`
*Private Method*
- Validates that:
  1. `TemplateNameBox.Text` contains non-whitespace characters.
  2. `OutputTemplateBox.GetSerializedText()` contains non-whitespace characters.
- Enables `SaveButton` if both conditions are met; disables it otherwise.
- Collapses `ErrorText` visibility if it is present.

---

### Dialog Actions (Save & Cancel)

#### `SaveButton_Click(object sender, RoutedEventArgs e)`
*Private Event Handler*
- Trims `TemplateNameBox.Text` and gets serialized text from `OutputTemplateBox`.
- Performs explicit validation checks:
  - If name is empty: Displays `"Template name is required."` in `ErrorText` and focuses `TemplateNameBox`.
  - If output template is empty: Displays `"Output template is required."` in `ErrorText` and focuses `OutputTemplateBox`.
- Constructs or updates the template target:
  - Uses `EditingTemplate` if present; otherwise instantiates a new `GrabTemplate`.
  - Sets `Name` and `OutputTemplate`.
  - Parses and sets `PatternMatches` via `GrabTemplateExecutor.ParsePatternMatchesFromOutputTemplate()`.
  - Parses and sets `RecognizerMatches` via `GrabTemplateExecutor.ParseRecognizerMatchesFromOutputTemplate()`.
- Registers the template by calling `GrabTemplateManager.AddOrUpdateTemplate(newTemplate)`.
- Sets `DialogResult = true` and closes the dialog window.

#### `CancelButton_Click(object sender, RoutedEventArgs e)`
*Private Event Handler*
- Sets `DialogResult = false` and closes the dialog window without saving changes.

---

## Referenced XAML Controls

The code directly interacts with the following XAML controls defined in the layout:
- `TitleBarControl`: Title bar control updated when editing a template.
- `TemplateNameBox`: `TextBox` where the template name is entered.
- `OutputTemplateBox`: Custom input control providing serialized output template text and custom item picker events.
- `SaveButton`: `Button` used to submit and save the template.
- `CancelButton`: `Button` used to close the dialog without saving.
- `ErrorText`: `TextBlock` used to display inline validation error messages.

---

## Core Workflow Summary

```
[User Action: Open Dialog]
       │
       ▼
 OnLoaded / OnActivated ──► LoadPatternItems() ──► Populate OutputTemplateBox.ItemsSource
       │
       ├─► User clicks "Manage Patterns" ──► Opens RegexManager (Re-focus triggers OnActivated)
       │
       ├─► User selects Pattern/Recognizer ──► Shows PatternMatchModeDialog ──► Returns match settings
       │
       ├─► Input changes ──► TextChanged ──► UpdateSaveButton() ──► Toggles SaveButton.IsEnabled
       │
       └─► User clicks Save
                 │
                 ├── Validate fields (Name, OutputTemplate)
                 ├── Parse PatternMatches & RecognizerMatches
                 ├── Call GrabTemplateManager.AddOrUpdateTemplate()
                 └── DialogResult = true ──► Close()
```