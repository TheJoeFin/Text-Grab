# Technical Documentation: `ShortcutControl.xaml.cs`

## Overview

The `ShortcutControl` class is a WPF `UserControl` within the `Text_Grab.Controls` namespace. Its primary purpose is to provide an interactive user interface component for viewing, enabling/disabling, and recording custom keyboard shortcuts (key combinations). 

It captures live keyboard input using a combination of WPF input events and native Win32 keyboard state polling (`NativeMethods.GetKeyboardState`), validates the shortcut structure (requiring at least one modifier key and one non-modifier key), and reflects the state in visual elements.

---

## Class Architecture & Hierarchy

* **Namespace:** `Text_Grab.Controls`
* **Base Class:** `System.Windows.Controls.UserControl`
* **Source File:** `Text-Grab/Controls/ShortcutControl.xaml.cs`

---

## Fields & Constants

### Visual Brushes
* `private readonly Brush BadBrush`: A `SolidColorBrush` initialized to `Colors.Red`, used to highlight borders when the shortcut has an error.
* `private readonly Brush GoodBrush`: A `SolidColorBrush` initialized to `Colors.Transparent`, used for normal border state.

### State Flags
* `private bool HasErrorWithKeySet`: Tracked internally to indicate if the current recorded key set is missing required components (e.g., missing a modifier or a non-modifier key).
* `public bool HasConflictingError`: Public property indicating if an external system or duplicate validation flagged a conflict with this shortcut.
* `private bool isRecording`: Indicates whether the control is actively listening to key presses to record a new shortcut sequence.
* `private string previousSequence`: Caches the key sequence string representation during input processing to prevent redundant processing.
* `public bool HasModifier`: Tracks whether at least one modifier key (Win, Shift, Ctrl, Alt) is currently pressed during recording.
* `public bool HasLetter`: Tracks whether a non-modifier key is currently pressed during recording.

### Native Key Mapping
* `private static readonly byte[] DistinctVirtualKeys`: A static cached array of unique byte values representing virtual key codes from range 0 to 255. Generated via `KeyInterop.KeyFromVirtualKey` and `KeyInterop.VirtualKeyFromKey`.

---

## Dependency Properties

The control defines two WPF Dependency Properties for binding and UI integration:

| Property Name | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `IsShortcutEnabled` | `bool` | `false` | Indicates whether the shortcut is enabled. Registered via `IsShortcutEnabledProperty`. |
| `ShortcutName` | `string` | `"shortcutName"` | The descriptive name of the shortcut. Registered via `ShortcutNameProperty`. |

---

## Model Properties & Events

### Properties

#### `KeySet`
* **Type:** `ShortcutKeySet`
* **Description:** Represents the current shortcut configuration.
* **Setter Logic:**
  1. Checks if the value is unchanged; if so, returns early.
  2. Updates the backing field `_keySet`.
  3. Toggles visibility for modifier UI badges (`WinKey`, `ShiftKey`, `CtrlKey`, `AltKey`) based on `_keySet.Modifiers`.
  4. Syncs `IsShortcutEnabled` with `_keySet.IsEnabled`.
  5. Sets `ButtonsPanel.Visibility` based on whether the shortcut is enabled (`Visible` if enabled, `Collapsed` if disabled).
  6. Updates `ShortcutName` from `_keySet.Name`.
  7. Updates `KeyLetterTextBlock.Text` to display `_keySet.NonModifierKey`.
  8. Raises the `KeySetChanged` event.

### Events

* `public event EventHandler? KeySetChanged`: Raised when the `KeySet` property changes or when the shortcut enabled state is toggled.
* `public event EventHandler? RecordingStarted`: Raised when the user toggles the recording state to `true`.

---

## Methods & Event Handlers

### Initialization & Automation

#### `ShortcutControl_Loaded(object sender, RoutedEventArgs e)`
* **Description:** Executed when the control loads.
* **Behavior:** Reads the `AutomationProperties.GetAutomationId` of the control. If present, sets automated accessibility IDs for internal controls:
  * `{AutomationId}.Enabled` on `IsEnabledToggleSwitch`
  * `{AutomationId}.Record` on `RecordingToggleButton`
  * `{AutomationId}.Error` on `ErrorText`

---

### UI Error State Management

#### `GoIntoErrorMode(string errorMessage = "")`
* Sets `BorderBrush` to `BadBrush` (Red).
* Sets `ErrorText.Text` if an error message string is provided.
* Sets `ErrorText.Visibility` to `Visibility.Visible`.

