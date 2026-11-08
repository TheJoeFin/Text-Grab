using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace Text_Grab.Utilities;

public static partial class MarkdownFlowDocumentUtilities
{
    private static readonly Regex HtmlTokenRegex = new(
        "<!--.*?-->|</?[a-zA-Z][a-zA-Z0-9]*(?:\\s+[^<>\"']*|\"[^\"]*\"|'[^']*')*/?\\s*>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex HtmlAttributeRegex = new(
        "(?<name>[a-zA-Z][a-zA-Z0-9-]*)\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s>]+))",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly Regex HtmlTextRegex = new(@"\s+|[^\s]+", RegexOptions.NonBacktracking);

    private static readonly DependencyProperty HtmlOpeningProperty = DependencyProperty.RegisterAttached(
        "HtmlOpening", typeof(string), typeof(MarkdownFlowDocumentUtilities), new PropertyMetadata(string.Empty));

    private static readonly DependencyProperty HtmlClosingProperty = DependencyProperty.RegisterAttached(
        "HtmlClosing", typeof(string), typeof(MarkdownFlowDocumentUtilities), new PropertyMetadata(string.Empty));

    private sealed class HtmlNode(string name, string opening, int start)
    {
        public string Name { get; } = name;
        public string Opening { get; } = opening;
        public string Closing { get; set; } = string.Empty;
        public int Start { get; } = start;
        public int End { get; set; } = start + opening.Length;
        public List<HtmlNode> Children { get; } = [];
        public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsHtmlParagraph(string name) => name is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";

    private static bool TryParseHtml(string html, int rawStart, out List<HtmlNode> nodes)
    {
        HtmlNode root = new(string.Empty, string.Empty, rawStart);
        nodes = root.Children;
        Stack<HtmlNode> stack = new();
        stack.Push(root);
        int position = 0;
        foreach (Match token in HtmlTokenRegex.Matches(html))
        {
            if (token.Index > position)
                stack.Peek().Children.Add(new HtmlNode("text", html[position..token.Index], rawStart + position));
            position = token.Index + token.Length;
            string tag = token.Value;
            if (tag.StartsWith("<!--", StringComparison.Ordinal))
            {
                stack.Peek().Children.Add(new HtmlNode("comment", tag, rawStart + token.Index));
                continue;
            }

            bool closing = tag.StartsWith("</", StringComparison.Ordinal);
            int nameStart = closing ? 2 : 1;
            int nameEnd = nameStart;
            while (nameEnd < tag.Length && char.IsAsciiLetterOrDigit(tag[nameEnd]))
                nameEnd++;
            string name = tag[nameStart..nameEnd].ToLowerInvariant();
            if (!IsHtmlParagraph(name) && name is not ("a" or "img" or "br" or "span" or "b" or "strong" or "i" or "em"))
                return false;

            if (closing)
            {
                if (stack.Count == 1 || stack.Peek().Name != name)
                    return false;
                HtmlNode node = stack.Pop();
                node.Closing = tag;
                node.End = rawStart + position;
                continue;
            }

            // Block elements are intentionally limited to top-level paragraphs and headings.
            if (IsHtmlParagraph(name) && stack.Count != 1)
                return false;
            HtmlNode element = new(name, tag, rawStart + token.Index);
            foreach (Match attribute in HtmlAttributeRegex.Matches(tag[nameEnd..].TrimEnd('>', '/')))
                element.Attributes.TryAdd(attribute.Groups["name"].Value, WebUtility.HtmlDecode(attribute.Groups["value"].Value));
            stack.Peek().Children.Add(element);
            if (name is not ("img" or "br") && !tag.TrimEnd().EndsWith("/>", StringComparison.Ordinal))
            {
                if (stack.Count >= 64)
                    return false;
                stack.Push(element);
            }
        }

        if (position < html.Length)
            stack.Peek().Children.Add(new HtmlNode("text", html[position..], rawStart + position));
        return stack.Count == 1 && nodes.Exists(node => node.Name != "text");
    }

    private static bool TryAppendHtmlBlocks(BlockCollection blocks, HtmlBlock block, string source, int quoteDepth, string? markdownFilePath)
    {
        if (!TryParseHtml(MarkdownDocumentUtilities.GetSourceSlice(source, block), block.Span.Start, out List<HtmlNode> nodes))
            return false;

        Paragraph? inlineParagraph = null;
        foreach (HtmlNode node in nodes)
        {
            if (IsHtmlParagraph(node.Name))
            {
                inlineParagraph = null;
                Paragraph paragraph = new() { Margin = new Thickness(0, 4, 0, 4) };
                SetQuoteDepth(paragraph, quoteDepth);
                SetRawSpan(paragraph, node.Start, node.End);
                SetHtmlTags(paragraph, node);
                if (node.Name != "p")
                {
                    int level = node.Name[1] - '0';
                    SetHeadingLevel(paragraph, level);
                    paragraph.FontWeight = FontWeights.Bold;
                    paragraph.Margin = new Thickness(0, 10, 0, 4);
                }
                paragraph.TextAlignment = GetHtmlAttribute(node, "align").ToLowerInvariant() switch
                {
                    "center" => TextAlignment.Center,
                    "right" => TextAlignment.Right,
                    "justify" => TextAlignment.Justify,
                    _ => TextAlignment.Left
                };
                AppendHtmlNodes(paragraph.Inlines, node.Children, markdownFilePath);
                TrimHtmlWhitespace(paragraph.Inlines);
                blocks.Add(paragraph);
            }
            else
            {
                if (inlineParagraph is null && node.Name == "text" && string.IsNullOrWhiteSpace(node.Opening))
                    continue;
                if (inlineParagraph is null)
                {
                    inlineParagraph = new Paragraph { Margin = new Thickness(0, 4, 0, 4) };
                    SetQuoteDepth(inlineParagraph, quoteDepth);
                    SetRawSpan(inlineParagraph, node.Start, node.End);
                    blocks.Add(inlineParagraph);
                }
                SetRawSpan(inlineParagraph, GetRawSpanStart(inlineParagraph), node.End);
                AppendHtmlNodes(inlineParagraph.Inlines, [node], markdownFilePath);
            }
        }
        return true;
    }

    private static bool TryAppendHtmlInline(InlineCollection inlines, HtmlInline first, string source,
        string? markdownFilePath, out MarkdigInline last)
    {
        last = first;
        for (MarkdigInline? candidate = first; candidate is not null; candidate = candidate.NextSibling)
        {
            if (candidate is not HtmlInline)
                continue;
            int end = candidate.Span.End + 1;
            if (!TryParseHtml(source[first.Span.Start..end], first.Span.Start, out List<HtmlNode> nodes))
                continue;
            if (nodes.Exists(node => IsHtmlParagraph(node.Name)))
                return false;
            AppendHtmlNodes(inlines, nodes, markdownFilePath);
            last = candidate;
            return true;
        }
        return false;
    }

    private static void AppendHtmlNodes(InlineCollection inlines, List<HtmlNode> nodes, string? markdownFilePath)
    {
        foreach (HtmlNode node in nodes)
        {
            if (node.Name == "text")
            {
                foreach (Match text in HtmlTextRegex.Matches(node.Opening))
                {
                    string content = char.IsWhiteSpace(text.Value[0]) ? " " : WebUtility.HtmlDecode(text.Value);
                    if (content == " " && inlines.LastInline is Run { Text: " " })
                        continue;
                    Run run = new(content);
                    SetRawSpan(run, node.Start + text.Index, node.Start + text.Index + text.Length);
                    inlines.Add(run);
                }
                continue;
            }

            if (node.Name == "img")
            {
                inlines.Add(CreateImageInline(GetHtmlAttribute(node, "src"), GetHtmlAttribute(node, "alt"),
                    GetHtmlAttribute(node, "title"), node.Opening, node.Start, node.End, markdownFilePath,
                    GetHtmlDimension(node, "width"), GetHtmlDimension(node, "height")));
                continue;
            }

            if (node.Name == "br")
            {
                LineBreak lineBreak = new();
                SetHtmlTags(lineBreak, node);
                inlines.Add(lineBreak);
                continue;
            }

            Span span = node.Name switch
            {
                "a" => new Hyperlink(),
                "b" or "strong" => new Bold(),
                "i" or "em" => new Italic(),
                _ => new Span()
            };
            if (span is Hyperlink hyperlink && Uri.TryCreate(GetHtmlAttribute(node, "href"), UriKind.RelativeOrAbsolute, out Uri? uri))
                hyperlink.NavigateUri = uri;
            SetHtmlTags(span, node);
            AppendHtmlNodes(span.Inlines, node.Children, markdownFilePath);
            inlines.Add(span);
        }
    }

    private static void TrimHtmlWhitespace(InlineCollection inlines)
    {
        TrimHtmlEdge(inlines, leading: true);
        TrimHtmlEdge(inlines, leading: false);
    }

    private static void TrimHtmlEdge(InlineCollection inlines, bool leading)
    {
        while ((leading ? inlines.FirstInline : inlines.LastInline) is System.Windows.Documents.Inline edge)
        {
            if (edge is Run { Text: " " })
                inlines.Remove(edge);
            else if (edge is Span span)
            {
                TrimHtmlEdge(span.Inlines, leading);
                if (span.Inlines.Count > 0)
                    return;
                // Empty elements and comments still need to survive serialization.
                return;
            }
            else
                return;
        }
    }

    private static string GetHtmlAttribute(HtmlNode node, string name) => node.Attributes.GetValueOrDefault(name, string.Empty);

    private static double GetHtmlDimension(HtmlNode node, string name) =>
        double.TryParse(GetHtmlAttribute(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
        && double.IsFinite(value) && value > 0 ? value : double.NaN;

    private static void SetHtmlTags(TextElement element, HtmlNode node)
    {
        element.SetValue(HtmlOpeningProperty, node.Opening);
        element.SetValue(HtmlClosingProperty, node.Closing);
    }

    private static string SerializeHtmlInlines(InlineCollection inlines)
    {
        StringBuilder builder = new();
        foreach (System.Windows.Documents.Inline inline in inlines)
        {
            if (inline is Run run && GetInlineRole(run) != MarkdownInlineRole.LiteralMarkdown)
                builder.Append(WebUtility.HtmlEncode(run.Text));
            else if ((string)inline.GetValue(HtmlOpeningProperty) == string.Empty && inline is LineBreak)
                builder.Append("<br />");
            else if (inline is Span span && (string)span.GetValue(HtmlOpeningProperty) == string.Empty)
            {
                string content = SerializeHtmlInlines(span.Inlines);
                builder.Append(span switch
                {
                    Bold => "<strong>" + content + "</strong>",
                    Italic => "<em>" + content + "</em>",
                    Hyperlink hyperlink => "<a href=\"" + WebUtility.HtmlEncode(hyperlink.NavigateUri?.OriginalString ?? string.Empty) + "\">" + content + "</a>",
                    _ => content
                });
            }
            else
                WriteInline(builder, inline, preserveLiteralMarkdown: false);
        }
        return builder.ToString();
    }
}
