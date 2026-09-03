# Technical Documentation: `AppUtilities.cs`

## Overview

The `AppUtilities` class in the `Text_Grab.Utilities` namespace is an internal static helper utility. It provides centralized access to application-level settings services, checks the execution context (packaged vs. unpackaged), and retrieves the current application version string.

---

## Class Details

- **Namespace:** `Text_Grab.Utilities`
- **Class Name:** `AppUtilities`
- **Access Modifier:** `internal`
- **Type:** Static class (contains only static members)

---

## Key Components

### Properties

#### 1. `TextGrabSettingsService`
```csharp
internal static SettingsService TextGrabSettingsService => Singleton<SettingsService>.Instance;
```
- **Type:** `SettingsService`
- **Access:** `internal static` (Read-only)
- **Purpose:** Provides access to the single shared instance of `SettingsService` managed by a `Singleton<T>` wrapper.

#### 2. `TextGrabSettings`
```csharp
internal static Settings TextGrabSettings => TextGrabSettingsService.ClassicSettings;
```
- **Type:** `Settings`
- **Access:** `internal static` (Read-only)
- **Purpose:** Shortcut property to retrieve the `ClassicSettings` property directly from the `TextGrabSettingsService`.

---

### Methods

#### 1. `IsPackaged()`
```csharp
internal static bool IsPackaged()
```
- **Return Type:** `bool`
- **Access:** `internal static`
- **Purpose:** Determines whether the application is running inside an MSIX / Windows App package container.
- **How It Works:**
  - Attempts to access `Package.Current.Id`.
  - If successful, it confirms the application is running within a package context and returns `true`.
  - If an exception is caught (indicating the application is running unpackaged), it returns `false`.

#### 2. `GetAppVersion()`
```csharp
internal static string GetAppVersion()
```
- **Return Type:** `string`
- **Access:** `internal static`
- **Purpose:** Retrieves the current version string of the application.
- **How It Works:**
  1. Calls `IsPackaged()` to check the runtime environment.
  2. **Packaged Context:**
     - Retrieves `Package.Current.Id.Version`.
     - Formats and returns the string in the format: `"{Major}.{Minor}.{Build}"`.
     - Returns `"unknown error reading package version"` if formatting resolves to `null`.
  3. **Unpackaged Context:**
     - Reads the version from `System.Reflection.Assembly.GetExecutingAssembly().GetName().Version`.
     - Converts the version to a string via `.ToString()`.
     - Returns `"unknown error reading assembly version"` if the version is `null`.

---

## Dependencies

- **`Text_Grab.Properties`**: Provides access to application settings properties (`Settings`).
- **`Text_Grab.Services`**: Provides access to `SettingsService` and the `Singleton<T>` generic instance.
- **`Windows.ApplicationModel`**: Used for `Package` and `PackageVersion` classes to handle Windows app packaging information.