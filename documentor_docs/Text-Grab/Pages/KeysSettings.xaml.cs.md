# Technical Documentation: `KeysSettings.xaml.cs`

**File Path:** `Text-Grab/Pages/KeysSettings.xaml.cs`  
**Namespace:** `Text_Grab.Pages`  
**Base Class:** `System.Windows.Controls.Page`

---

## Overview

The `KeysSettings` class represents the code-behind for the Keyboard Settings page in the Text-Grab application. It manages user preferences related to keyboard shortcuts, global hotkeys, and background execution settings. 

Key responsibilities include:
- Loading shortcut settings into corresponding `ShortcutControl` UI elements.
- Ensuring only one shortcut control records input at a time.
- Validating shortcut sets to prevent duplicate hotkey collisions.
- Persisting shortcut key changes to application settings.
- Managing background application execution and global hotkey toggle states.

---

## Class Fields

| Field | Type | Description |
| :--- | :--- | :--- |
| `DefaultSettings` | `Settings` (readonly) | Reference to the application settings instance obtained via `AppUtilities.TextGrabSettings`. |
| `settingsSet` | `bool` | Flag indicating whether the initial loading and mapping of settings to UI controls has completed. Used to prevent event handlers from executing during setup. |

---

## Constructor

### `KeysSettings()`
Initializes a new instance of the `KeysSettings` page by calling `InitializeComponent()`.

---

## Lifecycle Event Handlers

### `Page_Loaded(object sender, RoutedEventArgs e)`
Handles the `Loaded` event of the WPF `Page`.

**Execution Flow:**
1. Unregisters existing global hotkeys by passing the current `App` instance to `NotifyIconUtilities.UnregisterHotkeys(app)`.
2. Sets UI check states for `RunInBackgroundChkBx` and `GlobalHotkeysCheckbox` based on `DefaultSettings`.
3. Calls `ShortcutKeysUtilities.GetShortcutKeySetsFromSettings()` to retrieve configured hotkey actions.
4. Iterates through the retrieved `ShortcutKeySet` items and maps each set to its respective UI `ShortcutControl` instance based on `keySet.Action`:
   - `ShortcutKeyActions.Fullscreen` $\rightarrow$ `FsgShortcutControl.KeySet`
   - `ShortcutKeyActions.GrabFrame` $\rightarrow$ `GfShortcutControl.KeySet`
   - `ShortcutKeyActions.Lookup` $\rightarrow$ `QslShortcutControl.KeySet`
   - `ShortcutKeyActions.EditWindow` $\rightarrow$ `EtwShortcutControl.KeySet`
   - `ShortcutKeyActions.PreviousRegionGrab` $\rightarrow$ `GlrShortcutControl.KeySet`
   - `ShortcutKeyActions.PreviousEditWindow` $\rightarrow$ `LetwShortcutControl.KeySet`
   - `ShortcutKeyActions.PreviousGrabFrame` $\rightarrow$ `LgfShortcutControl.KeySet`
   - `ShortcutKeyActions.OpenClipboardContent` $\rightarrow$ `OccShortcutControl.KeySet`
   - *Note: `None` and `Settings` actions perform no control assignment.*
5. Sets `settingsSet = true` to enable operational event handling.

---

## User Interaction Event Handlers

### `ShortcutControl_Recording(object sender, EventArgs e)`
Fired when a `ShortcutControl` begins recording user hotkey input.

* **Guard Clause:** Returns early if `settingsSet` is `false`.
* **Logic:** Iterates through `ShortcutsStackPanel.Children`. For any child that is a `ShortcutControl` other than the sender control, it calls `shortcutControl.StopRecording(sender)` to ensure only one control is actively recording hotkeys at a time.

---

### `ShortcutControl_KeySetChanged(object sender, EventArgs e)`
Fired when a `ShortcutControl` modifies its assigned key sequence.

