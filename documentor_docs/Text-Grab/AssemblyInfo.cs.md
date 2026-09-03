# Technical Documentation: `Text-Grab/AssemblyInfo.cs`

## Overview

The `Text-Grab/AssemblyInfo.cs` file defines assembly-level attributes for the `Text-Grab` project. In .NET applications, `AssemblyInfo.cs` is used to configure compilation options, runtime requirements, UI framework behavior (specifically for Windows Presentation Foundation / WPF), and assembly accessibility settings across boundaries.

---

## Imported Namespaces

* `using System.Runtime.CompilerServices;`: Provides attributes that control compiler behavior, such as granting assembly internals access to external assemblies (`InternalsVisibleTo`).
* `using System.Runtime.Versioning;`: Contains attributes used to declare platform and operating system support requirements (`SupportedOSPlatform`).
* `using System.Windows;`: Contains core WPF types, including `ThemeInfo` and `ResourceDictionaryLocation`.
* `using System.Windows.Media;`: Provides rendering and media-related attributes, including `DisableDpiAwareness`.

---

## Assembly Attributes & Key Components

### 1. Target Operating System Platform
```csharp
[assembly: SupportedOSPlatform("windows10.0.19041.0")]
```
* **Attribute:** `SupportedOSPlatformAttribute`
* **Purpose:** Informs the .NET compiler and code analysis tools that this assembly requires Windows 10 version 2004 (Build 19041.0) or higher to run. Calling APIs that require this version or higher will not trigger platform compatibility warnings within this assembly.

---

### 2. DPI Awareness Configuration
```csharp
[assembly: DisableDpiAwareness]
```
* **Attribute:** `DisableDpiAwarenessAttribute`
* **Purpose:** Disables automatic High-DPI scaling behavior for all WPF visual elements within this assembly. This forces the system or application to handle rendering without automatic WPF DPI scaling transformations.

---

### 3. WPF Theme Resource Dictionary Configuration
```csharp
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, 
    ResourceDictionaryLocation.SourceAssembly 
)]
```
* **Attribute:** `ThemeInfoAttribute`
* **Purpose:** Configures where WPF looks for theme-specific and generic resource dictionaries when resolving control styles and templates.
* **Parameters:**
  * **Parameter 1 (`ResourceDictionaryLocation.None`):** Specifies that theme-specific resource dictionaries (e.g., theme-dependent XAML files) are not used or located in theme assemblies.
  * **Parameter 2 (`ResourceDictionaryLocation.SourceAssembly`):** Specifies that generic resource dictionaries (used when a resource is not found in the page, application, or theme-specific dictionaries) are defined directly within this source assembly.

---

### 4. Unit Testing Visibility
```csharp
[assembly: InternalsVisibleTo("Tests")]
```
* **Attribute:** `InternalsVisibleToAttribute`
* **Purpose:** Exposes `internal` types and `internal` members within the `Text-Grab` assembly to an external assembly named `"Tests"`. This allows unit test projects named `Tests` to directly access and test non-public (internal) code components.

---

## How It Works

1. **Compilation Phase:**
   * The C# compiler reads `InternalsVisibleTo("Tests")` and grants access rights to the `Tests` assembly during build time.
   * The compiler evaluates platform checks via `SupportedOSPlatform("windows10.0.19041.0")` to enforce API availability warnings or suppress warnings for Windows 10 build 19041+.

2. **Runtime Execution Phase:**
   * **WPF Resource Resolution:** The WPF framework checks `ThemeInfo` during runtime to resolve XAML styles and control templates from the assembly's generic resource dictionary.
   * **DPI Handling:** The WPF rendering pipeline reads `DisableDpiAwareness` to bypass default automatic High-DPI scaling calculations for UI visuals in the assembly.