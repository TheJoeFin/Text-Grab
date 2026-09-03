# Technical Documentation: `InlinePickerItem.cs`

## Overview

The `InlinePickerItem` class is a data model located within the `Text_Grab.Controls` namespace. It represents a single selectable item displayed within an inline picker popup UI component. It stores details such as display text, actual string values, optional grouping header labels, and pattern classification types.

---

## General Information

- **File Path:** `Text-Grab/Controls/InlinePickerItem.cs`
- **Namespace:** `Text_Grab.Controls`
- **Dependencies:** `Text_Grab.Models`

---

## Class Definition

```csharp
public class InlinePickerItem
```

---

## Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `DisplayName` | `string` | `string.Empty` | The human-readable string displayed in the UI for the item. |
| `Value` | `string` | `string.Empty` | The underlying value or string representation associated with the item. |
| `Group` | `string` | `string.Empty` | Optional group label used to group items under section headers (e.g., "Regions", "Saved Patterns", "Smart Patterns"). |
| `Kind` | `PatternKind?` | `null` | Optional nullable `PatternKind` enum indicating the backing pattern engine. Used to determine if selection emits a saved regex placeholder (`{p:}`) or recognizer placeholder (`{r:}`). Null for non-pattern items. |

---

## Constructors

### Parameterless Constructor
```csharp
public InlinePickerItem()
```
Initializes a new instance of `InlinePickerItem` with default property values (`DisplayName`, `Value`, and `Group` initialized to `string.Empty`, and `Kind` initialized to `null`).

### Parameterized Constructor
```csharp
public InlinePickerItem(string displayName, string value, string group = "")
```
Initializes a new instance of `InlinePickerItem` with specified parameters:
- `displayName`: Sets the `DisplayName` property.
- `value`: Sets the `Value` property.
- `group`: (Optional) Sets the `Group` property. Defaults to `string.Empty`.

---

## Methods

### `ToString()`

```csharp
public override string ToString()
```

- **Returns:** `string`
- **Description:** Overrides the base `object.ToString()` method to return the `DisplayName` property. This allows controls or debug tools to display the item's name directly when converting the object to a string.

---

## How It Works

1. **Instantiation:** An `InlinePickerItem` can be created either empty via the default constructor or pre-populated using the parameterized constructor.
2. **Grouping & Display:** UI controls reading instances of `InlinePickerItem` use `DisplayName` for list rendering and `Group` for categorizing entries into distinct sections.
3. **Pattern Identification:** The `Kind` property allows consumer logic to inspect whether an item represents a pattern (`PatternKind`) and decide whether to format output as a `{p:}` regex or `{r:}` recognizer placeholder.