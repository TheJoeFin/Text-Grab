# Technical Documentation: `Text-Grab/Services/SettingsService.cs`

## Overview

The `SettingsService` class in `Text-Grab.Services` is the central service responsible for managing, persisting, migrating, and synchronizing application settings across multiple storage layers. It supports standard classic settings (`Properties.Settings`), Windows packaged app storage (`ApplicationDataContainer`), sidecar JSON files, and dedicated managed JSON setting files.

The class implements `IDisposable` to manage property change subscription lifecycles on the underlying `Properties.Settings` instance.

---

## Key Architecture & Storage Model

`SettingsService` orchestrates four primary settings storage mechanisms:

1. **Classic Settings (`Properties.Settings`)**: The standard .NET Application/User settings backend.
2. **Local Settings Container (`Windows.Storage.ApplicationDataContainer`)**: Used when running as a packaged Windows app (MSIX/AppX) to persist user values across package upgrades.
3. **Regular Settings Sidecar File (`Settings.json`)**: A JSON file used in unpackaged/file-backed environments to persist non-managed user-scoped settings.
4. **Managed JSON Settings Directory (`settings-data/*.json`)**: Individual JSON files dedicated to complex structured types (e.g., shortcuts, buttons, regular expressions) to avoid size limits and enable easy file-backed syncing/editing.

```
                         ┌───────────────────────────────┐
                         │         SettingsService       │
                         └──────────────┬────────────────┘
                                        │
    ┌───────────────────┬───────────────┼───────────────────┬───────────────────┐
    │                   │               │                   │                   │
┌───▼─────────────┐ ┌───▼────────────┐ ┌▼─────────────────┐ ┌▼─────────────────┐ ┌▼─────────────────┐
│ ClassicSettings │ │ LocalSettings  │ │ Regular Sidecar │ │ Managed JSON     │ │ In-Memory Caches │
│ (Properties)    │ │ (AppContainer) │ │ (Settings.json) │ │ (settings-data/) │ │ (Thread-Locked)  │
└─────────────────┘ └────────────────┘ └─────────────────┘ └───────────────────┘ └─────────────────┘
```

---

## Constants & File Mappings

### Constants

* `ManagedJsonSettingsFolderName`: `"settings-data"`
* `RegularSettingsSidecarFileName`: `"Settings.json"`

### Managed JSON Mappings (`ManagedJsonSettingFiles`)

The following dictionary maps `Properties.Settings` property names to their respective JSON filenames within the `settings-data` directory:

| `Properties.Settings` Property Key | File Name |
| :--- | :--- |
| `RegexList` | `RegexList.json` |
| `HiddenSmartPatternIds` | `HiddenSmartPatternIds.json` |
| `ShortcutKeySets` | `ShortcutKeySets.json` |
| `BottomButtonsJson` | `BottomButtons.json` |
| `WebSearchItemsJson` | `WebSearchItems.json` |
| `PostGrabJSON` | `PostGrabActions.json` |
| `PostGrabCheckStates` | `PostGrabCheckStates.json` |

---

## Class Members & Public API

### Fields & Properties

* **`ClassicSettings`** (`Properties.Settings`): Public field holding the current instance of classic .NET settings.
* **`IsFileBackedManagedSettingsEnabled`** (`bool`): Returns whether file-backed managed settings are preferred.
* **`ManagedJsonSettingsFolderPath`** (`string`): Returns the directory path for the managed JSON setting files.

### Constructors

#### `public SettingsService()`
Default constructor. Delegates to the internal constructor, providing `Properties.Settings.Default` and, if running packaged without an active `AutomationProfile`, `ApplicationData.Current.LocalSettings`.

#### `internal SettingsService(...)`
```csharp
internal SettingsService(
    Properties.Settings classicSettings,
    ApplicationDataContainer? localSettings,
    string? managedJsonSettingsFolderPath = null,
    string? regularSettingsSidecarFilePath = null,
    bool saveClassicSettingsChanges = true)
```
* **Initialization Flow**:
  1. Sets internal paths and configuration parameters.
  2. Reads existing sidecar snapshot (`Settings.json`) if unpackaged.
  3. **First-Run / Automation Handling**:
     * If an `AutomationProfile` exists without a classic settings file, applies the seed and saves.
     * If `FirstRun` is true:
       * Packaged: Migrates `_localSettings` (`ApplicationDataContainer`) into `ClassicSettings`.
       * Unpackaged: Calls `ClassicSettings.Upgrade()` to pull settings from previous app versions.
  4. Synchronizes sidecar file with classic settings if file-backed settings are enabled or requested by the sidecar file.
  5. Subscribes to `ClassicSettings.PropertyChanged`.

---

## Method Reference

### Lifecycle & Event Handling

#### `public void Dispose()`
Unsubscribes `ClassicSettings_PropertyChanged` from `ClassicSettings.PropertyChanged` to prevent memory leaks.

#### `private void ClassicSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)`
Listens for property changes on `ClassicSettings`.
* If property is a Managed JSON setting and changes are not suppressed, calls `HandleManagedJsonSettingChanged`.
* Otherwise, saves property to `ApplicationDataContainer` via `SaveSettingInContainer` and updates the sidecar file if required.

---

### Container Access Methods (`ApplicationDataContainer`)

#### `public T? GetSettingFromContainer<T>(string name)`
Attempts to fetch a typed setting value from `_localSettings` (`ApplicationDataContainer`). Returns `default(T)` if running unpackaged or if the key does not exist.

