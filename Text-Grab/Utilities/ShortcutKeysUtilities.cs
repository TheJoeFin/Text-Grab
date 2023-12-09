using System.Collections.Generic;
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

        if (string.IsNullOrWhiteSpace(json))
            return ShortcutKeySet.DefaultShortcutKeySets;

        // create a list of custom bottom bar items
        List<ShortcutKeySet>? shortcutKeySets = new();

        // deserialize the json string into a list of custom bottom bar items
        shortcutKeySets = JsonSerializer.Deserialize<List<ShortcutKeySet>>(json);

        // return the list of custom bottom bar items
        if (shortcutKeySets is null || shortcutKeySets.Count == 0)
            return ShortcutKeySet.DefaultShortcutKeySets;

        return shortcutKeySets;
    }
}
