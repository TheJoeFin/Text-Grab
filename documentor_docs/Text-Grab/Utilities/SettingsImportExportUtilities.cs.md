# Technical Documentation: `SettingsImportExportUtilities`

**File Path:** `Text-Grab/Utilities/SettingsImportExportUtilities.cs`  
**Namespace:** `Text_Grab.Utilities`  
**Class Type:** `public static class SettingsImportExportUtilities`

---

## Overview

The `SettingsImportExportUtilities` class provides asynchronous utility functions to export and import Text Grab application configuration and data. It serializes settings, managed JSON configuration files, grab templates (and associated template images), and user history (text-only and image history artifacts) into a single compressed `.zip` archive or restores them from one.

---

## Constants & Directory Structure

The utility structures the exported archive using specific file and folder names:

| Constant Name | Value | Description |
| :--- | :--- | :--- |
| `SettingsFileName` | `"settings.json"` | Main exported JSON file containing application settings properties. |
| `HistoryTextOnlyFileName` | `"HistoryTextOnly.json"` | History JSON file for text-only entries. |
| `HistoryWithImageFileName` | `"HistoryWithImage.json"` | History JSON file for entries with associated images. |
| `HistoryFolderName` | `"history"` | Subfolder inside the ZIP storing history image artifacts. |
| `GrabTemplatesFileName` | `"GrabTemplates.json"` | Exported JSON representation of grab templates. |
| `TemplateImagesFolderName` | `"template-images"` | Subfolder inside the ZIP storing template images. |
| `ManagedSettingsFolderName` | `"settings-data"` | Subfolder inside the ZIP storing managed JSON setting files. |

### ZIP Archive Structure
```
TextGrab_Settings_yyyyMMdd_HHmmss.zip
├── settings.json
├── GrabTemplates.json
├── settings-data/
│   └── *.json
├── template-images/
│   └── [image files]
├── HistoryTextOnly.json
├── HistoryWithImage.json
└── history/
    └── [history artifact files]
```

---

## Public Methods

### 1. `ExportSettingsToZipAsync`
```csharp
public static async Task<string> ExportSettingsToZipAsync(bool includeHistory)
```

Exports application settings, templates, managed JSON settings, and optionally user history into a `.zip` archive.

* **Parameters:**
  * `includeHistory` (`bool`): Determines whether history files and history image artifacts should be included.
* **Returns:** `Task<string>` — The absolute file path to the newly created `.zip` file.
* **Execution Flow:**
  1. Creates a unique temporary directory via `AutomationProfile.GetTemporaryDirectory()` named `TextGrab_Export_{Guid}`.
  2. Calls `ExportSettingsToJsonAsync` to serialize settings to `settings.json`.
  3. Calls `ExportManagedJsonSettingsFolder` to copy managed JSON files to `settings-data/`.
  4. Calls `ExportGrabTemplatesAsync` to write `GrabTemplates.json` and copy template images to `template-images/`.
  5. If `includeHistory` is `true`:
     * Flushes in-memory history changes to disk using `Singleton<HistoryService>.Instance.WriteHistory()`.
     * Calls `ExportHistoryAsync` to bundle history JSON files and history artifact files.
  6. Determines output path using `AutomationProfile.Current?.OutputDirectory` (or defaults to `Environment.SpecialFolder.MyDocuments`).
  7. Names the zip file with format `TextGrab_Settings_yyyyMMdd_HHmmss.zip`. Overwrites if a file with the same name already exists.
  8. Compresses the temp directory into the output `.zip` file via `ZipFile.CreateFromDirectory`.
  9. Cleans up the temporary staging directory in a `finally` block.

---

### 2. `ImportSettingsFromZipAsync`
```csharp
public static async Task ImportSettingsFromZipAsync(string zipFilePath)
```

Imports application settings, templates, managed JSON settings, and history data from a `.zip` archive.

* **Parameters:**
  * `zipFilePath` (`string`): The full path of the `.zip` archive to extract and import.
* **Returns:** `Task`
* **Execution Flow:**
  1. Creates a unique temporary directory named `TextGrab_Import_{Guid}`.
  2. Extracts the contents of `zipFilePath` to the temporary directory using `ZipFile.ExtractToDirectory`.
  3. Checks for `settings.json`; if present, executes `ImportSettingsFromJsonAsync`.
  4. Calls `ImportManagedJsonSettingsFolder` to copy managed JSON settings files and runs `AppUtilities.TextGrabSettingsService.ReconcileManagedJsonSettings()`.
  5. Calls `ImportGrabTemplatesAsync` to import grab templates and copy template images.
  6. Checks if any history artifacts (`HistoryTextOnly.json`, `HistoryWithImage.json`, or `history/` directory) exist; if so, calls `ImportHistoryAsync`.
  7. Cleans up the temporary extraction directory in a `finally` block.

---

## Internal & Helper Methods

### Settings Management

