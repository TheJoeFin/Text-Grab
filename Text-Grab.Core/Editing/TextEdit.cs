namespace Text_Grab.Core.Editing;

/// <summary>
/// One replacement that turns one text into another: remove <see cref="Removed"/>, then insert
/// <see cref="Inserted"/> at its start.
/// </summary>
public readonly record struct TextEdit(TextSpan Removed, string Inserted)
{
    public bool IsEmpty => Removed.Length == 0 && Inserted.Length == 0;

    /// <summary>
    /// The smallest single replacement that turns <paramref name="oldText"/> into
    /// <paramref name="newText"/>: everything between their common prefix and common suffix.
    /// </summary>
    /// <remarks>
    /// Pushing only this into an editor, rather than the whole text, keeps its caret, scroll position
    /// and undo history meaningful. The prefix and suffix never overlap, so the result is exact.
    /// </remarks>
    public static TextEdit Between(string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        int maxPrefix = Math.Min(oldText.Length, newText.Length);
        int prefix = 0;
        while (prefix < maxPrefix && oldText[prefix] == newText[prefix])
            prefix++;

        int maxSuffix = maxPrefix - prefix;
        int suffix = 0;
        while (suffix < maxSuffix && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
            suffix++;

        return new TextEdit(
            new TextSpan(prefix, oldText.Length - prefix - suffix),
            newText.Substring(prefix, newText.Length - prefix - suffix));
    }

    /// <summary>Applies this edit to <paramref name="text"/>.</summary>
    public string ApplyTo(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Concat(text.AsSpan(0, Removed.Start), Inserted, text.AsSpan(Removed.End));
    }
}
