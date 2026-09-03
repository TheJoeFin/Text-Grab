# Documentation: `ShortcutKeysUtilities.cs`

## Overview

The `ShortcutKeysUtilities` class is an internal utility within the `Text_Grab.Utilities` namespace. It provides static helper methods to manage, load, parse, and save user-configured shortcut key settings (`ShortcutKeySet`) for the Text-Grab application. 

It handles backward compatibility by attempting to parse legacy setting strings into structured `ShortcutKeySet` objects if no modern shortcut configurations exist.

---

## Class Details

* **Namespace:** `Text_Grab.Utilities`
* **Access Modifier:** `internal`
* **Class Type:** Static utility class (`internal class ShortcutKeysUtilities`)

---

## Static Methods

### 1. `SaveShortcutKeySetSettings`

Saves the given collection of shortcut key sets to the application settings.

```csharp
public static void SaveShortcutKeySetSettings(IEnumerable<ShortcutKeySet> shortcutKeySets)
```

* **Parameters:**
  * `shortcutKeySets` (`IEnumerable<ShortcutKeySet>`): A collection of `ShortcutKeySet` objects to be persisted.
* **Return Value:** `void`
* **Behavior:** Delegates the persistence of the shortcut configurations directly to `AppUtilities.TextGrabSettingsService.SaveShortcutKeySets(shortcutKeySets)`.

---

### 2. `GetShortcutKeySetsFromSettings`

Retrieves the active shortcut key settings for the application.

```csharp
public static IEnumerable<ShortcutKeySet> GetShortcutKeySetsFromSettings()
```

* **Parameters:** None
* **Return Value:** `IEnumerable<ShortcutKeySet>` – A merged collection of shortcut key sets containing saved user settings supplemented by default settings for missing actions.
* **Behavior:**
  1. Retrieves the default shortcut key sets via `ShortcutKeySet.DefaultShortcutKeySets`.
  2. Attempts to load saved `ShortcutKeySet` list from `AppUtilities.TextGrabSettingsService.LoadShortcutKeySets()`.
  3. If the loaded list is empty (`shortcutKeySets.Count == 0`), it falls back to `ParseFromPreviousAndDefaultsSettings()`.
  4. If saved settings exist, it extracts all actions already defined in `shortcutKeySets`.
  5. Concatenates the saved shortcut key sets with any default shortcut key sets whose actions are not present in the custom settings.

---

### 3. `ParseFromPreviousAndDefaultsSettings`

Parses shortcut key configurations from older individual string setting properties and combines them with defaults for unhandled actions.

```csharp
public static IEnumerable<ShortcutKeySet> ParseFromPreviousAndDefaultsSettings()
```

* **Parameters:** None
* **Return Value:** `IEnumerable<ShortcutKeySet>` – A collection of legacy-parsed settings combined with default key sets.
* **Behavior:**
  1. Reads the legacy hotkey string `FullscreenGrabHotKey` from `AppUtilities.TextGrabSettings`.
  2. If `FullscreenGrabHotKey` is null or whitespace, returns `ShortcutKeySet.DefaultShortcutKeySets` immediately.
  3. Reads legacy hotkey strings for standard actions:
     * `FullscreenGrabHotKey`
     * `GrabFrameHotkey`
     * `EditWindowHotKey`
     * `LookupHotKey`
  4. Iterates through the standard actions (`Fullscreen`, `GrabFrame`, `EditWindow`, `Lookup`):
     * Maps action to friendly names: `"Fullscreen Grab"`, `"Grab Frame"`, `"Edit Text Window"`, `"Quick Simple Lookup"`.
     * Tries to parse the corresponding key string into a `System.Windows.Input.Key` enum using `Enum.TryParse`.
     * If successful, instantiates a `ShortcutKeySet` object with:
       * `NonModifierKey`: The parsed `Key`.
       * `Modifiers`: A `HashSet<KeyModifiers>` containing `KeyModifiers.Shift` and `KeyModifiers.Windows`.
       * `IsEnabled`: `true`
       * `Name`: Corresponding action friendly name.
       * `Action`: The corresponding `ShortcutKeyActions` enum value.
  5. Concatenates these newly converted settings with any default actions in `ShortcutKeySet.DefaultShortcutKeySets` that are not part of the standard legacy actions set (`Fullscreen`, `GrabFrame`, `EditWindow`, `Lookup`).

---

## Dependencies & Referenced Types

* **`Text_Grab.Models`**:
  * `ShortcutKeySet`: Object model representing a shortcut key mapping.
  * `ShortcutKeyActions`: Enum representing shortcut key actions.
  * `KeyModifiers`: Enum defining modifier keys (e.g., `Shift`, `Windows`).
* **`AppUtilities`**:
  * `TextGrabSettingsService`: Service responsible for loading/saving shortcut key configurations (`LoadShortcutKeySets`, `SaveShortcutKeySets`).
  * `TextGrabSettings`: Application settings container supplying string properties (`FullscreenGrabHotKey`, `GrabFrameHotkey`, `EditWindowHotKey`, `LookupHotKey`).
* **`System.Windows.Input.Key`**: Standard WPF key enumeration used to identify the target non-modifier key.