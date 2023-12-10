using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Text_Grab.Models;
using Text_Grab.Properties;

namespace Text_Grab.Utilities;

internal class ShortcutKeysUtilities
{
    public static void SaveShortcutKeySetSettings(IEnumerable<ShortcutKeySet> shortcutKeySets)
    {
        // serialize the list of custom bottom bar items to json
        JsonSerializerOptions options = new JsonSerializerOptions()
        {
            
        };
        string json = JsonSerializer.Serialize(shortcutKeySets);

        // save the json string to the settings
        Settings.Default.ShortcutKeySets = json;

        // save the settings
        Settings.Default.Save();
    }

    public static IEnumerable<ShortcutKeySet> GetShortcutKeySetsFromSettings()
    {
        string json = Settings.Default.ShortcutKeySets;

        List<ShortcutKeySet> defaultKeys = ShortcutKeySet.DefaultShortcutKeySets;

        if (string.IsNullOrWhiteSpace(json))
            return defaultKeys;

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
}
