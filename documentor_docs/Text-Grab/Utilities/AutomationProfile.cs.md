# Technical Documentation: `AutomationProfile.cs`

## Overview

The `AutomationProfile` class in `Text-Grab.Utilities` provides isolated, profile-based configuration and directory management for automated testing and execution environments. It allows Text-Grab to run in an isolated environment by defining isolated paths for settings, diagnostics, templates, history, and temporary files. It also provides mechanisms to seed application settings and override the active profile for testing.

---

## Class Definition

```csharp
namespace Text_Grab.Utilities;

internal sealed class AutomationProfile
```

* **Access Modifier:** `internal`
* **Type:** `sealed class`

---

## Constants

### Environment Variables
* `ProfileEnvironmentVariable` (`"TEXT_GRAB_AUTOMATION_PROFILE"`): Environment variable specifying the root path of the automation profile.
* `SystemIntegrationEnvironmentVariable` (`"TEXT_GRAB_AUTOMATION_SYSTEM_INTEGRATION"`): Environment variable enabling system integration permissions.
* `DisposableRegistrationEnvironmentVariable` (`"TEXT_GRAB_AUTOMATION_DISPOSABLE_REGISTRATION"`): Environment variable requesting persistent system registration permissions.
* `DisposableVmEnvironmentVariable` (`"TEXT_GRAB_DISPOSABLE_VM"`): Environment variable indicating that the environment is running inside a disposable virtual machine.

### Command-Line Arguments
* `ProfileArgument` (`"--automation-profile"`): Argument prefix or flag to pass the profile root path.
* `SystemIntegrationArgument` (`"--automation-system-integration"`): Argument flag to enable system integration.
* `DisposableRegistrationArgument` (`"--automation-disposable-registration"`): Argument flag to request persistent registration.

### File Names
* `SeedFileName` (`"seed.json"`): The JSON filename located in the automation profile root used to seed initial application settings.

---

## Properties

### Static Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| `Current` | `AutomationProfile?` | Gets the currently active `AutomationProfile`. Returns `_currentOverride` if a test override is active; otherwise, returns the lazily evaluated ambient profile (`CurrentProfile.Value`). |

### Instance Properties

#### Configuration Flags
* `RootPath` (`string`): Absolute path to the root directory of the automation profile.
* `AllowsSystemIntegration` (`bool`): Indicates whether system integration is allowed.
* `AllowsPersistentRegistration` (`bool`): Indicates whether persistent HKCU registration is allowed (requires system integration, disposable registration, and a disposable VM flag).

#### Path Helpers
All path properties are dynamically calculated relative to `RootPath`:

| Property | Path Relative to `RootPath` |
| :--- | :--- |
| `SettingsDirectory` | `\settings` |
| `ClassicSettingsFilePath` | `\settings\classic-settings.json` |
| `ManagedSettingsDirectory` | `\settings-data` |
| `TemplatesFilePath` | `\GrabTemplates.json` |
| `TemplateImagesDirectory` | `\template-images` |
| `HistoryDirectory` | `\history` |
| `DataDirectory` | `\data` |
| `OutputDirectory` | `\output` |
| `LookupFilePath` | `\lookup\QuickSimpleLookup.csv` |
| `TemporaryDirectory` | `\temp` |
| `DiagnosticsDirectory` | `\diagnostics` |
| `DiagnosticsLogPath` | `\diagnostics\events.jsonl` |
| `FailureSentinelPath` | `\diagnostics\failure.json` |

---

## Public & Internal Methods

### Profile Creation & Checking

