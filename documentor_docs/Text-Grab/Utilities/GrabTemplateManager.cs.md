# Documentation: `GrabTemplateManager.cs`

## Overview

The `GrabTemplateManager` is a static utility class in the `Text_Grab.Utilities` namespace responsible for managing the lifecycle, storage, and persistence of `GrabTemplate` objects. 

It implements a dual-storage strategy during a transition release phase, keeping legacy application settings (`DefaultSettings.GrabTemplatesJSON`) synchronized with a file-backed JSON store (`GrabTemplates.json`). Additionally, it provides methods for saving reference images, generating UI button wrappers (`ButtonInfo`), and managing template usage metadata.

> **Thread Safety Note:** This class currently contains no explicit thread-safety guards or lock mechanisms. All calls are expected to take place on the UI thread. If template read/write operations are moved to background threads, a locking mechanism should be introduced.

---

## Class Architecture & Configuration

### Static Constants & Fields

* **`DefaultSettings`** (`Settings`): Reference to `AppUtilities.TextGrabSettings`.
* **`JsonOptions`** (`JsonSerializerOptions`): Pre-configured JSON options set to indent output (`WriteIndented = true`) and perform case-insensitive property matching (`PropertyNameCaseInsensitive = true`).
* **`TemplatesFileName`** (`string`): Constant set to `"GrabTemplates.json"`.

### Test Seams / Property Overrides
The class provides internal static properties to override paths and behaviors during automated testing:
* **`TestFilePath`** (`string?`): Overrides the default target path for the templates JSON file.
* **`TestImagesFolderPath`** (`string?`): Overrides the directory path used to store template reference images.
* **`TestPreferFileBackedMode`** (`bool?`): Overrides the setting that determines whether file-backed storage takes precedence over application settings.

---

## Path Resolution

### `GetTemplatesFilePath()`
Resolves the location of the `GrabTemplates.json` file using the following priority order:
1. `TestFilePath` (if set).
2. `AutomationProfile.Current.TemplatesFilePath` (if an active automation profile exists).
3. Packaged Application Local Folder (`Windows.Storage.ApplicationData.Current.LocalFolder.Path`).
4. Unpackaged Executable Directory (`c:\Text-Grab` fallback if executable path resolution fails).

### `GetTemplateImagesFolder()`
Resolves the directory path for reference images (`template-images`) using the following priority order:
1. `TestImagesFolderPath` (if set).
2. Path relative to `TestFilePath` directory (if `TestFilePath` is set).
3. `AutomationProfile.Current.TemplateImagesDirectory` (if an active automation profile exists).
4. Packaged Application Local Folder (`LocalFolder/template-images`).
5. Unpackaged Executable Directory (`c:\Text-Grab\template-images` fallback).

---

## Operations & Methods

### 1. Read Operations

* **`GetAllTemplates()`** -> `List<GrabTemplate>`
  * Retrieves all saved templates by resolving the storage state via `ResolveTemplatesJson()` and deserializing the returned JSON.
  * Exception Handling: Silently catches `JsonException` and logs `IOException` via `Debug.WriteLine`. Returns an empty list `[]` on failure to prevent application crashes.

* **`GetTemplateById(string id)`** -> `GrabTemplate?`
  * Searches all saved templates and returns the template matching the specified ID, or `null` if the ID is invalid or not found.

---

### 2. Write & CRUD Operations

* **`SaveTemplates(List<GrabTemplate> templates)`** -> `void`
  * Serializes the provided list of `GrabTemplate` objects into JSON format and persists them across both storage providers via `SaveTemplatesJson`.

* **`AddOrUpdateTemplate(GrabTemplate template)`** -> `void`
  * Checks if a template with the given `Id` already exists. If found, it updates the existing entry; otherwise, it appends the new template. Automatically persists changes.

* **`DeleteTemplate(string id)`** -> `void`
  * Removes the template matching the provided `id`. If a template was successfully removed, changes are persisted.

* **`DuplicateTemplate(string id)`** -> `GrabTemplate?`
  * Creates a clone of the specified template via JSON serialization/deserialization.
  * Modifies the cloned object:
    * Generates a new `Guid` for `Id`.
    * Appends `" (copy)"` to the original `Name`.
    * Sets `CreatedDate` to `DateTimeOffset.Now`.
    * Resets `LastUsedDate` to `null`.
  * Saves and returns the duplicated template (or `null` if the source template was not found or deserialization failed).

