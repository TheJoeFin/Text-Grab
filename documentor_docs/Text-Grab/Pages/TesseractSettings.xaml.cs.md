# Technical Documentation: `TesseractSettings.xaml.cs`

**File Path:** `Text-Grab/Pages/TesseractSettings.xaml.cs`  
**Namespace:** `Text_Grab.Pages`  
**Base Class:** `System.Windows.Controls.Page`

---

## Overview

The `TesseractSettings` class provides the interaction logic for the `TesseractSettings.xaml` page in the Text-Grab application. Its primary responsibility is to manage user settings related to the Tesseract OCR engine, including configuring the Tesseract executable file path, toggling Tesseract usage, copying installation commands to the clipboard, opening file directories, and navigating external hyperlinks.

---

## Class Fields & Properties

| Field | Type | Description |
| :--- | :--- | :--- |
| `DefaultSettings` | `Settings` | A `readonly` reference to application settings obtained via `AppUtilities.TextGrabSettings`. |
| `settingsSet` | `bool` | A flag indicating whether initial settings have finished loading into the UI controls. Prevents event handlers from firing during initial UI setup. |

---

## Component Methods & Event Handlers

### Constructor

#### `public TesseractSettings()`
Initializes a new instance of the `TesseractSettings` page and calls `InitializeComponent()` to load the associated XAML elements.

---

### Page Lifecycle Events

#### `private void Page_Loaded(object sender, RoutedEventArgs e)`
Executed when the page is loaded into the UI visual tree.
1. Checks if the Tesseract executable can be located via `TesseractHelper.CanLocateTesseractExe()`.
2. **If executable is found:**
   - Populates `UseTesseractCheckBox.IsChecked` with `DefaultSettings.UseTesseract`.
   - Populates `TesseractPathTextBox.Text` with `DefaultSettings.TesseractPath`.
   - Sets `settingsSet = true`.
3. **If executable is NOT found:**
   - Unchecks `UseTesseractCheckBox` (`IsChecked = false`).
   - Disables `UseTesseractCheckBox` (`IsEnabled = false`).
   - Updates `DefaultSettings.UseTesseract = false`.
   - Sets `settingsSet = true`.

---

### Configuration & Input Event Handlers

#### `private void TesseractPathTextBox_TextChanged(object sender, TextChangedEventArgs e)`
Triggers when the text inside `TesseractPathTextBox` changes.
1. Returns early if `settingsSet` is `false` or if `sender` cannot be cast to a WPF `TextBox`.
2. Verifies whether the entered path exists on disk (`File.Exists(pathText)`):
   - If the file exists, `UseTesseractCheckBox.IsEnabled` is set to `true`.
   - If the file does not exist, `UseTesseractCheckBox.IsEnabled` is set to `false`.
3. Updates `DefaultSettings.TesseractPath` with the new text and saves the application settings (`DefaultSettings.Save()`).

#### `private void UseTesseractCheckBox_Checked(object sender, RoutedEventArgs e)`
Triggers when the state of the `UseTesseractCheckBox` (a `Wpf.Ui.Controls.ToggleSwitch`) changes.
1. Returns early if `settingsSet` is `false` or if `sender` cannot be cast to a `ToggleSwitch`.
2. Updates `DefaultSettings.UseTesseract` with the boolean value of `useTesseractSwitch.IsChecked`.
3. Persists the setting change by calling `DefaultSettings.Save()`.

---

### Action & Navigation Handlers

#### `private void OpenPathButton_Click(object sender, RoutedEventArgs args)`
Opens the parent directory of the specified Tesseract executable path in Windows File Explorer.
1. Validates that `TesseractPathTextBox.Text` is non-empty and points to an existing file.
2. Extracts the directory path using `Path.GetDirectoryName(...)`.
3. Spawns a system process via `Process.Start` with `UseShellExecute = true` to launch the directory in the default system file manager.
4. Sets `e.Handled = true`.

#### `private void WinGetCodeCopyButton_Click(object sender, RoutedEventArgs e)`
Copies the Winget installation command contained inside `WinGetInstallTextBox.Text` directly to the system clipboard (`Clipboard.SetText(...)`).

#### `private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)`
Handles hyperlink navigation requests within the page.
1. Launches the URI specified in `e.Uri.AbsoluteUri` using `Process.Start` with `UseShellExecute = true`.
2. Sets `e.Handled = true` to prevent default navigation behavior.

---

## Control Dependencies

The logic directly interacts with the following UI controls defined in the corresponding XAML page:
* `TesseractPathTextBox` (`TextBox`)
* `UseTesseractCheckBox` (`Wpf.Ui.Controls.ToggleSwitch`)
* `WinGetInstallTextBox` (`TextBox`)

## External Utilities Used
* `Text_Grab.Utilities.AppUtilities`
* `Text_Grab.Utilities.TesseractHelper`
* `Text_Grab.Properties.Settings`