#### `TryCreate(IEnumerable<string> arguments, Func<string, string?> environmentVariable)`
* **Type:** `static AutomationProfile?`
* **Purpose:** Evaluates command-line arguments and environment variables to construct an `AutomationProfile`.
* **Behavior:**
  1. Checks environment variables for default values.
  2. Parses argument list for `--automation-profile` (supports both space-separated `--automation-profile <path>` and key-value `--automation-profile=<path>`), `--automation-system-integration`, and `--automation-disposable-registration`.
  3. Returns `null` if no valid profile path is supplied or if path resolution fails.
  4. Evaluates `AllowsPersistentRegistration` which requires `AllowsSystemIntegration == true`, `RequestsPersistentRegistration == true`, and `TEXT_GRAB_DISPOSABLE_VM` set to `"true"` or `"1"`.
  5. Reads `seed.json` from the root path if present and instantiates the `AutomationProfile`.

#### `IsAutomationArgument(string argument)`
* **Type:** `static bool`
* **Purpose:** Determines if a given command-line argument string is an automation-specific flag.
* **Returns:** `true` if `argument` matches or starts with any of the automation CLI argument constants; otherwise, `false`.

---

### Temporary File Operations

#### `GetTemporaryDirectory()`
* **Type:** `static string`
* **Purpose:** Retrieves the temporary directory path.
* **Returns:** `Current.TemporaryDirectory` (creating the directory on disk if it does not exist) if an active automation profile exists; otherwise, returns the system default `Path.GetTempPath()`.

#### `GetTemporaryFilePath(string extension = ".tmp")`
* **Type:** `static string`
* **Purpose:** Generates a unique temporary file path.
* **Parameters:** `extension` (default: `".tmp"`). Automatically prepends a period if missing.
* **Returns:** A file path inside the temporary directory using a generated GUID (`Guid.NewGuid():N`).

---

### Test Seam Support

#### `OverrideCurrentForTests(AutomationProfile? profile)`
* **Type:** `static IDisposable`
* **Purpose:** Temporarily replaces `Current` with a mock or specific profile during unit testing.
* **Returns:** An `IDisposable` token (`CurrentOverrideScope`) that restores the previous override state when disposed.

---

### Settings Seeding

#### `ApplySeed(Properties.Settings settings)`
* **Type:** `void`
* **Purpose:** Applies default automated testing configurations and overrides them with custom key-value pairs loaded from `seed.json`.
* **Default Applied Overrides:**
  * `FirstRun` = `false`
  * `RunInTheBackground` = `false`
  * `StartupOnLogin` = `false`
  * `GlobalHotkeysEnabled` = `false`
  * `ShowToast` = `false`
  * `DefaultLaunch` = `TextGrabMode.EditText.ToString()`
  * `LastUsedLang` = `"en-US"`
  * `UseTesseract` = `false`
  * `UiAutomationEnabled` = `false`
  * `WindowsAiDescriptionEnabled` = `false`
  * `EnableFileBackedManagedSettings` = `true`
  * `LookupFileLocation` = `LookupFilePath`
* **Custom Seed Application:** Iterates through `_seedValues` (from `seed.json`) and attempts to convert and assign values to matching properties in `settings`.

---

## Private Helper Methods

* `IsEnabled(string? value)`: Checks if a string representation is `"true"` (case-insensitive) or `"1"`.
* `ReadSeedValues(string rootPath)`: Reads `seed.json` in the given `rootPath`. Parses either a root object or a nested object under the `"settings"` key into a `Dictionary<string, JsonElement>`. Returns an empty dictionary if the file is missing or invalid.
* `TryConvert(JsonElement value, Type targetType, out object? convertedValue)`: Deserializes a `JsonElement` to `targetType` using `JsonSerializer`.

---

## Nested Classes

### `CurrentOverrideScope`

```csharp
private sealed class CurrentOverrideScope : IDisposable
```

* **Purpose:** Implements the `IDisposable` pattern to track and revert changes made to static fields `_currentOverride` and `_hasCurrentOverride`.
* **Constructor:** Captures previous override state and applies the new `AutomationProfile?`.
* **Dispose():** Restores `_currentOverride` and `_hasCurrentOverride` to their states prior to scope instantiation.