#### `public void SaveSettingInContainer<T>(string name, T value)`
Saves a setting to `_localSettings`.
* Handles `COMException` with HResult `0x80073DC8` (exceeding the 8 KB limit of `ApplicationDataContainer`) by catching the exception and logging a debug trace.

---

### Domain Model Persistence Methods

The following load/save pairs load structured models from JSON storage, using internal caching and deep cloning to protect thread safety.

| Domain Data Type | Load Method | Save Method |
| :--- | :--- | :--- |
| Custom Regexes | `LoadStoredRegexes()` | `SaveStoredRegexes(IEnumerable<StoredRegex>)` |
| Hidden Smart Pattern IDs | `LoadHiddenSmartPatternIds()` | `SaveHiddenSmartPatternIds(IEnumerable<string>)` |
| Shortcut Keys | `LoadShortcutKeySets()` | `SaveShortcutKeySets(IEnumerable<ShortcutKeySet>)` |
| Bottom Bar Buttons | `LoadBottomBarButtons()` | `SaveBottomBarButtons(IEnumerable<ButtonInfo>)` |
| Web Search URLs | `LoadWebSearchUrls()` | `SaveWebSearchUrls(IEnumerable<WebSearchUrlModel>)` |
| Post-Grab Actions | `LoadPostGrabActions()` | `SavePostGrabActions(IEnumerable<ButtonInfo>)` |
| Post-Grab Check States | `LoadPostGrabCheckStates()` | `SavePostGrabCheckStates(IReadOnlyDictionary<string, bool>)` |

---

### Managed JSON Processing Logic

#### `internal static bool IsManagedJsonSetting(string propertyName)`
Checks if the given setting property name is registered in `ManagedJsonSettingFiles`.

#### `internal string GetManagedJsonSettingValueForExport(string propertyName)`
Reads and returns the JSON string for export purposes without backfilling or mutating existing settings stores.

#### `internal void ReconcileManagedJsonSettings()`
Invalidates all managed JSON caches and re-reads all managed JSON setting files, backfilling values between files and classic settings where appropriate.

#### `private T LoadManagedJson<T>(...)`
Thread-safe generic loader that:
1. Checks and returns a clone of the cached instance under `_managedJsonLock`.
2. Reads raw text via `ReadManagedJsonSettingText`.
3. Deserializes JSON into model object `T` (falls back to `emptyFactory` on failure).
4. Stores cloned data in cache and returns a defensive copy.

#### `private void SaveManagedJson<T>(...)`
Thread-safe generic writer that:
1. Serializes input `T` into JSON string.
2. Updates in-memory cache under `_managedJsonLock`.
3. Sets value in `ClassicSettings` (suppressing property change events to avoid re-entry loops).
4. Writes JSON text to disk (`settings-data/*.json`) and container (`_localSettings`).
5. Persists `ClassicSettings.Save()` if enabled.

#### `private string ReadManagedJsonSettingText(string propertyName)`
Evaluates the preferred source (File vs ClassicSettings based on `_preferFileBackedManagedSettings`).
If a discrepancy or missing value exists between sources, it automatically **backfills** the missing/out-of-date storage layer with the selected value.

---

### Regular Settings Sidecar Processing (`Settings.json`)

Used to mirror user settings to a JSON sidecar file when running unpackaged or file-backed:

* `SyncRegularSettingsSidecarWithClassic`: Merges regular properties from sidecar JSON snapshot into `ClassicSettings`.
* `CaptureRegularSettingsSnapshot`: Filters user-scoped non-managed settings from `ClassicSettings` into a dictionary.
* `ApplyRegularSettingsSnapshot`: Writes sidecar snapshot values into `ClassicSettings`.
* `PersistRegularSettingsSidecar`: Serializes regular settings into indented JSON and writes to `Settings.json`.
* `ReadRegularSettingsSidecarSnapshot`: Reads `Settings.json` into a key-value `Dictionary<string, JsonElement>`.
* `IsRegularSettingsSidecarProperty`: Evaluates whether a property has `UserScopedSettingAttribute` and is neither a managed JSON setting nor `GrabTemplatesJSON`.
* `TryConvertJsonElementToSettingValue`: Converts primitive `JsonElement` values to `string`, `bool`, `int`, `double`, or `long`.

---

## Defensive Copying Helpers

To guarantee object isolation and prevent side effects from direct mutation of cached objects, `SettingsService` provides private cloning methods:

* `CloneStoredRegexes`
* `CloneHiddenSmartPatternIds`
* `CloneShortcutKeySets`
* `CloneButtonInfos`
* `CloneWebSearchUrls`
* `CloneCheckStates`

---

## Directory Location Resolution Logic

### Managed JSON Directory (`GetManagedJsonSettingsFolderPath`)
1. Active `AutomationProfile` directory (if set).
2. `ApplicationData.Current.LocalFolder.Path/settings-data` (if Packaged).
3. `{ExecutableDirectory}/settings-data` (if Unpackaged, defaulting to `c:\Text-Grab\settings-data`).

### Regular Settings Sidecar Path (`GetRegularSettingsSidecarFilePath`)
1. Active `AutomationProfile` directory + `Settings.json`.
2. `ApplicationData.Current.LocalFolder.Path/Settings.json` (if Packaged).
3. `{ExecutableDirectory}/Settings.json` (if Unpackaged, defaulting to `c:\Text-Grab\Settings.json`).