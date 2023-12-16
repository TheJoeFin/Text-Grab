using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Text_Grab.Utilities;

namespace Text_Grab.Models;

[DebuggerDisplay("{Name} : enabled {IsEnabled} modifiers {Modifiers} non-modifiers {NonModifierKey}")]
public class ShortcutKeySet : IEquatable<ShortcutKeySet>
{
    public HashSet<KeyModifiers> Modifiers { get; set; } = new();
    public Key NonModifierKey { get; set; } = Key.None;

    public bool IsEnabled { get; set; } = false;

    public string Name { get; set; } = "EmptyName";

    public ShortcutKeyActions Action { get; set; } = ShortcutKeyActions.None;

    public ShortcutKeySet()
    {

    }

    public ShortcutKeySet(string shortcutsAsString)
    {
        HashSet<KeyModifiers> validModifiersToCheck = new()
        {
            KeyModifiers.Windows,
            KeyModifiers.Shift,
            KeyModifiers.Control,
            KeyModifiers.Alt,
        };

        foreach (KeyModifiers modifier in validModifiersToCheck)
            if (shortcutsAsString.Contains(modifier.ToString(), StringComparison.CurrentCultureIgnoreCase))
                Modifiers.Add(modifier);

        if (!shortcutsAsString.Contains('-'))
            return;

        var enabledSplitKeys = shortcutsAsString.Split('-');

        bool parsedEnabledSuccessfully = bool.TryParse(enabledSplitKeys[0], out bool parsedEnabled);

        if (!parsedEnabledSuccessfully || enabledSplitKeys.Length < 2)
            return;

        var splitUpString = enabledSplitKeys[1].Split('+');
        string? keyString = splitUpString.LastOrDefault();

        if (Enum.TryParse(keyString, out Key parsedKey))
            NonModifierKey = parsedKey;
    }

    public bool AreKeysEqual(ShortcutKeySet otherKeySet)
    {
        if (NonModifierKey != otherKeySet.NonModifierKey)
            return false;

        if (!Modifiers.SequenceEqual(otherKeySet.Modifiers))
            return false;

        return true;
    }

    public bool Equals(HotKeyEventArgs e)
    {
        if (!Enum.TryParse(e.Key.ToString(), out Key pressedKey))
            return false;

        if (pressedKey != NonModifierKey)
            return false;

        if (e.Modifiers != Modifiers.Aggregate((x, y) => x | y))
            return false;

        return true;
    }

    public bool Equals(ShortcutKeySet? other)
    {
        if (other is null)
            return false;

        if (GetHashCode() == other.GetHashCode())
            return true;

        return false;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        return Equals(obj as ShortcutKeySet);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + Modifiers.GetHashCode();
            hash = hash * 23 + NonModifierKey.GetHashCode();
            hash = hash * 23 + IsEnabled.GetHashCode();
            return hash;
        }
    }

    public static List<ShortcutKeySet> DefaultShortcutKeySets { get; set; } = new()
    {
        // When adding a new shortcut:
        // 1. Add it to the list below
        // 2. Add it to the Settings Window Loaded method
        // .\Text-Grab\Views\SettingsWindow.xaml.cs
        // 3. Add the action to the NotifyIconUtilities HotKeyManager_HotKeyPressed method

        new()
        {
            Modifiers = {KeyModifiers.Windows, KeyModifiers.Shift},
            NonModifierKey = Key.F,
            IsEnabled = true,
            Name = "Fullscreen Grab",
            Action = ShortcutKeyActions.Fullscreen
        },
        new()
        {
            Modifiers = {KeyModifiers.Windows, KeyModifiers.Shift},
            NonModifierKey = Key.G,
            IsEnabled = true,
            Name = "Grab Frame",
            Action = ShortcutKeyActions.GrabFrame
        },
        new()
        {
            Modifiers = {KeyModifiers.Windows, KeyModifiers.Shift},
            NonModifierKey = Key.Q,
            IsEnabled = true,
            Name = "Quick Simple Lookup",
            Action = ShortcutKeyActions.Lookup
        },
        new()
        {
            Modifiers = {KeyModifiers.Windows, KeyModifiers.Shift},
            NonModifierKey = Key.E,
            IsEnabled = true,
            Name = "Edit Text Window",
            Action = ShortcutKeyActions.EditWindow
        },
        new()
        {
            Modifiers = {KeyModifiers.Windows, KeyModifiers.Shift, KeyModifiers.Control},
            NonModifierKey = Key.F,
            IsEnabled = false,
            Name = "Copy Last Region Selection",
            Action = ShortcutKeyActions.PreviousRegionGrab
        },
        new()
        {
            Modifiers = {KeyModifiers.Windows, KeyModifiers.Shift, KeyModifiers.Control},
            NonModifierKey = Key.E,
            IsEnabled = false,
            Name = "Open Last Edit Text Window",
            Action = ShortcutKeyActions.PreviousEditWindow
        },
        new()
        {
            Modifiers = {KeyModifiers.Windows, KeyModifiers.Shift, KeyModifiers.Control},
            NonModifierKey = Key.G,
            IsEnabled = false,
            Name = "Edit last Grab Frame",
            Action = ShortcutKeyActions.PreviousGrabFrame
        },
    };
}

public enum ShortcutKeyActions
{
    None = 0,
    Settings = 1,
    Fullscreen = 2,
    GrabFrame = 3,
    Lookup = 4,
    EditWindow = 5,
    PreviousRegionGrab = 6,
    PreviousEditWindow = 7,
    PreviousGrabFrame = 8,
}
