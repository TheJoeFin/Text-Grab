using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using Text_Grab.Models;
using Text_Grab.Properties;

namespace Text_Grab.Utilities;

internal class ShortcutKeysUtilities
{
    public static void SaveShortcutKeySetSettings(IEnumerable<ShortcutKeySet> shortcutKeySets)
    {
        string json = JsonSerializer.Serialize(shortcutKeySets);

        // save the json string to the settings
        AppUtilities.TextGrabSettings.ShortcutKeySets = json;

        // save the settings
        AppUtilities.TextGrabSettings.Save();
    }

    public static IEnumerable<ShortcutKeySet> GetShortcutKeySetsFromSettings()
    {
        string json = AppUtilities.TextGrabSettings.ShortcutKeySets;

        List<ShortcutKeySet> defaultKeys = ShortcutKeySet.DefaultShortcutKeySets;

        if (string.IsNullOrWhiteSpace(json))
            return ParseFromPreviousAndDefaultsSettings();

        // create a list of custom bottom bar items
        List<ShortcutKeySet>? shortcutKeySets = new();

        // deserialize the json string into a list of custom bottom bar items
        shortcutKeySets = JsonSerializer.Deserialize<List<ShortcutKeySet>>(json);

        // return the list of custom bottom bar items
        if (shortcutKeySets is null || shortcutKeySets.Count == 0)
            return defaultKeys;

        var actionsList = shortcutKeySets.Select(x => x.Action).ToList();
        return shortcutKeySets.Concat(defaultKeys.Where(x => !actionsList.Contains(x.Action)).ToList()).ToList();
    }

    public static IEnumerable<ShortcutKeySet> ParseFromPreviousAndDefaultsSettings()
    {
        string fsgKey = AppUtilities.TextGrabSettings.FullscreenGrabHotKey;

        if (string.IsNullOrWhiteSpace(fsgKey))
            return ShortcutKeySet.DefaultShortcutKeySets;

        string gfKey = AppUtilities.TextGrabSettings.GrabFrameHotkey;
        string etwKey = AppUtilities.TextGrabSettings.EditWindowHotKey;
        string qslKey = AppUtilities.TextGrabSettings.LookupHotKey;

        List<ShortcutKeySet> priorAndDefaultSettings = new();

        List<ShortcutKeyActions> standardActions = new()
        {
            ShortcutKeyActions.Fullscreen,
            ShortcutKeyActions.GrabFrame,
            ShortcutKeyActions.EditWindow,
            ShortcutKeyActions.Lookup,
        };

        foreach (ShortcutKeyActions action in standardActions)
        {
            string name = string.Empty;
            bool couldParse = false;
            Key parsedKey = Key.None;

            switch (action)
            {
                case ShortcutKeyActions.Fullscreen:
                    name = "Fullscreen Grab";
                    couldParse = Enum.TryParse(fsgKey, out parsedKey);
                    break;
                case ShortcutKeyActions.GrabFrame:
                    name = "Grab Frame";
                    couldParse = Enum.TryParse(gfKey, out parsedKey);
                    break;
                case ShortcutKeyActions.EditWindow:
                    name = "Edit Text Window";
                    couldParse = Enum.TryParse(etwKey, out parsedKey);
                    break;
                case ShortcutKeyActions.Lookup:
                    name = "Quick Simple Lookup";
                    couldParse = Enum.TryParse(qslKey, out parsedKey);
                    break;
            }

            if (!couldParse)
                continue;

            ShortcutKeySet newKeySet = new()
            {
                NonModifierKey = parsedKey,
                Modifiers = new HashSet<KeyModifiers>() { KeyModifiers.Shift, KeyModifiers.Windows },
                IsEnabled = true,
                Name = name,
                Action = action
            };

            priorAndDefaultSettings.Add(newKeySet);
        }

        return priorAndDefaultSettings
            .Concat(ShortcutKeySet.DefaultShortcutKeySets
                .Where(x => !standardActions
                    .Contains(x.Action)).ToList()).ToList();
    }
}
