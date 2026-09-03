# Documentation: `Tests/GrabTemplateManagerTests.cs`

## Overview

The `GrabTemplateManagerTests` class is a test suite designed to validate the functionality of `GrabTemplateManager` and related model classes (`GrabTemplate`, `TemplateRegion`, `ButtonInfo`) in the `Text_Grab` application. 

It tests template data persistence, CRUD (Create, Read, Update, Delete) operations, template duplication, sidecar file/legacy settings migration, button model generation, invalid JSON fallback, and template model validation rules.

---

## Class Architecture & Test Lifecycle

### Attributes & Interfaces
* **`[Collection("Settings isolation")]`**: Ensures tests run within an isolated xUnit test collection to prevent race conditions or cross-test contamination when reading and modifying shared application settings (`Settings.Default`).
* **`IDisposable`**: Implements fixture setup and cleanup around every test run.

### State Initialization & Teardown

#### Constructor (`GrabTemplateManagerTests()`)
Before each test executes, the constructor performs environment isolation:
1. Generates unique temporary file paths for JSON storage (`_tempFilePath`) and image storage (`_tempImagesFolder`).
2. Backs up original application configuration state:
   * `Settings.Default.GrabTemplatesJSON`
   * `Settings.Default.EnableFileBackedManagedSettings`
   * `GrabTemplateManager.TestPreferFileBackedMode`
3. Overrides `GrabTemplateManager` static test properties with the isolated test paths:
   * Sets `GrabTemplateManager.TestFilePath` to `_tempFilePath`
   * Sets `GrabTemplateManager.TestImagesFolderPath` to `_tempImagesFolder`
   * Sets `GrabTemplateManager.TestPreferFileBackedMode` to `false`
4. Clears and saves default application settings (`GrabTemplatesJSON` = `string.Empty`, `EnableFileBackedManagedSettings` = `false`).

#### Teardown (`Dispose()`)
After each test finishes, `Dispose()` resets all modified global and static states:
1. Resets `GrabTemplateManager` static test overrides back to original values or `null`.
2. Restores and saves the original application settings (`GrabTemplatesJSON` and `EnableFileBackedManagedSettings`).
3. Deletes temporary files (`_tempFilePath`) and directories (`_tempImagesFolder`) created during the test.

---

## Helper Methods

### `CreateSampleTemplate(string name)`
* **Type**: `private static GrabTemplate`
* **Purpose**: Generates a standard `GrabTemplate` instance pre-populated with test values:
  * `Id`: Newly generated `Guid` string.
  * `Name`: Parameterized string.
  * `Description`: `"Test template"`
  * `OutputTemplate`: `"{1}"`
  * `ReferenceImageWidth`: `800`
  * `ReferenceImageHeight`: `600`
  * `Regions`: Contains a single `TemplateRegion` with `RegionNumber = 1`, `Label = "Field 1"`, and bounding coordinates (`RatioLeft`, `RatioTop`, `RatioWidth`, `RatioHeight`).

---

## Test Cases Detailed Breakdown

### 1. Template Persistence & Synchronization Tests

| Test Method | Description | Assertions |
| :--- | :--- | :--- |
| `GetAllTemplates_WhenEmpty_ReturnsEmptyList` | Tests `GetAllTemplates()` behavior when no template data exists. | Verifies that the returned list is empty. |
| `GetAllTemplates_BackfillsLegacyFromSidecarWhenLegacyMissing` | Verifies that if template data exists in the sidecar file but legacy settings are empty, `GetAllTemplates()` reads from the file and backfills `Settings.Default.GrabTemplatesJSON`. | Asserts a single template is returned, matching the original ID, and verifies `Settings.Default.GrabTemplatesJSON` contains the ID. |
| `GetAllTemplates_FileBackedModePrefersFileAndBackfillsLegacy` | Verifies that when `TestPreferFileBackedMode` is enabled, `GetAllTemplates()` prioritizes the sidecar file template over the legacy settings template and updates legacy settings. | Asserts the returned template matches the sidecar template ID and updates `GrabTemplatesJSON`. |
| `SaveTemplates_WritesBothFileAndLegacySetting` | Verifies `SaveTemplates` persists template data to both the sidecar file path and the legacy settings store. | Asserts `_tempFilePath` exists, and both the file text and `GrabTemplatesJSON` contain the template ID. |
| `GetAllTemplates_CorruptJson_ReturnsEmptyList` | Tests handling of malformed/invalid JSON data inside the sidecar file. | Writes invalid JSON text to `_tempFilePath` and asserts `GetAllTemplates()` gracefully returns an empty list. |

