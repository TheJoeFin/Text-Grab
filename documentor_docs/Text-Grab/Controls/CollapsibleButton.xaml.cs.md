# Documentation Guide: `CollapsibleButton.xaml.cs`

## Overview

The `CollapsibleButton` class is a custom WPF button control defined in the `Text_Grab.Controls` namespace. It extends `System.Windows.Controls.Button` and implements `INotifyPropertyChanged`. 

The primary purpose of `CollapsibleButton` is to provide a UI button control that can switch dynamic visual layouts between a standard styled button displaying text (`TealColor` style) and a collapsed icon-only button (`SymbolButton` style) using WPF resource lookups.

---

## Class Declaration

```csharp
namespace Text_Grab.Controls;

public partial class CollapsibleButton : System.Windows.Controls.Button, INotifyPropertyChanged
```

---

## Key Components

### 1. Dependency Properties

* **`ButtonTextProperty`**
  * **Type:** `string`
  * **Default Value:** `"ButtonText"`
  * **Description:** Registers the `ButtonText` dependency property to hold the text label for the button.
  * **Property Wrapper:**
    ```csharp
    public string ButtonText
    {
        get => (string)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }
    ```

* **`ButtonSymbolProperty`**
  * **Type:** `Wpf.Ui.Controls.SymbolRegular`
  * **Default Value:** `SymbolRegular.Diamond24`
  * **Description:** Registers the `ButtonSymbol` dependency property to hold the icon symbol associated with the button.
  * **Property Wrapper:**
    ```csharp
    public SymbolRegular ButtonSymbol
    {
        get => (SymbolRegular)GetValue(ButtonSymbolProperty);
        set => SetValue(ButtonSymbolProperty, value);
    }
    ```

---

### 2. Properties & Fields

* **`private bool isSymbol`**
  * **Type:** `bool`
  * **Default Value:** `false`
  * **Description:** Internal backing field tracking whether the button is currently in symbol-only mode.

* **`CanChangeStyle`**
  * **Type:** `bool`
  * **Default Value:** `true`
  * **Description:** Public property intended to indicate whether style changes are allowed.

* **`CustomButton`**
  * **Type:** `ButtonInfo?` (from `Text_Grab.Models`)
  * **Default Value:** `null`
  * **Description:** Gets or sets custom button data model details.

* **`IsSymbol`**
  * **Type:** `bool`
  * **Description:** Public getter/setter property wrapper around `isSymbol`. When the setter is called, it updates `isSymbol` and invokes `ChangeButtonLayout_Click()` to update the UI layout accordingly.

---

### 3. Events

* **`public event PropertyChangedEventHandler? PropertyChanged;`**
  * Implementation of `INotifyPropertyChanged` to notify listeners when a property value changes.

---

### 4. Constructor

```csharp
public CollapsibleButton()
{
    DataContext = this;
    InitializeComponent();
}
```
* Sets the control's `DataContext` to itself (`this`).
* Calls `InitializeComponent()` to load the associated XAML component.

---

### 5. Methods

#### `ChangeButtonLayout_Click(object? sender = null, System.Windows.RoutedEventArgs? e = null)`
* **Scope:** `private`
* **Purpose:** Updates the button style and text visibility based on the state of `isSymbol`.
* **Behavior:**
  1. If `sender` is provided (i.e., not `null`), toggles the `isSymbol` state (`isSymbol = !isSymbol`).
  2. **When `isSymbol` is `false` (Normal Button Layout):**
     * Looks up the `TealColor` resource using `FindResource("TealColor")`.
     * If found and castable to `Style`, applies it to `Style`.
     * Sets `ButtonTextBlock.Visibility` to `Visibility.Visible`.
  3. **When `isSymbol` is `true` (Symbol-Only Layout):**
     * Looks up the `SymbolButton` resource using `FindResource("SymbolButton")`.
     * If found and castable to `Style`, applies it to `Style`.
     * Sets `ButtonTextBlock.Visibility` to `Visibility.Collapsed`.

#### `CollapsibleButton_Loaded(object sender, RoutedEventArgs e)`
* **Scope:** `private`
* **Purpose:** Event handler intended for the control's loaded event.
* **Behavior:**
  * Checks if `isSymbol` is `true`.
  * If `isSymbol` is `true`, finds the `SymbolButton` style resource, sets `Style` to `SymbolButtonStyle`, and sets `ButtonTextBlock.Visibility` to `Visibility.Collapsed`.

#### `OnPropertyChanged([CallerMemberName] string? propertyName = null)`
* **Scope:** `private`
* **Purpose:** Helper method to safely invoke the `PropertyChanged` event using `[CallerMemberName]`.

---

## How It Works

1. **Initialization:** When `CollapsibleButton` is instantiated, its `DataContext` is assigned to itself and component initialization occurs.
2. **Layout Toggling:**
   * Setting `IsSymbol = true` programmatically updates `isSymbol` and invokes `ChangeButtonLayout_Click()`.
   * `ChangeButtonLayout_Click()` evaluates `isSymbol`. If `true`, it collapses `ButtonTextBlock` and applies the `"SymbolButton"` style resource.
   * If `IsSymbol` is set to `false`, it makes `ButtonTextBlock` visible and attempts to apply the `"TealColor"` style resource.
3. **Loaded State:** When the control triggers its loaded event, `CollapsibleButton_Loaded` checks if `isSymbol` is enabled and adjusts the visual layout to symbol-only mode if required.