# Technical Documentation: `GrabFrameSettings.xaml.cs`

## Overview

The `GrabFrameSettings` class is the code-behind file for the WPF `Page` defined in `GrabFrameSettings.xaml`. It provides interaction logic for managing user settings related to the **Grab Frame** feature within the Text-Grab application.

This class serves two primary responsibilities:
1. **Initializing UI Controls**: Reading stored settings from application configuration (`AppUtilities.TextGrabSettings`) when the page loads and binding those values to UI elements.
2. **Persisting UI State**: Reacting to user interactions (clicks, radio selections, focus loss) and immediately persisting those changes back to the settings configuration.

---

## Class Definition & Fields

```csharp
namespace Text_Grab.Pages;

public partial class GrabFrameSettings : Page
```

### Class Fields

* `private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;`
  * A reference to the global `Settings` instance managed via `AppUtilities`. Used to read and update persistent user settings.
* `private bool _loaded = false;`
  * A flag used to prevent event handlers from saving settings during the initial populating phase (`Page_Loaded`). Handlers check `if (!_loaded) return;` before executing update logic.

---

## Page Initialization & Load Logic

### `GrabFrameSettings()`
Constructor that executes WPF's `InitializeComponent()` to load the XAML layout.

### `Page_Loaded(object sender, RoutedEventArgs e)`
Executed when the settings page is loaded into the UI. It performs the following steps in order:

1. **Populate Checkbox States and Text Inputs**:
   * `GrabFrameAutoOcrCheckBox.IsChecked` ← `DefaultSettings.GrabFrameAutoOcr`
   * `GrabFrameUpdateEtwCheckBox.IsChecked` ← `DefaultSettings.GrabFrameUpdateEtw`
   * `CloseFrameOnGrabCheckBox.IsChecked` ← `DefaultSettings.CloseFrameOnGrab`
   * `GrabFrameReadBarcodesCheckBox.IsChecked` ← `DefaultSettings.GrabFrameReadBarcodes`
   * `GrabFrameTranslationCheckBox.IsChecked` ← `DefaultSettings.GrabFrameTranslationEnabled`
   * `GrabFrameTranslationLanguageText.Text` ← `DefaultSettings.GrabFrameTranslationLanguage`
   * `GrabFrameTranslationLanguageText.IsEnabled` ← `DefaultSettings.GrabFrameTranslationEnabled`

2. **Set Scroll Behavior Radio Buttons**:
   * Parses `DefaultSettings.GrabFrameScrollBehavior` string into a `ScrollBehavior` enum (defaults to `ScrollBehavior.Resize`).
   * Selects the matching radio button:
     * `ScrollBehavior.None` → `NoneScrollRadio`
     * `ScrollBehavior.Zoom` → `ZoomScrollRadio`
     * `ScrollBehavior.ZoomWhenFrozen` → `ZoomWhenFrozenScrollRadio`
     * `ScrollBehavior.Resize` (default) → `ResizeScrollRadio`

3. **Set Border Style Radio Buttons & Panels**:
   * Parses `DefaultSettings.GrabFrameBorderStyle` string into a `GrabFrameBorderStyle` enum (defaults to `GrabFrameBorderStyle.Theme`).
   * Selects the matching radio button:
     * `GrabFrameBorderStyle.HighContrast` → `HighContrastBorderRadio`
     * `GrabFrameBorderStyle.Color` → `ColorBorderRadio`
     * `GrabFrameBorderStyle.Theme` (default) → `ThemeBorderRadio`
   * Enables `BorderColorSwatchPanel` if the current style is `GrabFrameBorderStyle.Color`.

4. **Set Execution Flag**:
   * Sets `_loaded = true`, allowing subsequent event handlers to process user interaction.

---

## Event Handlers

All event handlers include an early guard clause `if (!_loaded) return;` to ignore triggered events during initial page load.

### Visual Styling Handlers