---

### 2. CRUD Operations & Duplication Tests

| Test Method | Description | Assertions |
| :--- | :--- | :--- |
| `GetAllTemplates_AfterAddingTemplate_ReturnsSavedTemplate` | Tests adding a new template via `AddOrUpdateTemplate`. | Asserts `GetAllTemplates()` returns a single template matching the added name (`"Invoice"`). |
| `GetTemplateById_ExistingId_ReturnsTemplate` | Tests searching for a template using a valid ID. | Asserts the returned object is non-null, with matching `Id` and `Name`. |
| `GetTemplateById_NonExistentId_ReturnsNull` | Tests searching for a template using an unmapped ID. | Asserts the method returns `null`. |
| `AddOrUpdateTemplate_AddNew_IncrementsCount` | Verifies adding distinct templates increases total count. | Adds two templates and asserts total count equals 2. |
| `AddOrUpdateTemplate_UpdateExisting_ReplacesByIdNotDuplicate` | Verifies updating an existing template (same `Id`) modifies the entry instead of creating a duplicate. | Adds a template, modifies its name, re-adds it, and asserts total template count remains 1 with the updated name. |
| `DeleteTemplate_ExistingId_RemovesTemplate` | Tests deleting a template by its ID. | Adds a template, calls `DeleteTemplate`, and asserts `GetAllTemplates()` is empty. |
| `DeleteTemplate_NonExistentId_DoesNotThrow` | Verifies attempting to delete a non-existent template ID executes without error or side effects. | Adds one template, attempts to delete `"does-not-exist"`, and asserts count remains 1. |
| `DuplicateTemplate_ValidId_CreatesNewTemplateWithCopyPrefix` | Tests cloning an existing template. | Asserts copy is non-null, has a different `Id`, contains `"(copy)"` in `Name`, and total template count becomes 2. |
| `DuplicateTemplate_NonExistentId_ReturnsNull` | Tests duplicating a missing template ID. | Asserts the method returns `null`. |

---

### 3. Integration & Model Utility Tests

#### UI Button Model Creation
* **`CreateButtonInfoForTemplate_SetsTemplateId`**
  * Verifies `GrabTemplateManager.CreateButtonInfoForTemplate(template)` correctly constructs a UI `ButtonInfo` instance.
  * **Assertions**:
    * `button.TemplateId` matches `template.Id`.
    * `button.ClickEvent` equals `"ApplyTemplate_Click"`.
    * `button.ButtonText` matches `template.Name`.

#### `GrabTemplate.IsValid` Property Validation
* **`GrabTemplate_IsValid_TrueWhenNameAndOutputTemplateSet`**: Asserts `IsValid` returns `true` when `Name` and `OutputTemplate` are provided.
* **`GrabTemplate_IsValid_TrueWhenNoRegionsButHasNameAndOutputTemplate`**: Asserts `IsValid` returns `true` even if `Regions` list is cleared, provided `Name` and `OutputTemplate` exist.
* **`GrabTemplate_IsValid_FalseWhenNameEmpty`**: Asserts `IsValid` returns `false` if `Name` is empty.
* **`GrabTemplate_IsValid_FalseWhenOutputTemplateEmpty`**: Asserts `IsValid` returns `false` if `OutputTemplate` is empty.

#### Region Parsing
* **`GrabTemplate_GetReferencedRegionNumbers_ParsesPlaceholders`**
  * Tests `template.GetReferencedRegionNumbers()` capability to extract referenced region index placeholders from `OutputTemplate` strings (e.g., `"{1} {2} {1:upper}"`).
  * **Assertions**:
    * Extracted set contains region numbers `1` and `2`.
    * Duplicate region references (`{1}` and `{1:upper}`) are deduplicated, producing a distinct count of `2`.