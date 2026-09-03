# Technical Documentation: `Tests/SettingsImportExportTests.cs`

## Overview

The `SettingsImportExportTests.cs` file contains automated unit and integration tests written in C# for validating the import and export capabilities of application settings within Text-Grab. 

It specifically tests the utilities responsible for packaging settings, managed JSON files, legacy settings data, and grab template image assets into compressed `.zip` archives, as well as importing them back to ensure data integrity and backward compatibility.

---

## Class Metadata & Environment Configuration

* **Namespace:** `Tests`
* **Class:** `SettingsImportExportTests`
* **Test Collection:** `[Collection("Settings isolation")]`
  * Prevents concurrent execution of tests in this collection to avoid race conditions when modifying global state, settings, or file assets during testing.
* **Test Attribute:** `[WpfFact]`
  * Marks methods as unit tests running on a WPF-compatible thread context.

---

## Key Tested Services & Utilities

1. **`SettingsImportExportUtilities`**: The primary utility under test, providing methods such as:
   * `ExportSettingsToZipAsync(bool includeHistory)`
   * `ImportSettingsFromZipAsync(string zipPath)`
2. **`SettingsService`**: Handles managed JSON settings, including loading/saving regexes and check states.
3. **`GrabTemplateManager`**: Manages templates and associated reference images, offering test override properties (`TestFilePath`, `TestImagesFolderPath`, `TestPreferFileBackedMode`).
4. **`Settings.Default`**: WPF / System properties settings instance.

---

## Managed JSON Setting Keys Checked

The exported `settings.json` file is expected to manage six specific keys for custom JSON configurations:
* `regexList`
* `shortcutKeySets`
* `bottomButtonsJson`
* `webSearchItemsJson`
* `postGrabJSON`
* `postGrabCheckStates`

---

## Detailed Test Method Reference

### 1. `CanExportSettingsWithoutHistory()`

* **Purpose:** Verifies basic functionality for exporting settings without history.
* **Process:**
  1. Calls `SettingsImportExportUtilities.ExportSettingsToZipAsync(includeHistory: false)`.
  2. Asserts that the returned ZIP path is non-empty, points to an existing file, and has a `.zip` file extension.
* **Cleanup:** Deletes the generated ZIP file.

---

### 2. `ExportedZipContainsSettingsJson()`

* **Purpose:** Ensures the generated ZIP archive contains a valid `settings.json` file containing standard configuration properties.
* **Process:**
  1. Generates an export ZIP file without history.
  2. Extracts the contents to a unique temporary directory (`TextGrab_Test_<Guid>`).
  3. Verifies that `settings.json` exists inside the extracted folder.
  4. Inspects the file contents to confirm presence of common setting keys (e.g., `ShowToast`, `FirstRun`, or `CorrectErrors`).
* **Cleanup:** Deletes the generated ZIP file and temporary extraction directory.

---

### 3. `RoundTripSettingsExportImportPreservesAllValues()`

* **Purpose:** Validates complete round-trip fidelity when exporting settings, mutating values, saving, importing, and re-exporting.
* **Process:**
  1. **Baseline:** Exports current settings to a ZIP file and extracts `settings.json`.
  2. **Deserialization:** Parses the JSON into a dictionary (`Dictionary<string, JsonElement>`).
  3. **Modification:** Selects a setting (e.g., `ShowToast`, `FirstRun`, or the first available key), toggles boolean values or alters string values to simulate modifications.
  4. **Packaging:** Writes modified JSON back to a new ZIP file.
  5. **Importing:** Imports the modified ZIP using `SettingsImportExportUtilities.ImportSettingsFromZipAsync`.
  6. **Re-export & Verification:** Exports the settings again, extracts them, and asserts that every property in the imported dictionary matches the modified values.
  7. **Restoration:** Restores the original settings by importing the baseline ZIP file.
* **Cleanup:** Cleans up all three generated ZIP files and three temporary extraction directories.

---

### 4. `ManagedJsonSettingWithDataSurvivesRoundTrip()`

