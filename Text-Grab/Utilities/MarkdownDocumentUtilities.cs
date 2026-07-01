using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MarkdigBlock = Markdig.Syntax.Block;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;
using MarkdigTable = Markdig.Extensions.Tables.Table;
using MarkdigTableCell = Markdig.Extensions.Tables.TableCell;
using MarkdigTableRow = Markdig.Extensions.Tables.TableRow;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;
using WpfList = System.Windows.Documents.List;
using WpfTable = System.Windows.Documents.Table;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableRow = System.Windows.Documents.TableRow;

namespace Text_Grab.Utilities;

public static partial class MarkdownDocumentUtilities
{
    private static readonly Regex LiveBlockTriggerRegex = LiveBlockTrigger();
    private static readonly Regex LiveInlinePromotionRegex = LiveInlinePromotion();
    private static readonly Regex MarkdownPatternRegex = MarkdownPattern();

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private enum MarkdownBlockRole
    {
        None,
        CodeBlock,
        ThematicBreak
    }

    private enum MarkdownInlineRole
    {
        None,
        CodeSpan,
        LiteralMarkdown,
        TaskListMarker
    }

    private sealed record MarkdownTheme(
        Brush ForegroundBrush,
        Brush BorderBrush,
        Brush AccentBrush,
        Brush QuoteBrush,
        Brush TableHeaderBrush,
        Brush CodeBackgroundBrush,
        FontFamily BaseFontFamily,
        FontFamily CodeFontFamily,
        double BaseFontSize);

    public static FlowDocument CreateFlowDocument(string? markdownText, FontFamily fontFamily, double fontSize)
    {
        string safeMarkdown = markdownText ?? string.Empty;
        FlowDocument document = new()
        {
            FontFamily = fontFamily,
            FontSize = fontSize,
            PagePadding = new Thickness(0)
        };

        MarkdownDocument markdownDocument = Markdown.Parse(safeMarkdown, MarkdownPipeline);
        foreach (MarkdigBlock block in markdownDocument)
            AppendBlock(document.Blocks, block, safeMarkdown, quoteDepth: 0);

        if (document.Blocks.Count == 0)
            document.Blocks.Add(new Paragraph());

        return document;
    }

