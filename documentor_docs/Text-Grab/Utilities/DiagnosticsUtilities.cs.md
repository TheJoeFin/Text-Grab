# Technical Documentation: `DiagnosticsUtilities.cs`

## Overview

The `DiagnosticsUtilities` class in `Text_Grab.Utilities` is a static helper responsible for generating and saving detailed, structured diagnostic reports (bug reports) in JSON format. It aggregates information about the host environment, application configuration, user preferences, capture history metadata, language and OCR capabilities (including Windows AI and Tesseract), and display monitor setups.

A key design aspect of this utility is **data sanitization and privacy protection**: sensitive user information—such as local username paths, exact search URLs, custom regex pattern contents, and full file paths—is either redacted or converted into non-sensitive metadata (such as counts or boolean flags).

---

## Class Architecture & Structure

The file defines the static class `DiagnosticsUtilities` alongside eight diagnostic data model classes:

*   **`DiagnosticsUtilities`**: The static orchestrator that compiles diagnostic data and handles file output.
*   **`BugReportModel`**: The root model serializable to JSON containing all diagnostic subsections.
*   **`StartupDetailsModel`**: Contains startup configuration details (packaging state, startup method, registry status).
*   **`SettingsInfoModel`**: Contains application preference flags and settings values.
*   **`ManagedSettingsSummaryModel`**: Summarizes managed assets such as custom regexes, post-grab actions, shortcut keys, bottom bar buttons, search URLs, and templates.
*   **`HistoryInfoModel`**: Contains non-sensitive metadata about history captures (counts, oldest/newest timestamps).
*   **`LanguageInfoModel`**: Information about input languages, available Windows OCR languages, and AI capabilities.
*   **`TesseractInfoModel`**: Diagnostic metrics regarding Tesseract OCR installation and language support.
*   **`MonitorInfoModel`**: Contains telemetry for individual connected display monitors (index, scale percentage, raw/scaled bounds).

---

## Public Methods