* **Purpose:** Confirms that managed JSON settings (specifically stored regular expressions) survive an export, clear, and import cycle intact.
* **Process:**
  1. Preserves original regex settings via `SettingsService.LoadStoredRegexes()`.
  2. Injects a test `StoredRegex` (`Id = "export-roundtrip-1"`, `Pattern = @"\d{4}-\d{2}-\d{2}"`) using `SettingsService.SaveStoredRegexes()`.
  3. Exports settings and confirms the regex data exists in `settings.json`.
  4. Clears all stored regexes in memory/storage to simulate a fresh/clean application state.
  5. Imports the exported ZIP file.
  6. Asserts that the regex array is restored with matching `Id` and `Pattern`.
* **Cleanup (`finally`):** Restores the initial regex list and cleans up temporary files/folders.

---

### 5. `ExportedSettingsJsonIncludesManagedSettingKeys()`

* **Purpose:** Verifies that all 6 managed JSON key names exist within the exported `settings.json` content.
* **Process:**
  1. Exports settings to a temporary ZIP.
  2. Extracts `settings.json` and checks for case-insensitive inclusion of:
     * `regexList`
     * `shortcutKeySets`
     * `bottomButtonsJson`
     * `webSearchItemsJson`
     * `postGrabJSON`
     * `postGrabCheckStates`
* **Cleanup (`finally`):** Deletes the ZIP file and temporary extraction directory.

---

### 6. `LegacyExportWithInlineManagedSettingsIsImportedToSidecarFiles()`

* **Purpose:** Validates backward compatibility when importing ZIP archives generated by legacy app versions. Legacy archives stored managed JSON settings inline as raw JSON strings inside `Properties.Settings` rather than sidecar files.
* **Process:**
  1. Saves original `StoredRegex` array and `postGrabCheckStates` dictionary.
  2. Constructs a legacy-style `settings.json` payload containing inline JSON string values for `regexList` and `postGrabCheckStates`, along with standard primitive keys (e.g., `correctErrors = false`).
  3. Packs the legacy JSON into a ZIP file.
  4. Clears active settings to empty state.
  5. Invokes `SettingsImportExportUtilities.ImportSettingsFromZipAsync` on the legacy ZIP.
  6. Asserts that the import pipeline correctly extracts and routes inline string blobs into sidecar/service data structures (verifying restored `StoredRegex`, restored check states dictionary, and updated standard `CorrectErrors` boolean).
* **Cleanup (`finally`):** Restores original regexes, check states, resets `CorrectErrors` to `true`, and cleans up filesystem artifacts.

---

### 7. `ExportImportRoundTripsGrabTemplatesAndTemplateImages()`

* **Purpose:** Verifies that `GrabTemplate` instances along with their associated image file assets (reference image dependencies) are preserved across an export and import cycle.
* **Process:**
  1. Overrides `GrabTemplateManager` environment paths (`TestFilePath`, `TestImagesFolderPath`, `TestPreferFileBackedMode = false`).
  2. Constructs a dummy reference image file (`reference.png`) containing binary data.
  3. Creates a `GrabTemplate` object referencing the created image and region data, saving it via `GrabTemplateManager.SaveTemplates()`.
  4. Exports the environment to a ZIP archive.
  5. Wipes active templates (`SaveTemplates([])`) and deletes the source reference image from disk.
  6. Imports the exported settings ZIP.
  7. Asserts that `GrabTemplateManager` restores the template details, updates `Settings.Default.GrabTemplatesJSON`, and recreates the missing reference image file on disk.
* **Cleanup (`finally`):** Restores original `GrabTemplateManager` configuration variables, restores original `Settings.Default.GrabTemplatesJSON`, and deletes all temporary test files/folders.

---

## Error Handling & Cleanup Architecture

To ensure tests do not leak state or leave stray files across test runs, all multi-step file operations use standard C# cleanup patterns:
* **Guaranteed Cleanup:** File and folder deletions are contained inside `finally` blocks or explicit deletion statements at test completion.
* **Unique Paths:** Temporary directories and files utilize `Guid.NewGuid()` to avoid directory collision issues across test executions.
* **State Restoration:** Global application state and static manager properties (`SettingsService`, `GrabTemplateManager`, `Settings.Default`) are snapshotted before modification and explicitly restored in `finally` blocks.