    public static string SerializeToMarkdown(FlowDocument document, bool preserveLiteralMarkdown = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        StringBuilder builder = new();
        bool wroteBlock = false;
        foreach (WpfBlock block in document.Blocks)
        {
            if (wroteBlock)
                builder.Append($"{Environment.NewLine}{Environment.NewLine}");

            WriteBlock(builder, block, listDepth: 0, preserveLiteralMarkdown);
            wroteBlock = true;
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    public static string GetDocumentPlainText(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return NormalizeDocumentText(new TextRange(document.ContentStart, document.ContentEnd).Text);
    }

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

    public static void ApplyTheme(FlowDocument document, FrameworkElement resourceHost, bool isLightTheme)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(resourceHost);

        MarkdownTheme theme = CreateTheme(resourceHost, isLightTheme, document.FontFamily, document.FontSize);
        document.Foreground = theme.ForegroundBrush;
        document.FontFamily = theme.BaseFontFamily;
        document.FontSize = theme.BaseFontSize;
        document.PagePadding = new Thickness(0);

        foreach (WpfBlock block in document.Blocks)
            ApplyBlockTheme(block, theme);
    }

    private static void AppendBlock(BlockCollection blocks, MarkdigBlock block, string source, int quoteDepth)
    {
        switch (block)
        {
            case HeadingBlock headingBlock:
                Paragraph headingParagraph = new()
                {
                    Margin = new Thickness(0, 10, 0, 4),
                    FontWeight = FontWeights.Bold
                };
                SetHeadingLevel(headingParagraph, Math.Clamp(headingBlock.Level, 1, 6));
                SetQuoteDepth(headingParagraph, quoteDepth);
                AppendInlineContainer(headingParagraph.Inlines, headingBlock.Inline, source);
                blocks.Add(headingParagraph);
                break;

            case ParagraphBlock paragraphBlock:
                Paragraph paragraph = new()
                {
                    Margin = new Thickness(0, 4, 0, 4)
                };
                SetQuoteDepth(paragraph, quoteDepth);
                AppendInlineContainer(paragraph.Inlines, paragraphBlock.Inline, source);
                blocks.Add(paragraph);
                break;

            case QuoteBlock quoteBlock:
                foreach (MarkdigBlock child in quoteBlock)
                    AppendBlock(blocks, child, source, quoteDepth + 1);
                break;

            case ListBlock listBlock:
                WpfList list = new()
                {
                    MarkerStyle = listBlock.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                    Margin = new Thickness(0, 4, 0, 4)
                };
                SetQuoteDepth(list, quoteDepth);

                foreach (ListItemBlock itemBlock in listBlock.OfType<ListItemBlock>())
                {
                    ListItem listItem = new();
                    foreach (MarkdigBlock child in itemBlock)
                        AppendBlock(listItem.Blocks, child, source, quoteDepth: 0);

                    if (listItem.Blocks.Count == 0)
                        listItem.Blocks.Add(new Paragraph());

                    list.ListItems.Add(listItem);
                }

                blocks.Add(list);
                break;

            case FencedCodeBlock fencedCodeBlock:
                blocks.Add(CreateCodeParagraph(GetCodeBlockText(fencedCodeBlock), fencedCodeBlock.Info, quoteDepth));
                break;

            case CodeBlock codeBlock:
                blocks.Add(CreateCodeParagraph(GetCodeBlockText(codeBlock), info: null, quoteDepth));
                break;

            case ThematicBreakBlock:
                Paragraph breakParagraph = new()
                {
                    Margin = new Thickness(0, 8, 0, 8)
                };
                SetBlockRole(breakParagraph, MarkdownBlockRole.ThematicBreak);
                SetQuoteDepth(breakParagraph, quoteDepth);
                breakParagraph.Inlines.Add(new Run("----------"));
                blocks.Add(breakParagraph);
                break;

            case MarkdigTable table:
                blocks.Add(CreateTable(table, source, quoteDepth));
                break;

            default:
                blocks.Add(CreateLiteralParagraph(GetSourceSlice(source, block), quoteDepth));
                break;
        }
    }

    private static Paragraph CreateCodeParagraph(string codeText, string? info, int quoteDepth)
    {
        Paragraph paragraph = new()
        {
            Margin = new Thickness(0, 6, 0, 6)
        };

        SetBlockRole(paragraph, MarkdownBlockRole.CodeBlock);
        SetQuoteDepth(paragraph, quoteDepth);
        SetCodeFenceInfo(paragraph, info?.ToString() ?? string.Empty);
        paragraph.Inlines.Add(new Run(codeText));
        return paragraph;
    }

    private static Paragraph CreateLiteralParagraph(string literalMarkdown, int quoteDepth)
    {
        Paragraph paragraph = new()
        {
            Margin = new Thickness(0, 4, 0, 4)
        };

        SetQuoteDepth(paragraph, quoteDepth);
        Run literalRun = new(literalMarkdown);
        SetInlineRole(literalRun, MarkdownInlineRole.LiteralMarkdown);
        paragraph.Inlines.Add(literalRun);
        return paragraph;
    }

    private static WpfTable CreateTable(MarkdigTable table, string source, int quoteDepth)
    {
        WpfTable flowTable = new()
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 6, 0, 6)
        };

        SetQuoteDepth(flowTable, quoteDepth);

        int maxColumnCount = table.OfType<MarkdigTableRow>().Select(row => row.Count).DefaultIfEmpty(0).Max();
        for (int columnIndex = 0; columnIndex < maxColumnCount; columnIndex++)
            flowTable.Columns.Add(new TableColumn());

        TableRowGroup rowGroup = new();
        flowTable.RowGroups.Add(rowGroup);

        foreach (MarkdigTableRow row in table.OfType<MarkdigTableRow>())
        {
            WpfTableRow flowRow = new();
            rowGroup.Rows.Add(flowRow);

            foreach (MarkdigTableCell cell in row.OfType<MarkdigTableCell>())
            {
                WpfTableCell flowCell = new()
                {
                    Padding = new Thickness(6, 4, 6, 4)
                };
                SetIsTableHeader(flowCell, row.IsHeader);

                foreach (MarkdigBlock child in cell)
                    AppendBlock(flowCell.Blocks, child, source, quoteDepth: 0);

                if (flowCell.Blocks.Count == 0)
                    flowCell.Blocks.Add(new Paragraph());

                flowRow.Cells.Add(flowCell);
            }
        }