### `GenerateBugReportAsync()`
*   **Signature:** `public static async Task<string> GenerateBugReportAsync()`
*   **Purpose:** Aggregates diagnostic data from all private sub-routines into a `BugReportModel` instance and serializes it to a formatted JSON string.
*   **Behavior:**
    1. Instantiates a `BugReportModel` populated by calling helper methods.
    2. Configures `JsonSerializerOptions` with `WriteIndented = true` and `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.
    3. Serializes the `BugReportModel` into a JSON string and returns it.

### `SaveBugReportToFileAsync()`
*   **Signature:** `public static async Task<string> SaveBugReportToFileAsync()`
*   **Purpose:** Generates a bug report using `GenerateBugReportAsync()` and writes it to a `.json` file on disk.
*   **Behavior:**
    1. Calls `GenerateBugReportAsync()` to obtain the JSON string.
    2. Constructs a filename using the current timestamp formatted as `TextGrab_BugReport_yyyyMMdd_HHmmss.json`.
    3. Determines the output directory using `AutomationProfile.Current?.OutputDirectory`; if null, defaults to the user's `MyDocuments` folder.
    4. Ensures the target directory exists via `Directory.CreateDirectory(...)`.
    5. Writes the file asynchronously using `File.WriteAllTextAsync(...)`.
    6. Returns the full path of the saved file.

---

## Private Helper Methods & Logic

### Environment & System Diagnostics

#### `GetInstallationType()`
*   **Returns:** `string`
*   **Logic:**
    *   Calls `AppUtilities.IsPackaged()`. If true, returns `"Packaged (Microsoft Store or sideloaded)"`.
    *   Otherwise, checks `AppContext.BaseDirectory` for the presence of `coreclr.dll` and `hostfxr.dll`.
    *   If both DLL files exist, returns `"Self-contained executable"`.
    *   Otherwise, returns `"Framework-dependent executable"`.

#### `GetWindowsVersion()`
*   **Returns:** `string`
*   **Logic:**
    *   Attempts to read registry subkey `SOFTWARE\Microsoft\Windows NT\CurrentVersion` under `Registry.LocalMachine`.
    *   Extracts `ProductName`, `DisplayVersion`, and `BuildLabEx`.
    *   If readable, returns formatted string: `"{ProductName} {DisplayVersion} (Build: {BuildLabEx})"`.
    *   If reading fails or throws an exception, catches the exception and returns the error string or falls back to `Windows {Environment.OSVersion.Version}`.

#### `GetStartupDetails()`
*   **Returns:** `StartupDetailsModel`
*   **Logic:**
    *   Sanitizes full executable paths by extracting only the filename using `Path.GetFileName(...)` to avoid exposing user directory paths.
    *   If packaged, sets `StartupMethod` to `"StartupTask API (packaged apps)"` and registry paths to `"N/A"`.
    *   If unpackaged, inspects `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` for the `"Text-Grab"` key.
    *   Sets `RegistryValueStatus` to `"Not set"`, `"Configured correctly"`, or `"Mismatch (points to different executable)"` depending on whether the registry value matches the current executable name.
    *   Catches registry read errors and logs the error message in `RegistryValueStatus`.

---

### Settings Diagnostics

#### `GetSettingsInfo()`
*   **Returns:** `SettingsInfoModel`
*   **Logic:**
    *   Extracts properties directly from `AppUtilities.TextGrabSettings`.
    *   Maps options across core behavior, grab behavior, OCR/error correction, global hotkeys, lookup tool options, display/fonts, Grab Frame settings, Fullscreen Grab settings, Edit Text Window (ETW) options, calculator pane options, web search preferences, UI Automation, and advanced toggles.
    *   **Data Sanitization:** File paths like `TesseractPath` and `LookupFileLocation` are converted to boolean values (`TesseractPathConfigured` and `LookupFileConfigured`) to prevent exposing user directory structures.

#### `GetManagedSettingsSummary()`
*   **Returns:** `ManagedSettingsSummaryModel`
*   **Logic:**
    *   Loads complex user configuration metrics via `AppUtilities.TextGrabSettingsService` and `GrabTemplateManager`.
    *   Counts total regex patterns, default patterns, and custom patterns. Keeps custom pattern *names* (`RegexCustomPatternNames`), but omits pattern strings to avoid leaking sensitive data domains.
    *   Counts post-grab actions, enabled actions, and captures action button labels.
    *   Counts shortcut key sets and enabled key sets.
    *   Counts bottom bar buttons, web search URLs (count only; URLs are excluded), and grab templates.
    *   Catches exceptions and assigns error messages to `ErrorMessage`.

---

### History Diagnostics

#### `GetHistoryInfo()`
*   **Returns:** `HistoryInfoModel`
*   **Logic:**
    *   Queries `Singleton<HistoryService>.Instance` for recent image captures and PDF documents.
    *   Queries last text history entry.
    *   Computes total counts and calculates oldest/newest capture dates using `GetOldestHistoryDate()` and `GetNewestHistoryDate()`.
    *   Catches exceptions and populates `HistoryInfoModel.ErrorMessage` with count indicators set to `-1`.

#### `GetOldestHistoryDate(...)` / `GetNewestHistoryDate(...)`
*   **Returns:** `DateTimeOffset?`
*   **Logic:** Aggregates capture dates from history lists and returns `dates.Min()` or `dates.Max()`. Returns `null` if no entries exist.

---

### Language & OCR Diagnostics

#### `GetLanguageInfo()`
*   **Returns:** `LanguageInfoModel`
*   **Logic:**
    *   Retrieves input language via `LanguageUtilities.GetCurrentInputLanguage()`.
    *   Retrieves available Windows OCR languages from `OcrEngine.AvailableRecognizerLanguages`.
    *   Queries AI availability from `WindowsAiUtilities.CanDeviceUseWinAI()`.
    *   Catches exceptions and populates `ErrorMessage` if language retrieval fails.

#### `GetTesseractInfoAsync()`
*   **Returns:** `Task<TesseractInfoModel>`
*   **Logic:**
    *   Checks if Tesseract executable exists via `TesseractHelper.CanLocateTesseractExe()`.
    *   Redacts full executable path string (`"Located (path redacted)"` or `"Not found"`).
    *   If located, fetches available language codes using `await TesseractHelper.TesseractLanguagesAsStrings()`.
    *   Catches exceptions gracefully per sub-step and root step.

---

### Display Telemetry Diagnostics

#### `GetMonitorsInfo()`
*   **Returns:** `List<MonitorInfoModel>`
*   **Logic:**
    *   Iterates through `DisplayInfo.AllDisplayInfos`.
    *   Calls `NativeMethods.GetScaleFactorForMonitor(...)` passing `di.MonitorHandle` to obtain DPI scaling percentage.
    *   Captures raw bounds (`di.Bounds`) and scaled bounds (`di.ScaledBounds()`).
    *   Appends a `MonitorInfoModel` per display.
    *   If an exception occurs, appends a single `MonitorInfoModel` with index `-1` and the exception message.

---

## Privacy & Data Sanitization Summary

To ensure bug reports can be safely shared publicly without exposing Personally Identifiable Information (PII) or sensitive environment variables, the file applies the following privacy rules:

| Information Type | Raw Value / Context | Sanitized Report Value |
| :--- | :--- | :--- |
| **Executables / Paths** | Full local paths containing system usernames | Filename only (e.g., `Text-Grab.exe`) |
| **Tesseract Path** | User file path | Boolean `TesseractPathConfigured` & `"Located (path redacted)"` |
| **Lookup File Path** | User file path | Boolean `LookupFileConfigured` |
| **Regex Patterns** | Regular expression match strings | Custom pattern count and custom pattern names only |
| **Web Search Config** | Target search URLs | Search engine name and total URL count |
| **Capture Content** | Captured text or image data | Length of text (`LastTextHistoryLength`), total counts, timestamps |

---

## Data Models Reference

### Root Model
```csharp
public class BugReportModel
{
    public DateTimeOffset GeneratedAt { get; set; }
    public string AppVersion { get; set; }
    public string InstallationType { get; set; }
    public string WindowsVersion { get; set; }
    public StartupDetailsModel StartupDetails { get; set; }
    public SettingsInfoModel SettingsInfo { get; set; }
    public ManagedSettingsSummaryModel ManagedSettingsSummary { get; set; }
    public HistoryInfoModel HistoryInfo { get; set; }
    public LanguageInfoModel LanguageInfo { get; set; }
    public TesseractInfoModel TesseractInfo { get; set; }
    public List<MonitorInfoModel> Monitors { get; set; }
}
```

### Component Models
*   **`StartupDetailsModel`**: Stores `IsPackaged`, `StartupMethod`, `ExecutableFileName`, `RegistryPath`, `RegistryValueStatus`.
*   **`SettingsInfoModel`**: Broad flat object matching `TextGrabSettings` fields across UI, behavior, hotkeys, ETW, Grab Frame, Fullscreen Grab, Calculator, UI Automation, and AI flags.
*   **`ManagedSettingsSummaryModel`**: Summarizes regex patterns, post-grab actions, shortcut sets, bottom bar button counts, search URL counts, template counts, and error string.
*   **`HistoryInfoModel`**: Tracks counts (`TextOnlyHistoryCount`, `ImageHistoryCount`, `PdfHistoryCount`, `TotalHistoryCount`), oldest/newest timestamps, and last text history existence/length.
*   **`LanguageInfoModel`**: Contains current input language tag, available OCR languages list, available language count, Windows AI state, and configured Tesseract languages.
*   **`TesseractInfoModel`**: Stores `IsInstalled`, `ExecutablePath` (redacted), `Version`, `AvailableLanguages`, and `ConfiguredLanguages`.
*   **`MonitorInfoModel`**: Stores monitor `Index`, `ScalePercent`, raw `Bounds`, and `ScaledBounds`. Contains `ErrorMessage` property if monitor info fails to read.