* **Guard Clause:** Returns early if `settingsSet` is `false`.
* **Logic:**
  1. Calls `HotKeysAllDifferent()` to validate that no shortcuts conflict with one another.
  2. If validation passes (`HotKeysAllDifferent()` returns `true`), it gathers all `ShortcutKeySet` objects from the child `ShortcutControl` elements in `ShortcutsStackPanel`.
  3. Saves the updated key sets by calling `ShortcutKeysUtilities.SaveShortcutKeySetSettings(shortcutKeys)`.

---

### `RunInBackgroundChkBx_Checked(object sender, RoutedEventArgs e)`
Fired when `RunInBackgroundChkBx` is checked.

* **Guard Clause:** Returns early if `settingsSet` is `false`.
* **Logic:** Updates `DefaultSettings.RunInTheBackground = true`, applies the background execution behavior via `ImplementAppOptions.ImplementBackgroundOption(true)`, and saves the settings.

---

### `RunInBackgroundChkBx_Unchecked(object sender, RoutedEventArgs e)`
Fired when `RunInBackgroundChkBx` is unchecked.

* **Guard Clause:** Returns early if `settingsSet` is `false`.
* **Logic:** Updates `DefaultSettings.RunInTheBackground = false`, invokes `ImplementAppOptions.ImplementBackgroundOption(false)`, unchecks `GlobalHotkeysCheckbox`, and saves the settings.

---

### `GlobalHotkeysCheckbox_Checked(object sender, RoutedEventArgs e)`
Fired when `GlobalHotkeysCheckbox` is checked.

* **Guard Clause:** Returns early if `settingsSet` is `false`.
* **Logic:** Sets `DefaultSettings.GlobalHotkeysEnabled = true`.

---

### `GlobalHotkeysCheckbox_Unchecked(object sender, RoutedEventArgs e)`
Fired when `GlobalHotkeysCheckbox` is unchecked.

* **Guard Clause:** Returns early if `settingsSet` is `false`.
* **Logic:** Sets `DefaultSettings.GlobalHotkeysEnabled = false`.

---

## Private Helper Methods

### `HotKeysAllDifferent()`
Validates that no two active `ShortcutControl` components share the exact same key combination.

* **Returns:** `bool` — `true` if all enabled shortcut combinations are unique; `false` if duplicates exist or if no shortcuts are found.

**Detailed Logic:**
1. Collects all `ShortcutControl` children from `ShortcutsStackPanel` into a `HashSet<ShortcutControl>`.
2. Returns `false` if no `ShortcutControl` elements are found in the set.
3. Compares each shortcut control against every other shortcut control in the set.
4. If two distinct controls both have `IsEnabled == true` and equal key sets (`keySet.AreKeysEqual(shortcut2.KeySet)`):
   - Sets `HasConflictingError = true` on both controls.
   - Puts the conflicting control into an error state via `GoIntoErrorMode("Cannot have two shortcuts that are the same")`.
   - Flags that matching keys exist.
5. If a control has no conflicting duplicate, resets `shortcut.HasConflictingError = false`.
6. Invokes `shortcut.CheckForErrors()` on each control to update UI error visual states.
7. Returns `false` if any matching key pairs were detected, otherwise returns `true`.

---

## Inter-component Dependencies

- **`Text_Grab.Controls.ShortcutControl`**: UI control representing individual key sequence inputs.
- **`Text_Grab.Models.ShortcutKeySet` & `ShortcutKeyActions`**: Data models describing key actions and bound shortcuts.
- **`Text_Grab.Utilities`**:
  - `AppUtilities`: Provides global app settings (`AppUtilities.TextGrabSettings`).
  - `ShortcutKeysUtilities`: Manages loading and saving shortcut key configurations.
  - `NotifyIconUtilities`: Handles unregistering hotkeys upon entering the settings view.
  - `ImplementAppOptions`: Configures runtime background execution based on setting changes.