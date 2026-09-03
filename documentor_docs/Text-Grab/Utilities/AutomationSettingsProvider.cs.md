# `AutomationSettingsProvider.cs` Technical Documentation

## Overview

The `AutomationSettingsProvider` class is a custom implementation of .NET's application settings architecture. Extending `LocalFileSettingsProvider` and implementing `IApplicationSettingsProvider`, it intercepts standard setting reads and writes. 

When an `AutomationProfile` is active (`AutomationProfile.Current` is not `null`), settings are stored in and retrieved from a custom JSON file specified by the active profile. When no automation profile is active (`AutomationProfile.Current` is `null`), the class delegates all settings operations to the base `LocalFileSettingsProvider` behavior.

---

## Class Signature

```csharp
namespace Text_Grab.Utilities;

public sealed class AutomationSettingsProvider : LocalFileSettingsProvider, IApplicationSettingsProvider
```

* **Inheritance:** `LocalFileSettingsProvider` -> `SettingsProvider` -> `ProviderBase`
* **Interfaces Implemented:** `IApplicationSettingsProvider`
* **Modifiers:** `sealed`

---

## Key Responsibilities & Methods

### 1. `GetPropertyValues`
```csharp
public override SettingsPropertyValueCollection GetPropertyValues(
    SettingsContext context,
    SettingsPropertyCollection collection)
```
Retrieves property values for the specified settings context and collection.

* **Behavior with Active Profile (`AutomationProfile.Current != null`):**
  1. Reads existing settings as a string-to-string dictionary from the profile's `ClassicSettingsFilePath` using `ReadValues()`.
  2. Iterates over each `SettingsProperty` in the provided `collection`.
  3. Looks up the property's value by its name (`property.Name`) in the read values dictionary.
  4. Creates a `SettingsPropertyValue` instance using the retrieved value (or `property.DefaultValue` if not found in the dictionary) as `SerializedValue`.
  5. Sets `IsDirty` to `false`.
  6. Returns the constructed `SettingsPropertyValueCollection`.
* **Behavior without Active Profile (`AutomationProfile.Current == null`):**
  * Calls and returns `base.GetPropertyValues(context, collection)`.

---

### 2. `SetPropertyValues`
```csharp
public override void SetPropertyValues(
    SettingsContext context,
    SettingsPropertyValueCollection collection)
```
Persists property values for the specified settings context and collection.

* **Behavior with Active Profile (`AutomationProfile.Current != null`):**
  1. Reads the current stored settings dictionary from `profile.ClassicSettingsFilePath`.
  2. Iterates through each `SettingsPropertyValue` in the `collection`.
  3. Converts the property value to a string invariant of culture using `ConvertToInvariantString()`.
  4. Updates or adds the string value in the dictionary using `propertyValue.Name` as the key.
  5. Ensures the target directory (`profile.SettingsDirectory`) exists via `Directory.CreateDirectory()`.
  6. Serializes the updated dictionary to JSON with indented formatting (`WriteIndented = true`) and writes it to `profile.ClassicSettingsFilePath`.
* **Behavior without Active Profile (`AutomationProfile.Current == null`):**
  * Calls `base.SetPropertyValues(context, collection)`.

---

### 3. `IApplicationSettingsProvider` Interface Implementations

Because `AutomationProfile` handles settings in a single profile-specific JSON file, standard user-config migration/reset operations are handled conditionally.

#### `Reset`
```csharp
void IApplicationSettingsProvider.Reset(SettingsContext context)
```
* **Active Profile:** Performs no action (no-op).
* **Inactive Profile:** Calls `base.Reset(context)`.

#### `Upgrade`
```csharp
void IApplicationSettingsProvider.Upgrade(
    SettingsContext context,
    SettingsPropertyCollection properties)
```
* **Active Profile:** Performs no action (no-op).
* **Inactive Profile:** Calls `base.Upgrade(context, properties)`.

#### `GetPreviousVersion`
```csharp
SettingsPropertyValue IApplicationSettingsProvider.GetPreviousVersion(
    SettingsContext context,
    SettingsProperty property)
```
* **Active Profile:** Returns a new `SettingsPropertyValue` initialized with `property`, setting `PropertyValue = null` and `IsDirty = false`.
* **Inactive Profile:** Returns `base.GetPreviousVersion(context, property)`.

---

### 4. Private Helper Methods

#### `ReadValues`
```csharp
private static Dictionary<string, string> ReadValues(string path)
```
Reads and deserializes the JSON settings file from disk into a dictionary.

* Checks if the file exists at `path`. If not, returns an empty `Dictionary<string, string>` initialized with `StringComparer.Ordinal`.
* Attempts to deserialize the file content into `Dictionary<string, string>` using `JsonSerializer`.
* Catches `JsonException` and returns an empty ordinal-compared dictionary if parsing fails or if deserialization yields `null`.

#### `ConvertToInvariantString`
```csharp
private static string ConvertToInvariantString(SettingsPropertyValue propertyValue)
```
Converts a setting value into a string using invariant culture.

* If `propertyValue.PropertyValue` is `null`, returns `string.Empty`.
* Retrieves a `TypeConverter` for the property's type via `TypeDescriptor.GetConverter()`.
* If the converter can convert to string (`converter.CanConvertTo(typeof(string))`), calls `converter.ConvertToInvariantString()`.
* As a fallback, uses `Convert.ToString(value, CultureInfo.InvariantCulture)`.
* Returns `string.Empty` if conversion results in `null`.

---

## Profile-Based Execution Summary

| Operation | When `AutomationProfile.Current` is `null` | When `AutomationProfile.Current` is Active |
| :--- | :--- | :--- |
| **Read Settings** | Invokes `LocalFileSettingsProvider.GetPropertyValues` | Reads from `profile.ClassicSettingsFilePath` (JSON dictionary) |
| **Write Settings** | Invokes `LocalFileSettingsProvider.SetPropertyValues` | Serializes values to `profile.ClassicSettingsFilePath` |
| **Reset Settings** | Invokes `LocalFileSettingsProvider.Reset` | No-op |
| **Upgrade Settings** | Invokes `LocalFileSettingsProvider.Upgrade` | No-op |
| **Get Previous Version** | Invokes `LocalFileSettingsProvider.GetPreviousVersion` | Returns `null` property value |