* **`RecordUsage(string templateId)`** -> `void`
  * Locates the specified template, updates its `LastUsedDate` to `DateTimeOffset.Now`, and persists the change.

---

### 3. Data Import / Export

* **`GetTemplatesJsonForExport()`** -> `string`
  * Retrieves all templates and serializes them into an indented JSON string suitable for exporting.

* **`ImportTemplatesFromJson(string templatesJson)`** -> `void`
  * Deserializes a raw JSON string into a list of `GrabTemplate` objects and saves them, replacing existing template data.

---

### 4. Reference Image Handling

* **`SaveTemplateReferenceImage(BitmapSource? imageSource, string templateName, string templateId)`** -> `string?`
  * Saves a WPF `BitmapSource` image as a PNG file inside the template images directory.
  * **File Naming Format:** `{SanitizedName}_{First8CharsOfID}.png`
  * **Atomic Write Strategy:** To avoid WPF file locking conflicts (e.g., when an image is read without `OnLoad` options), the image is encoded via `PngBitmapEncoder` into a temporary file (`{Guid}.tmp`) before atomically overwriting the target destination via `File.Move(..., overwrite: true)`.
  * Returns the full file path on success, or `null` if `imageSource` is null or writing fails.

---

### 5. UI Integration Bridge

* **`CreateButtonInfoForTemplate(GrabTemplate template)`** -> `ButtonInfo`
  * Constructs a WPF UI action configuration object (`ButtonInfo`) associated with the provided template.
  * **Properties set:**
    * `buttonText`: Template name.
    * `clickEvent`: `"ApplyTemplate_Click"`.
    * `symbolIcon`: `SymbolRegular.DocumentTableSearch24`.
    * `defaultCheckState`: `DefaultCheckState.Off`.
    * `TemplateId`: Template GUID string.
    * `IsRelevantForFullscreenGrab`: `true`.
    * `IsRelevantForEditWindow`: `false`.
    * `OrderNumber`: `7.0`.

---

## Synchronisation & Dual-Storage Mechanism

During the migration period, settings can be stored in both `AppUtilities.TextGrabSettings.GrabTemplatesJSON` (legacy) and a standalone `GrabTemplates.json` file. Synchronization is managed internally via private methods:

```
                  ┌───────────────────────────────┐
                  │    ResolveTemplatesJson()     │
                  └──────────────┬────────────────┘
                                 │
           ┌─────────────────────┴─────────────────────┐
           ▼                                           ▼
┌──────────────────────┐                   ┌──────────────────────┐
│ Legacy App Settings  │                   │  File-Backed JSON    │
│  GrabTemplatesJSON   │                   │  GrabTemplates.json  │
└──────────┬───────────┘                   └──────────┬───────────┘
           │                                          │
           └─────────────────────┬────────────────────┘
                                 │
                        Evaluate Preference
                (PreferFileBackedTemplates flag)
                                 │
                                 ▼
                     Select Preferred / Fallback
                                 │
                   ┌─────────────┴─────────────┐
                   ▼                           ▼
          Sync Legacy Settings         Sync Storage File
        (if out-of-date/empty)       (if out-of-date/empty)
```

### Key Internal Synchronization Methods

* **`PreferFileBackedTemplates`**: Evaluates whether file-backed storage takes precedence using `TestPreferFileBackedMode` or `AppUtilities.TextGrabSettingsService.IsFileBackedManagedSettingsEnabled`.
* **`ResolveTemplatesJson()`**:
  1. Reads both legacy settings JSON and file-backed JSON.
  2. Selects the primary text payload based on `PreferFileBackedTemplates`. If the primary is empty, falls back to secondary.
  3. Automatically updates and writes back to whichever storage layer is out of date or missing the content to ensure both sources remain synchronized.
* **`SaveTemplatesJson(string json)`**: Updates both `SetLegacyTemplatesJson` and `TryWriteTemplatesFile` simultaneously whenever a modification occurs.
* **`TryWriteTemplatesFile(string json)`**: Ensures the target directory exists and executes `File.WriteAllText`. Returns `true` on success or catches exceptions and returns `false`.
* **`SetLegacyTemplatesJson(string json)`**: Updates `DefaultSettings.GrabTemplatesJSON` and persists application settings if the value has changed.