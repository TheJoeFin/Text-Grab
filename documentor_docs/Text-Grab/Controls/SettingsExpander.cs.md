# Technical Documentation: `SettingsExpander.cs`

**File Location:** `Text-Grab/Controls/SettingsExpander.cs`  
**Namespace:** `Text_Grab.Controls`  

---

## 1. Overview

The `SettingsExpander` class is a custom WPF control derived from `Wpf.Ui.Controls.CardExpander`. It provides an expandable settings card UI modeled after the Windows Community Toolkit's `SettingsExpander` pattern. 

It features:
* A main header area containing a title (`HeaderText`) and an optional description (`Description`).
* An optional content area on the right side of the header row (`SettingsContent`) designed to hold controls such as buttons, toggles, or comboboxes.
* Expandable body content (inherited from `CardExpander` via its standard `Content` property).

The visual elements of the header are constructed in code inside the constructor, enabling parent pages to directly target child controls using `x:Name`.

---

## 2. Inheritance & Class Signature

```csharp
public class SettingsExpander : Wpf.Ui.Controls.CardExpander
```

* **Base Class:** `Wpf.Ui.Controls.CardExpander`

---

## 3. Visual Layout Architecture

The `SettingsExpander` constructs its visual header programmatically within the constructor.

```
Header (Grid - 2 Columns)
 ├── Column 0 (Star Width): StackPanel (textPanel)
 │    ├── TextBlock (headerTextBlock)
 │    └── TextBlock (descriptionTextBlock)
 └── Column 1 (Auto Width): ContentPresenter (settingsContentPresenter)
```

### Layout Details
* **Header Grid (`headerGrid`):**
  * Column 0: Width `1*` (takes remaining available horizontal space).
  * Column 1: Width `Auto` (sized to fit `SettingsContent`).
  * Right margin: `8` (`Thickness(0, 0, 8, 0)`).
* **Text Panel (`textPanel`):**
  * Type: `StackPanel`
  * Vertical Alignment: `Center`
  * Contains `headerTextBlock` and `descriptionTextBlock`.
* **Header Text (`headerTextBlock`):**
  * `FontSize`: `14`
  * `TextWrapping`: `Wrap`
  * Foreground Resource: `"TextFillColorPrimaryBrush"`
* **Description Text (`descriptionTextBlock`):**
  * `FontSize`: `12`
  * `TextWrapping`: `Wrap`
  * Default `Visibility`: `Collapsed`
  * Foreground Resource: `"TextFillColorSecondaryBrush"`
* **Settings Content Presenter (`settingsContentPresenter`):**
  * Type: `ContentPresenter`
  * Column: `1`
  * Margin: `Thickness(12, 0, 12, 0)`
  * Vertical Alignment: `Center`

---

## 4. Dependency Properties

| Dependency Property | Type | Default Value | Property Changed Callback | Description |
| :--- | :--- | :--- | :--- | :--- |
| `HeaderTextProperty` | `string` | `string.Empty` | `OnHeaderTextChanged` | Primary title text displayed in the header. |
| `DescriptionProperty` | `string` | `string.Empty` | `OnDescriptionChanged` | Subtitle/description text displayed beneath the title. |
| `SettingsContentProperty` | `object` | `null` | `OnSettingsContentChanged` | Custom UI element displayed on the right edge of the header row. |

---

## 5. Public Properties

### `HeaderText`
```csharp
public string HeaderText { get; set; }
```
Gets or sets the header text string. Backed by `HeaderTextProperty`.

### `Description`
```csharp
public string Description { get; set; }
```
Gets or sets the secondary description string. Backed by `DescriptionProperty`.

### `SettingsContent`
```csharp
public object? SettingsContent { get; set; }
```
Gets or sets the custom content object hosted in the header's right action area (`settingsContentPresenter`). Backed by `SettingsContentProperty`.

---

## 6. Constructor Logic

```csharp
public SettingsExpander()
```

When instantiated, the constructor performs the following initialization steps:
1. **Style Resolution:** Calls `SetResourceReference(StyleProperty, typeof(Wpf.Ui.Controls.CardExpander))` to ensure WPF-UI resolves the base control implicit style correctly for the derived type.
2. **Padding and Margins:** Sets default values:
   * `Margin = new Thickness(0, 0, 0, 3)`
   * `ContentPadding = new Thickness(14, 10, 14, 12)`
3. **Element Initialization:** Instantiates and configures `headerTextBlock`, `descriptionTextBlock`, `textPanel`, `settingsContentPresenter`, and `headerGrid`.
4. **Header Assignment:** Sets the constructed `headerGrid` to the base class's `Header` property.

---

## 7. Private Static Event Callbacks

### `OnHeaderTextChanged`
```csharp
private static void OnHeaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
```
* Triggered when `HeaderTextProperty` changes.
* Updates the `Text` property of the internal `headerTextBlock`.

### `OnDescriptionChanged`
```csharp
private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
```
* Triggered when `DescriptionProperty` changes.
* Updates the `Text` property of the internal `descriptionTextBlock`.
* Toggles `descriptionTextBlock.Visibility`:
  * `Visibility.Collapsed` if the string is `null` or empty (`string.IsNullOrEmpty`).
  * `Visibility.Visible` if content is present.

### `OnSettingsContentChanged`
```csharp
private static void OnSettingsContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
```
* Triggered when `SettingsContentProperty` changes.
* Assigns the new object to the `Content` property of `settingsContentPresenter`.