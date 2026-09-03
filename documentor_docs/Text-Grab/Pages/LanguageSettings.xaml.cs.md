# Technical Documentation: LanguageSettings.xaml.cs

## Overview

The `LanguageSettings.xaml.cs` file serves as the code-behind logic for the `LanguageSettings` WPF `Page` in the Text-Grab application. This page allows users to configure and manage language and text-recognition settings across different recognition engines and system features, including:

- **Windows Native OCR**: Displays installed Windows recognizer languages.
- **Tesseract OCR Integration**: Displays installed Tesseract languages, allows downloading new Tesseract trained data (`.traineddata`) files from GitHub, and handles administrative permissions required to install them.
- **UI Automation Configuration**: Controls UI Automation options for reading text directly from UI elements (traversal mode, fallback to OCR, offscreen elements, focused element priority).
- **Windows AI Capabilities**: Verifies system compatibility (Windows version, packaged app status, device hardware support) and toggles AI image description features.
- **Text Processing Rules**: Option to strip Furigana (reading aids) from Japanese text captures.

---

## Class Architecture & Fields

```csharp
namespace Text_Grab.Pages;

public partial class LanguageSettings : Page
```

### Fields

- **`DefaultSettings`** (`Settings`): Reference to the application's global settings object obtained from `AppUtilities.TextGrabSettings`.
- **`loadingLanguageSettings`** (`bool`): A guard flag used during page initialization to prevent UI event handlers from triggering unnecessary settings saves or cache invalidations while controls are being populated.

---

## Key Functionality & Methods

### 1. Page Lifecycle & Initialization

#### `Page_Loaded(object sender, RoutedEventArgs e)`
An asynchronous event handler triggered when the page loads:
1. Sets `loadingLanguageSettings = true` to suppress event handler execution during control population.
2. Invokes status and setting loader methods:
   - `LoadAiStatus()`
   - `LoadWindowsAiDescriptionSettings()`
   - `LoadWindowsLanguages()`
   - `LoadUiAutomationSettings()`
3. Sets the state of `RemoveFuriganaToggle`.
4. Checks `DefaultSettings.UseTesseract`. If enabled, shows `TesseractLanguagesStackPanel` and loads Tesseract languages via `LoadTesseractContent()`; otherwise, hides the panel.
5. Resets `loadingLanguageSettings = false`.

---

### 2. Windows AI Capabilities

#### `LoadAiStatus()`
Determines whether Windows AI feature support is available on the current host machine:
- Checks if running on Windows 10 via `OSInterop.IsWindows10()`. If so, sets status text to `"Not supported"`.
- Checks if the application is packaged via `AppUtilities.IsPackaged()`. Unpackaged builds set status to `"Not supported"` and show `StoreLink`.
- Calls `WindowsAiUtilities.CanDeviceUseWinAI()` to check system device support and updates `StatusTextBlock` and `ReasonTextBlock` accordingly.
- Catches and displays any exception messages encountered during status checks.

#### `LoadWindowsAiDescriptionSettings()`
Sets `WindowsAiDescriptionEnabledToggle.IsChecked` from settings and sets `IsEnabled` based on `WindowsAiUtilities.CanDeviceDescribeImagesWithWinAI()`.

#### `WindowsAiDescriptionEnabledToggle_Checked(object sender, RoutedEventArgs e)`
Handles toggling the Windows AI Image Description feature:
- Exits early if `loadingLanguageSettings` is `true`.
- Prevents enabling if `WindowsAiUtilities.CanDeviceDescribeImagesWithWinAI()` returns `false`.
- Updates `DefaultSettings.WindowsAiDescriptionEnabled`, saves settings, and calls `LanguageUtilities.InvalidateAllCaches()`.

---

### 3. Language Recognition Engines

#### `LoadWindowsLanguages()`
Retrieves available Windows OCR languages from `Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages` and populates `WindowsLanguagesListView`.

#### `LoadTesseractContent()`
An asynchronous task that populates the Tesseract language controls:
- Clears and populates `TesseractLanguagesListView` with installed languages returned by `TesseractHelper.TesseractLanguages()`. Each item displays the padded filename and `CultureDisplayName`.
- Clears and populates `AllLanguagesComboBox` with available downloadable languages from `TesseractGitHubFileDownloader.tesseractTrainedDataFileNames`.

