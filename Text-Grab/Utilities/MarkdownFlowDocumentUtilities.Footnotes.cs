using Markdig.Extensions.Footnotes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;
using WpfList = System.Windows.Documents.List;

namespace Text_Grab.Utilities;

public static partial class MarkdownFlowDocumentUtilities
{
    private static WpfList CreateFootnoteList(FootnoteGroup footnotes, string source, string? markdownFilePath)
    {
        WpfList list = new()
        {
            MarkerStyle = TextMarkerStyle.Decimal,
            Margin = new Thickness(0, 16, 0, 4),
            Padding = new Thickness(24, 8, 0, 0),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        SetBlockRole(list, MarkdownBlockRole.FootnoteList);
        foreach (Footnote footnote in footnotes.OfType<Footnote>().OrderBy(note => note.Order))
        {
            ListItem item = new();
            item.SetValue(FootnoteLabelProperty, footnote.Label);
            foreach (Markdig.Syntax.Block child in footnote)
                AppendBlock(item.Blocks, child, source, quoteDepth: 0, markdownFilePath);
            if (item.Blocks.Count == 0)
                item.Blocks.Add(new Paragraph());
            list.ListItems.Add(item);
        }
        return list;
    }

    private static Hyperlink CreateFootnoteReference(FootnoteLink reference, string source)
    {
        Run number = new(reference.Footnote.Order.ToString(CultureInfo.InvariantCulture));
        SetRawSpan(number, reference.Span.Start, reference.Span.End + 1);
        Hyperlink link = new(number)
        {
            BaselineAlignment = BaselineAlignment.Superscript,
            ToolTip = $"Go to footnote {reference.Footnote.Order} ({reference.Footnote.Label})"
        };
        SetInlineRole(link, MarkdownInlineRole.FootnoteReference);
        link.SetValue(FootnoteLabelProperty, reference.Footnote.Label);
        link.SetValue(FootnoteMarkdownProperty, MarkdownDocumentUtilities.GetSourceSlice(source, reference));
        AutomationProperties.SetName(link, $"Footnote {reference.Footnote.Order}");
        return link;
    }

    private static void ConnectFootnotes(FlowDocument document)
    {
        ILookup<string, Hyperlink> references = GetFootnoteReferences(document)
            .ToLookup(link => (string)link.GetValue(FootnoteLabelProperty), StringComparer.OrdinalIgnoreCase);
        foreach (WpfList list in document.Blocks.OfType<WpfList>().Where(list => GetBlockRole(list) == MarkdownBlockRole.FootnoteList).ToArray())
        {
            foreach (ListItem item in list.ListItems.ToArray())
            {
                Hyperlink[] links = references[(string)item.GetValue(FootnoteLabelProperty)].ToArray();
                Paragraph? paragraph = item.Blocks.LastBlock as Paragraph;
                if (paragraph is null || GetBlockRole(paragraph) != MarkdownBlockRole.None ||
                    (string)paragraph.GetValue(HtmlOpeningProperty) != string.Empty)
                {
                    paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
                    SetBlockRole(paragraph, MarkdownBlockRole.FootnoteBacklinks);
                    item.Blocks.Add(paragraph);
                }

                for (int index = 0; index < links.Length; index++)
                {
                    Hyperlink reference = links[index];
                    reference.FontSize = document.FontSize * 0.75;
                    reference.Click += (_, args) => NavigateFootnote(item, args);
                    Hyperlink backLink = new(new Run(links.Length == 1 ? " \u21a9" : $" \u21a9{index + 1}"))
                    {
                        ToolTip = $"Return to footnote reference {index + 1}"
                    };
                    SetInlineRole(backLink, MarkdownInlineRole.FootnoteBacklink);
                    AutomationProperties.SetName(backLink, backLink.ToolTip.ToString());
                    backLink.Click += (_, args) => NavigateFootnote(reference, args);
                    paragraph.Inlines.Add(backLink);
                }
            }
        }
    }

    private static IEnumerable<Hyperlink> GetFootnoteReferences(FlowDocument document)
    {
        for (TextPointer? position = document.ContentStart; position is not null;
            position = position.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart &&
                position.GetAdjacentElement(LogicalDirection.Forward) is Hyperlink link &&
                GetInlineRole(link) == MarkdownInlineRole.FootnoteReference)
            {
                yield return link;
            }
        }
    }

    private static void NavigateFootnote(FrameworkContentElement target, RoutedEventArgs args)
    {
        args.Handled = true;
        target.BringIntoView();
    }

    private static void WriteFootnotes(StringBuilder builder, WpfList list, bool preserveLiteralMarkdown)
    {
        bool wroteNote = false;
        foreach (ListItem item in list.ListItems)
        {
            if (wroteNote)
                builder.Append($"{Environment.NewLine}{Environment.NewLine}");
            wroteNote = true;

            StringBuilder body = new();
            bool wroteBlock = false;
            foreach (Block block in item.Blocks)
            {
                if (GetBlockRole(block) == MarkdownBlockRole.FootnoteBacklinks)
                    continue;
                if (wroteBlock)
                    body.Append($"{Environment.NewLine}{Environment.NewLine}");
                WriteBlock(body, block, listDepth: 0, preserveLiteralMarkdown);
                wroteBlock = true;
            }

            string[] lines = MarkdownDocumentUtilities.NormalizeNewlines(body.ToString()).Split('\n');
            builder.Append($"[{item.GetValue(FootnoteLabelProperty)}]: {lines[0]}");
            foreach (string line in lines.Skip(1))
            {
                builder.AppendLine();
                builder.Append("    ");
                builder.Append(line);
            }
        }
    }

    private static bool TryWriteFootnoteInline(StringBuilder builder, Inline inline)
    {
        if (GetInlineRole(inline) == MarkdownInlineRole.FootnoteReference)
        {
            builder.Append((string)inline.GetValue(FootnoteMarkdownProperty));
            return true;
        }
        return GetInlineRole(inline) == MarkdownInlineRole.FootnoteBacklink;
    }

    private static readonly DependencyProperty FootnoteLabelProperty =
        DependencyProperty.RegisterAttached("FootnoteLabel", typeof(string), typeof(MarkdownFlowDocumentUtilities), new PropertyMetadata(string.Empty));

    private static readonly DependencyProperty FootnoteMarkdownProperty =
        DependencyProperty.RegisterAttached("FootnoteMarkdown", typeof(string), typeof(MarkdownFlowDocumentUtilities), new PropertyMetadata(string.Empty));
}