        return flowTable;
    }

    private static void AppendInlineContainer(InlineCollection inlines, ContainerInline? container, string source)
    {
        if (container is null)
            return;

        for (MarkdigInline? inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
            AppendInline(inlines, inline, source);
    }

    private static void AppendInline(InlineCollection inlines, MarkdigInline inline, string source)
    {
        switch (inline)
        {
            case LiteralInline literalInline:
                inlines.Add(new Run(literalInline.Content.ToString()));
                break;

            case LineBreakInline:
                inlines.Add(new LineBreak());
                break;

            case CodeInline codeInline:
                Run codeRun = new(codeInline.Content)
                {
                    FontFamily = new FontFamily("Consolas")
                };
                SetInlineRole(codeRun, MarkdownInlineRole.CodeSpan);
                inlines.Add(codeRun);
                break;

            case TaskList taskList:
                Run taskListRun = new(taskList.Checked ? "\u2611" : "\u2610");
                SetInlineRole(taskListRun, MarkdownInlineRole.TaskListMarker);
                SetTaskListMarkerChecked(taskListRun, taskList.Checked);
                inlines.Add(taskListRun);
                break;

            case EmphasisInline emphasisInline:
                Span emphasisSpan = emphasisInline.DelimiterCount >= 2
                    ? new Bold()
                    : new Italic();

                AppendInlineContainer(emphasisSpan.Inlines, emphasisInline, source);
                if (emphasisInline.DelimiterCount >= 3)
                    inlines.Add(new Italic(emphasisSpan));
                else
                    inlines.Add(emphasisSpan);
                break;

            case LinkInline linkInline when !linkInline.IsImage:
                Hyperlink hyperlink = new();
                string? linkUrl = linkInline.GetDynamicUrl != null ? linkInline.GetDynamicUrl() : linkInline.Url;
                if (!string.IsNullOrWhiteSpace(linkUrl) &&
                    Uri.TryCreate(linkUrl, UriKind.RelativeOrAbsolute, out Uri? navigateUri))
                {
                    hyperlink.NavigateUri = navigateUri;
                }

                AppendInlineContainer(hyperlink.Inlines, linkInline, source);
                if (hyperlink.Inlines.FirstInline is null)
                    hyperlink.Inlines.Add(new Run(linkInline.Url ?? string.Empty));

                inlines.Add(hyperlink);
                break;

            case LinkInline linkInline:
                Run literalImageRun = new(GetSourceSlice(source, linkInline));
                SetInlineRole(literalImageRun, MarkdownInlineRole.LiteralMarkdown);
                inlines.Add(literalImageRun);
                break;

            case HtmlInline htmlInline:
                Run htmlRun = new(htmlInline.Tag);
                SetInlineRole(htmlRun, MarkdownInlineRole.LiteralMarkdown);
                inlines.Add(htmlRun);
                break;

            case ContainerInline containerInline:
                Span containerSpan = new();
                AppendInlineContainer(containerSpan.Inlines, containerInline, source);
                inlines.Add(containerSpan);
                break;

            default:
                Run literalRun = new(GetSourceSlice(source, inline));
                SetInlineRole(literalRun, MarkdownInlineRole.LiteralMarkdown);
                inlines.Add(literalRun);
                break;
        }
    }

    private static void WriteBlock(StringBuilder builder, WpfBlock block, int listDepth, bool preserveLiteralMarkdown)
    {
        switch (block)
        {
            case Paragraph paragraph:
                WriteParagraph(builder, paragraph, preserveLiteralMarkdown);
                break;

            case WpfList list:
                WriteList(builder, list, listDepth, preserveLiteralMarkdown);
                break;

            case WpfTable table:
                WriteTable(builder, table);
                break;

            default:
                builder.Append(SerializeLiteralText(block, preserveLiteralMarkdown));
                break;
        }
    }

    private static void WriteParagraph(StringBuilder builder, Paragraph paragraph, bool preserveLiteralMarkdown)
    {
        string quotePrefix = GetQuotePrefix(GetQuoteDepth(paragraph));

        if (GetBlockRole(paragraph) == MarkdownBlockRole.ThematicBreak)
        {
            builder.Append(ApplyQuotePrefix("---", quotePrefix));
            return;
        }

        if (GetBlockRole(paragraph) == MarkdownBlockRole.CodeBlock)
        {
            string codeInfo = GetCodeFenceInfo(paragraph);
            string codeText = NormalizeDocumentText(new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
            string fencedBlock = string.IsNullOrWhiteSpace(codeInfo)
                ? $"```{Environment.NewLine}{codeText}{Environment.NewLine}```"
                : $"```{codeInfo}{Environment.NewLine}{codeText}{Environment.NewLine}```";
            builder.Append(ApplyQuotePrefix(fencedBlock, quotePrefix));
            return;
        }

        string content = SerializeInlines(paragraph.Inlines, preserveLiteralMarkdown);
        int headingLevel = GetHeadingLevel(paragraph);
        if (headingLevel > 0)
            content = $"{new string('#', headingLevel)} {content}";

        builder.Append(ApplyQuotePrefix(content, quotePrefix));
    }

    private static void WriteList(StringBuilder builder, WpfList list, int listDepth, bool preserveLiteralMarkdown)
    {
        string quotePrefix = GetQuotePrefix(GetQuoteDepth(list));
        bool isOrdered = list.MarkerStyle == TextMarkerStyle.Decimal;
        int itemIndex = 1;

        foreach (ListItem item in list.ListItems)
        {
            if (itemIndex > 1)
                builder.AppendLine();

            StringBuilder itemBuilder = new();
            bool wroteItemBlock = false;
            foreach (WpfBlock block in item.Blocks)
            {
                if (wroteItemBlock)
                    itemBuilder.Append($"{Environment.NewLine}{Environment.NewLine}");

                WriteBlock(itemBuilder, block, listDepth + 1, preserveLiteralMarkdown);
                wroteItemBlock = true;
            }

            string[] itemLines = NormalizeNewlines(itemBuilder.ToString()).Split('\n');
            string indent = new(' ', listDepth * 2);
            string marker = isOrdered ? $"{itemIndex}. " : "- ";

            builder.Append(ApplyQuotePrefix($"{indent}{marker}{itemLines[0]}", quotePrefix));
            string continuationIndent = $"{indent}{new string(' ', marker.Length)}";
            for (int lineIndex = 1; lineIndex < itemLines.Length; lineIndex++)
            {
                builder.AppendLine();
                builder.Append(ApplyQuotePrefix($"{continuationIndent}{itemLines[lineIndex]}", quotePrefix));
            }

            itemIndex++;
        }
    }

    private static void WriteTable(StringBuilder builder, WpfTable table)
    {
        string quotePrefix = GetQuotePrefix(GetQuoteDepth(table));
        TableRowGroup? firstGroup = table.RowGroups.FirstOrDefault();
        if (firstGroup is null || firstGroup.Rows.Count == 0)
            return;

        List<WpfTableRow> rows = [.. firstGroup.Rows.Cast<WpfTableRow>()];
        List<string> headerCells = [.. rows[0].Cells.Cast<WpfTableCell>().Select(SerializeTableCell)];

        builder.Append(ApplyQuotePrefix($"| {string.Join(" | ", headerCells)} |", quotePrefix));
        builder.AppendLine();
        builder.Append(ApplyQuotePrefix($"| {string.Join(" | ", Enumerable.Repeat("---", Math.Max(1, headerCells.Count)))} |", quotePrefix));

        IEnumerable<WpfTableRow> dataRows = rows.Count > 1 && rows[0].Cells.Cast<WpfTableCell>().Any(GetIsTableHeader)
            ? rows.Skip(1)
            : rows;

        foreach (WpfTableRow row in dataRows)
        {
            builder.AppendLine();
            List<string> rowCells = [.. row.Cells.Cast<WpfTableCell>().Select(SerializeTableCell)];
            builder.Append(ApplyQuotePrefix($"| {string.Join(" | ", rowCells)} |", quotePrefix));
        }
    }

    private static string SerializeTableCell(WpfTableCell cell)
    {
        string rawText = NormalizeDocumentText(new TextRange(cell.ContentStart, cell.ContentEnd).Text);
        return rawText
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal);
    }

    private static string SerializeInlines(InlineCollection inlines, bool preserveLiteralMarkdown)
    {
        StringBuilder builder = new();
        foreach (WpfInline inline in inlines)
            WriteInline(builder, inline, preserveLiteralMarkdown);

        return builder.ToString();
    }

    private static void WriteInline(StringBuilder builder, WpfInline inline, bool preserveLiteralMarkdown)
    {
        switch (inline)
        {
            case LineBreak:
                builder.Append($"  {Environment.NewLine}");
                break;

            case Run run:
                builder.Append(GetInlineRole(run) switch
                {
                    MarkdownInlineRole.TaskListMarker => GetTaskListMarkerChecked(run) ? "[x]" : "[ ]",
                    MarkdownInlineRole.CodeSpan => $"`{NormalizeDocumentText(run.Text)}`",
                    MarkdownInlineRole.LiteralMarkdown => run.Text,
                    _ when preserveLiteralMarkdown => run.Text,
                    _ => EscapeMarkdownText(run.Text)
                });
                break;

            case Hyperlink hyperlink:
                string linkText = SerializeInlines(hyperlink.Inlines, preserveLiteralMarkdown);
                string linkTarget = hyperlink.NavigateUri?.OriginalString ?? linkText;
                builder.Append($"[{linkText}]({EscapeLinkDestination(linkTarget)})");
                break;

            case Bold bold:
                builder.Append("**");
                builder.Append(SerializeInlines(bold.Inlines, preserveLiteralMarkdown));
                builder.Append("**");
                break;

            case Italic italic:
                builder.Append('*');
                builder.Append(SerializeInlines(italic.Inlines, preserveLiteralMarkdown));
                builder.Append('*');
                break;

            case Span span when GetInlineRole(span) == MarkdownInlineRole.CodeSpan:
                builder.Append('`');
                builder.Append(NormalizeDocumentText(new TextRange(span.ContentStart, span.ContentEnd).Text));
                builder.Append('`');
                break;

            case Span span:
                builder.Append(SerializeInlines(span.Inlines, preserveLiteralMarkdown));
                break;

            default:
                builder.Append(SerializeLiteralText(inline, preserveLiteralMarkdown));
                break;
        }
    }

    private static void ApplyBlockTheme(WpfBlock block, MarkdownTheme theme)
    {
        switch (block)
        {
            case Paragraph paragraph:
                paragraph.Foreground = theme.ForegroundBrush;
                paragraph.BorderThickness = new Thickness(0);
                paragraph.Padding = new Thickness(0);

                int headingLevel = GetHeadingLevel(paragraph);
                if (headingLevel > 0)
                {
                    paragraph.FontWeight = FontWeights.SemiBold;
                    paragraph.FontSize = theme.BaseFontSize + Math.Max(2, 14 - (headingLevel * 2));
                }
                else if (GetBlockRole(paragraph) == MarkdownBlockRole.CodeBlock)
                {
                    paragraph.FontFamily = theme.CodeFontFamily;
                    paragraph.Background = theme.CodeBackgroundBrush;
                    paragraph.Padding = new Thickness(8, 6, 8, 6);
                    paragraph.BorderBrush = theme.BorderBrush;
                    paragraph.BorderThickness = new Thickness(1);
                }
                else
                {
                    paragraph.FontFamily = theme.BaseFontFamily;
                    paragraph.FontSize = theme.BaseFontSize;
                    paragraph.Background = Brushes.Transparent;
                }

                int quoteDepth = GetQuoteDepth(paragraph);
                paragraph.Margin = quoteDepth > 0
                    ? new Thickness(18 * quoteDepth, 4, 0, 4)
                    : paragraph.Margin;

                if (quoteDepth > 0 && GetBlockRole(paragraph) != MarkdownBlockRole.CodeBlock)
                    paragraph.Foreground = theme.QuoteBrush;

                foreach (WpfInline inline in paragraph.Inlines)
                    ApplyInlineTheme(inline, theme);

                break;

            case WpfList list:
                list.Foreground = theme.ForegroundBrush;
                list.Margin = GetQuoteDepth(list) > 0
                    ? new Thickness(18 * GetQuoteDepth(list), 4, 0, 4)
                    : list.Margin;

                foreach (ListItem item in list.ListItems)
                {
                    foreach (WpfBlock child in item.Blocks)
                        ApplyBlockTheme(child, theme);
                }

                break;

            case WpfTable table:
                table.Foreground = theme.ForegroundBrush;
                table.Margin = GetQuoteDepth(table) > 0
                    ? new Thickness(18 * GetQuoteDepth(table), 6, 0, 6)
                    : table.Margin;

                foreach (TableRowGroup rowGroup in table.RowGroups)
                {
                    foreach (WpfTableRow row in rowGroup.Rows.Cast<WpfTableRow>())
                    {
                        foreach (WpfTableCell cell in row.Cells.Cast<WpfTableCell>())
                        {
                            cell.BorderBrush = theme.BorderBrush;
                            cell.BorderThickness = new Thickness(0.5);
                            cell.Background = GetIsTableHeader(cell) ? theme.TableHeaderBrush : Brushes.Transparent;

                            foreach (WpfBlock child in cell.Blocks)
                                ApplyBlockTheme(child, theme);
                        }
                    }
                }

                break;
        }
    }

    private static void ApplyInlineTheme(WpfInline inline, MarkdownTheme theme)
    {
        switch (inline)
        {
            case Hyperlink hyperlink:
                hyperlink.Foreground = theme.AccentBrush;
                hyperlink.TextDecorations = TextDecorations.Underline;
                foreach (WpfInline child in hyperlink.Inlines)
                    ApplyInlineTheme(child, theme);
                break;

            case Run run when GetInlineRole(run) == MarkdownInlineRole.CodeSpan:
                run.FontFamily = theme.CodeFontFamily;
                run.Background = theme.CodeBackgroundBrush;
                break;

            case Span span:
                foreach (WpfInline child in span.Inlines)
                    ApplyInlineTheme(child, theme);
                break;
        }
    }

    private static MarkdownTheme CreateTheme(FrameworkElement resourceHost, bool isLightTheme, FontFamily baseFontFamily, double baseFontSize)
    {
        Brush foreground = FindBrush(resourceHost, "TextFillColorPrimaryBrush", Colors.Black);
        Brush border = FindBrush(resourceHost, "ControlStrokeColorDefaultBrush", Color.FromRgb(120, 120, 120));
        Brush accent = FindBrush(resourceHost, "Teal", Color.FromRgb(48, 142, 152));
        Brush quote = FindBrush(resourceHost, "TextFillColorSecondaryBrush", isLightTheme ? Color.FromRgb(70, 70, 70) : Color.FromRgb(190, 190, 190));
        Brush tableHeader = new SolidColorBrush(isLightTheme ? Color.FromRgb(244, 246, 248) : Color.FromRgb(43, 43, 46));
        Brush codeBackground = new SolidColorBrush(isLightTheme ? Color.FromRgb(245, 245, 245) : Color.FromRgb(32, 32, 36));

        return new MarkdownTheme(
            foreground,
            border,
            accent,
            quote,
            tableHeader,
            codeBackground,
            baseFontFamily,
            new FontFamily("Consolas"),
            baseFontSize);
    }

    private static Brush FindBrush(FrameworkElement resourceHost, string resourceKey, Color fallback)
    {
        return resourceHost.TryFindResource(resourceKey) switch
        {
            Brush brush => brush,
            Color color => new SolidColorBrush(color),
            _ => new SolidColorBrush(fallback)
        };
    }

    private static string GetCodeBlockText(LeafBlock block)
    {
        return NormalizeDocumentText(block.Lines.ToString());
    }

    private static string SerializeLiteralText(TextElement element, bool preserveLiteralMarkdown)
    {
        string text = NormalizeDocumentText(new TextRange(element.ContentStart, element.ContentEnd).Text);
        return preserveLiteralMarkdown ? text : EscapeMarkdownText(text);
    }

    private static string EscapeMarkdownText(string? text)
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

    private static string EscapeLinkDestination(string destination)
    {
        return destination.Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static string ApplyQuotePrefix(string text, string quotePrefix)
    {
        if (string.IsNullOrEmpty(quotePrefix))
            return text;

        return string.Join(
            Environment.NewLine,
            NormalizeNewlines(text).Split('\n').Select(line => string.IsNullOrEmpty(line)
                ? quotePrefix.TrimEnd()
                : $"{quotePrefix}{line}"));
    }

    private static string GetQuotePrefix(int quoteDepth)
    {
        if (quoteDepth <= 0)
            return string.Empty;

        StringBuilder builder = new();
        for (int i = 0; i < quoteDepth; i++)
            builder.Append("> ");

        return builder.ToString();
    }

    private static string NormalizeDocumentText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return NormalizeNewlines(text).TrimEnd('\n');
    }

    private static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string GetSourceSlice(string source, MarkdownObject markdownObject)
    {
        if (markdownObject.Span.Start < 0
            || markdownObject.Span.End < markdownObject.Span.Start
            || markdownObject.Span.End >= source.Length)
            return string.Empty;

        return source.Substring(markdownObject.Span.Start, markdownObject.Span.End - markdownObject.Span.Start + 1);
    }

    private static readonly DependencyProperty QuoteDepthProperty =
        DependencyProperty.RegisterAttached("QuoteDepth", typeof(int), typeof(MarkdownDocumentUtilities), new PropertyMetadata(0));

    private static readonly DependencyProperty HeadingLevelProperty =
        DependencyProperty.RegisterAttached("HeadingLevel", typeof(int), typeof(MarkdownDocumentUtilities), new PropertyMetadata(0));

    private static readonly DependencyProperty BlockRoleProperty =
        DependencyProperty.RegisterAttached("BlockRole", typeof(MarkdownBlockRole), typeof(MarkdownDocumentUtilities), new PropertyMetadata(MarkdownBlockRole.None));

    private static readonly DependencyProperty InlineRoleProperty =
        DependencyProperty.RegisterAttached("InlineRole", typeof(MarkdownInlineRole), typeof(MarkdownDocumentUtilities), new PropertyMetadata(MarkdownInlineRole.None));

    private static readonly DependencyProperty TaskListMarkerCheckedProperty =
        DependencyProperty.RegisterAttached("TaskListMarkerChecked", typeof(bool), typeof(MarkdownDocumentUtilities), new PropertyMetadata(false));

    private static readonly DependencyProperty CodeFenceInfoProperty =
        DependencyProperty.RegisterAttached("CodeFenceInfo", typeof(string), typeof(MarkdownDocumentUtilities), new PropertyMetadata(string.Empty));

    private static readonly DependencyProperty IsTableHeaderProperty =
        DependencyProperty.RegisterAttached("IsTableHeader", typeof(bool), typeof(MarkdownDocumentUtilities), new PropertyMetadata(false));

    private static void SetQuoteDepth(DependencyObject element, int value) => element.SetValue(QuoteDepthProperty, value);
    private static int GetQuoteDepth(DependencyObject element) => (int)element.GetValue(QuoteDepthProperty);
    private static void SetHeadingLevel(DependencyObject element, int value) => element.SetValue(HeadingLevelProperty, value);
    private static int GetHeadingLevel(DependencyObject element) => (int)element.GetValue(HeadingLevelProperty);
    private static void SetBlockRole(DependencyObject element, MarkdownBlockRole value) => element.SetValue(BlockRoleProperty, value);
    private static MarkdownBlockRole GetBlockRole(DependencyObject element) => (MarkdownBlockRole)element.GetValue(BlockRoleProperty);
    private static void SetInlineRole(DependencyObject element, MarkdownInlineRole value) => element.SetValue(InlineRoleProperty, value);
    private static MarkdownInlineRole GetInlineRole(DependencyObject element) => (MarkdownInlineRole)element.GetValue(InlineRoleProperty);
    private static void SetTaskListMarkerChecked(DependencyObject element, bool value) => element.SetValue(TaskListMarkerCheckedProperty, value);
    private static bool GetTaskListMarkerChecked(DependencyObject element) => (bool)element.GetValue(TaskListMarkerCheckedProperty);
    private static void SetCodeFenceInfo(DependencyObject element, string value) => element.SetValue(CodeFenceInfoProperty, value);
    private static string GetCodeFenceInfo(DependencyObject element) => (string)element.GetValue(CodeFenceInfoProperty);
    private static void SetIsTableHeader(DependencyObject element, bool value) => element.SetValue(IsTableHeaderProperty, value);
    private static bool GetIsTableHeader(DependencyObject element) => (bool)element.GetValue(IsTableHeaderProperty);


    [GeneratedRegex(@"^\s{0,3}(#{1,6}|>+|[-+*]|\d+[.)])$", RegexOptions.Compiled)]
    private static partial Regex LiveBlockTrigger();

    [GeneratedRegex(@"(^|\s)\[( |x|X)\](\s|$)|(\*\*|__)(?=\S).+?\4|(?<!\*)\*(?=\S).+?(?<=\S)\*|(?<!_)_(?=\S).+?(?<=\S)_|`[^`\r\n]+`|\[[^\]\r\n]+\]\([^)]+\)", RegexOptions.Compiled)]
    private static partial Regex LiveInlinePromotion();

    [GeneratedRegex(@"(^|\n)\s{0,3}(#{1,6}\s|>+\s|[-+*]\s|\d+[.)]\s|```|~~~|---\s*$|___\s*$|\*\*\*\s*$)|\[[^\]]+\]\([^)]+\)|!\[[^\]]*\]\([^)]+\)|(^|\n)\|.+\|\s*$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex MarkdownPattern();
}
