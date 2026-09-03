# Documentation: `DangerSettings.xaml.cs`

## Overview

The `DangerSettings.xaml.cs` file is the code-behind for the `DangerSettings` WPF page in the Text-Grab application (`namespace Text_Grab.Pages`). It provides administrative, diagnostic, destructive, and advanced maintenance controls. 

Key functionality includes:
- Exporting diagnostic bug reports.
- Resetting application settings to defaults and clearing history.
- Importing and exporting user settings (with optional history inclusion).
- Managing experimental toggle states (such as AI architecture check overrides and file-backed managed settings).
- Troubleshooting system tray notifications and restarting/shutting down the application.

---

## Class Architecture & Fields

```csharp
public partial class DangerSettings : System.Windows.Controls.Page
```

### Private Fields

* `private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;`  
  Holds a reference to the global application settings instance retrieved from `AppUtilities`.
* `private bool _loadingDangerSettings;`  
  A state flag used to prevent event handlers (such as toggle switches) from firing logic prematurely while the page controls are being populated during initialization.

---

## Initialization & Lifecycle Events

### `DangerSettings()`
The class constructor. Calls `InitializeComponent()` to load the XAML components.

### `Page_Loaded(object sender, RoutedEventArgs e)`
Executes when the page is loaded into the UI visual tree.
1. Sets `_loadingDangerSettings = true` to guard against unintended event triggers.
2. Synchronizes UI controls with application settings:
   - Sets `OverrideArchCheckWinAI.IsChecked` to `DefaultSettings.OverrideAiArchCheck`.
   - Sets `EnableFileBackedManagedSettingsToggle.IsChecked` to `DefaultSettings.EnableFileBackedManagedSettings`.
3. Resets `_loadingDangerSettings = false`.

---

## Key Features & Event Handlers

### 1. Diagnostic Reporting

#### `ExportBugReportButton_Click(object sender, RoutedEventArgs e)`
* **Purpose**: Asynchronously generates a bug report and allows opening its file location.
* **Flow**:
  1. Calls `DiagnosticsUtilities.SaveBugReportToFileAsync()` to generate and retrieve the report file path.
  2. Displays a WPF UI `MessageBox` detailing the saved path and asking if the user wants to open the location.
  3. If the user clicks **Yes** (`Primary`), it launches File Explorer with the file selected via `Process.Start("explorer.exe", $"/select,\"{filePath}\"")`.
  4. Catches exceptions and displays an error `MessageBox` if report generation fails.

---

### 2. Settings & History Reset Operations

#### `ResetSettingsButton_Click(object sender, RoutedEventArgs e)`
* **Purpose**: Completely resets the application to its initial state.
* **Flow**:
  1. Displays a confirmation `MessageBox` asking if the user wants to reset all settings and delete history.
  2. If confirmed:
     - Calls `DefaultSettings.Reset()` to restore default settings.
     - Calls `Singleton<HistoryService>.Instance.DeleteHistory()` to wipe stored history.
     - Shuts down the application via `App.Current.Shutdown()`.

#### `ClearHistoryButton_Click(object sender, RoutedEventArgs e)`
* **Purpose**: Wipes all user action history without resetting other application settings.
* **Flow**:
  1. Displays a confirmation `MessageBox`.
  2. If confirmed, deletes the history via `Singleton<HistoryService>.Instance.DeleteHistory()`.

#### `ShutdownButton_Click(object sender, RoutedEventArgs e)`
* **Purpose**: Immediately shuts down the application via `Application.Current.Shutdown()`.

---

### 3. Settings Import & Export

#### `ExportSettingsButton_Click(object sender, RoutedEventArgs e)` & `BackupSettingsHyperlink_Click(object sender, RoutedEventArgs e)`
Both event handlers delegate execution directly to the helper method `ExportSettingsAsync()`.

#### `ExportSettingsAsync()`
* **Purpose**: Packs settings and optional history into a ZIP archive.
* **Flow**:
  1. Reads `IncludeHistoryCheckBox.IsChecked` (defaulting to `false` if null).
  2. Calls `SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory)`.
  3. Displays a success dialog displaying the file path and offers to reveal the file in File Explorer.
  4. Handles errors by displaying an "Export Error" `MessageBox`.

#### `ImportSettingsButton_Click(object sender, RoutedEventArgs e)`
* **Purpose**: Restores settings from a user-selected ZIP backup archive.
* **Flow**:
  1. Opens an `OpenFileDialog` filtered to `.zip` files. Initial directory defaults to `AutomationProfile.Current?.OutputDirectory` or the user's `MyDocuments` folder.
  2. If a file is selected, prompts the user with a warning that current settings will be overwritten and that the application will restart.
  3. Upon confirmation, calls `SettingsImportExportUtilities.ImportSettingsFromZipAsync(filePath)`.
  4. Displays a success message informing the user to open Text Grab again, then calls `App.Current.Shutdown()`.
  5. Catches errors and displays an "Import Error" `MessageBox` if the operation fails.

---

### 4. Tray Icon Troubleshooting

#### `RetrySettingTrayButton_Click(object sender, RoutedEventArgs e)`
* **Purpose**: Attempts to fix or re-initialize the system tray notification icon.
* **Flow**: Calls `await NotifyIconUtilities.ResetNotifyIcon()`.

---

### 5. Advanced & Experimental Toggles

#### `OverrideArchCheckWinAI_Click(object sender, RoutedEventArgs e)`
* **Purpose**: Overrides architecture check requirements for Windows AI features.
* **Flow**:
  1. Validates that `sender` is a `Wpf.Ui.Controls.ToggleSwitch`.
  2. Updates `DefaultSettings.OverrideAiArchCheck` with the toggle state (`IsChecked ?? false`).
  3. Calls `DefaultSettings.Save()`.

#### `EnableFileBackedManagedSettingsToggle_Checked(object sender, RoutedEventArgs e)`
* **Purpose**: Enables or disables experimental file-backed settings storage.
* **Flow**:
  1. Aborts if `_loadingDangerSettings` is `true` or if the toggle state matches the current setting value.
  2. Updates `DefaultSettings.EnableFileBackedManagedSettings` and saves settings via `DefaultSettings.Save()`.
  3. Displays a `MessageBox` explaining that an application restart is required to apply the storage preference.

---

## Dependencies & External Utilities

This page interacts with several key utility classes and services within the Text-Grab ecosystem:

| Utility / Service | Usage / Role |
| :--- | :--- |
| `AppUtilities` | Accesses global application settings (`TextGrabSettings`). |
| `DiagnosticsUtilities` | Asynchronously generates bug reports (`SaveBugReportToFileAsync`). |
| `HistoryService` | Managed via `Singleton<HistoryService>.Instance` for clearing history. |
| `SettingsImportExportUtilities` | Handles settings import/export to and from ZIP files. |
| `NotifyIconUtilities` | Re-initializes system tray icon (`ResetNotifyIcon`). |
| `AutomationProfile` | Used to determine default output directory for import dialogs. |
| `Wpf.Ui.Controls.MessageBox` | Asynchronous modern dialog UI prompts used throughout the page. |