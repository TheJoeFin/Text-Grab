using System.Text.Json;
using System.Text.RegularExpressions;

namespace Text_Grab.Core.Editing;

/// <summary>
/// Guesses which <see cref="CodeLanguages"/> entry a block of text is written in, to pick syntax
/// highlighting when code mode is entered without a file extension to go on.
/// </summary>
/// <remarks>
/// A heuristic, not a parser. JSON, PHP, markup and diffs are recognized by their shape. Everything
/// else is scored: each language has a few weighted patterns, each pattern counts up to three matches,
/// and the highest score wins if it reaches <see cref="MinimumScore"/>. The patterns are deliberately
/// loose because the text is often OCR'd from a screenshot.
/// </remarks>
public static class CodeLanguageDetector
{
    /// <summary>The lowest score a language needs to be picked.</summary>
    public const double MinimumScore = 4;

    /// <summary>Only this much of the text is examined, so detection stays fast on large documents.</summary>
    public const int MaximumSampleLength = 20_000;

    private const int MaximumCountedMatchesPerPattern = 3;
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly IReadOnlyList<(string Language, IReadOnlyList<(Regex Pattern, double Weight)> Patterns)> ScoredLanguages =
    [
        (CodeLanguages.CSharp,
        [
            (Pattern(@"^\s*using\s+(static\s+)?System(\.[\w.]+)?\s*;"), 3),
            (Pattern(@"^\s*namespace\s+[\w.]+\s*[;{]?\s*$"), 2),
            (Pattern(@"\b(public|private|protected|internal)\s+((static|async|override|virtual|readonly|sealed|partial|abstract)\s+)*(class|record|struct|interface|enum|void|string|int|bool|Task)\b"), 2),
            (Pattern(@"\{\s*get;\s*((private\s+)?set|init);?\s*\}"), 3),
            (Pattern(@"\b(Console\.Write(Line)?|string\.(Empty|IsNullOrWhiteSpace|IsNullOrEmpty)|nameof\(|await\s+Task\.)"), 2),
            (Pattern(@"\bvar\s+\w+\s*=\s*new\b"), 1),
        ]),
        (CodeLanguages.Java,
        [
            (Pattern(@"^\s*import\s+(java|javax|android)\.[\w.*]+\s*;"), 3),
            (Pattern(@"^\s*package\s+[\w.]+\s*;"), 3),
            (Pattern(@"System\.(out|err)\.print(ln|f)?\s*\("), 3),
            (Pattern(@"public\s+static\s+void\s+main\s*\(\s*String"), 3),
            (Pattern(@"@Override\b"), 2),
            (Pattern(@"\b(extends|implements)\s+[A-Z]\w*"), 1),
        ]),
        (CodeLanguages.JavaScript,
        [
            (Pattern(@"\bfunction\s*\w*\s*\([^)]*\)\s*\{"), 2),
            (Pattern(@"\b(const|let)\s+[\w{}\[\], ]+\s*="), 2),
            (Pattern(@"\bconsole\.(log|error|warn)\s*\("), 3),
            (Pattern(@"\b(document|window)\.\w+"), 2),
            (Pattern(@"\brequire\s*\(\s*['""]|\bmodule\.exports\b|^\s*export\s+(default|const|function|class)\b|^\s*import\s+.+\s+from\s+['""]"), 3),
            (Pattern(@"[!=]=="), 2),
            (Pattern(@"=>\s*\{"), 1),
        ]),
        (CodeLanguages.Python,
        [
            (Pattern(@"^\s*def\s+\w+\s*\(.*\)\s*(->\s*[^:]+)?:\s*$"), 3),
            (Pattern(@"^\s*from\s+[\w.]+\s+import\s+"), 3),
            (Pattern(@"^\s*import\s+[\w.]+(\s+as\s+\w+)?\s*$"), 2),
            (Pattern(@"^\s*class\s+\w+(\([^)]*\))?:\s*$"), 3),
            (Pattern(@"^\s*(elif\b.*|else|try|except\b.*|finally)\s*:\s*$"), 3),
            (Pattern(@"\bself\.\w+"), 2),
            (Pattern(@"if\s+__name__\s*==\s*['""]__main__['""]"), 3),
            (Pattern(@"\bprint\s*\("), 1),
        ]),
        (CodeLanguages.PowerShell,
        [
            (Pattern(@"\b(Get|Set|New|Remove|Write|Invoke|Import|Export|Start|Stop|Test|Add|Out|Select|Where|ForEach)-[A-Z]\w+"), 3),
            (Pattern(@"\s-(eq|ne|gt|lt|ge|le|like|notlike|match|notmatch|contains|and|or|not)\s"), 2),
            (Pattern(@"\$(_\b|PSItem\b|env:\w+|true\b|false\b|null\b)"), 2),
            (Pattern(@"\bparam\s*\("), 2),
            (Pattern(@"\[CmdletBinding\(|\[Parameter\("), 3),
            (Pattern(@"^\s*\$\w+\s*="), 1),
        ]),
        (CodeLanguages.Php,
        [
            (Pattern(@"\$\w+->\w+"), 2),
            (Pattern(@"\bfunction\s+\w+\s*\(\s*\$"), 3),
            (Pattern(@"\becho\s+[$'""]"), 2),
        ]),
        (CodeLanguages.Sql,
        [
            (Pattern(@"\bSELECT\b[\s\S]{1,400}?\bFROM\b", RegexOptions.IgnoreCase), 3),
            (Pattern(@"\bINSERT\s+INTO\b", RegexOptions.IgnoreCase), 3),
            (Pattern(@"\bUPDATE\s+[\w.\[\]]+\s+SET\b", RegexOptions.IgnoreCase), 3),
            (Pattern(@"\b(CREATE|ALTER|DROP)\s+(TABLE|VIEW|PROCEDURE|INDEX|FUNCTION)\b", RegexOptions.IgnoreCase), 3),
            (Pattern(@"\b(INNER|LEFT|RIGHT|FULL|OUTER|CROSS)\s+JOIN\b", RegexOptions.IgnoreCase), 2),
            (Pattern(@"\b(GROUP|ORDER)\s+BY\b"), 2),
            (Pattern(@"\bWHERE\b"), 1),
        ]),
        (CodeLanguages.Cpp,
        [
            (Pattern(@"^\s*#include\s*[<""]"), 4),
            (Pattern(@"\bstd::\w+"), 3),
            (Pattern(@"\bint\s+main\s*\("), 2),
            (Pattern(@"\b(cout|cerr)\s*<<|\bcin\s*>>|\bprintf\s*\("), 2),
            (Pattern(@"\b(nullptr|template\s*<|typename)\b"), 2),
            (Pattern(@"^\s*#(define|ifdef|ifndef|endif|pragma)\b"), 2),
        ]),
        (CodeLanguages.Css,
        [
            (Pattern(@"^\s*[^{};\r\n]+\{\s*$"), 1),
            (Pattern(@"^\s*[a-z-]+\s*:\s*[^;{}\r\n]+;\s*$"), 2),
            (Pattern(@"@(media|import|font-face|keyframes)\b"), 3),
            (Pattern(@"\b\d+(\.\d+)?(px|em|rem|vh|vw)\b"), 1),
            (Pattern(@"#[0-9a-fA-F]{3}([0-9a-fA-F]{3})?\s*;"), 1),
        ]),
        (CodeLanguages.VisualBasic,
        [
            (Pattern(@"\bDim\s+\w+\s+As\b"), 3),
            (Pattern(@"\bEnd\s+(Sub|Function|If|Class|Module|Property)\b"), 3),
            (Pattern(@"\b(Public|Private|Friend)\s+(Shared\s+)?(Sub|Function)\b"), 3),
            (Pattern(@"^\s*Imports\s+[\w.]+\s*$"), 3),
        ]),
    ];

