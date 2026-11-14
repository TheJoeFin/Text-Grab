namespace Text_Grab.Core.Editing;

/// <summary>
/// A language the Edit Text Window's code editor can highlight.
/// </summary>
/// <param name="Name">
/// The highlighting definition's name in the code editor (AvalonEdit's built-in names), and the value
/// stored in history. Not shown to people; use <paramref name="DisplayName"/>.
/// </param>
/// <param name="DisplayName">The name shown in menus and the status bar.</param>
/// <param name="Extensions">
/// File extensions, lower case with the dot, that open in code mode with this language. The first is
/// the default when saving a document that has no file yet.
/// </param>
public sealed record CodeLanguage(string Name, string DisplayName, IReadOnlyList<string> Extensions)
{
    public string DefaultExtension => Extensions.Count > 0 ? Extensions[0] : ".txt";
}

/// <summary>
/// The languages offered in code mode, in menu order.
/// </summary>
/// <remarks>
/// Pure data, so lower tiers and tests can use it. The app checks each <see cref="CodeLanguage.Name"/>
/// against the editor's highlighting definitions.
/// </remarks>
public static class CodeLanguages
{
    public const string PlainTextDisplayName = "Plain Text";

    public const string CSharp = "C#";
    public const string Cpp = "C++";
    public const string Css = "CSS";
    public const string Html = "HTML";
    public const string Java = "Java";
    public const string JavaScript = "JavaScript";
    public const string Json = "Json";
    public const string Markdown = "MarkDown";
    public const string Patch = "Patch";
    public const string Php = "PHP";
    public const string PowerShell = "PowerShell";
    public const string Python = "Python";
    public const string Sql = "TSQL";
    public const string TeX = "TeX";
    public const string VisualBasic = "VB";
    public const string Xml = "XML";

    public static IReadOnlyList<CodeLanguage> All { get; } =
    [
        new(CSharp, "C#", [".cs", ".csx"]),
        new(Cpp, "C / C++", [".cpp", ".c", ".h", ".cc", ".cxx", ".hpp", ".hxx"]),
        new(Css, "CSS", [".css", ".scss", ".less"]),
        new(Html, "HTML", [".html", ".htm"]),
        new(Java, "Java", [".java"]),
        new(JavaScript, "JavaScript", [".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx"]),
        new(Json, "JSON", [".json", ".jsonc"]),
        // .md opens in Markdown mode; this entry is for highlighting Markdown source in code mode.
        new(Markdown, "Markdown", [".md"]),
        new(Patch, "Diff / Patch", [".patch", ".diff"]),
        new(Php, "PHP", [".php"]),
        new(PowerShell, "PowerShell", [".ps1", ".psm1", ".psd1"]),
        new(Python, "Python", [".py", ".pyw"]),
        new(Sql, "SQL", [".sql"]),
        new(TeX, "TeX", [".tex"]),
        new(VisualBasic, "Visual Basic", [".vb"]),
        new(Xml, "XML", [".xml", ".xaml", ".xsd", ".xsl", ".xslt", ".config", ".csproj", ".vbproj", ".props", ".targets", ".nuspec", ".manifest", ".resx"]),
    ];

    /// <summary>The language with this <see cref="CodeLanguage.Name"/>, ignoring case.</summary>
    public static CodeLanguage? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return All.FirstOrDefault(language => string.Equals(language.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The language that opens files with <paramref name="extension"/> (with or without the dot).</summary>
    public static CodeLanguage? FindByExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        string normalized = extension.StartsWith('.') ? extension : "." + extension;
        return All.FirstOrDefault(language =>
            language.Extensions.Any(candidate => string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The display name for a language name; <see cref="PlainTextDisplayName"/> for none.</summary>
    public static string GetDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return PlainTextDisplayName;

        return Find(name)?.DisplayName ?? name;
    }
}
