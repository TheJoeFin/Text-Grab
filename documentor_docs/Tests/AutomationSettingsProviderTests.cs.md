# Technical Documentation: `Tests/AutomationSettingsProviderTests.cs`

## Overview

The `AutomationSettingsProviderTests.cs` file contains unit tests for verifying setting persistence, reading, scoping, and initialization behaviors when using automated/isolated profiles (`AutomationProfile`) in the Text-Grab application.

The primary objective of this test suite is to guarantee that when an `AutomationProfile` override is active:
1. Application settings write directly into the profile's dedicated directory rather than the user's standard application configuration file (`user.config`).
2. Settings read and written within one profile context do not leak into or affect other profiles.
3. The `SettingsService` properly seeds classic settings upon first execution within an isolated profile without overwriting existing mutated values on subsequent runs.

---

## Class-Level Metadata

```csharp
[Collection("Settings isolation")]
public class AutomationSettingsProviderTests
```

* **Test Collection**: Decorated with `[Collection("Settings isolation")]`.
* **Purpose**: `AutomationProfile.OverrideCurrentForTests` modifies process-global state. Running tests in parallel that alter this process-global state could cause settings saves to target wrong temporary directories. The shared collection ensures tests touching global `Settings.Default` or `AutomationProfile` execute sequentially.

---

## Key Components & Test Methods

### 1. `Save_UnderProfile_WritesClassicSettingsIntoProfileDirectory()`

* **Attribute**: `[Fact]`
* **Purpose**: Tests that invoking `Save()` on a `Settings` instance while scoped to an active `AutomationProfile` writes the settings data as JSON into the temporary profile's classic settings file path (`ClassicSettingsFilePath`).

#### Execution Steps:
1. Creates a temporary profile instance via `TempProfile.Create()`.
2. Overrides the active automation profile using `AutomationProfile.OverrideCurrentForTests(temp.Profile)`.
3. Instantiates `Settings`, sets `DefaultLaunch` to `"GrabFrame"` and `ShowToast` to `false`, and calls `settings.Save()`.
4. Asserts that the file exists at `temp.Profile.ClassicSettingsFilePath`.
5. Deserializes the classic settings JSON file using `ReadClassicSettings` and asserts that:
   * Key `"DefaultLaunch"` equals `"GrabFrame"`.
   * Key `"ShowToast"` equals `"False"`.
6. Instantiates a new `Settings` instance (`reloaded`) within the same profile scope and asserts:
   * `reloaded.DefaultLaunch` is `"GrabFrame"`.
   * `reloaded.ShowToast` is `false`.

---

### 2. `Reads_AreScopedToTheActiveProfile()`

* **Attribute**: `[Fact]`
* **Purpose**: Ensures complete isolation between distinct automation profiles. Settings saved in one profile must not leak or be visible when another profile is active.

#### Execution Steps:
1. Instantiates two separate temporary profiles (`first` and `second`).
2. Within the override scope of `first.Profile`:
   * Instantiates `Settings`, sets `DefaultLaunch = "GrabFrame"`, and calls `Save()`.
3. Within the override scope of `second.Profile`:
   * Instantiates a new `Settings` object.
   * Asserts that `settings.DefaultLaunch` does **not** equal `"GrabFrame"`.
   * Asserts that `second.Profile.ClassicSettingsFilePath` does **not** exist on disk.

---

### 3. `SettingsService_UnderProfile_SeedsClassicSettingsFileOnce()`

* **Attribute**: `[Fact]`
* **Purpose**: Verifies that `SettingsService` creates and seeds the classic settings file on the initial run under a profile, but respects pre-existing values on subsequent runs.

#### Execution Steps:
1. Creates a `TempProfile` and sets the profile override context.
2. Asserts that `temp.Profile.ClassicSettingsFilePath` initially does not exist.
3. Instantiates `Settings first = new()` and wraps it in a `SettingsService(first, localSettings: null)` instance:
   * Verifies the settings file is created on disk.
   * Verifies `first.FirstRun` is `false`.
   * Verifies `first.DefaultLaunch` is set to `TextGrabMode.EditText.ToString()`.
   * Mutates `first.DefaultLaunch` to `"GrabFrame"` and calls `first.Save()`.
4. Instantiates `Settings second = new()` and wraps it in a second `SettingsService` instance:
   * Verifies that `second.DefaultLaunch` retains `"GrabFrame"` and is not re-seeded or overwritten back to default values.

---

## Private Helper Components

### `ReadClassicSettings(string path)`

```csharp
private static Dictionary<string, string> ReadClassicSettings(string path)
```

* **Purpose**: Utility method to read and deserialize a classic settings JSON file directly from the filesystem.
* **Returns**: `Dictionary<string, string>` representing key-value settings pairs stored in JSON format.
* **Exceptions**: Throws `InvalidOperationException` if `JsonSerializer.Deserialize` returns `null`.

---

### `TempProfile`

A private sealed helper class implementing `IDisposable` that encapsulates creation and cleanup of temporary test directories and `AutomationProfile` objects.

#### Properties
* `RootPath` (`string`): The file path to the generated temporary directory.
* `Profile` (`AutomationProfile`): The created profile instance tied to `RootPath`.

#### Methods

* `internal static TempProfile Create()`
  * Generates a unique temporary directory path using `Path.GetTempPath()` and a `Guid` (pattern: `tg-ui-tests-{Guid}`).
  * Calls `AutomationProfile.TryCreate(["Text-Grab.exe"], provider)` where `provider` responds with the temporary root path when queried for `AutomationProfile.ProfileEnvironmentVariable`.
  * Throws `InvalidOperationException` if creation fails.
  * Returns a new `TempProfile` instance.

* `public void Dispose()`
  * Cleans up test artifacts by recursively deleting `RootPath` if it exists (`Directory.Delete(RootPath, recursive: true)`).
  * Ignores `IOException` during cleanup to prevent failed best-effort file deletions from throwing test execution errors.