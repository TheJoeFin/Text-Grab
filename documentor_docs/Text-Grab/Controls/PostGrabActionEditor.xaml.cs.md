# Technical Documentation: `PostGrabActionEditor.xaml.cs`

## Overview

The `PostGrabActionEditor.xaml.cs` file contains the code-behind logic for the `PostGrabActionEditor` window in the Text-Grab application. It provides user interface mechanisms to manage:
1. **Post-Grab Actions**: Enabling, disabling, reordering, saving, and resetting post-capture actions.
2. **Grab Templates**: Creating, editing (inline or region-based), and deleting text and image-based templates.
3. **Application Settings**: Toggling post-grab persistent window states (`PostGrabStayOpen`).

It also defines a utility value converter class (`EnumToIntConverter`) for WPF data bindings between enums and integer-based control properties.

---

## Class Definitions

### 1. `EnumToIntConverter`
* **Base Class**: `IValueConverter`
* **Purpose**: Translates enum values to integer values and back for WPF control bindings (e.g., `ComboBox.SelectedIndex`).

#### Methods
* **`Convert(object value, Type targetType, object parameter, CultureInfo culture)`**
  * Casts an input `Enum` object to its underlying `int` representation.
  * Returns `0` if the value is not an `Enum`.
* **`ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)`**
  * Converts an `int` back to an enum value of type `targetType` using `Enum.ToObject`.
  * Returns `DefaultCheckState.Off` if the input is not an integer or `targetType` is not an enum.

---

### 2. `PostGrabActionEditor`
* **Base Class**: `FluentWindow` (from `Wpf.Ui.Controls`)
* **Purpose**: Main window control for managing post-grab actions and templates.

---

## Properties & Fields

| Property / Field | Type | Description |
| :--- | :--- | :--- |
| `AvailableActions` | `ObservableCollection<ButtonInfo>` | Actions that are available to be added to the enabled post-grab actions list. |
| `EnabledActions` | `ObservableCollection<ButtonInfo>` | Actions currently enabled and active in the user's workflow. |
| `_editingTemplate` | `GrabTemplate?` | Holds a reference to the `GrabTemplate` object currently undergoing inline editing. |

---

## Key Functionalities & Methods

### Initialization & State Management

#### `PostGrabActionEditor()` (Constructor)
* Initializes WPF components via `InitializeComponent()`.
* Retrieves available and enabled actions using `PostGrabActionManager`.
* Populates `EnabledActionsListBox` and `AvailableActionsListBox` (sorted by `OrderNumber`).
* Loads `PostGrabStayOpen` setting into `StayOpenToggle.IsChecked`.
* Updates UI state visibilities via `UpdateEmptyStateVisibility()` and loads templates using `LoadTemplates()`.

#### `UpdateEmptyStateVisibility()`
* Displays `NoAvailableActionsText` and hides `AvailableActionsListBox` when `AvailableActions.Count` is zero. Otherwise, shows the list box and hides the empty message.

#### `LoadTemplates()`
* Fetches all saved `GrabTemplate` items via `GrabTemplateManager.GetAllTemplates()`.
* Updates `TemplatesListBox.ItemsSource` and calls `UpdateTemplateEmptyState()`.

#### `UpdateTemplateEmptyState(int count)`
* Shows `TemplatesListBox` if `count > 0`. Otherwise, displays `NoTemplatesText`.

#### `RefreshTemplatesAndActions()`
* Reloads templates and updates the `AvailableActions` list.
* Excludes actions from `AvailableActions` if they are already enabled (matched by `ButtonText` and `TemplateId`).

---

### Post-Grab Action Operations

#### `AddButton_Click(object sender, RoutedEventArgs e)`
* Transfers the selected `ButtonInfo` item from `AvailableActionsListBox` to `EnabledActions`.

#### `RemoveButton_Click(object sender, RoutedEventArgs e)`
* Transfers the selected `ButtonInfo` item from `EnabledActionsListBox` back to `AvailableActions`.
* Re-sorts `AvailableActions` by `OrderNumber`.

#### `MoveUpButton_Click(object sender, RoutedEventArgs e)`
* Moves the selected action in `EnabledActions` up by one position in the collection.

#### `MoveDownButton_Click(object sender, RoutedEventArgs e)`
* Moves the selected action in `EnabledActions` down by one position in the collection.

