namespace Text_Grab.Utilities;

/// <summary>
/// Selects values from an ordered match list according to a mode string
/// ("first", "last", "all", or 1-based indices like "2" / "1,3,5"). Shared by
/// <see cref="RecognizerExecutor"/>, <see cref="PatternExecutor"/>, and
/// <see cref="GrabTemplateExecutor"/>'s placeholder resolution.
/// </summary>
public static class MatchModeSelector
{
    public static string ExtractMatchesByMode(IReadOnlyList<string> allValues, string mode, string separator)
    {
        if (allValues.Count == 0)
            return string.Empty;

        return mode.ToLowerInvariant() switch
        {
            "first" => allValues[0],
            "last" => allValues[^1],
            "all" => string.Join(separator, allValues),
            _ => ExtractByIndices(allValues, mode, separator)
        };
    }

    private static string ExtractByIndices(IReadOnlyList<string> values, string mode, string separator)
    {
        // mode is either a single index like "2" or comma-separated like "1,3,5"
        string[] parts = mode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<string> selected = [];

        foreach (string part in parts)
        {
            if (int.TryParse(part, out int index) && index >= 1 && index <= values.Count)
                selected.Add(values[index - 1]); // convert 1-based to 0-based
        }

        return string.Join(separator, selected);
    }
}
