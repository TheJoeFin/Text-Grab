# Technical Documentation: `Tests/AutomationProfileTests.cs`

## Overview

The `AutomationProfileTests.cs` file contains unit tests for the `AutomationProfile` class and the command-line argument parsing logic within the `Text-Grab` application. Using the **xUnit** framework, these tests verify that automation profiles are correctly initialized from environment variables and command-line arguments, validate security/opt-in conditions for system integration, and ensure that startup argument parsing logic ignores internal automation flags.

---

## File Details

- **File Path:** `Tests/AutomationProfileTests.cs`
- **Namespace:** `Tests`
- **Dependencies:**
  - `System.IO`
  - `Text_Grab`
  - `Text_Grab.Utilities`

---

## Test Suite Summary: `AutomationProfileTests`

The `AutomationProfileTests` class contains 5 unit test methods marked with the xUnit `[Fact]` attribute.

### Summary of Tested Behaviors

1. **Environment Variable Fallback:** Verifies that `AutomationProfile.TryCreate` falls back to the environment variable if command-line arguments do not specify a profile path.
2. **Command-Line Override & Property Resolution:** Verifies that command-line options override environment configurations and correctly enable system integration.
3. **Persistent Registration Rules:** Enforces strict conditional checks for `AllowsPersistentRegistration`, requiring explicit command-line flags, system integration, and a specific environment variable opt-in.
4. **Null Handling:** Verifies that `AutomationProfile.TryCreate` returns `null` when neither command-line arguments nor environment variables specify an automation profile.
5. **Startup Argument Filter:** Verifies that `App.ParseStartupArguments` ignores automation profile flags and isolates application-level arguments.

---

## Test Methods

### 1. `TryCreate_UsesEnvironmentProfileAndKeepsIntegrationDisabled`

* **Purpose:** Tests the creation of an `AutomationProfile` using an environment variable when no command-line profile path is provided.
* **Inputs:**
  * Command-line arguments: `["Text-Grab.exe"]`
  * Environment lookup function: Returns `@"C:\UiRuns\environment-profile"` when queried for `AutomationProfile.ProfileEnvironmentVariable`.
* **Assertions Verified:**
  * `profile` is not `null`.
  * `profile.RootPath` equals `@"C:\UiRuns\environment-profile"`.
  * `profile.AllowsSystemIntegration` is `false`.
  * `profile.HistoryDirectory` equals `@"C:\UiRuns\environment-profile\history"`.
  * `profile.ClassicSettingsFilePath` equals `@"C:\UiRuns\environment-profile\settings\classic-settings.json"`.

---

### 2. `TryCreate_CommandLineProfileAndIntegrationOverrideEnvironment`

* **Purpose:** Ensures that command-line flags take precedence over environment variables and correctly populate dependent paths and flags.
* **Inputs:**
  * Command-line arguments:
    * `"Text-Grab.exe"`
    * `"--automation-profile"`
    * `@"C:\UiRuns\command-line-profile"`
    * `"--automation-system-integration"`
  * Environment lookup function: Returns `@"C:\UiRuns\environment-profile"` for `AutomationProfile.ProfileEnvironmentVariable`.
* **Assertions Verified:**
  * `profile` is not `null`.
  * `profile.RootPath` equals `@"C:\UiRuns\command-line-profile"`.
  * `profile.AllowsSystemIntegration` is `true`.
  * `profile.AllowsPersistentRegistration` is `false`.
  * `profile.TemporaryDirectory` equals `@"C:\UiRuns\command-line-profile\temp"`.

---

### 3. `TryCreate_PersistentRegistrationRequiresSystemAndDisposableOptIn`

* **Purpose:** Verifies the conditional requirements needed for `AllowsPersistentRegistration` to evaluate to `true`.
* **Tested Scenarios:**
  1. **Ordinary System Profile:** Includes `--automation-system-integration` only. Expects `AllowsPersistentRegistration == false`.
  2. **Disposable Profile:** Includes `--automation-system-integration`, `--automation-disposable-registration`, and environment variable `AutomationProfile.DisposableVmEnvironmentVariable` set to `"1"`. Expects `AllowsPersistentRegistration == true`.
  3. **Incomplete Profile:** Includes `--automation-disposable-registration` without system integration. Expects `AllowsPersistentRegistration == false`.
  4. **Non-Disposable Profile:** Includes `--automation-system-integration` and `--automation-disposable-registration`, but the environment variable is not set. Expects `AllowsPersistentRegistration == false`.
* **Assertions Verified:**
  * All created profiles are not `null`.
  * Only the fully matching configuration (`disposableProfile`) evaluates `AllowsPersistentRegistration` as `true`.

---

### 4. `TryCreate_ReturnsNullWithoutProfile`

* **Purpose:** Ensures that `AutomationProfile.TryCreate` returns `null` when no profile arguments or environment variables are passed.
* **Inputs:**
  * Command-line arguments: `["Text-Grab.exe"]`
  * Environment lookup function: Returns `null` for all queries.
* **Assertions Verified:**
  * `profile` is `null`.

---

### 5. `ParseStartupArguments_IgnoresAutomationProfileArguments`

* **Purpose:** Tests `App.ParseStartupArguments` to confirm that automation profile command-line flags do not interfere with standard application startup argument parsing.
* **Inputs:**
  * Command-line arguments:
    * `"--automation-profile"`
    * `@"C:\UiRuns\run-1"`
    * `"--automation-system-integration"`
    * `"--automation-disposable-registration"`
    * `"Settings"`
* **Assertions Verified:**
  * `startupArguments.PrimaryArgument` equals `"Settings"`.

---

## Interfaced Types and Members

The test file exercises the following elements from the application:

### `AutomationProfile` Class
* **Static Methods:**
  * `TryCreate(string[] args, Func<string, string?> getEnvironmentVariable)`
* **Static Constants / Fields:**
  * `ProfileEnvironmentVariable`
  * `DisposableVmEnvironmentVariable`
* **Properties:**
  * `RootPath`
  * `AllowsSystemIntegration`
  * `AllowsPersistentRegistration`
  * `HistoryDirectory`
  * `ClassicSettingsFilePath`
  * `TemporaryDirectory`

### `App` Class
* **Static Methods:**
  * `ParseStartupArguments(string[] args)`
* **Nested Types:**
  * `App.StartupArguments` (Property referenced: `PrimaryArgument`)