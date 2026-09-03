# Documentation: `GeneralSettings.xaml.cs`

## Overview

The `GeneralSettings.xaml.cs` file is the code-behind logic for the **General Settings** page (`GeneralSettings.xaml`) in the Text-Grab application. It acts as the controller for managing user preferences and application behavior, including:

* Theme options (System, Light, Dark).
* Default launch modes (Fullscreen, Grab Frame, Edit Text, Quick Lookup).
* Windows Startup and background execution settings.
* Optical Character Recognition (OCR) options (barcode reading, error correction, paragraph detection, Latin normalization).
* Windows Shell Integration (Context menu, "Open With" file registration).
* Insertion delays, clipboard behavior, and default search engine options.
* HDR capture borderless access permission handling.

---

## Fields & Constants

| Field | Type | Description |
| :--- | :--- | :--- |
| `DefaultSettings` | `Settings` | Private reference to application-wide user settings (`AppUtilities.TextGrabSettings`). |
| `BadBrush` | `Brush` | `SolidColorBrush` (Red) used to indicate invalid user input in UI elements. |
| `GoodBrush` | `Brush` | `SolidColorBrush` (Transparent) used to indicate valid user input. |
| `InsertDelaySeconds` | `double` | Stores the parsed insert delay duration in seconds (default is `1.5`). |
| `settingsSet` | `bool` | Initialization flag set to `true` after `Page_Loaded` finishes populating UI elements. Used to prevent event handlers from prematurely saving settings during initialization. |

---

## Lifecycle Methods

### `GeneralSettings()` (Constructor)
1. Calls `InitializeComponent()`.
2. Checks whether the application is running as a packaged app via `AppUtilities.IsPackaged()`. If it is **not** packaged, it sets `OpenExeFolderButton.Visibility` to `Visibility.Visible`.
3. Displays the current application version by updating `VersionTextblock.Text` using `AppUtilities.GetAppVersion()`.

### `Page_Loaded(object sender, RoutedEventArgs e)` (Async)
Triggers when the settings page finishes loading into the visual tree. Populates UI components with saved values from `DefaultSettings`:

1. **Theme Selection**: Reads `DefaultSettings.AppTheme` and checks the corresponding radio button (`SystemThemeRdBtn`, `DarkThemeRdBtn`, or `LightThemeRdBtn`).
2. **Default Launch Mode**: Reads `DefaultSettings.DefaultLaunch` and selects the corresponding radio button (`FullScreenRDBTN`, `GrabFrameRDBTN`, `EditTextRDBTN`, or `QuickLookupRDBTN`).
3. **Startup on Login**:
   * **Packaged App**: Queries `StartupTask.GetAsync("StartTextGrab")`.
     * `Disabled`: Unchecks `StartupOnLoginCheckBox`.
     * `DisabledByUser`: Unchecks and disables `StartupOnLoginCheckBox`, displaying `"Auto start is disabled in Task Manager"` in `StartupTextBlock`.
     * `Enabled`: Checks `StartupOnLoginCheckBox`.
   * **Unpackaged App**: Sets `StartupOnLoginCheckBox.IsChecked` from `DefaultSettings.StartupOnLogin`.
4. **Web Searchers**: Clears `WebSearchersComboBox`, populates it with items from `Singleton<WebSearchUrlModel>.Instance.WebSearchers`, and sets the active item to `Singleton<WebSearchUrlModel>.Instance.DefaultSearcher`.
5. **General Preferences**: Loads boolean settings into checkboxes and toggles for toasts, background running, barcode detection, HDR correction, history, error correction, Latin correction, paragraph detection, auto-clipboard bypass, and auto-insertion (`TryInsert`).
6. **Insert Delay**: Loads `DefaultSettings.InsertDelay` into `InsertDelaySeconds` and formats it into `SecondsTextBox.Text`.
7. **Shell Integration**:
   * **Unpackaged**: Sets `AddToContextMenuCheckBox.IsChecked` via `ContextMenuUtilities.IsRegisteredInContextMenu()` and loads `RegisterOpenWithCheckBox.IsChecked`.
   * **Packaged**: Disables and unchecks both `AddToContextMenuCheckBox` and `RegisterOpenWithCheckBox`.
8. Sets `settingsSet = true` to enable event handlers to update user settings during UI interaction.

---

## Event Handlers & Core Features

### 1. Navigation & App Actions
* **`OpenExeFolderButton_Click`**: Resolves the executable folder path using `FileUtilities.GetExePath()` and opens Windows File Explorer at that directory via `Process.Start`.
* **`AboutBTN_Click`**: Invokes `WindowUtilities.OpenOrActivateWindow<FirstRunWindow>()` to display the "First Run / About" window.

### 2. Theme Settings
* **`SystemThemeRdBtn_Checked`**, **`LightThemeRdBtn_Checked`**, **`DarkThemeRdBtn_Checked`**: Update `DefaultSettings.AppTheme` to the selected `AppTheme` enum string (`System`, `Light`, or `Dark`) and apply the theme instantly via `App.SetTheme()`.

