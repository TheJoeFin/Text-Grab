using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Text_Grab.Properties;

namespace Text_Grab.Services;

internal static class BottomBarFileTypePreferences
{
    internal static string KeyFor(string? path)
        => string.IsNullOrWhiteSpace(path) ? "empty" : "file:" + Path.GetExtension(path).ToLowerInvariant();

    internal static bool IsHidden(Settings settings, string? path)
    {
        return TryRead(settings.EditWindowBottomBarHiddenByFileType, out Dictionary<string, bool> preferences)
            && preferences.TryGetValue(KeyFor(path), out bool hidden)
                ? hidden
                : settings.EditWindowBottomBarIsHidden;
    }

    internal static bool SetHidden(Settings settings, string? path, bool hidden)
    {
        if (!TryRead(settings.EditWindowBottomBarHiddenByFileType, out Dictionary<string, bool> preferences))
            return false;

        preferences[KeyFor(path)] = hidden;
        settings.EditWindowBottomBarHiddenByFileType = JsonSerializer.Serialize(preferences);
        return true;
    }

    private static bool TryRead(string json, out Dictionary<string, bool> preferences)
    {
        preferences = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            Dictionary<string, bool>? stored = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            if (stored is null)
                throw new JsonException("The bottom bar preferences are null.");

            preferences = new Dictionary<string, bool>(stored, StringComparer.OrdinalIgnoreCase);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            Debug.WriteLine($"Could not read bottom bar preferences: {ex.Message}");
            return false;
        }
    }
}
