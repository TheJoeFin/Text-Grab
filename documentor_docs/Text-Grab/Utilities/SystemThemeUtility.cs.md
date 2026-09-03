# Technical Documentation: `SystemThemeUtility.cs`

**File Path:** `Text-Grab/Utilities/SystemThemeUtility.cs`  
**Namespace:** `Text_Grab.Utilities`  
**Class Name:** `SystemThemeUtility`

---

## Overview

The `SystemThemeUtility` class provides a utility method to query the Windows Registry and determine whether the current Windows system theme is set to **Light Mode**. 

---

## Class Definition

```csharp
namespace Text_Grab.Utilities;

public class SystemThemeUtility
```

---

## Fields and Constants

### `themeKeyPath`

```csharp
public const string themeKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
```

* **Type:** `string` (Constant)
* **Access Level:** `public`
* **Description:** Represents the relative path within the Windows Registry (`HKEY_CURRENT_USER`) where Windows stores theme and personalization settings.

---

## Method Documentation

### `IsLightTheme()`

Checks the Windows Registry to determine if the system is actively using a light theme.

```csharp
public static bool IsLightTheme()
```

#### Syntax & Signature
* **Access Level:** `public static`
* **Parameters:** None
* **Return Type:** `bool`
  * Returns `true` if the system registry value `SystemUsesLightTheme` equals `"1"`.
  * Returns `false` if the registry key or value is missing, if the value is not `"1"`, or if an exception occurs during the registry read operation.

#### Detailed Execution Steps

1. **Open Registry Subkey:**
   Attempts to open the subkey defined by `themeKeyPath` under `Registry.CurrentUser` (`HKEY_CURRENT_USER`). Uses a `using` declaration to ensure proper disposal of the `RegistryKey` object.
2. **Null Key Check:**
   If the opened `RegistryKey` object (`key`) is `null`, the method returns `false`.
3. **Read Value:**
   Retrieves the value associated with the key name `"SystemUsesLightTheme"`.
4. **Null Value Check:**
   If the retrieved value (`o`) is `null`, the method returns `false`.
5. **Evaluate Value:**
   Converts the object `o` to a string (`o.ToString()`). If the string representation is equal to `"1"`, the method returns `true`. Otherwise, it returns `false`.
6. **Exception Handling:**
   Encloses the entire registry read operation in a `try-catch` block. If any `Exception` is thrown during execution (e.g., security/permission issues or missing registry paths), the exception is caught and the method safely returns `false`.

---

## Code Reference Example

```csharp
using Text_Grab.Utilities;

// Example Usage
bool isLight = SystemThemeUtility.IsLightTheme();

if (isLight)
{
    // System is using Light Theme
}
else
{
    // System is using Dark Theme or theme could not be determined
}
```

---

## Dependencies

* **`Microsoft.Win32`**: Used for accessing the Windows Registry (`Registry`, `RegistryKey`).
* **`System`**: Provides base system types and exception handling capabilities (`Exception`, `Object`, `Boolean`, `String`).