# Technical Documentation: `ShortcutKeySet.cs`

## Overview

The `ShortcutKeySet.cs` file defines the `ShortcutKeySet` class and the `ShortcutKeyActions` enum within the `Text-Grab.Models` namespace. This model represents a key combination (modifier keys plus a primary non-modifier key), its activation status, associated action, and name within the Text-Grab application. It also provides string parsing logic, custom equality comparisons, hash code generation, and a predefined list of default application shortcuts.

---

## Enumerations

### `ShortcutKeyActions`

An enumeration representing the distinct actions that can be triggered by a shortcut key set.

| Enum Member | Value | Description |
| :--- | :--- | :--- |
| `None` | `0` | Represents no action assigned. |
| `Settings` | `1` | Triggers opening the Settings window. |
| `Fullscreen` | `2` | Triggers the Fullscreen Grab action. |
| `GrabFrame` | `3` | Triggers the Grab Frame action. |
| `Lookup` | `4` | Triggers Quick Simple Lookup. |
| `EditWindow` | `5` | Triggers opening an Edit Text Window. |
| `PreviousRegionGrab` | `6` | Triggers copying the last region selection. |
| `PreviousEditWindow` | `7` | Triggers opening the last Edit Text Window. |
| `PreviousGrabFrame` | `8` | Triggers editing the last Grab Frame. |
| `OpenClipboardContent` | `9` | Triggers opening clipboard content. |

---

## Class: `ShortcutKeySet`

Implements `IEquatable<ShortcutKeySet>`.

### Attributes

* **`[DebuggerDisplay("{Name} : enabled {IsEnabled} modifiers {Modifiers} non-modifiers {NonModifierKey}")]`**: Defines the formatted string display for debugging purposes.

---

### Properties

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `Modifiers` | `HashSet<KeyModifiers>` | `new()` | A set containing modifier keys (e.g., Windows, Control, Shift, Alt). |
| `NonModifierKey` | `Key` | `Key.None` | The non-modifier key component of the shortcut (from `System.Windows.Input.Key`). |
| `IsEnabled` | `bool` | `false` | Indicates whether the shortcut key set is active/enabled. |
| `Name` | `string` | `"EmptyName"` | The display name or description of the shortcut. |
| `Action` | `ShortcutKeyActions` | `ShortcutKeyActions.None` | The action enum value associated with this shortcut. |
| `DefaultShortcutKeySets` | `List<ShortcutKeySet>` | *(Static list)* | A static collection containing the default shortcuts configured for Text-Grab. |

---

### Constructors

#### 1. Parameterless Constructor
```csharp
public ShortcutKeySet()
```
Initializes a new instance of `ShortcutKeySet` with default property values.

#### 2. String Parsing Constructor
```csharp
public ShortcutKeySet(string shortcutsAsString)
```
Initializes a `ShortcutKeySet` instance by parsing a string representation of key bindings.

**Parsing Logic:**
1. Checks if the input string contains `Windows`, `Shift`, `Control`, or `Alt` (case-insensitive) and adds any matches to the `Modifiers` set.
2. Checks if the input string contains a hyphen (`-`). If not, execution returns early.
3. Splits `shortcutsAsString` by `-`.
4. Attempts to parse the first segment (`enabledSplitKeys[0]`) as a `bool`. If parsing fails or the split produces fewer than 2 segments, execution returns.
5. Splits the second segment (`enabledSplitKeys[1]`) by `+` and extracts the last string item using `LastOrDefault()`.
6. Attempts to parse that item into a `System.Windows.Input.Key` enum and assigns it to `NonModifierKey`.

---

### Methods

#### `AreKeysEqual`
```csharp
public bool AreKeysEqual(ShortcutKeySet otherKeySet)
```
* **Parameters:** `otherKeySet` (`ShortcutKeySet`) — The other key set to compare.
* **Returns:** `bool` — `true` if both `NonModifierKey` values match and the `Modifiers` sequence matches; otherwise, `false`.

#### `Equals(HotKeyEventArgs e)`
```csharp
public bool Equals(HotKeyEventArgs e)
```
* **Parameters:** `e` (`HotKeyEventArgs`) — Hotkey event arguments to evaluate.
* **Returns:** `bool` — `true` if the event key matches `NonModifierKey` and the aggregated bitwise combination of `Modifiers` equals `e.Modifiers`; otherwise, `false`.

#### `Equals(ShortcutKeySet? other)`
```csharp
public bool Equals(ShortcutKeySet? other)
```
* **Parameters:** `other` (`ShortcutKeySet?`) — The instance to compare against.
* **Returns:** `bool` — Returns `false` if `other` is `null`. Otherwise returns `true` if `GetHashCode()` of both instances are equal.

#### `Equals(object? obj)`
```csharp
public override bool Equals(object? obj)
```
* **Parameters:** `obj` (`object?`) — The object to compare with the current instance.
* **Returns:** `bool` — Delegates equality check to `Equals(obj as ShortcutKeySet)`.

#### `GetHashCode`
```csharp
public override int GetHashCode()
```
* **Returns:** `int` — A hash code calculated in an `unchecked` block using prime multipliers (`17` and `23`) combined with the hash codes of `Modifiers`, `NonModifierKey`, and `IsEnabled`.

---

## Default Shortcuts Configuration

The static `DefaultShortcutKeySets` property pre-configures 8 standard shortcuts:

1. **Fullscreen Grab**
   * Modifiers: `Win + Shift`
   * Key: `F`
   * Enabled: `true`
   * Action: `ShortcutKeyActions.Fullscreen`
2. **Grab Frame**
   * Modifiers: `Win + Shift`
   * Key: `G`
   * Enabled: `true`
   * Action: `ShortcutKeyActions.GrabFrame`
3. **Quick Simple Lookup**
   * Modifiers: `Win + Shift`
   * Key: `Q`
   * Enabled: `true`
   * Action: `ShortcutKeyActions.Lookup`
4. **Edit Text Window**
   * Modifiers: `Win + Shift`
   * Key: `E`
   * Enabled: `true`
   * Action: `ShortcutKeyActions.EditWindow`
5. **Copy Last Region Selection**
   * Modifiers: `Win + Shift + Ctrl`
   * Key: `F`
   * Enabled: `false`
   * Action: `ShortcutKeyActions.PreviousRegionGrab`
6. **Open Last Edit Text Window**
   * Modifiers: `Win + Shift + Ctrl`
   * Key: `E`
   * Enabled: `false`
   * Action: `ShortcutKeyActions.PreviousEditWindow`
7. **Edit last Grab Frame**
   * Modifiers: `Win + Shift + Ctrl`
   * Key: `G`
   * Enabled: `false`
   * Action: `ShortcutKeyActions.PreviousGrabFrame`
8. **Open Clipboard Content**
   * Modifiers: `Win + Shift + Ctrl`
   * Key: `V`
   * Enabled: `true`
   * Action: `ShortcutKeyActions.OpenClipboardContent`