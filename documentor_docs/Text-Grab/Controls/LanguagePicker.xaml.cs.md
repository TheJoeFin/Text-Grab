# Technical Documentation: `LanguagePicker.xaml.cs`

**File Path:** `Text-Grab/Controls/LanguagePicker.xaml.cs`  
**Namespace:** `Text_Grab.Controls`  
**Base Class:** `System.Windows.Controls.UserControl`

---

## 1. Overview

The `LanguagePicker` control is a custom WPF `UserControl` that provides a UI dropdown interface (via a WPF `ComboBox` named `MainComboBox`) for selecting languages. It populates a list of available languages, handles language fallback logic based on input/keyboard settings, persists selected options, and notifies listeners when the selected language changes.

---

## 2. Properties & Fields

### Properties

| Property | Type | Access | Description |
| :--- | :--- | :--- | :--- |
| `Languages` | `ObservableCollection<ILanguage>` | `public get` | An observable collection holding the filtered list of available languages bound to the UI control. |
| `SelectedLanguage` | `ILanguage` | `public get / set` | A CLR wrapper property for the `SelectedLanguageProperty` dependency property. Represents the currently chosen language. |

### Dependency Properties

| Dependency Property | Type | Property Metadata Default | Description |
| :--- | :--- | :--- | :--- |
| `SelectedLanguageProperty` | `ILanguage` | `null` | A registered WPF `DependencyProperty` backing `SelectedLanguage`. |

### Events

| Event | Delegate Type | Description |
| :--- | :--- | :--- |
| `LanguageChanged` | `RoutedEventHandler?` | Raised whenever a user changes the selection in the combo box. |

---

## 3. Methods & Event Handlers

### Constructor

#### `public LanguagePicker()`
* **Purpose:** Initializes the user control.
* **Logic:**
  1. Sets `DataContext = this;` so UI bindings can reference properties like `Languages` and `SelectedLanguage`.
  2. Calls `InitializeComponent()` to load the XAML layout.

---

### Internal Methods

#### `internal void Select(string languageTag)`
* **Purpose:** Programmatically sets the selected language based on a matching language tag (e.g., BCP-47 tag string).
* **Parameters:** 
  * `languageTag` (`string`): The tag string to match against available `ILanguage.LanguageTag` items.
* **Logic:**
  1. Iterates through the items in the `Languages` collection.
  2. If an item's `LanguageTag` matches `languageTag`:
     * Sets `MainComboBox.SelectedIndex` to the matching index.
     * Assigns `SelectedLanguage` to that `ILanguage` object.
     * Terminates the loop.

---

### Private Event Handlers

#### `private void UserControl_Loaded(object sender, RoutedEventArgs e)`
* **Purpose:** Populates the `Languages` collection and determines the initial selected language when the control loads.
* **Logic:**
  1. Clears existing items from the `Languages` collection.
  2. Retrieves the current OCR language using `LanguageUtilities.GetOCRLanguage()`.
  3. Obtains the current active keyboard culture using `InputLanguageManager.Current.CurrentInputLanguage`.
  4. **Fallback Handling:** If `currentSelectedLanguage` is an instance of `UiAutomationLang`, `WindowsAiLang`, or `WindowsAiDescriptionLang`, it converts `currentSelectedLanguage` to a new `GlobalLang` using the keyboard culture's name (`keyboardLanguage.Name`).
  5. **Filtering & Populating:** Iterates over all languages returned by `LanguageUtilities.GetAllLanguages()`:
     * Ignores/skips instances of `UiAutomationLang`, `WindowsAiLang`, or `WindowsAiDescriptionLang`.
     * Adds allowed languages to `Languages`.
     * Compares each language's `LanguageTag` to `currentSelectedLanguage.LanguageTag` to find the matching index.
  6. **UI Index Assignment:** Sets `MainComboBox.SelectedIndex` to the matched index, or defaults to `0` if valid languages exist in the list.

#### `private void MainComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)`
* **Purpose:** Handles selection changes triggered by interaction with `MainComboBox`.
* **Logic:**
  1. Checks if `MainComboBox.SelectedItem` is a valid `ILanguage`.
  2. Sets `SelectedLanguage` to the selected `ILanguage`.
  3. Persists the language setting by calling `CaptureLanguageUtilities.PersistSelectedLanguage(selectedILanguage)`.
  4. Raises the `LanguageChanged` event.

---

## 4. Dependencies & Utility Classes

The control relies on the following internal interfaces and utility classes:

* `ILanguage`: Interface representing a language object containing a `LanguageTag` property.
* `LanguageUtilities`: Provides `GetOCRLanguage()` and `GetAllLanguages()`.
* `CaptureLanguageUtilities`: Provides `PersistSelectedLanguage()`.
* Language Models: `UiAutomationLang`, `WindowsAiLang`, `WindowsAiDescriptionLang`, and `GlobalLang`.