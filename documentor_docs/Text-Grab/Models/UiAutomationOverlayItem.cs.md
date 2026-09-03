# Technical Documentation: `UiAutomationOverlayItem.cs`

**File Path:** `Text-Grab/Models/UiAutomationOverlayItem.cs`  
**Namespace:** `Text_Grab.Models`  

---

## Overview

The `UiAutomationOverlayItem.cs` file defines the data models used to represent UI elements captured or targeted via Windows UI Automation. It consists of an enumeration (`UiAutomationOverlaySource`) indicating how an overlay item's bounding data was derived, and a positional record (`UiAutomationOverlayItem`) that holds text content, screen position, automation identifiers, and source metadata.

---

## Dependencies

* `System.Windows`: Provides the `Rect` structure used for defining screen boundary coordinates.

---

## Code Components

### 1. Enumeration: `UiAutomationOverlaySource`

The `UiAutomationOverlaySource` enum specifies the origin mechanism or technique used to extract the UI element's region and text.

```csharp
public enum UiAutomationOverlaySource
{
    PointTextRange = 0,
    VisibleTextRange = 1,
    ElementBounds = 2,
}
```

#### Members:
* **`PointTextRange` (`0`)**: Indicates that the source data was retrieved using a text range derived from a specific point location.
* **`VisibleTextRange` (`1`)**: Indicates that the source data was retrieved from the visible text range of a UI element.
* **`ElementBounds` (`2`)**: Indicates that the source data was derived directly from the bounding rectangle of the UI element itself.

---

### 2. Record: `UiAutomationOverlayItem`

`UiAutomationOverlayItem` is an immutable record that stores detailed UI Automation metadata for a specific visual or textual element on the screen.

```csharp
public record UiAutomationOverlayItem(
    string Text,
    Rect ScreenBounds,
    UiAutomationOverlaySource Source,
    string ControlTypeProgrammaticName = "",
    string AutomationId = "",
    string RuntimeId = "");
```

#### Properties / Parameters:

| Parameter | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Text` | `string` | *None (Required)* | The textual content extracted from or associated with the UI element. |
| `ScreenBounds` | `Rect` | *None (Required)* | The screen coordinates and dimensions (`Rect`) defining the bounding box of the item on screen. |
| `Source` | `UiAutomationOverlaySource` | *None (Required)* | The extraction method used to obtain the item, as defined by `UiAutomationOverlaySource`. |
| `ControlTypeProgrammaticName` | `string` | `""` (Empty string) | The programmatic name of the control type (e.g., `ControlType.Button`). Defaults to an empty string. |
| `AutomationId` | `string` | `""` (Empty string) | The UI Automation identifier assigned to the element. Defaults to an empty string. |
| `RuntimeId` | `string` | `""` (Empty string) | The unique runtime identifier assigned to the UI element by the operating system or application. Defaults to an empty string. |

---

## Key Characteristics & Behavior

* **Immutability**: As a C# `record`, instances of `UiAutomationOverlayItem` provide value-based equality semantics and immutable property states by default.
* **Default Values**: Optional parameters (`ControlTypeProgrammaticName`, `AutomationId`, and `RuntimeId`) allow creating an instance with minimal required positional data (`Text`, `ScreenBounds`, `Source`) while leaving automation identifiers empty if they are unavailable or unneeded.