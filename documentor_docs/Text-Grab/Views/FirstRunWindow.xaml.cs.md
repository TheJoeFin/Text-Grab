# Documentation: `FirstRunWindow.xaml.cs`

## Overview

The `FirstRunWindow` class serves as the code-behind for the first-run / onboarding window in the Text-Grab application. Its primary purpose is to introduce the user to the application, configure initial application settings (such as default launch mode, Windows startup behavior, background execution, and notifications), and allow the user to test out various Text-Grab features.

The window inherits from `Wpf.Ui.Controls.FluentWindow` to support modern Windows Fluent Design styling.

---

## Class Definition

* **Namespace:** `Text_Grab`
* **Class Name:** `FirstRunWindow`
* **Inherits From:** `Wpf.Ui.Controls.FluentWindow`

---

## Class Fields

| Field Name | Type | Access Modifier | Purpose |
| :--- | :--- | :--- | :--- |
| `DefaultSettings` | `Settings` | `private readonly` | Holds a reference to the global application settings instance (`AppUtilities.TextGrabSettings`). |
| `settingsInitialized` | `bool` | `private` | A state flag used to suppress setting change logic while UI components are being initially loaded/populated. |

---

## Key Dependencies & External References

* **`AppUtilities`**: Accesses global application settings (`TextGrabSettings`) and package status (`IsPackaged()`).
* **`WindowUtilities`**: Controls window navigation, opening windows (`OpenOrActivateWindow<T>`), full-screen grabs (`LaunchFullScreenGrab()`), and application shutdown logic (`ShouldShutDown()`).
* **`ImplementAppOptions`**: Applies system-level choices, specifically startup settings (`ImplementStartupOption`) and background mode (`ImplementBackgroundOption`).
* **`TextGrabMode`**: An enum representing the mode Text-Grab launches in (`Fullscreen`, `GrabFrame`, `EditText`, `QuickLookup`).
* **`Windows.ApplicationModel.StartupTask`**: Managed startup API for packaged (MSIX/Store) applications.

---

## Logic & Workflow

### 1. Initial Load & Hydration (`FirstRun_Loaded`)

When the window loads (`Loaded` event), the following steps occur:
1. `settingsInitialized` is set to `false` to block event handlers from overriding saved settings during UI populating.
2. The initial default launch mode is retrieved via `GetDefaultLaunchSetting()`. The corresponding radio button (`FullScreenRDBTN`, `GrabFrameRDBTN`, `EditWindowRDBTN`, or `QuickLookupRDBTN`) is checked.
3. **Startup Handling**:
   * **Packaged App**: Queries `StartupTask.GetAsync("StartTextGrab")`.
     * `Disabled`: `StartupCheckbox` is unchecked and enabled.
     * `DisabledByUser`: `StartupCheckbox` is unchecked and disabled; `StartupTextblock` appends `"\nDisabled in Task Manager"` and turns gray (`Colors.Gray`).
     * `Enabled`: `StartupCheckbox` is checked.
   * **Unpackaged App**: Sets `StartupCheckbox.IsChecked` from `DefaultSettings.StartupOnLogin`.
4. The controls `BackgroundCheckBox` and `NotificationsCheckBox` are synchronized with `DefaultSettings`.
5. `settingsInitialized` is set to `true`, enabling interaction events to persist settings changes.

### 2. Settings Persistence Guard

To prevent control events from firing and overwriting application settings while the controls are populated, all setting update handlers evaluate `settingsInitialized`:

```csharp
if (!settingsInitialized)
    return;
```

### 3. Window Closing (`Window_Closed`)

When the window closes:
1. If `settingsInitialized` is false (window closed before fully loaded), it immediately triggers `WindowUtilities.ShouldShutDown()`.
2. Updates `DefaultSettings.RunInTheBackground` based on `BackgroundCheckBox.IsChecked`.
3. Saves settings (`DefaultSettings.Save()`) and applies background option settings via `ImplementAppOptions.ImplementBackgroundOption(...)`.
4. Invokes `WindowUtilities.ShouldShutDown()` to exit the application if no other primary windows remain open.

---

## Detailed Method & Event Handler Descriptions

### Constructors & Lifecycle

#### `FirstRunWindow()`
Initializes UI components and sets the active visual theme using `App.SetTheme()`.

#### `async void FirstRun_Loaded(object sender, RoutedEventArgs e)`
Asynchronous event handler that initializes the selection states for launch modes, Windows startup configurations, background persistence, and notifications.

#### `void Window_Closed(object? sender, EventArgs e)`
Handles final cleanup when the window closes. Persists the background execution setting and checks whether the app should shut down.

---

### UI Settings Event Handlers

#### `void RadioButton_Checked(object sender, RoutedEventArgs e)`
Fired when any launch mode radio button is selected. Determines which radio button is active (`GrabFrameRDBTN`, `FullScreenRDBTN`, `QuickLookupRDBTN`, or default `EditText`), saves the corresponding `TextGrabMode` name into `DefaultSettings.DefaultLaunch`, and calls `DefaultSettings.Save()`.

#### `async void StartupCheckbox_Checked(object sender, RoutedEventArgs e)`
Fired when the user toggles the startup setting. Updates `DefaultSettings.StartupOnLogin`, calls `ImplementAppOptions.ImplementStartupOption(...)` to register or unregister application startup, and persists the setting.

#### `void NotificationsCheckBox_Checked(object sender, RoutedEventArgs e)`
Fired when the user toggles the notification setting. Updates `DefaultSettings.ShowToast` and persists the setting.

---

### Navigation & Utility Event Handlers

#### `void OkayButton_Click(object sender, RoutedEventArgs e)`
Fired when the user clicks the "Okay" / primary completion button. 
* Checks if `Application.Current.Windows.Count` is 1 or 2.
* If so, launches the configured default launch mode window (`FullScreen`, `GrabFrame`, `EditTextWindow`, or `QuickSimpleLookup`).
* Closes `FirstRunWindow`.

#### `void SettingsButton_Click(object sender, RoutedEventArgs e)`
Opens or activates the `SettingsWindow` and closes the `FirstRunWindow`.

#### `void LicensesButton_Click(object sender, RoutedEventArgs e)`
Opens or activates the `LicensesWindow`.

#### `void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)`
Handles clicking hyperlinked text in the window. Launches the OS default browser using `Process.Start` with `UseShellExecute = true` and marks the event as handled (`e.Handled = true`).

---

### Mode Test Handlers ("Try" Actions)

These methods allow the user to test individual features directly from the onboarding window:

* **`TryEditWindow_Click(object sender, RoutedEventArgs e)`**: Opens/activates `EditTextWindow`.
* **`TryFullscreen_Click(object sender, RoutedEventArgs e)`**: Triggers a full-screen grab via `WindowUtilities.LaunchFullScreenGrab()`.
* **`TryGrabFrame_Click(object sender, RoutedEventArgs e)`**: Opens/activates `GrabFrame`.
* **`TryQuickLookup_Click(object sender, RoutedEventArgs e)`**: Opens/activates `QuickSimpleLookup`.

---

### Helper Methods

#### `private TextGrabMode GetDefaultLaunchSetting()`
Parses the `DefaultSettings.DefaultLaunch` string into a `TextGrabMode` enum using `Enum.TryParse`. If parsing fails, it defaults to returning `TextGrabMode.Fullscreen`.