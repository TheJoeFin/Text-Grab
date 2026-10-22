using System;

namespace Text_Grab.Utilities;

/// <summary>
/// Helpers for deciding what to do with the text a Local AI task hands back.
/// </summary>
public static class LocalAiResultUtilities
{
    /// <summary>
    /// Whether a Local AI result is the same text the task was given, so there is nothing new to
    /// show. Line endings and leading/trailing whitespace are ignored because the model routinely
    /// returns <c>\n</c> where the editor had <c>\r\n</c>, or adds a trailing newline, and neither
    /// counts as a real change to the user.
    /// </summary>
    public static bool IsUnchanged(string sourceText, string resultText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(resultText);

        return string.Equals(Normalize(sourceText), Normalize(resultText), StringComparison.Ordinal);
    }

    private static string Normalize(string text) => text.ReplaceLineEndings("\n").Trim();
}