#### `ResetButton_Click(object sender, RoutedEventArgs e)` (Async)
* Prompts the user with a WPF UI `MessageBox` asking to reset post-grab actions.
* If confirmed:
  * Restores actions to defaults from `PostGrabActionManager.GetDefaultPostGrabActions()`.
  * Re-populates `EnabledActions` and `AvailableActions`.

#### `SaveButton_Click(object sender, RoutedEventArgs e)`
* Saves the current `EnabledActions` collection via `PostGrabActionManager.SavePostGrabActions()`.
* Persists the `PostGrabStayOpen` toggle state to `AppUtilities.TextGrabSettings`.
* Closes the window.

#### `CancelButton_Click(object sender, RoutedEventArgs e)`
* Closes the editor window without saving pending changes to actions or settings.

---

### Template Management Operations

#### `TemplateInfoButton_Click(object sender, RoutedEventArgs e)`
* Toggles the open/closed state of `TemplateInfoPopup`.

#### `NewTextOnlyTemplateButton_Click(object sender, RoutedEventArgs e)`
* Opens the `TextOnlyTemplateDialog` window.
* Calls `RefreshTemplatesAndActions()` if the dialog returns `true`.

#### `NewTemplateFromImageButton_Click(object sender, RoutedEventArgs e)`
* Opens an `OpenFileDialog` with an image filter (`FileUtilities.GetImageFilter()`).
* If a valid image path is selected:
  1. Instantiates a new `GrabTemplate` using the image filename as its name and assigning the file path.
  2. Opens a `GrabFrame` window passing the template.
  3. Refreshes templates and actions when the `GrabFrame` closes.

#### `EditTemplateRegionsButton_Click(object sender, RoutedEventArgs e)` (Async)
* Checks if a template is selected in `TemplatesListBox`. Displays an error message dialog if no selection is made.
* If the template has no `SourceImagePath` (text-only):
  * Opens `TextOnlyTemplateDialog` populated with template name and serialized output template data.
* If the template has a `SourceImagePath`:
  * Launches `GrabFrame` with the selected template and refreshes templates/actions when closed.

#### `DeleteTemplateButton_Click(object sender, RoutedEventArgs e)` (Async)
* Prompts user for deletion confirmation via an asynchronous `MessageBox`.
* If confirmed:
  1. Deletes the template using `GrabTemplateManager.DeleteTemplate()`.
  2. Removes any post-grab actions bound to the deleted template ID from `EnabledActions`.
  3. Calls `RefreshTemplatesAndActions()`.

---

### Inline Template Editing

#### `TemplatesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)`
* Detects when template selection changes while editing.
* Resets `_editingTemplate` to `null` and hides `TemplateEditPanel` if selection switches to a different template.

#### `EditTemplateButton_Click(object sender, RoutedEventArgs e)`
* Begins inline editing for the selected `GrabTemplate`:
  * Assigns selection to `_editingTemplate`.
  * Populates `EditTemplateNameBox` and `EditOutputTemplateBox`.
  * Displays `TemplateEditPanel` and focuses/selects the template name input field.

#### `ApplyTemplateEdit_Click(object sender, RoutedEventArgs e)`
* Validates that `EditTemplateNameBox` is not blank.
* Updates `_editingTemplate.Name` and `_editingTemplate.OutputTemplate`.
* Re-parses pattern matches via `GrabTemplateExecutor.ParsePatternMatchesFromOutputTemplate()`.
* Persists updates via `GrabTemplateManager.AddOrUpdateTemplate()`.
* Hides `TemplateEditPanel` and calls `RefreshTemplatesAndActions()`.

#### `CancelTemplateEdit_Click(object sender, RoutedEventArgs e)`
* Clears `_editingTemplate` reference and hides `TemplateEditPanel` without saving changes.

---

## Dependencies & External References

* **`Text_Grab.Models`**: References `GrabTemplate` and `ButtonInfo`.
* **`Text_Grab.Utilities`**: Uses `PostGrabActionManager`, `GrabTemplateManager`, `GrabTemplateExecutor`, `AppUtilities`, and `FileUtilities`.
* **`Text_Grab.Views`**: Interacts with `GrabFrame` and `TextOnlyTemplateDialog`.
* **`Wpf.Ui.Controls`**: Utilizes UI components including `FluentWindow` and custom `MessageBox`.