# Technical Documentation: `EditTextWindowSettings.xaml.cs`

**File Path:** `Text-Grab/Pages/EditTextWindowSettings.xaml.cs`  
**Namespace:** `Text_Grab.Pages`  
**Class:** `EditTextWindowSettings` (inherits from `System.Windows.Controls.Page`)

---

## 1. Overview

The `EditTextWindowSettings` code-behind class controls the user interface (UI) logic for managing settings specific to the **Edit Text Window (ETW)** in Text-Grab. 

It handles:
* Reading saved user settings from `AppUtilities.TextGrabSettings` and populating UI controls upon page load.
* Handling user interaction events (button clicks, text changes, slider movements, radio button selections).
* Instantly saving modified settings to disk via `DefaultSettings.Save()`.

---

## 2. Fields & Dependencies

### Private Fields

| Field | Type | Description |
| :--- | :--- | :--- |
| `DefaultSettings` | `Settings` | Reference to application settings accessed via `AppUtilities.TextGrabSettings`. |
| `_loaded` | `bool` | A guard flag used to prevent event handlers from firing and saving settings before the UI controls are fully loaded. |

### Assemblies and Namespaces
* `System`: Provides core primitives and `Math` functions (`Math.Clamp`, `Math.Round`).
* `System.Globalization`: Provides `CultureInfo.InvariantCulture` for consistent string formatting.
* `System.Windows`, `System.Windows.Controls`: Standard WPF framework components (`Page`, `RoutedEventArgs`, `RadioButton`, etc.).
* `Text_Grab.Properties`: Contains application settings models.
* `Text_Grab.Utilities`: Provides `AppUtilities`.

---

## 3. Configuration Categories Handled

The page reads and updates settings across several distinct groups:

1. **Window Behavior**:
   * `EditWindowStartFullscreen`: Launch window in fullscreen.
   * `EditWindowIsOnTop`: Keep window pinned on top of other windows.
   * `EditWindowIsWordWrapOn`: Enable or disable word wrapping.
   * `RestoreEtwPositions`: Save and restore window positions.
2. **Toolbar & UI**:
   * `EditWindowBottomBarIsHidden`: Hide or show the bottom bar.
   * `EtwShowLangPicker`: Show or hide the language selection picker.
   * `EtwUseMargins`: Enable/disable text margins.
3. **Font Settings**:
   * `FontFamilySetting`: Font family name.
   * `FontSizeSetting`: Font size value (clamped between `FontSizeSlider.Minimum` and `FontSizeSlider.Maximum`).
   * `IsFontBold`, `IsFontItalic`, `IsFontUnderline`, `IsFontStrikeout`: Text style flags.
4. **Status Bar Details**:
   * `EtwShowWordCount`, `EtwShowCharDetails`, `EtwShowMatchCount`, `EtwShowRegexPattern`, `EtwShowSimilarMatches`: Toggles for various status bar counters and indicators.
5. **Paste Options**:
   * `EtwNormalizeLineEndingsOnPaste`: Toggle line ending normalization when pasting text.
6. **Spell Check**:
   * `EtwSpellCheckMode`: Stores spell checking strategy (`Auto`, `AlwaysOn`, `Off`) represented as an enum/string.
7. **Calculator Pane**:
   * `CalcShowPane`: Display the calculation pane.
   * `CalcShowErrors`: Display calculation errors (dependent on `CalcShowPane` being enabled).

---

## 4. Key Logic & Workflow

### Initialization & Loading (`Page_Loaded`)
1. Fetches current values from `DefaultSettings`.
2. Populates corresponding UI controls (CheckBoxes, TextBox, Slider, RadioButtons).
3. Clamps font size within the minimum and maximum boundaries allowed by `FontSizeSlider`.
4. Parses `EtwSpellCheckMode` string to the `SpellCheckMode` enum (defaults to `SpellCheckMode.Auto` if parsing fails) and selects the appropriate radio button.
5. Sets `CalcShowErrorsCheckBox.IsEnabled` based on whether `CalcShowPane` is checked.
6. Sets `_loaded = true` at the completion of loading to enable input processing.

### Guarding State Updates (`_loaded` check)
Every event handler begins with:
```csharp
if (!_loaded) return;
```
This ensures that UI control initialization during `Page_Loaded` does not trigger event handlers and unintentionally overwrite saved settings.

---

## 5. Method Reference

### Constructor
* `public EditTextWindowSettings()`
  Calls `InitializeComponent()` to initialize the XAML UI elements.

### Lifecycle Handlers
* `private void Page_Loaded(object sender, RoutedEventArgs e)`
  Populates UI elements with values from `DefaultSettings` and sets `_loaded` to `true`.

### Font Settings Handlers
* `private void FontFamilyTextBox_LostFocus(object sender, RoutedEventArgs e)`
  Updates `DefaultSettings.FontFamilySetting` when the text box loses focus and saves settings.
* `private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)`
  Rounds slider value to the nearest integer, updates `DefaultSettings.FontSizeSetting`, formats `FontSizeValueText` using `CultureInfo.InvariantCulture`, and saves settings.
* `private void IsFontBoldCheckBox_Click(object sender, RoutedEventArgs e)`
* `private void IsFontItalicCheckBox_Click(object sender, RoutedEventArgs e)`
* `private void IsFontUnderlineCheckBox_Click(object sender, RoutedEventArgs e)`
* `private void IsFontStrikeoutCheckBox_Click(object sender, RoutedEventArgs e)`
  Update respective font style booleans in `DefaultSettings` and save.

### Feature Toggle Handlers (CheckBox Clicks)
The following methods check the status of their associated CheckBox control (`IsChecked == true`), update the corresponding property in `DefaultSettings`, and call `DefaultSettings.Save()`:

* `EditWindowStartFullscreenCheckBox_Click`
* `EditWindowIsOnTopCheckBox_Click`
* `EditWindowIsWordWrapOnCheckBox_Click`
* `RestoreEtwPositionsCheckBox_Click`
* `EditWindowBottomBarIsHiddenCheckBox_Click`
* `EtwShowLangPickerCheckBox_Click`
* `EtwUseMarginsCheckBox_Click`
* `EtwShowWordCountCheckBox_Click`
* `EtwShowCharDetailsCheckBox_Click`
* `EtwShowMatchCountCheckBox_Click`
* `EtwShowRegexPatternCheckBox_Click`
* `EtwShowSimilarMatchesCheckBox_Click`
* `EtwNormalizeLineEndingsOnPasteCheckBox_Click`
* `CalcShowErrorsCheckBox_Click`

### Specialized Event Handlers

* `private void SpellCheckModeRadio_Click(object sender, RoutedEventArgs e)`
  Handles selection change for spell check radio buttons (`SpellCheckAutoRadio`, `SpellCheckAlwaysOnRadio`, `SpellCheckOffRadio`).
  * Validates that `sender` is a `RadioButton` and `IsChecked` is `true`.
  * Parses the string value stored in `radioButton.Tag` to a `SpellCheckMode` enum.
  * Updates `DefaultSettings.EtwSpellCheckMode` with the string representation of the enum and saves.

* `private void CalcShowPaneCheckBox_Click(object sender, RoutedEventArgs e)`
  Updates `DefaultSettings.CalcShowPane`, saves settings, and dynamically updates the `IsEnabled` state of `CalcShowErrorsCheckBox`.