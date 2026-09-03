# Technical Documentation: `VoiceOutputSettings.xaml.cs`

**Path:** `Text-Grab/Pages/VoiceOutputSettings.xaml.cs`  
**Namespace:** `Text_Grab.Pages`  
**Base Class:** `System.Windows.Controls.Page`

---

## Overview

`VoiceOutputSettings` is a WPF partial class code-behind file for managing Text-to-Speech (TTS) user preferences within the Text-Grab application. It acts as an interface between the user interface controls (comboboxes, sliders, textboxes, checkboxes) and persistent application settings, allowing users to configure TTS voices, speaking rate, word count limits, and status readouts, as well as previewing the voice.

---

## Class Fields & Properties

* **`private readonly Settings DefaultSettings`**  
  Holds a reference to the application settings instance retrieved via `AppUtilities.TextGrabSettings`.

* **`private bool _loaded`**  
  A flag set to `true` after the page finishes its initial setup (`Page_Loaded`). Event handlers check this flag to prevent updating settings during control initialization.

---

## Constructor

### `VoiceOutputSettings()`
Instantiates the page and executes `InitializeComponent()` to load the associated XAML components.

---

## Event Handlers & Core Methods

### 1. Page Lifecycle

#### `Page_Loaded(object sender, RoutedEventArgs e)`
Executes when the WPF page is loaded.
* **Voice Selection Population:**
  * Clears `VoiceComboBox`.
  * Iterates over `Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices`, ordered alphabetically by `DisplayName`, adding each voice's display name to `VoiceComboBox`.
  * Checks `DefaultSettings.TtsVoiceName`. If a saved voice name exists in `VoiceComboBox`, it sets it as selected; otherwise, defaults selection to the first item (index `0`).
* **UI Initialization from Settings:**
  * Sets `SpeakingRateSlider.Value` to `DefaultSettings.TtsSpeakingRate`.
  * Formats and sets `SpeakingRateValue.Text` to display the rate formatted as `0.0`.
  * Sets `TtsSpeakWordLimitTextBox.Text` to `DefaultSettings.TtsSpeakWordLimit`.
  * Sets `SpeakProcessingStatusCheckBox.IsChecked` to `DefaultSettings.SpeakProcessingStatus`.
* Sets `_loaded` to `true` to enable runtime event handlers.

---

### 2. Setting Change Handlers

#### `SpeakingRateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)`
* Guards execution with `if (!_loaded) return;`.
* Rounds the new slider value (`e.NewValue`) to 1 decimal place.
* Updates `SpeakingRateValue.Text` with the rounded value formatted to `"0.0"`.
* Assigns the rounded rate to `DefaultSettings.TtsSpeakingRate` and calls `DefaultSettings.Save()`.

#### `VoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)`
* Guards execution with `if (!_loaded) return;`.
* If the selected item is a string, updates `DefaultSettings.TtsVoiceName` to the selected voice name and calls `DefaultSettings.Save()`.

#### `TtsSpeakWordLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)`
* Guards execution with `if (!_loaded) return;`.
* Validates user input by attempting to parse `TtsSpeakWordLimitTextBox.Text` as a positive integer (> 0).
* **If Valid:**
  * Updates `DefaultSettings.TtsSpeakWordLimit` with the parsed integer value.
  * Calls `DefaultSettings.Save()`.
  * Collapses the error indicator (`TtsWordLimitError.Visibility = Visibility.Collapsed`).
* **If Invalid:**
  * Displays the error indicator (`TtsWordLimitError.Visibility = Visibility.Visible`).

#### `SpeakProcessingStatusCheckBox_CheckChanged(object sender, RoutedEventArgs e)`
* Guards execution with `if (!_loaded) return;`.
* Checks if `SpeakProcessingStatusCheckBox.IsChecked` is `true`.
* Sets `DefaultSettings.SpeakProcessingStatus` accordingly and invokes `DefaultSettings.Save()`.

---

### 3. Action Handlers

#### `PreviewVoiceButton_Click(object sender, RoutedEventArgs e)`
* Triggers a preview audio message by calling `Singleton<TtsService>.Instance.Speak(...)` with the hardcoded phrase:
  `"Hello, this is a preview of the selected voice."`

---

## UI Components Reference

The code interacts with the following UI controls defined in the corresponding XAML page:

| Control Identifier | Type | Description |
| :--- | :--- | :--- |
| `VoiceComboBox` | `ComboBox` | Holds installed system TTS voices. |
| `SpeakingRateSlider` | `Slider` | Adjusts TTS playback speed. |
| `SpeakingRateValue` | `TextBlock` / `TextBox` | Displays numerical reading of the speaking rate. |
| `TtsSpeakWordLimitTextBox` | `TextBox` | Accepts input for maximum words to speak. |
| `TtsWordLimitError` | `UIElement` | Error UI element shown on invalid word limit input. |
| `SpeakProcessingStatusCheckBox` | `CheckBox` | Enables or disables speaking processing status notifications. |

---

## External Dependencies

* **`Text_Grab.Properties.Settings`**: Accesses persistent setting values.
* **`Text_Grab.Services.TtsService`**: Service instance used to trigger speech playback.
* **`Text_Grab.Utilities.AppUtilities`**: Supplies default application settings references (`AppUtilities.TextGrabSettings`).
* **`Text_Grab.Utilities.Singleton<T>`**: Generic singleton pattern provider used to access `TtsService`.
* **`Windows.Media.SpeechSynthesis`**: Used to query system speech synthesis voices (`SpeechSynthesizer.AllVoices`).