#### `GoIntoNormalMode()`
* Sets `ErrorText.Visibility` to `Visibility.Collapsed`.
* Clears `ErrorText.Text`.
* Sets `BorderBrush` to `GoodBrush` (Transparent).

#### `CheckForErrors()`
* Evaluates `HasErrorWithKeySet` and `HasConflictingError`.
* Calls `GoIntoErrorMode()` if either error flag is `true`; otherwise calls `GoIntoNormalMode()`.

---

### Key Recording & Event Handling

#### `ShortcutControl_PreviewKeyDown(object sender, KeyEventArgs e)`
* **Execution Condition:** Exits immediately if `isRecording` is `false`.
* **Behavior:**
  1. Marks event as handled (`e.Handled = true`).
  2. Calls `GetDownKeys()` to obtain all currently pressed physical keys via low-level state polling.
  3. Evaluates modifier state (`containsWin`, `containsShift`, `containsCtrl`, `containsAlt`).
  4. Isolates non-modifier keys using `RemoveModifierKeys()`.
  5. Updates UI element visibilities (`KeyKey`, `WinKey`, `ShiftKey`, `CtrlKey`, `AltKey`).
  6. **Validation Rule:**
     * **Valid:** Must contain **at least one modifier key** (`HasModifier`) and **at least one non-modifier key** (`HasLetter`).
     * If valid: Sets `HasErrorWithKeySet = false` and updates `KeySet` with a new `ShortcutKeySet` instance containing the pressed modifiers and key.
     * If invalid: Sets `HasErrorWithKeySet = true` and updates `ErrorText.Text` with `"Need to have at least one modifier and one non-modifier key"`.
  7. Updates `KeyLetterTextBlock.Text` and tracks `previousSequence`.

#### `ShortcutControl_PreviewKeyUp(object sender, KeyEventArgs e)`
* **Execution Condition:** Exits immediately if `isRecording` is `false`.
* **Behavior:** Calls `CheckForErrors()` when a key is released.

#### `RecordingToggleButton_Click(object sender, RoutedEventArgs e)`
* **Behavior:** Reads the state of `RecordingToggleButton`. Updates `isRecording`. If set to `true`, invokes `RecordingStarted`.

#### `StopRecording(object sender)`
* **Behavior:** Explicitly stops recording mode by setting `RecordingToggleButton.IsChecked = false` and `isRecording = false`.

#### `IsEnabledToggleSwitch_Click(object sender, RoutedEventArgs e)`
* **Behavior:**
  * Updates `_keySet.IsEnabled` with `IsShortcutEnabled`.
  * Adjusts `ButtonsPanel.Visibility` based on `IsShortcutEnabled`.
  * Raises `KeySetChanged`.

---

### Helper & Low-Level Keyboard Functions

#### `GetDownKeys()`
* **Return Type:** `HashSet<Key>`
* **Description:** Interrogates the system keyboard state using native P/Invoke.
* **Mechanism:**
  1. Allocates a 256-byte array.
  2. Executes `NativeMethods.GetKeyboardState(keyboardState)`.
  3. Iterates over `DistinctVirtualKeys`.
  4. Performs a bitwise operation `(keyboardState[virtualKey] & 0x80) != 0` to check if the high order bit is set (indicating the key is down).
  5. Maps virtual keys back to WPF `Key` enum values via `KeyInterop.KeyFromVirtualKey` and adds them to the result set.

#### `RemoveModifierKeys(HashSet<Key> downKeys)`
* **Return Type:** `HashSet<Key>`
* **Description:** Takes a set of pressed keys and returns a filtered set with left/right variants of modifier keys (`LWin`, `RWin`, `LeftShift`, `RightShift`, `LeftCtrl`, `RightCtrl`, `LeftAlt`, `RightAlt`) removed.

---

## Workflow Summary

1. **Displaying Shortcuts:** When assigned a `ShortcutKeySet` via `KeySet`, the control updates UI elements to visually represent modifier keys (`Ctrl`, `Alt`, `Shift`, `Win`) and the action key text.
2. **Recording Input:**
   - User toggles the `RecordingToggleButton`. `RecordingStarted` fires.
   - During `PreviewKeyDown`, key down states are queried using `NativeMethods.GetKeyboardState()`.
   - The key combination is dynamically parsed into modifiers and non-modifiers.
   - If the key combination satisfies validation criteria (1+ Modifier + 1+ Non-Modifier), a new `ShortcutKeySet` is generated and assigned.
   - Missing required keys trigger error modes (`GoIntoErrorMode`).
3. **Stopping Recording:** Key release (`PreviewKeyUp`) re-checks for errors, and calling `StopRecording()` turns off the toggle state.