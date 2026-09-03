# Technical Documentation: `SettingsCard.cs`

## Overview

The `SettingsCard` class is a custom control in the `Text_Grab.Controls` namespace. It derives from `Wpf.Ui.Controls.CardControl` and implements a Windows Community Toolkit-style settings row card. 

It provides a formatted row layout containing a header text block and an optional description text block on the left side (managed via the `Header` property), designed to sit alongside interactive controls placed on the right side (via the inherited `Content` property). The internal visual elements are instantiated programmatically in code to allow consuming pages to assign `x:Name` references directly to child elements.

---

## Class Definition

```csharp
namespace Text_Grab.Controls;

public class SettingsCard : Wpf.Ui.Controls.CardControl
```

* **Inherits from**: `Wpf.Ui.Controls.CardControl`

---

## Properties & Dependency Properties

### 1. `HeaderText`
* **Type**: `string`
* **Dependency Property**: `HeaderTextProperty`
* **Default Value**: `string.Empty`
* **Description**: Sets or retrieves the main text displayed in the card's header area.
* **Property Changed Callback**: `OnHeaderTextChanged`

```csharp
public static readonly DependencyProperty HeaderTextProperty =
    DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(SettingsCard),
        new PropertyMetadata(string.Empty, OnHeaderTextChanged));

public string HeaderText
{
    get => (string)GetValue(HeaderTextProperty);
    set => SetValue(HeaderTextProperty, value);
}
```

### 2. `Description`
* **Type**: `string`
* **Dependency Property**: `DescriptionProperty`
* **Default Value**: `string.Empty`
* **Description**: Sets or retrieves detailed text displayed directly beneath the header text.
* **Property Changed Callback**: `OnDescriptionChanged`

```csharp
public static readonly DependencyProperty DescriptionProperty =
    DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsCard),
        new PropertyMetadata(string.Empty, OnDescriptionChanged));

public string Description
{
    get => (string)GetValue(DescriptionProperty);
    set => SetValue(DescriptionProperty, value);
}
```

---

## Private Fields

* `headerTextBlock` (`TextBlock`): Internal text control responsible for rendering the `HeaderText`.
* `descriptionTextBlock` (`TextBlock`): Internal text control responsible for rendering the `Description`.

---

## Implementation Details

### Constructor: `SettingsCard()`

The constructor configures styling, keyboard focus behavior, visual layout, and internal element properties:

1. **Style Initialization**:
   * Calls `SetResourceReference(StyleProperty, typeof(Wpf.Ui.Controls.CardControl))` to resolve implicit styles mapped to the base type `CardControl`.

2. **Focus & Layout Setup**:
   * `Focusable = false`: Prevents the container card from acquiring keyboard focus.
   * `IsTabStop = false`: Skips the outer card during keyboard tab navigation so focus moves directly to inner controls (e.g., switches, buttons).
   * `Margin = new Thickness(0, 0, 0, 3)`: Applies a 3-pixel bottom margin to separate adjacent cards.

3. **Header Text Block (`headerTextBlock`)**:
   * `FontSize`: Set to `14`.
   * `TextWrapping`: Set to `TextWrapping.Wrap`.
   * `Foreground`: Bound to dynamic resource `"TextFillColorPrimaryBrush"`.

4. **Description Text Block (`descriptionTextBlock`)**:
   * `FontSize`: Set to `12`.
   * `TextWrapping`: Set to `TextWrapping.Wrap`.
   * `Visibility`: Initially set to `Visibility.Collapsed`.
   * `Foreground`: Bound to dynamic resource `"TextFillColorSecondaryBrush"`.

5. **Header Layout Panel (`headerPanel`)**:
   * Creates a `StackPanel` with `Margin` set to `Thickness(0, 0, 12, 0)` and `VerticalAlignment` set to `VerticalAlignment.Center`.
   * Adds `headerTextBlock` and `descriptionTextBlock` as child elements.
   * Assigns `headerPanel` to the base class `Header` property.

---

## Dependency Property Callback Methods

### `OnHeaderTextChanged`
```csharp
private static void OnHeaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
```
* **Trigger**: Fired when the `HeaderText` property value changes.
* **Logic**: Casts `d` to `SettingsCard` and updates `headerTextBlock.Text` with the new value (or `string.Empty` if null).

### `OnDescriptionChanged`
```csharp
private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
```
* **Trigger**: Fired when the `Description` property value changes.
* **Logic**:
  1. Validates that the dependency object `d` is a `SettingsCard`.
  2. Updates `descriptionTextBlock.Text` with the new string value (or `string.Empty` if null).
  3. Updates `descriptionTextBlock.Visibility`:
     * Set to `Visibility.Collapsed` if the description string is null or empty.
     * Set to `Visibility.Visible` if the description string contains content.