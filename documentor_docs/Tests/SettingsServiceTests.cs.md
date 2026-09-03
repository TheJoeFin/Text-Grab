# `Tests/SettingsServiceTests.cs` Documentation

## Overview

The `SettingsServiceTests` class contains unit tests for the `SettingsService` component within the `Text_Grab` application. It verifies settings management behavior, focusing on:
* **Default vs. File-Backed Settings Modes**: Ensuring settings are properly read from and persisted to standard application settings (legacy/classic settings) or isolated JSON sidecar files (managed settings).
* **Migration & Fallback Logic**: Validating fallback, backfilling, and persistence when upgrading packages or transitioning between classic and sidecar settings.
* **Sidecar File Management**: Testing dynamic creation, updating, overriding, and clearing of JSON files representing individual managed setting stores (e.g., `RegexList.json`, `PostGrabCheckStates.json`, `HiddenSmartPatternIds.json`, `Settings.json`).

---

## Class Architecture & Lifecycle

### Setup and Teardown (`IDisposable`)

`SettingsServiceTests` implements `IDisposable` to manage isolated temporary file systems for test runs:

* **Fields**:
  * `_tempFolder` (`string`): A unique directory created in the temporary folder using a `Guid` (`TextGrab_SettingsService_{Guid}`).
  * `_regularSettingsFilePath` (`string`): Full path to `Settings.json` inside `_tempFolder`.
* **Constructor (`SettingsServiceTests()`)**: Creates `_tempFolder` and sets up `_regularSettingsFilePath`.
* **Teardown (`Dispose()`)**: Recursively deletes `_tempFolder` and its contents after each test completes.

### Helper Methods

* **`CreateService(Settings settings)`**:
  Constructs a `SettingsService` instance pre-configured for unit testing.
  * Disables saving changes to classic settings (`saveClassicSettingsChanges: false`).
  * Directs managed JSON files and regular sidecar files to `_tempFolder`.
  * Passes `localSettings: null`.
* **`SerializeRegexes(string id)`**:
  Serializes a single `StoredRegex` object (containing `Id`, `Name`, `Pattern`, and `Description`) into a JSON string representing a single-element array.

---

## Key Test Categories & Logic

### 1. Stored Regexes & Sidecar Migration (`LoadStoredRegexes`)

These tests cover loading and backfilling `StoredRegex` items depending on whether file-backed mode is enabled and whether sidecar files (`RegexList.json`) exist.

* **`LoadStoredRegexes_DefaultModePrefersLegacyAndKeepsLegacyPopulated`**
  * **Condition**: `EnableFileBackedManagedSettings = false`, classic `RegexList` populated, sidecar JSON file exists with different content.
  * **Assertion**: Prefers legacy regex ID over sidecar ID; leaves legacy setting and sidecar file populated.
* **`LoadStoredRegexes_DefaultModeBackfillsLegacyFromSidecarWhenNeeded`**
  * **Condition**: `EnableFileBackedManagedSettings = false`, classic `RegexList` is empty, sidecar JSON exists.
  * **Assertion**: Loads sidecar regex and backfills classic `RegexList` setting with sidecar content.
* **`LoadStoredRegexes_FileBackedModePrefersSidecarAndBackfillsLegacy`**
  * **Condition**: `EnableFileBackedManagedSettings = true`, legacy `RegexList` and sidecar JSON both exist.
  * **Assertion**: Prefers sidecar regex ID over legacy ID and updates legacy `RegexList` with sidecar content.
* **`LoadStoredRegexes_SidecarSurvivesSimulatedPackageUpgrade`**
  * **Condition**: Legacy `RegexList` is empty (simulating package upgrade reset), but `RegexList.json` sidecar exists.
  * **Assertion**: Successfully loads sidecar regex and backfills `Settings.RegexList`.

### 2. File-Backed Managed Settings & Deletion

* **`ClearingManagedSettingClearsLegacyAndSidecar`**
  * **Logic**: Saves a `StoredRegex`, ensures `RegexList.json` exists, then clears `settings.RegexList`.
  * **Assertion**: Verifies setting `settings.RegexList = string.Empty` deletes `RegexList.json` from disk and leaves `LoadStoredRegexes()` empty.
* **`SavePostGrabCheckStates_FileBackedModeWritesBothStores`**
  * **Logic**: Calls `service.SavePostGrabCheckStates(...)` in file-backed mode.
  * **Assertion**: Updates `settings.PostGrabCheckStates`, writes `PostGrabCheckStates.json` to disk, and reads values back via `LoadPostGrabCheckStates()`.
* **`SaveHiddenSmartPatternIds_FileBackedModeWritesBothStores`**
  * **Logic**: Calls `service.SaveHiddenSmartPatternIds(...)` in file-backed mode.
  * **Assertion**: Updates `settings.HiddenSmartPatternIds`, writes `HiddenSmartPatternIds.json` to disk, and matches results from `LoadHiddenSmartPatternIds()`.

### 3. Service Constructor & Sidecar Synchronization Behavior

* **`Constructor_FileBackedModeReflectsSettingsValueSetBeforeConstruction`**
  * **Assertion**: `IsFileBackedManagedSettingsEnabled` evaluates to `true` when initialized with `EnableFileBackedManagedSettings = true`.
* **`Constructor_FileBackedModeDefaultsToFalseWhenNotSet`**
  * **Assertion**: `IsFileBackedManagedSettingsEnabled` evaluates to `false` when initialized with `EnableFileBackedManagedSettings = false`.
* **`Constructor_UnpackagedUpgradePathDoesNotThrowWhenNoPreviousVersion`**
  * **Assertion**: Verifies service constructs without throwing when `FirstRun` is `true` and `localSettings` is `null`.
* **`Constructor_RegularSettingsSidecarWithFileBackedFlagImportsPortableSettings`**
  * **Condition**: A `Settings.json` sidecar exists on disk with portable settings overrides.
  * **Assertion**: Importing imports values into classic settings (e.g., `EnableFileBackedManagedSettings`, `ShowToast`, `DefaultLaunch`).
* **`Constructor_FileBackedModeWithoutRegularSettingsSidecarCreatesOneFromClassicSettings`**
  * **Condition**: `EnableFileBackedManagedSettings = true`, no `Settings.json` sidecar file present.
  * **Assertion**: Creates `Settings.json` sidecar from classic settings while ignoring complex properties like `GrabTemplatesJSON`.
* **`Constructor_RegularSettingsSidecarOnlyOverridesKnownValuesAndBackfillsMissingOnes`**
  * **Condition**: `Settings.json` contains partial properties.
  * **Assertion**: Overrides specified properties, retains classic settings for missing properties, and backfills missing properties into `Settings.json`.
* **`RegularSettingChange_PersistsToRegularSettingsSidecarWhenFileBackedModeEnabled`**
  * **Logic**: Changes property `ShowToast` directly on `Settings` when file-backed mode is active.
  * **Assertion**: Automatically updates and persists the changed property into `Settings.json`.

---

## Dependencies & Imports

* **`System.IO`**: File and folder management (`Directory`, `File`, `Path`).
* **`System.Text.Json`**: Serialization (`JsonSerializer`).
* **`Text_Grab.Models`**: Data models (`StoredRegex`).
* **`Text_Grab.Properties`**: Settings definitions (`Settings`).
* **`Text_Grab.Services`**: Implementation being tested (`SettingsService`).
* **`xUnit`**: Test framework primitives (`[Fact]`, `Assert`).