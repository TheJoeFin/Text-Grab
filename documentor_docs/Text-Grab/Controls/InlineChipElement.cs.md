# Technical Documentation: `InlineChipElement.cs`

**File Path:** `Text-Grab/Controls/InlineChipElement.cs`  
**Namespace:** `Text_Grab.Controls`  
**Base Class:** `System.Windows.Controls.Control`

---

## Overview

The `InlineChipElement` class is a custom, templated WPF (Windows Presentation Foundation) control designed to represent a "chip" or tag UI element. It holds text information (display name and underlying value) and provides built-in interaction support for requesting its own removal via a dedicated remove button defined in its control template.

---

## Class Metadata & Attributes

### `[TemplatePart]` Attribute
```csharp
[TemplatePart(Name = PartRemoveButton, Type = typeof(Button))]
```
* **Name:** `PART_RemoveButton`
* **Type:** `System.Windows.Controls.Button`
* **Purpose:** Informs consumers and template designers that the control's template expects a `Button` child named `PART_RemoveButton` to enable removal functionality.

---

## Constants and Fields

| Name | Type | Description |
| :--- | :--- | :--- |
| `PartRemoveButton` | `string` (`"PART_RemoveButton"`) | The expected template part name for the remove button. |
| `_removeButton` | `Button?` | Internal reference to the template's remove button instance. |

---

## Dependency Properties

### 1. `DisplayNameProperty`
* **Type:** `string`
* **Default Value:** `string.Empty`
* **CLR Wrapper:** `DisplayName`
* **Description:** Represents the visible text or label displayed on the chip element.

### 2. `ValueProperty`
* **Type:** `string`
* **Default Value:** `string.Empty`
* **CLR Wrapper:** `Value`
* **Description:** Holds the underlying string value associated with the chip element.

---

## Events

### `RemoveRequested`
* **Type:** `EventHandler?`
* **Description:** Fired when the user clicks the template's remove button (`PART_RemoveButton`). External listeners (such as parent containers) can subscribe to this event to handle removing or detaching the chip.

---

## Key Methods & Lifecycle

### Static Constructor
```csharp
static InlineChipElement()
```
* Overrides the default metadata for `DefaultStyleKeyProperty`.
* Ensures that WPF looks up the default style resource matching `typeof(InlineChipElement)` in themes/generic.xaml or control resources.

### `OnApplyTemplate()`
```csharp
public override void OnApplyTemplate()
```
* Overrides `Control.OnApplyTemplate()` to hook up template parts whenever a new template is applied to the control.
* **Logic:**
  1. If `_removeButton` was previously attached to an instance, it unhooks the `Click` event handler (`RemoveButton_Click`) to prevent memory leaks or duplicate handlers.
  2. Resolves the new `Button` child named `PART_RemoveButton` using `GetTemplateChild()`.
  3. If found, re-attaches the `Click` event handler to the newly assigned `_removeButton`.

### `RemoveButton_Click(object sender, RoutedEventArgs e)`
```csharp
private void RemoveButton_Click(object sender, RoutedEventArgs e)
```
* Private click handler for the template's remove button.
* Triggers the `RemoveRequested` event, passing `this` as the sender and `EventArgs.Empty` as the event arguments.

---

## Workflow Summary

1. **Initialization:** When the control is instantiated, WPF applies the default style using `DefaultStyleKeyProperty`.
2. **Template Binding:** `OnApplyTemplate()` searches the visual tree for a `Button` named `PART_RemoveButton`.
3. **User Action:** Clicking `PART_RemoveButton` triggers `RemoveButton_Click`.
4. **Event Notification:** `RemoveButton_Click` raises the `RemoveRequested` event to inform parent controllers or containers that the element should be processed or removed.