# Documentation: `QuickLookupSettings.xaml.cs`

## Overview

The `QuickLookupSettings.xaml.cs` file is the code-behind file for the `QuickLookupSettings` WPF `Page` control within the `Text-Grab` application (specifically under the `Text_Grab.Pages` namespace). 

Its primary purpose is to manage user settings related to the Quick Lookup feature. It binds interactive UI controls (text boxes, checkboxes, sliders, buttons) to persistent application settings stored in `AppUtilities.TextGrabSettings`, ensuring that changes made by the user in the UI are immediately validated, displayed, and saved to disk.

---

## Class Architecture & Fields

### Class Signature
```csharp
namespace Text_Grab.Pages;

public partial class QuickLookupSettings : Page
```

### Class Fields

* `private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;`
  * **Description**: Holds a reference to the application's global settings object (`TextGrabSettings`), which manages persistent configuration parameters.
* `private bool _loaded = false;`
  * **Description**: A flag used to track whether the page initialization/loading process is complete. This prevents event handlers (such as `ValueChanged` or `Click`) from prematurely overwriting settings while the controls are being populated during initial load.

---

## Methods and Event Handlers

### 1. `QuickLookupSettings()` (Constructor)
* **Description**: Standard WPF page constructor.
* **Logic**: Calls `InitializeComponent()` to load and initialize the XAML components associated with this page.

---

### 2. `Page_Loaded(object sender, RoutedEventArgs e)`
* **Trigger**: Fired when the `Page` is loaded into the element tree.
* **Logic**:
  1. Sets the text of `LookupFileLocationTextBox` from `DefaultSettings.LookupFileLocation`.
  2. Sets the checked state of `LookupSearchHistoryCheckBox` from `DefaultSettings.LookupSearchHistory`.
  3. Sets the checked state of `TryInsertCheckBox` from `DefaultSettings.TryInsert`.
  4. Clamps `DefaultSettings.InsertDelay` between `InsertDelaySlider.Minimum` and `InsertDelaySlider.Maximum`, assigning the result to `InsertDelaySlider.Value`.
  5. Updates `InsertDelayValueText.Text` with the string representation of `DefaultSettings.InsertDelay` formatted to one decimal place (`"0.0"`) using `CultureInfo.InvariantCulture`.
  6. Enables or disables `InsertDelaySlider` based on `DefaultSettings.TryInsert`.
  7. Sets `_loaded = true` to allow event handlers to process user interaction.

---

### 3. `LookupFileLocationTextBox_LostFocus(object sender, RoutedEventArgs e)`
* **Trigger**: Fired when `LookupFileLocationTextBox` loses focus.
* **Logic**:
  1. Checks if `_loaded` is `true`; returns early if `false`.
  2. Updates `DefaultSettings.LookupFileLocation` with the text from `LookupFileLocationTextBox`.
  3. Saves the updated settings by calling `DefaultSettings.Save()`.

---

### 4. `BrowseLookupFileButton_Click(object sender, RoutedEventArgs e)`
* **Trigger**: Fired when the "Browse" button is clicked.
* **Logic**:
  1. Instantiates a `SaveFileDialog` with the following initial configuration:
     * `AddExtension`: `true`
     * `DefaultExt`: `".csv"`
     * `Filter`: `"CSV files (*.csv)|*.csv"`
     * `InitialDirectory`: User's `MyDocuments` directory.
     * `FileName`: `"QuickSimpleLookupDataFile.csv"`
     * `OverwritePrompt`: `false`
  2. If `DefaultSettings.LookupFileLocation` is non-empty, overrides `InitialDirectory` with the directory path of `DefaultSettings.LookupFileLocation` and `FileName` with its file name.
  3. Displays the dialog using `ShowDialog()`.
  4. If the user confirms a file selection (`ShowDialog() == true`):
     * Updates `LookupFileLocationTextBox.Text` with the selected file path (`dlg.FileName`).
     * Updates `DefaultSettings.LookupFileLocation` with `dlg.FileName`.
     * Persists the setting via `DefaultSettings.Save()`.

---

### 5. `LookupSearchHistoryCheckBox_Click(object sender, RoutedEventArgs e)`
* **Trigger**: Fired when `LookupSearchHistoryCheckBox` is clicked.
* **Logic**:
  1. Checks if `_loaded` is `true`; returns early if `false`.
  2. Sets `DefaultSettings.LookupSearchHistory` to `true` if the checkbox is checked, or `false` otherwise.
  3. Saves the updated settings by calling `DefaultSettings.Save()`.

---

### 6. `TryInsertCheckBox_Click(object sender, RoutedEventArgs e)`
* **Trigger**: Fired when `TryInsertCheckBox` is clicked.
* **Logic**:
  1. Checks if `_loaded` is `true`; returns early if `false`.
  2. Evaluates whether `TryInsertCheckBox.IsChecked` is `true`.
  3. Updates `DefaultSettings.TryInsert` with this boolean value.
  4. Calls `DefaultSettings.Save()`.
  5. Updates the `IsEnabled` state of `InsertDelaySlider` to match the enabled state.

---

### 7. `InsertDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)`
* **Trigger**: Fired when the value of `InsertDelaySlider` changes.
* **Logic**:
  1. Checks if `_loaded` is `true`; returns early if `false`.
  2. Rounds the current `InsertDelaySlider.Value` to 1 decimal place using `Math.Round(..., 1)`.
  3. Updates `DefaultSettings.InsertDelay` with the rounded value.
  4. Calls `DefaultSettings.Save()`.
  5. Formats the rounded double value using `"0.0"` and `CultureInfo.InvariantCulture`, and updates `InsertDelayValueText.Text`.

---

## Workflow Summary

1. **Initialization**: When the page is loaded, control values are read from `DefaultSettings` and assigned to UI elements. The `_loaded` flag is set to `true` at the end of `Page_Loaded` to enable event-driven saving.
2. **User Interaction & Persistence**:
   * **Text Input**: Updates the setting when focus leaves `LookupFileLocationTextBox`.
   * **File Dialog**: Allows browsing and selecting/creating a CSV file path via `SaveFileDialog`.
   * **Toggles**: Checkbox clicks directly update `LookupSearchHistory` and `TryInsert` settings, with `TryInsert` enabling/disabling the delay slider dynamically.
   * **Numeric Inputs**: Slider changes round values to one decimal place, format the display text, and update `InsertDelay`.
   * **Save Call**: Every user interaction triggers `DefaultSettings.Save()` to persist changes immediately.