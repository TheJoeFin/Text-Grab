# Documentation Guide: `Tests/DiagnosticsTests.cs`

## Overview

The `DiagnosticsTests` class is an xUnit unit testing suite designed to validate the bug report generation and file persistence functionality provided by `Text_Grab.Utilities.DiagnosticsUtilities`.

Its primary responsibilities are ensuring that:
1. Generated bug reports are correctly formatted in JSON.
2. Reports contain expected diagnostic fields, application settings, and managed setting summaries.
3. Reports are safely saved to the user's `MyDocuments` directory with the correct naming convention.
4. Personally Identifiable Information (PII), such as full local file paths, directory structures containing usernames, full web search URLs, and sensitive registry values, is omitted or redacted.

---

## Class Information

- **Namespace:** `Tests`
- **Target Utility Tested:** `Text_Grab.Utilities.DiagnosticsUtilities`
- **Dependencies:** 
  - `System.IO`
  - `Text_Grab.Utilities`
  - xUnit Framework (`Fact`, `Assert`)

---

## Test Methods Summary

| Test Method | Description | Skip Condition |
| :--- | :--- | :--- |
| `GenerateBugReport_ReturnsValidJson()` | Asserts that `GenerateBugReportAsync()` generates non-empty JSON with basic valid syntax and top-level fields. | None |
| `SaveBugReportToFile_CreatesFileInDocuments()` | Verifies that `SaveBugReportToFileAsync()` creates a valid file in the user's `MyDocuments` folder with expected naming conventions, then attempts cleanup. | None |
| `BugReport_ContainsStartupPathDiagnostics()` | Verifies presence of PII-safe startup diagnostics (e.g., `Text-Grab.exe`). | **Skipped** (`"because this fails in GitHub Actions"`) |
| `BugReport_IncludesAllRequestedInformation()` | Validates top-level JSON keys generated to address issue #553 diagnostics requirements. | None |
| `BugReport_SettingsInfo_ContainsAllKeySettings()` | Ensures application settings related to grab behavior, OCR, display, edit window, fullscreen grab, and grab frame are included. | None |
| `BugReport_ManagedSettingsSummary_ContainsExpectedFields()` | Verifies presence of summary counts and names for regex patterns, actions, shortcuts, templates, and buttons. | None |
| `BugReport_DoesNotContainPii()` | Enforces data privacy by asserting sensitive paths, absolute directories (`C:\Users\`, `C:\Program`), raw paths, and full search URLs are omitted. | None |

---

## Detailed Test Method Descriptions

### 1. `GenerateBugReport_ReturnsValidJson()`
- **Purpose:** Verifies that calling `DiagnosticsUtilities.GenerateBugReportAsync()` yields a valid JSON payload string containing essential top-level fields.
- **Async Execution:** Yes (`Task`)
- **Assertions:**
  - `Assert.NotEmpty(bugReport)`: The output string is not null or empty.
  - `Assert.StartsWith("{", bugReport.Trim())` & `Assert.EndsWith("}", bugReport.Trim())`: Structure starts and ends like a JSON object.
  - Presence of expected top-level keys:
    - `"generatedAt"`
    - `"appVersion"`
    - `"installationType"`
    - `"startupDetails"`

---

### 2. `SaveBugReportToFile_CreatesFileInDocuments()`
- **Purpose:** Tests the asynchronous saving of the bug report to disk.
- **Behavior:**
  - Invokes `DiagnosticsUtilities.SaveBugReportToFileAsync()`.
  - Obtains the default `MyDocuments` directory using `Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)`.
- **Assertions:**
  - `Assert.True(File.Exists(filePath))`: Confirms file creation.
  - `Assert.StartsWith(documentsPath, filePath)`: Confirms placement inside the user's Documents folder.
  - `Assert.Contains("TextGrab_BugReport_", Path.GetFileName(filePath))`: Verifies the filename prefix.
  - `Assert.EndsWith(".json", filePath)`: Verifies `.json` extension.
- **Cleanup:** Contains a `try-catch` block that attempts `File.Delete(filePath)` and explicitly catches and suppresses any exceptions upon test completion.

---

### 3. `BugReport_ContainsStartupPathDiagnostics()`
- **Attribute:** `[Fact(Skip = "because this fails in GitHub Actions")]`
- **Purpose:** Asserts that startup path diagnostic info includes safely formatted filenames without exposing sensitive full paths.
- **Assertions:**
  - Must contain: `"startupDetails"`, `"executableFileName"`, `"registryValueStatus"`, `"Text-Grab.exe"`.
  - Must **not** contain sensitive path fields: `"baseDirectory"`, `"calculatedRegistryValue"`, `"actualRegistryValue"`.

---

### 4. `BugReport_IncludesAllRequestedInformation()`
- **Purpose:** Checks for the inclusion of essential system and feature diagnostic metadata requested in issue #553.
- **Assertions:** Asserts presence of top-level JSON fields:
  - `"settingsInfo"`
  - `"installationType"`
  - `"startupDetails"`
  - `"windowsVersion"`
  - `"historyInfo"`
  - `"languageInfo"`
  - `"tesseractInfo"`
  - `"managedSettingsSummary"`

---

### 5. `BugReport_SettingsInfo_ContainsAllKeySettings()`
- **Purpose:** Asserts that user-configurable application settings are correctly serialized within the report output.
- **Categorized Fields Tested:**
  - **Grab Behavior:** `"tryInsert"`, `"insertDelay"`, `"closeFrameOnGrab"`, `"postGrabStayOpen"`
  - **OCR Settings:** `"correctErrors"`, `"correctToLatin"`, `"paragraphDetection"`, `"useTesseract"`, `"tesseractPathConfigured"`, `"uiAutomationEnabled"`, `"uiAutomationFallbackToOcr"`
  - **Display:** `"appTheme"`, `"fontSizeSetting"`
  - **Edit Text Window:** `"editWindowIsWordWrapOn"`, `"etwShowWordCount"`, `"etwUseMargins"`
  - **Fullscreen Grab:** `"fsgDefaultMode"`, `"fsgSelectionStyle"`
  - **Grab Frame:** `"grabFrameTranslationEnabled"`, `"grabFrameScrollBehavior"`

---

### 6. `BugReport_ManagedSettingsSummary_ContainsExpectedFields()`
- **Purpose:** Validates that summary stats and safe metadata names for complex user settings are exported.
- **Assertions:** Confirms JSON contains:
  - `"regexPatternCount"`
  - `"regexCustomPatternCount"`
  - `"regexCustomPatternNames"`
  - `"postGrabActionCount"`
  - `"postGrabActionNames"`
  - `"shortcutKeySetCount"`
  - `"bottomBarButtonCount"`
  - `"webSearchUrlCount"`
  - `"grabTemplateCount"`

---

### 7. `BugReport_DoesNotContainPii()`
- **Purpose:** Security and privacy regression test ensuring sensitive local system paths, raw search URLs, and individual user environment details are stripped or masked out.
- **Negative Assertions (`Assert.DoesNotContain`):**
  - Path fields from startup models: `"baseDirectory"`, `"calculatedRegistryValue"`, `"actualRegistryValue"`
  - Absolute system path strings: `@"C:\Users\"`, `@"C:\Program"`
  - Replaced path fields: `"tesseractPath"` (replaced by boolean `"tesseractPathConfigured"`)
  - Detailed URL collections: `"webSearchUrls"` (only `"webSearchUrlCount"` is permitted)