    private static readonly Regex HtmlTag = Pattern(@"<(!DOCTYPE\s+html|html|head|body|div|span|p|a\s|script|table|ul|li|img|br|form|input)\b", RegexOptions.IgnoreCase);
    private static readonly Regex PatchLine = Pattern(@"^(\+\+\+ |--- |@@ -\d)");

    /// <summary>
    /// The <see cref="CodeLanguage.Name"/> <paramref name="text"/> is most likely written in, or
    /// <c>null</c> when nothing scores high enough to be worth highlighting.
    /// </summary>
    public static string? Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        string sample = text.Length > MaximumSampleLength ? text[..MaximumSampleLength] : text;
        string trimmed = sample.TrimStart();

        try
        {
            if (trimmed.StartsWith("<?php", StringComparison.OrdinalIgnoreCase))
                return CodeLanguages.Php;

            if ((trimmed.StartsWith('{') || trimmed.StartsWith('[')) && IsJson(text))
                return CodeLanguages.Json;

            if (trimmed.StartsWith('<'))
                return HtmlTag.IsMatch(sample) ? CodeLanguages.Html : CodeLanguages.Xml;

            if (PatchLine.Matches(sample).Count >= 2)
                return CodeLanguages.Patch;

            string? best = null;
            double bestScore = 0;
            foreach ((string language, IReadOnlyList<(Regex Pattern, double Weight)> patterns) in ScoredLanguages)
            {
                double score = Score(sample, patterns);
                if (score > bestScore)
                {
                    best = language;
                    bestScore = score;
                }
            }

            return bestScore >= MinimumScore ? best : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private static double Score(string sample, IReadOnlyList<(Regex Pattern, double Weight)> patterns)
    {
        double score = 0;
        foreach ((Regex pattern, double weight) in patterns)
        {
            int count = 0;
            for (Match match = pattern.Match(sample); match.Success && count < MaximumCountedMatchesPerPattern; match = match.NextMatch())
                count++;

            score += count * weight;
        }

        return score;
    }

    private static bool IsJson(string text)
    {
        try
        {
            using JsonDocument _ = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Regex Pattern(string pattern, RegexOptions options = RegexOptions.None)
        => new(pattern, options | RegexOptions.Multiline | RegexOptions.CultureInvariant, PatternTimeout);
}
