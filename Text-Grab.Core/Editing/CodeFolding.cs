namespace Text_Grab.Core.Editing;

/// <summary>
/// Which folding rule each <see cref="CodeLanguages"/> entry uses: brackets for C-family languages,
/// tags for markup, none for the rest (Python, SQL and Visual Basic would need indentation or keyword
/// rules).
/// </summary>
public static class CodeFolding
{
    private static readonly IReadOnlyDictionary<string, BraceFoldingSyntax> BraceLanguages =
        new Dictionary<string, BraceFoldingSyntax>(StringComparer.OrdinalIgnoreCase)
        {
            [CodeLanguages.CSharp] = BraceFoldingSyntax.CStyle with { VerbatimStrings = true, TripleQuotedStrings = true },
            [CodeLanguages.Java] = BraceFoldingSyntax.CStyle with { TripleQuotedStrings = true },
            [CodeLanguages.Cpp] = BraceFoldingSyntax.CStyle,
            [CodeLanguages.JavaScript] = BraceFoldingSyntax.CStyle with { Brackets = ["{}", "[]"], MultiLineQuotes = "`" },
            [CodeLanguages.Json] = BraceFoldingSyntax.CStyle with { Brackets = ["{}", "[]"], Quotes = "\"" },
            // No line comments: "//" in an unquoted url(...) would hide the rest of the line.
            [CodeLanguages.Css] = BraceFoldingSyntax.CStyle with { LineComments = [] },
            [CodeLanguages.Php] = BraceFoldingSyntax.CStyle with { LineComments = ["//", "#"] },
            [CodeLanguages.PowerShell] = BraceFoldingSyntax.CStyle with { LineComments = ["#"], BlockComments = [("<#", "#>")], EscapeCharacter = '`' },
        };

    /// <summary>True when <paramref name="languageName"/> has a folding rule.</summary>
    public static bool IsSupported(string? languageName)
        => languageName is not null
            && (BraceLanguages.ContainsKey(languageName) || IsMarkup(languageName));

    /// <summary>
    /// The foldable regions of <paramref name="text"/> in <paramref name="languageName"/>, sorted by
    /// start; empty when the language has no folding rule.
    /// </summary>
    public static IReadOnlyList<TextSpan> Find(string text, string? languageName)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (languageName is null)
            return [];

        if (BraceLanguages.TryGetValue(languageName, out BraceFoldingSyntax? syntax))
            return BraceFolding.Find(text, syntax);

        if (IsMarkup(languageName))
            return TagFolding.Find(text, isHtml: string.Equals(languageName, CodeLanguages.Html, StringComparison.OrdinalIgnoreCase));

        return [];
    }

    private static bool IsMarkup(string languageName)
        => string.Equals(languageName, CodeLanguages.Xml, StringComparison.OrdinalIgnoreCase)
            || string.Equals(languageName, CodeLanguages.Html, StringComparison.OrdinalIgnoreCase);
}
