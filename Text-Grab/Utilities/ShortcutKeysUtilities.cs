using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

internal class ShortcutKeysUtilities
{
    public static void SaveShortcutKeySetSettings(IEnumerable<ShortcutKeySet> shortcutKeySets)
    {
        AppUtilities.TextGrabSettingsService.SaveShortcutKeySets(shortcutKeySets);
    }

    public static IEnumerable<ShortcutKeySet> GetShortcutKeySetsFromSettings()
    {
        List<ShortcutKeySet> defaultKeys = ShortcutKeySet.DefaultShortcutKeySets;
        List<ShortcutKeySet> shortcutKeySets = AppUtilities.TextGrabSettingsService.LoadShortcutKeySets();

        if (shortcutKeySets.Count == 0)
            return ParseFromPreviousAndDefaultsSettings();

        // return the list of custom bottom bar items
        List<ShortcutKeyActions> actionsList = shortcutKeySets.Select(x => x.Action).ToList();
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