#### `BorderStyleRadio_Click(object sender, RoutedEventArgs e)`
* **Trigger**: Click on any border style radio button (`HighContrastBorderRadio`, `ColorBorderRadio`, `ThemeBorderRadio`).
* **Logic**:
  * Determines the selected `GrabFrameBorderStyle` based on radio button state.
  * Writes the string representation of the enum to `DefaultSettings.GrabFrameBorderStyle`.
  * Calls `DefaultSettings.Save()`.
  * Enables `BorderColorSwatchPanel` if `Color` is selected, otherwise disables it.

#### `BorderColorSwatch_Click(object sender, RoutedEventArgs e)`
* **Trigger**: Click on a color swatch button within `BorderColorSwatchPanel`.
* **Logic**:
  * Expects `sender` to be a `Button` with a hex color string stored in its `Tag` property.
  * Sets `DefaultSettings.GrabFrameBorderColor` to the extracted hex string.
  * Sets `DefaultSettings.GrabFrameBorderStyle` to `GrabFrameBorderStyle.Color.ToString()`.
  * Calls `DefaultSettings.Save()`.
  * Programmatically checks `ColorBorderRadio` and enables `BorderColorSwatchPanel`.

---

### Feature Toggle (Checkbox) Handlers

All checkbox handlers operate identically: they set the corresponding setting in `DefaultSettings` to `CheckBox.IsChecked == true` and call `DefaultSettings.Save()`.

| Event Handler | Target Setting | Control | Additional Logic |
| :--- | :--- | :--- | :--- |
| `GrabFrameAutoOcrCheckBox_Click` | `GrabFrameAutoOcr` | `GrabFrameAutoOcrCheckBox` | None |
| `GrabFrameUpdateEtwCheckBox_Click` | `GrabFrameUpdateEtw` | `GrabFrameUpdateEtwCheckBox` | None |
| `CloseFrameOnGrabCheckBox_Click` | `CloseFrameOnGrab` | `CloseFrameOnGrabCheckBox` | None |
| `GrabFrameReadBarcodesCheckBox_Click` | `GrabFrameReadBarcodes` | `GrabFrameReadBarcodesCheckBox` | None |
| `GrabFrameTranslationCheckBox_Click` | `GrabFrameTranslationEnabled` | `GrabFrameTranslationCheckBox` | Enables/disables `GrabFrameTranslationLanguageText` based on checked state. |

---

### Text Input Handlers

#### `GrabFrameTranslationLanguageText_LostFocus(object sender, RoutedEventArgs e)`
* **Trigger**: The text box `GrabFrameTranslationLanguageText` loses focus.
* **Logic**:
  * Saves the current text value of `GrabFrameTranslationLanguageText.Text` to `DefaultSettings.GrabFrameTranslationLanguage`.
  * Calls `DefaultSettings.Save()`.

---

### Navigation & Interaction Handlers

#### `ScrollBehaviorRadio_Click(object sender, RoutedEventArgs e)`
* **Trigger**: Click on any scroll behavior radio button (`NoneScrollRadio`, `ZoomScrollRadio`, `ZoomWhenFrozenScrollRadio`, `ResizeScrollRadio`).
* **Logic**:
  * Identifies which radio button is active and converts it to the corresponding `ScrollBehavior` enum value (`None`, `Zoom`, `ZoomWhenFrozen`, or `Resize`).
  * Saves the string representation of the enum to `DefaultSettings.GrabFrameScrollBehavior`.
  * Calls `DefaultSettings.Save()`.

---

## Managed Settings Reference

Below is a summary of all settings properties read and written by this page:

* `DefaultSettings.GrabFrameAutoOcr` (`bool`)
* `DefaultSettings.GrabFrameUpdateEtw` (`bool`)
* `DefaultSettings.CloseFrameOnGrab` (`bool`)
* `DefaultSettings.GrabFrameReadBarcodes` (`bool`)
* `DefaultSettings.GrabFrameTranslationEnabled` (`bool`)
* `DefaultSettings.GrabFrameTranslationLanguage` (`string`)
* `DefaultSettings.GrabFrameScrollBehavior` (`string`)
* `DefaultSettings.GrabFrameBorderStyle` (`string`)
* `DefaultSettings.GrabFrameBorderColor` (`string`)