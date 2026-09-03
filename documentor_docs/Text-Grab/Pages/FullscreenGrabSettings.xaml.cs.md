# Technical Documentation: `FullscreenGrabSettings.xaml.cs`

## Overview

The `FullscreenGrabSettings` class represents the code-behind for the Fullscreen Grab settings page (`FullscreenGrabSettings.xaml`) in the Text-Grab application. Its primary responsibility is to provide the user interface logic for configuring Fullscreen Grab (FSG) options, such as default extraction modes, selection styles, overlay behavior, text insertion delays, and post-grab action configurations.

---

## Class Details

* **Namespace:** `Text_Grab.Pages`
* **Class Name:** `FullscreenGrabSettings`
* **Base Class:** `System.Windows.Controls.Page`

---

## Fields and Properties

| Name | Type | Access Modifier | Description |
| :--- | :--- | :--- | :--- |
| `DefaultSettings` | `Settings` | `private readonly` | Reference to global application settings obtained via `AppUtilities.TextGrabSettings`. |
| `_loaded` | `bool` | `private` | A state flag set to `true` after the initial page hydration completes. Prevents settings from saving prematurely during UI control initialization. |

---

## Core Operations

### 1. Page Hydration (`Page_Loaded`)
When the page triggers the `Loaded` event, `Page_Loaded` executes to populate UI controls with current configuration values stored in `DefaultSettings`:

1. **Checkboxes & Sliders:**
   * `SendToEtwCheckBox`: Checked based on `DefaultSettings.FsgSendEtwToggle`.
   * `TryInsertCheckBox`: Checked based on `DefaultSettings.TryInsert`.
   * `ShadeOverlayCheckBox`: Checked based on `DefaultSettings.FsgShadeOverlay`.
   * `InsertDelaySlider`: Sets slider value clamped between `InsertDelaySlider.Minimum` and `InsertDelaySlider.Maximum` using `DefaultSettings.InsertDelay`. Enabled state depends on `TryInsertCheckBox.IsChecked`.
   * `InsertDelayValueText`: Displays formatted text representation of `DefaultSettings.InsertDelay` (`0.0` format using `CultureInfo.InvariantCulture`).

2. **Default Grab Mode Selection:**
   * Evaluates `DefaultSettings.FsgDefaultMode` and `DefaultSettings.FSGMakeSingleLineToggle`.
   * Sets `TableModeRadio`, `SingleLineModeRadio`, or `DefaultModeRadio` accordingly.

3. **Selection Style:**
   * Parses `DefaultSettings.FsgSelectionStyle` into `FsgSelectionStyle` enum.
   * Checks the corresponding radio button (`WindowSelectionStyleRadio`, `FreeformSelectionStyleRadio`, `AdjustAfterSelectionStyleRadio`, or `RegionSelectionStyleRadio`).

4. **Post-Grab Action Summary:**
   * Invokes `UpdateActionsCountText()` to refresh the active post-grab action UI text summary.

5. **Loaded Flag:**
   * Sets `_loaded = true` to allow event handlers to process user interaction events and save updates.

---

## Event Handlers

### Mode Selection Radio Buttons
* **`DefaultModeRadio_Click`**: Sets `FsgDefaultMode` to `Default`, sets `FSGMakeSingleLineToggle` to `false`, and saves settings.
* **`SingleLineModeRadio_Click`**: Sets `FSGMakeSingleLineToggle` to `true`, resets `FsgDefaultMode` to `Default`, and saves settings.
* **`TableModeRadio_Click`**: Sets `FsgDefaultMode` to `Table`, sets `FSGMakeSingleLineToggle` to `false`, and saves settings.

### Selection Style Radio Buttons
* **`RegionSelectionStyleRadio_Click`**: Invokes `SaveSelectionStyle` with `FsgSelectionStyle.Region`.
* **`WindowSelectionStyleRadio_Click`**: Invokes `SaveSelectionStyle` with `FsgSelectionStyle.Window`.
* **`FreeformSelectionStyleRadio_Click`**: Invokes `SaveSelectionStyle` with `FsgSelectionStyle.Freeform`.
* **`AdjustAfterSelectionStyleRadio_Click`**: Invokes `SaveSelectionStyle` with `FsgSelectionStyle.AdjustAfter`.

### Checkbox Controls
* **`SendToEtwCheckBox_Click`**: Updates `DefaultSettings.FsgSendEtwToggle` based on checkbox state and saves.
* **`TryInsertCheckBox_Click`**: Updates `DefaultSettings.TryInsert` state, enables/disables `InsertDelaySlider`, and saves settings.
* **`ShadeOverlayCheckBox_Click`**: Updates `DefaultSettings.FsgShadeOverlay` based on toggle state and saves settings.

### Slider Controls
* **`InsertDelaySlider_ValueChanged`**: Rounds slider value to 1 decimal place, saves to `DefaultSettings.InsertDelay`, and updates `InsertDelayValueText.Text`.

### Action & Template Management Buttons
* **`CustomizeActionsButton_Click`**: Instantiates and displays `PostGrabActionEditor` as a modal dialog (`ShowDialog()`). If the dialog result is `true`, calls `UpdateActionsCountText()`.
* **`ManageTemplatesButton_Click`**: Instantiates and displays `PostGrabActionEditor` as a modal dialog (`ShowDialog()`). Calls `UpdateActionsCountText()` after the dialog closes.

---

## Helper Methods

### `UpdateActionsCountText()`
`private void UpdateActionsCountText()`

Retrieves enabled post-grab actions via `PostGrabActionManager.GetEnabledPostGrabActions()` and formats `ActionsCountText.Text` depending on the count:
* **0 items:** Sets text to `"No actions enabled"`.
* **1 item:** Displays text formatted as `"1 action enabled: {actionName}"`.
* **>1 items:** Formats list displaying up to the first 3 enabled action names. If there are more than 3, appends `", and {X} more"`.

### `SaveSelectionStyle(RadioButton radioButton, FsgSelectionStyle selectionStyle)`
`private void SaveSelectionStyle(RadioButton radioButton, FsgSelectionStyle selectionStyle)`

Validates that `_loaded` is `true` and the given `radioButton` is checked. Converts `selectionStyle` enum to string, assigns it to `DefaultSettings.FsgSelectionStyle`, and calls `DefaultSettings.Save()`.

---

## Dependencies & External References

* **`Text_Grab.Models`**: Contains `FsgDefaultMode` and `FsgSelectionStyle` enums.
* **`Text_Grab.Properties`**: Provides `Settings` object type.
* **`Text_Grab.Utilities`**: Provides `AppUtilities.TextGrabSettings` and `PostGrabActionManager`.
* **`Text_Grab.Controls`**: Provides `PostGrabActionEditor` window dialog.