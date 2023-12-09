using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Text_Grab.Utilities;

namespace Text_Grab.Models;

public class ShortcutKeySet : IEquatable<ShortcutKeySet>
{
    public HashSet<KeyModifiers> Modifiers { get; set; } = new();
    public Key NonModifierKey { get; set; } = Key.None;

    public ShortcutKeySet(HashSet<KeyModifiers> modifiers, Key key)
    {
        Modifiers = modifiers;
        NonModifierKey = key;
    }

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

        var splitUpString = shortcutsAsString.Split('+');
        string? keyString = splitUpString.LastOrDefault();

        if (Enum.TryParse(keyString, out Key parsedKey))
            NonModifierKey = parsedKey;
    }

    public override string ToString()
    {
        List<string> keyStrings = new();

        foreach (var key in Modifiers)
            keyStrings.Add(key.ToString());

        keyStrings.Add(NonModifierKey.ToString());

        return string.Join('+', keyStrings);
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

        if (string.Equals(other.ToString(), ToString(), StringComparison.InvariantCultureIgnoreCase))
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
            return hash;
        }
    }
}