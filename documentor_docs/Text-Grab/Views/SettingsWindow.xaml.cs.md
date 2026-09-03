# Technical Documentation: `Text-Grab/Views/SettingsWindow.xaml.cs`

## Overview

The `SettingsWindow.xaml.cs` file contains the code-behind logic for the Settings window in the Text-Grab application. It manages the lifecycle events of the settings window, including applying application themes, handling navigation upon window load, managing global hotkeys, persisting settings on window closure, and evaluating application shutdown states.

---

## Class Details

* **Namespace:** `Text_Grab`
* **Class Name:** `SettingsWindow`
* **Inheritance:** `Wpf.Ui.Controls.FluentWindow`
* **Modifier:** `public partial`

---

## Dependencies / Imports

* `System`: Provides base types and `EventArgs`.
* `System.Windows`: Provides core WPF functionality (`RoutedEventArgs`).
* `Text_Grab.Pages`: Contains page types used for navigation (e.g., `GeneralSettings`).
* `Text_Grab.Utilities`: Provides helper utilities (`AppUtilities`, `NotifyIconUtilities`, `WindowUtilities`).

---

## Members & Implementation Details

### Constructors

#### `public SettingsWindow()`
The default constructor for the `SettingsWindow`.

**Behavior:**
1. Calls `InitializeComponent()` to load the associated XAML component UI elements.
2. Calls `App.SetTheme()` to apply the current application theme.

---

### Event Handlers

#### `private void Window_Loaded(object sender, RoutedEventArgs e)`
Handles the WPF `Loaded` event for the settings window.

**Behavior:**
1. **Initial Navigation:** Calls `SettingsNavView.Navigate(typeof(GeneralSettings))` to load the `GeneralSettings` page inside the navigation view control.
2. **Hotkey Management:** Checks if `App.Current` is an instance of `App`. If true, calls `NotifyIconUtilities.UnregisterHotkeys(app)` to unregister application hotkeys while the settings window is active.

#### `private void Window_Closed(object? sender, EventArgs e)`
Handles the WPF `Closed` event for the settings window.

**Behavior:**
1. **Save Settings:** Executes `AppUtilities.TextGrabSettings.Save()` to write updated application settings to storage.
2. **Restore Hotkeys:** Checks if `App.Current` is an instance of `App`. If true, calls `NotifyIconUtilities.RegisterHotKeys(app)` to re-register global hotkeys.
3. **Shutdown Evaluation:** Invokes `WindowUtilities.ShouldShutDown()` to check whether the application should exit if no other windows remain open.

---

## Control References

* **`SettingsNavView`**: A UI control (likely a navigation frame or view control declared in the corresponding `.xaml` file) used to navigate to `GeneralSettings`.