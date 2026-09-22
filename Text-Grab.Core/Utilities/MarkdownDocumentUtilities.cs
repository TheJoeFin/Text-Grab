using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Text_Grab.Utilities;

public static partial class MarkdownDocumentUtilities
{
    private static readonly Regex LiveBlockTriggerRegex = LiveBlockTrigger();
    private static readonly Regex LiveInlinePromotionRegex = LiveInlinePromotion();
    private static readonly Regex MarkdownPatternRegex = MarkdownPattern();

    internal static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)  // Must be BEFORE UseAdvancedExtensions to override default
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .UseEmojiAndSmiley(enableSmileys: false)
        .Build();

    public static bool ShouldPromoteLiveBlock(string? lineTextBeforeSpace)
    {
        if (string.IsNullOrWhiteSpace(lineTextBeforeSpace))
            return false;

        return LiveBlockTriggerRegex.IsMatch(lineTextBeforeSpace);
    }

    public static bool LooksLikeMarkdown(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return MarkdownPatternRegex.IsMatch(text);
    }

    public static bool ShouldPromoteLiveMarkdown(string? paragraphText)
    {
        if (string.IsNullOrWhiteSpace(paragraphText))
            return false;

        return LiveInlinePromotionRegex.IsMatch(NormalizeDocumentText(paragraphText));
    }

    internal static int GetOrderedListStart(ListBlock listBlock)
    {
        return listBlock.IsOrdered
            && int.TryParse(listBlock.OrderedStart, out int startIndex)
            && startIndex > 0
                ? startIndex
                : 1;
    }

    internal static string GetCodeBlockText(LeafBlock block)
    {
        return NormalizeDocumentText(block.Lines.ToString());
    }

    internal static string EscapeMarkdownText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string escapedText = text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal);

        escapedText = Regex.Replace(escapedText, @"^(#{1,6}\s)", @"\$1", RegexOptions.Multiline);
        escapedText = Regex.Replace(escapedText, @"^(\s*>+)", @"\$1", RegexOptions.Multiline);
        escapedText = Regex.Replace(escapedText, @"^(\s*[-+]\s)", @"\$1", RegexOptions.Multiline);
        escapedText = Regex.Replace(escapedText, @"^(\s*\d+\.\s)", @"\$1", RegexOptions.Multiline);
        return escapedText;
    }

    internal static string EscapeLinkDestination(string destination)
    {
        return destination.Replace(")", "\\)", StringComparison.Ordinal);
    }

    internal static string ApplyQuotePrefix(string text, string quotePrefix)
    {
        if (string.IsNullOrEmpty(quotePrefix))
            return text;

        return string.Join(
            Environment.NewLine,
            NormalizeNewlines(text).Split('\n').Select(line => string.IsNullOrEmpty(line)
                ? quotePrefix.TrimEnd()
                : $"{quotePrefix}{line}"));
    }

    internal static string GetQuotePrefix(int quoteDepth)
    {
        if (quoteDepth <= 0)
            return string.Empty;

        StringBuilder builder = new();
        for (int i = 0; i < quoteDepth; i++)
            builder.Append("> ");

        return builder.ToString();
    }

    internal static string NormalizeDocumentText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return NormalizeNewlines(text).TrimEnd('\n');
    }

    internal static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>
    /// A code span's <see cref="CodeInline.Span"/> covers the whole backtick-delimited run (e.g.
    /// <c>`dotnet build`</c>), but <see cref="CodeInline.Content"/> is just the inner text. Assumes
    /// a symmetric fence (equal backtick count on both sides), which covers the vast majority of
    /// real-world code spans; degrades to the fenced span if that assumption doesn't hold.
    /// </summary>
    internal static int GetCodeSpanContentRawStart(CodeInline codeInline)
    {
        int totalLength = codeInline.Span.End - codeInline.Span.Start + 1;
        int contentLength = codeInline.Content.Length;
        int fenceLength = Math.Max(0, (totalLength - contentLength) / 2);
        return codeInline.Span.Start + fenceLength;
    }

    /// <summary>
    /// A <see cref="LiteralInline"/>'s <c>Span</c> is not always tight to its own <c>Content</c> —
    /// e.g. inside a pipe table cell, Markdig's reported span includes the cell's padding
    /// whitespace (<c>"| Alpha |"</c>'s content is <c>"Alpha"</c> but the span covers <c>" Alpha "</c>),
    /// while ordinary paragraph text elsewhere has no such padding and the span is already exact.
    /// Searches the reported span's own window for the literal content and returns its tight bounds;
    /// falls back to the untrimmed span if the content can't be found there (should not normally happen).
    /// </summary>
    internal static (int Start, int End) ResolveContentSpan(string source, string content, int spanStart, int spanEndExclusive)
    {
        if (string.IsNullOrEmpty(content) || spanStart < 0 || spanEndExclusive > source.Length || spanEndExclusive <= spanStart)
            return (spanStart, spanEndExclusive);

        int windowLength = spanEndExclusive - spanStart;
        if (content.Length > windowLength)
            return (spanStart, spanEndExclusive);

        int found = source.IndexOf(content, spanStart, windowLength, StringComparison.Ordinal);
        return found < 0 ? (spanStart, spanEndExclusive) : (found, found + content.Length);
    }

    internal static string GetSourceSlice(string source, MarkdownObject markdownObject)
    {
        if (markdownObject.Span.Start < 0
            || markdownObject.Span.End < markdownObject.Span.Start
            || markdownObject.Span.End >= source.Length)
            return string.Empty;

        return source.Substring(markdownObject.Span.Start, markdownObject.Span.End - markdownObject.Span.Start + 1);
    }

    [GeneratedRegex(@"^\s{0,3}(#{1,6}|>+|[-+*]|\d+[.)])$", RegexOptions.Compiled)]
    private static partial Regex LiveBlockTrigger();

    [GeneratedRegex(@"(^|\s)\[( |x|X)\](\s|$)|(\*\*|__)(?=\S).+?\4|(?<!\*)\*(?=\S).+?(?<=\S)\*|(?<!_)_(?=\S).+?(?<=\S)_|`[^`\r\n]+`|\[[^\]\r\n]+\]\([^)]+\)", RegexOptions.Compiled)]
    private static partial Regex LiveInlinePromotion();

    [GeneratedRegex(@"(^|\n)\s{0,3}(#{1,6}\s|>+\s|[-+*]\s|\d+[.)]\s|```|~~~|---\s*$|___\s*$|\*\*\*\s*$)|\[[^\]]+\]\([^)]+\)|!\[[^\]]*\]\([^)]+\)|(^|\n)\|.+\|\s*$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex MarkdownPattern();
}