### 3. Default Launch Mode
Handlers respond to radio button selections and update `DefaultSettings.DefaultLaunch`:
* `FullScreenRDBTN_Checked`: Sets mode to `TextGrabMode.Fullscreen`.
* `GrabFrameRDBTN_Checked`: Sets mode to `TextGrabMode.GrabFrame`.
* `EditTextRDBTN_Checked`: Sets mode to `TextGrabMode.EditText`.
* `QuickLookupRDBTN_Checked`: Sets mode to `TextGrabMode.QuickLookup`.

### 4. Application Behavior & Startup
* **`RunInBackgroundChkBx_Checked`**: Updates `DefaultSettings.RunInTheBackground`, applies the runtime setting via `ImplementAppOptions.ImplementBackgroundOption()`, and saves the setting state.
* **`StartupOnLoginCheckBox_Checked` / `Unchecked`**: Updates `DefaultSettings.StartupOnLogin` and asynchronously executes `ImplementAppOptions.ImplementStartupOption(...)`.

### 5. Input Validation
* **`ValidateTextIsNumber(object sender, TextChangedEventArgs e)`**: Validates the insert delay field (`SecondsTextBox`).
  * If `double.TryParse` succeeds and the value is between $0$ and $10$ seconds:
    * Updates `InsertDelaySeconds` and `DefaultSettings.InsertDelay`.
    * Hides the error indicator (`DelayTimeErrorSeconds.Visibility = Visibility.Collapsed`).
    * Sets the textbox border to `GoodBrush` (Transparent).
  * If validation fails (non-numeric, $\le 0$, or $\ge 10$):
    * Defaults `InsertDelaySeconds` to `3`.
    * Displays the error message (`DelayTimeErrorSeconds.Visibility = Visibility.Visible`).
    * Sets the textbox border to `BadBrush` (Red).

### 6. HDR Capture & Permission Check
* **`HdrCaptureCorrectionToggle_Checked` / `Unchecked`**: Toggles `DefaultSettings.HdrCaptureCorrection`.
* **`CheckHdrPermissionButton_Click`**:
  * Temporarily disables the button.
  * Calls `HdrScreenCapture.RequestBorderlessAccessAsync()` to request borderless access permission.
  * Sets `DefaultSettings.HdrBorderlessGranted` to `true` if access is `Allowed`, then saves settings.
  * Displays a UI dialog (`Wpf.Ui.Controls.MessageBox`) with detailed status messages corresponding to the `AppCapabilityAccessStatus` result (`Allowed`, `DeniedByUser`, `DeniedBySystem`, or default).

### 7. Recognition & Workflow Options
Handles simple boolean toggles for recognition logic and UI capabilities:
* **Barcodes**: `ReadBarcodesBarcode_Checked` / `Unchecked` updates `DefaultSettings.TryToReadBarcodes`.
* **History**: `HistorySwitch_Checked` / `Unchecked` updates `DefaultSettings.UseHistory`.
* **OCR Error Correction**: `ErrorCorrectBox_Checked` / `Unchecked` updates `DefaultSettings.CorrectErrors`.
* **Paragraph Detection**: `ParagraphDetectionToggle_Checked` / `Unchecked` updates `DefaultSettings.ParagraphDetection`.
* **Latin Correction**: `CorrectToLatin_Checked` / `Unchecked` updates `DefaultSettings.CorrectToLatin`.
* **Clipboard Auto-Use Bypass**: `NeverUseClipboardChkBx_Checked` / `Unchecked` updates `DefaultSettings.NeverAutoUseClipboard`.
* **Auto-Insert**: `TryInsertCheckbox_Checked` / `Unchecked` updates `DefaultSettings.TryInsert`.
* **Notifications**: `ShowToastCheckBox_Checked` / `Unchecked` updates `DefaultSettings.ShowToast`.
* **Web Search Engine**: `WebSearchersComboBox_SelectionChanged` updates `Singleton<WebSearchUrlModel>.Instance.DefaultSearcher` with the selected `WebSearchUrlModel`.

### 8. Windows Explorer Integration (Unpackaged Only)
* **`AddToContextMenuCheckBox_Checked`**:
  * Invokes `ContextMenuUtilities.AddToContextMenu(out string? errorMessage)`.
  * On success: Updates `DefaultSettings.AddToContextMenu = true` and saves.
  * On failure: Temporarily sets `settingsSet = false`, reverts the checkbox state to unchecked, and displays an error `MessageBox`.
* **`AddToContextMenuCheckBox_Unchecked`**:
  * Invokes `ContextMenuUtilities.RemoveFromContextMenu(out string? errorMessage)`.
  * On success: Updates `DefaultSettings.AddToContextMenu = false` and saves.
  * On failure: Temporarily sets `settingsSet = false`, reverts the checkbox state to checked, and displays an error `MessageBox`.
* **`RegisterOpenWithCheckBox_Checked` / `Unchecked`**:
  * Calls `ImplementAppOptions.RegisterAsImageOpenWithApp()` or `UnregisterAsImageOpenWithApp()`.
  * Saves `DefaultSettings.RegisterOpenWith`.

---

## Control Guard Pattern

All non-initialization handlers include a guard clause:
```csharp
if (!settingsSet)
    return;
```
This ensures settings are not overwritten while values are being populated into controls inside `Page_Loaded`.