#### `ExportSettingsToJsonAsync(string filePath)`
Iterates through all properties defined in `AppUtilities.TextGrabSettings.Properties`. 
* Checks if a property is a managed JSON setting via `SettingsService.IsManagedJsonSetting`. If true, retrieves the value via `GetManagedJsonSettingValueForExport`. Otherwise, retrieves the value directly from the settings indexer.
* Fallback: If `settings.Properties` is empty, reflects over public instance properties decorated with `UserScopedSettingAttribute`.
* Serializes the resulting dictionary into JSON formatted with camelCase property names and indentation enabled (`WriteIndented = true`), writing it asynchronously to `filePath`.

#### `ImportSettingsFromJsonAsync(string filePath)`
Deserializes `settings.json` into a `Dictionary<string, JsonElement>` using camelCase naming policy.
* Converts each key from camelCase to PascalCase via `ConvertToPascalCase`.
* Attempts to match the key against `settings.Properties[propertyName]`.
* If matching fails, attempts to match against reflected properties retrieved via `GetSerializableSettingProperties`.
* Converts the `JsonElement` to the required target property type using `ConvertJsonElementToSettingValue` and applies the setting.
* Calls `settings.Save()` upon completion. Logs exceptions during setting assignment to `System.Diagnostics.Debug.WriteLine`.

#### `ExportManagedJsonSettingsFolder(string tempDir)`
Copies all `*.json` files from `AppUtilities.TextGrabSettingsService.ManagedJsonSettingsFolderPath` to the `settings-data` subfolder inside the temporary directory.

#### `ImportManagedJsonSettingsFolder(string tempDir)`
Copies all `*.json` files located in the `settings-data` folder of the extracted archive into the active local managed JSON settings folder.

---

### Grab Templates Management

#### `ExportGrabTemplatesAsync(string tempDir)`
* Obtains JSON string representation of templates via `GrabTemplateManager.GetTemplatesJsonForExport()` and writes it to `GrabTemplates.json`.
* Copies all image files from `GrabTemplateManager.GetTemplateImagesFolder()` into the `template-images` staging directory inside `tempDir`.

#### `ImportGrabTemplatesAsync(string tempDir)`
* If `GrabTemplates.json` exists in `tempDir`, imports templates using `GrabTemplateManager.ImportTemplatesFromJson`.
* If `GrabTemplates.json` is absent but existing templates exist locally, invokes `GrabTemplateManager.SaveTemplates(...)` to trigger synchronization between legacy settings and sidecar stores.
* Copies template image files from `template-images` subfolder to the local system folder returned by `GrabTemplateManager.GetTemplateImagesFolder()`.

---

### History Management

#### `ExportHistoryAsync(string tempDir)`
* Resolves local history path using `FileUtilities.GetPathToHistory()`.
* Copies `HistoryTextOnly.json` and `HistoryWithImage.json` to `tempDir` if they exist.
* Copies all additional history artifact files (excluding the two JSON files) into the `history` subfolder within `tempDir`.

#### `ImportHistoryAsync(string tempDir)`
* Resolves local history directory path using `FileUtilities.GetPathToHistory()` and creates it if it does not exist.
* Copies `HistoryTextOnly.json` and `HistoryWithImage.json` from `tempDir` into the active history directory.
* Copies all artifact files from the `history` subfolder in `tempDir` to the active history directory.
* Reloads history into memory asynchronously by calling `Singleton<HistoryService>.Instance.LoadHistories()`.

---

### Type Conversion & Reflection Helpers

#### `ConvertToPascalCase(string camelCase)`
Converts camelCase strings to PascalCase by converting the first character to uppercase:
```csharp
char.ToUpper(camelCase[0]) + camelCase.Substring(1)
```

#### `GetSerializableSettingProperties(Type settingsType)`
Reflects over the provided settings `Type` and returns an `IEnumerable<PropertyInfo>` matching criteria:
* Must be readable (`CanRead == true`) and writable (`CanWrite == true`).
* Must not be an indexed property (`GetIndexParameters().Length == 0`).
* Must have `UserScopedSettingAttribute` applied.

#### `ConvertJsonElementToSettingValue(JsonElement jsonElement, Type propertyType)`
Parses a `JsonElement` into a targeted primitive type.
* Supported explicit target types: `string`, `bool`, `int`, `double`, `long`.
* Default/Fallback behavior: Attempts `jsonElement.GetString()`. Returns `null` if conversion fails or throws an exception.

---

## Dependencies & External Services Called

* **System Libraries:** `System.IO`, `System.IO.Compression.ZipFile`, `System.Text.Json`, `System.Reflection`, `System.Configuration`.
* **Application Services/Utilities:**
  * `AutomationProfile`: Resolves temporary directories and configured output directories.
  * `AppUtilities`: Access point for `TextGrabSettings` and `TextGrabSettingsService`.
  * `Singleton<HistoryService>`: Flushes pending history writes (`WriteHistory`) and reloads history (`LoadHistories`).
  * `GrabTemplateManager`: Handles import, export, image directories, and template persistence.
  * `FileUtilities`: Resolves root history folder path (`GetPathToHistory`).