#### `InstallButton_Click(object sender, RoutedEventArgs e)`
Handles downloading and installing a selected Tesseract `.traineddata` file:
1. Extracts the target filename from the selected item in `AllLanguagesComboBox`.
2. Resolves paths for the destination directory (`DefaultSettings.TesseractPath` + `\tessdata\`) and temporary download folder (`AutomationProfile.GetTemporaryDirectory()`).
3. Downloads the file asynchronously using `TesseractGitHubFileDownloader.DownloadFileAsync()`.
4. Invokes `CopyFileWithElevatedPermissions()` to move the file to the destination folder.
5. Reloads Tesseract language lists via `LoadTesseractContent()`.
6. Deletes the temporary downloaded file.

#### `CopyFileWithElevatedPermissions(string sourcePath, string destinationPath)`
Executes an elevated `cmd.exe` process (`runas` verb) to copy the downloaded Tesseract data file to system directories requiring administrator privileges:
- Process parameters: `cmd.exe /c copy "<sourcePath>" "<destinationPath>"`.
- Catches process start errors or user cancellation of UAC prompts and displays the error using `Wpf.Ui.Controls.MessageBox`.

#### `OpenPathButton_Click(object sender, RoutedEventArgs e)`
Opens the Tesseract `tessdata` folder in Windows Explorer using `Process.Start("explorer.exe", tesseractFilePath)`.

---

### 4. UI Automation Settings

#### `LoadUiAutomationSettings()`
Populates controls related to UI Automation text extraction:
- Sets toggle controls (`UiAutomationEnabledToggle`, `UiAutomationFallbackToggle`, `UiAutomationIncludeOffscreenToggle`, `UiAutomationPreferFocusedToggle`).
- Binds `UiAutomationTraversalModeComboBox` to the `UiAutomationTraversalMode` enum values and sets the currently selected mode from `DefaultSettings.UiAutomationTraversalMode` (defaults to `UiAutomationTraversalMode.Balanced` if parsing fails).
- Calls `UpdateUiAutomationControlState()`.

#### Event Handlers for UI Automation Options
- **`UiAutomationEnabledToggle_Checked`**: Updates `DefaultSettings.UiAutomationEnabled`, saves settings, invalidates language caches via `LanguageUtilities.InvalidateAllCaches()`, and updates panel visibility via `UpdateUiAutomationControlState()`.
- **`UiAutomationFallbackToggle_Checked`**: Updates `DefaultSettings.UiAutomationFallbackToOcr` and saves settings.
- **`UiAutomationPreferFocusedToggle_Checked`**: Updates `DefaultSettings.UiAutomationPreferFocusedElement` and saves settings.
- **`UiAutomationIncludeOffscreenToggle_Checked`**: Updates `DefaultSettings.UiAutomationIncludeOffscreen` and saves settings.
- **`UiAutomationTraversalModeComboBox_SelectionChanged`**: Updates `DefaultSettings.UiAutomationTraversalMode` string representation and saves settings.
- **`UpdateUiAutomationControlState()`**: Toggles `UiAutomationAdvancedOptionsPanel` visibility (`Visible` if UI Automation is enabled, otherwise `Collapsed`).

---

### 5. Miscellaneous Logic & Controls

#### `RemoveFuriganaToggle_Checked(object sender, RoutedEventArgs e)`
Updates `DefaultSettings.RemoveFurigana` when toggled, saves settings, and calls `LanguageUtilities.InvalidateAllCaches()`.

#### `Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)`
Opens external URI links in the system's default browser via `Process.Start` with `UseShellExecute = true` and sets `e.Handled = true`.

#### `HyperlinkButton_Click(object sender, RoutedEventArgs e)`
Empty placeholder click handler.

---

## Control & Settings Mapping Summary

| UI Control / Event | Target Setting (`DefaultSettings`) | Side Effects |
| :--- | :--- | :--- |
| `UiAutomationEnabledToggle` | `UiAutomationEnabled` | Saves settings, invalidates caches, updates panel UI state |
| `UiAutomationFallbackToggle` | `UiAutomationFallbackToOcr` | Saves settings |
| `UiAutomationIncludeOffscreenToggle` | `UiAutomationIncludeOffscreen` | Saves settings |
| `UiAutomationPreferFocusedToggle` | `UiAutomationPreferFocusedElement` | Saves settings |
| `UiAutomationTraversalModeComboBox` | `UiAutomationTraversalMode` | Saves settings |
| `RemoveFuriganaToggle` | `RemoveFurigana` | Saves settings, invalidates caches |
| `WindowsAiDescriptionEnabledToggle` | `WindowsAiDescriptionEnabled` | Saves settings, invalidates caches |
| `InstallButton` | N/A | Downloads file, invokes elevated copy, refreshes Tesseract lists |
| `OpenPathButton` | N/A | Opens Tesseract `tessdata` directory in Windows Explorer |