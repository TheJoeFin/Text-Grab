using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Text_Grab.Editing;
using WpfList = System.Windows.Documents.List;

namespace Text_Grab.Utilities;

public static partial class MarkdownFlowDocumentUtilities
{
    internal static MarkdownFormattingAction GetAvailableFormatting(RichTextBox editor) =>
        GetAvailableFormatting(GetSelectedParagraphs(editor), editor.Selection);

    internal static void ApplyFormatting(RichTextBox editor, MarkdownFormattingAction action, int headingLevel,
        FrameworkElement resourceHost, bool isLightTheme)
    {
        List<Paragraph> paragraphs = GetSelectedParagraphs(editor);
        if (!GetAvailableFormatting(paragraphs, editor.Selection).HasFlag(action))
            return;

        if (action == MarkdownFormattingAction.ToDo)
        {
            using (editor.DeclareChangeBlock())
            {
                ToggleTaskList(editor, paragraphs);
                ApplyListStyling(editor.Document);
            }
            return;
        }

        RoutedUICommand? command = action switch
        {
            MarkdownFormattingAction.Bold => EditingCommands.ToggleBold,
            MarkdownFormattingAction.Italic => EditingCommands.ToggleItalic,
            MarkdownFormattingAction.Bullets => EditingCommands.ToggleBullets,
            MarkdownFormattingAction.Numbering => EditingCommands.ToggleNumbering,
            _ => null
        };
        if (command is not null)
        {
            // A new list starts with WPF's own marker and indent, so restyle in the same undo step.
            using (editor.DeclareChangeBlock())
            {
                command.Execute(null, editor);
                if (action is MarkdownFormattingAction.Bullets or MarkdownFormattingAction.Numbering)
                    ApplyListStyling(editor.Document);
            }
            return;
        }

        string? delimiter = action switch
        {
            MarkdownFormattingAction.Strikethrough => "~~",
            MarkdownFormattingAction.Superscript => "^",
            MarkdownFormattingAction.Subscript => "~",
            _ => null
        };
        if (delimiter is not null)
        {
            ToggleExtendedEmphasis(editor.Selection, paragraphs, delimiter);
            return;
        }

        MarkdownTheme theme = CreateTheme(resourceHost, isLightTheme, editor.Document.FontFamily, editor.Document.FontSize);
        switch (action)
        {
            case MarkdownFormattingAction.Heading:
                foreach (Paragraph paragraph in paragraphs)
                {
                    SetHeadingLevel(paragraph, headingLevel);
                    ApplyEditedParagraphTheme(paragraph, theme);
                }
                break;
            case MarkdownFormattingAction.Quote:
                int depth = paragraphs.All(paragraph => GetQuoteDepth(paragraph) > 0) ? 0 : 1;
                foreach (Paragraph paragraph in paragraphs)
                {
                    SetQuoteDepth(paragraph, depth);
                    ApplyEditedParagraphTheme(paragraph, theme);
                }
                break;
            case MarkdownFormattingAction.CodeBlock:
                ToggleCodeBlock(editor, paragraphs, theme);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private static List<Paragraph> GetSelectedParagraphs(RichTextBox editor)
    {
        TextSelection selection = editor.Selection;
        Paragraph? first = selection.Start.GetInsertionPosition(LogicalDirection.Forward).Paragraph;
        if (first is null)
            return [];

        List<Paragraph> paragraphs = [first];
        if (selection.IsEmpty || selection.End.CompareTo(first.ElementEnd) <= 0)
            return paragraphs;

        for (TextPointer? position = first.ElementEnd; position is not null && position.CompareTo(selection.End) < 0;
             position = position.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart
                && position.GetAdjacentElement(LogicalDirection.Forward) is Paragraph paragraph
                && paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward).CompareTo(selection.End) < 0)
                paragraphs.Add(paragraph);
        }
        return paragraphs;
    }

    private static MarkdownFormattingAction GetAvailableFormatting(List<Paragraph> paragraphs, TextSelection selection)
    {
        if (paragraphs.Count == 0)
            return MarkdownFormattingAction.None;

        if (paragraphs is [Paragraph code] && IsCodeBlock(code))
            return code.Inlines.Any(inline => inline is InlineUIContainer) ? MarkdownFormattingAction.None : MarkdownFormattingAction.CodeBlock;

        if (!paragraphs.All(IsEditableMarkdownParagraph))
            return MarkdownFormattingAction.None;

        bool canEmphasize = !paragraphs.Any(paragraph => IntersectsProtectedInline(paragraph.Inlines, selection));
        MarkdownFormattingAction actions = canEmphasize
            ? MarkdownFormattingAction.Bold | MarkdownFormattingAction.Italic
            : MarkdownFormattingAction.None;
        actions |= GetAvailableExtendedEmphasis(paragraphs, selection, canEmphasize);
        if (CanToggleTaskList(paragraphs))
            actions |= MarkdownFormattingAction.ToDo;
        if (paragraphs.All(paragraph => !StartsWithTaskListMarker(paragraph) && !HasFormattingAncestor<TableCell>(paragraph)))
        {
            actions |= MarkdownFormattingAction.Heading;
            if (paragraphs.All(paragraph => GetQuoteDepth(paragraph) == 0 && !HasFormattingAncestor<WpfList>(paragraph,
                    list => GetQuoteDepth(list) != 0 || list.MarkerStyle == TextMarkerStyle.None || GetBlockRole(list) != MarkdownBlockRole.None)))
                actions |= MarkdownFormattingAction.Bullets | MarkdownFormattingAction.Numbering;
        }

        if (paragraphs.All(paragraph => paragraph.Parent is FlowDocument))
        {
            actions |= MarkdownFormattingAction.Quote;
            if (paragraphs.All(paragraph => GetQuoteDepth(paragraph) == GetQuoteDepth(paragraphs[0])
                    && paragraph.Inlines.All(IsTextualCodeContent)))
                actions |= MarkdownFormattingAction.CodeBlock;
        }
        return actions;
    }

    private static bool CanToggleTaskList(List<Paragraph> paragraphs)
    {
        if (paragraphs.Any(paragraph => GetHeadingLevel(paragraph) != 0 || GetQuoteDepth(paragraph) != 0))
            return false;

        if (paragraphs.All(paragraph => paragraph.Parent is FlowDocument))
            return true;

        if (paragraphs[0].Parent is not ListItem { Parent: WpfList list }
            || list.Parent is not FlowDocument || GetQuoteDepth(list) != 0
            || GetBlockRole(list) != MarkdownBlockRole.None
            || list.MarkerStyle is not (TextMarkerStyle.Disc or TextMarkerStyle.None or TextMarkerStyle.Decimal))
            return false;

        return paragraphs.All(paragraph => paragraph.Parent is ListItem item
                && item.Parent == list && item.Blocks.FirstBlock == paragraph)
            && (list.MarkerStyle != TextMarkerStyle.Decimal || paragraphs.Count == list.ListItems.Count);
    }

    private static void ToggleTaskList(RichTextBox editor, List<Paragraph> paragraphs)
    {
        bool hasCaret = editor.Selection.IsEmpty;
        if (paragraphs[0].Parent is FlowDocument)
        {
            EditingCommands.ToggleBullets.Execute(null, editor);
            paragraphs = GetSelectedParagraphs(editor);
        }

        WpfList list = (WpfList)((ListItem)paragraphs[0].Parent).Parent;
        bool remove = paragraphs.All(StartsWithTaskListMarker);
        foreach (Paragraph paragraph in paragraphs)
        {
            if (remove)
            {
                paragraph.Inlines.Remove(paragraph.Inlines.FirstInline);
                paragraph.ClearValue(Paragraph.PaddingProperty);
                paragraph.ClearValue(Paragraph.TextIndentProperty);
            }
            else if (!StartsWithTaskListMarker(paragraph))
            {
                TextPointer labelStart = InsertTaskListMarker(paragraph);
                if (hasCaret && editor.CaretPosition.CompareTo(labelStart) < 0)
                    editor.CaretPosition = labelStart;
            }
        }

        bool allTasks = list.ListItems.All(IsTaskListItem);
        list.MarkerStyle = allTasks ? TextMarkerStyle.None : TextMarkerStyle.Disc;
        if (allTasks)
        {
            list.Padding = new Thickness(0);
            list.MarkerOffset = 0;
        }
        else
        {
            list.ClearValue(WpfList.PaddingProperty);
            list.ClearValue(WpfList.MarkerOffsetProperty);
        }
    }

    private static bool IsEditableMarkdownParagraph(Paragraph paragraph) =>
        GetBlockRole(paragraph) == MarkdownBlockRole.None
        && (string)paragraph.GetValue(HtmlOpeningProperty) == string.Empty
        && !HasFormattingAncestor<WpfList>(paragraph, list => GetBlockRole(list) == MarkdownBlockRole.FootnoteList);

    private static bool IntersectsProtectedInline(InlineCollection inlines, TextRange selection, bool protectHyperlinks = false)
    {
        foreach (Inline inline in inlines)
        {
            if (inline.ElementStart.CompareTo(selection.End) >= 0)
                break;
            if (inline.ElementEnd.CompareTo(selection.Start) <= 0)
                continue;

            // WPF's inline commands can flatten spans and split runs, losing Markdown-specific metadata.
            if (GetInlineRole(inline) != MarkdownInlineRole.None
                || inline is InlineUIContainer
                || protectHyperlinks && inline is Hyperlink
                || (string)inline.GetValue(HtmlOpeningProperty) != string.Empty
                || (string)inline.GetValue(ImageMarkdownProperty) != string.Empty
                || (string)inline.GetValue(EmphasisDelimiterProperty) != string.Empty
                || inline is Span span && IntersectsProtectedInline(span.Inlines, selection, protectHyperlinks))
                return true;
        }
        return false;
    }

    private static bool HasFormattingAncestor<T>(TextElement element, Predicate<T>? predicate = null) where T : TextElement
    {
        for (DependencyObject? parent = element.Parent; parent is TextElement ancestor; parent = ancestor.Parent)
        {
            if (ancestor is T match && (predicate is null || predicate(match)))
                return true;
        }
        return false;
    }

    private static bool IsTextualCodeContent(Inline inline) =>
        inline is Run or LineBreak || inline is Span span && span.Inlines.All(IsTextualCodeContent);

    private static void ApplyEditedParagraphTheme(Paragraph paragraph, MarkdownTheme theme)
    {
        int headingLevel = GetHeadingLevel(paragraph);
        if (headingLevel == 0)
            paragraph.ClearValue(TextElement.FontWeightProperty);

        paragraph.Margin = GetBlockRole(paragraph) == MarkdownBlockRole.CodeBlock
            ? new Thickness(0, 6, 0, 6)
            : headingLevel > 0 ? new Thickness(0, 10, 0, 4) : new Thickness(0, 4, 0, 4);
        ApplyBlockTheme(paragraph, theme);
    }

    private static void ToggleCodeBlock(RichTextBox editor, List<Paragraph> paragraphs, MarkdownTheme theme)
    {
        Paragraph first = paragraphs[0];
        if (GetBlockRole(first) == MarkdownBlockRole.CodeBlock)
        {
            SetBlockRole(first, MarkdownBlockRole.None);
            ApplyEditedParagraphTheme(first, theme);
            return;
        }

        bool hasSelection = !editor.Selection.IsEmpty;
        int caretOffset = hasSelection ? 0 : new TextRange(first.ContentStart, editor.CaretPosition).Text.Length;
        string code = string.Join("\n", paragraphs.Select(paragraph => string.Concat(paragraph.Inlines.Select(GetCodeInlineText))));
        first.Inlines.Clear();
        first.Inlines.Add(new Run(code));
        foreach (Paragraph paragraph in paragraphs.Skip(1))
            editor.Document.Blocks.Remove(paragraph);
        SetHeadingLevel(first, 0);
        SetBlockRole(first, MarkdownBlockRole.CodeBlock);
        ApplyEditedParagraphTheme(first, theme);
        if (first.NextBlock is null)
            editor.Document.Blocks.Add(new Paragraph());

        TextPointer start = GetLocalTextPointer(first, caretOffset);
        editor.Selection.Select(start, hasSelection ? first.ContentEnd : start);
    }

    internal static void InsertTable(RichTextBox editor, int columns, int rows, FrameworkElement resourceHost, bool isLightTheme)
    {
        Table table = CreateTableElement(columns);
        TableRowGroup rowGroup = new();
        table.RowGroups.Add(rowGroup);
        for (int row = 0; row < rows; row++)
        {
            TableRow tableRow = new();
            rowGroup.Rows.Add(tableRow);
            for (int column = 0; column < columns; column++)
                tableRow.Cells.Add(CreateTableCell(row == 0, new Paragraph()));
        }

        FlowDocument document = editor.Document;
        TextPointer insertion = editor.Selection.End.GetInsertionPosition(LogicalDirection.Forward);
        Block? anchor = insertion.Paragraph;
        for (DependencyObject? parent = anchor?.Parent; parent is TextElement element; parent = element.Parent)
        {
            if (parent is Block block)
                anchor = block;
        }

        if (anchor is null)
            document.Blocks.Add(table);
        else if (anchor is Paragraph paragraph && IsEditableMarkdownParagraph(paragraph)
                 && GetHeadingLevel(paragraph) == 0 && GetQuoteDepth(paragraph) == 0)
        {
            if (insertion.CompareTo(paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward)) == 0)
            {
                document.Blocks.InsertBefore(paragraph, table);
            }
            else
            {
                if (insertion.CompareTo(paragraph.ContentEnd.GetInsertionPosition(LogicalDirection.Backward)) < 0)
                    insertion.InsertParagraphBreak();
                document.Blocks.InsertAfter(paragraph, table);
            }
        }
        else
            document.Blocks.InsertAfter(anchor, table);

        if (table.NextBlock is null)
            document.Blocks.Add(new Paragraph());

        ApplyBlockTheme(table, CreateTheme(resourceHost, isLightTheme, document.FontFamily, document.FontSize));
        TextPointer cellStart = rowGroup.Rows[0].Cells[0].Blocks.FirstBlock!.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        editor.Selection.Select(cellStart, cellStart);
    }

    [Flags]
    private enum RunEmphasis { None = 0, Bold = 1, Italic = 2 }

    private static RunEmphasis GetRunEmphasis(Inline? inline) => inline is Run run
        ? (Equals(run.ReadLocalValue(TextElement.FontWeightProperty), FontWeights.Bold) ? RunEmphasis.Bold : RunEmphasis.None)
            | (Equals(run.ReadLocalValue(TextElement.FontStyleProperty), FontStyles.Italic) ? RunEmphasis.Italic : RunEmphasis.None)
        : RunEmphasis.None;

    private static void WriteFormattedRuns(StringBuilder builder, ref Inline? inline, RunEmphasis enclosing,
        bool preserveLiteralMarkdown, bool isTableCell)
    {
        RunEmphasis emphasis = GetRunEmphasis(inline) & ~enclosing;
        RunEmphasis outer = emphasis.HasFlag(RunEmphasis.Bold) ? RunEmphasis.Bold : RunEmphasis.Italic;
        RunEmphasis required = enclosing | outer;
        StringBuilder content = new();
        while (inline is not null && (GetRunEmphasis(inline) & required) == required)
        {
            if ((GetRunEmphasis(inline) & ~required) != RunEmphasis.None)
                WriteFormattedRuns(content, ref inline, required, preserveLiteralMarkdown, isTableCell);
            else
            {
                WriteInline(content, inline, preserveLiteralMarkdown, isTableCell);
                inline = inline.NextInline;
            }
        }

        AppendEmphasis(builder, content.ToString(), outer == RunEmphasis.Bold ? "**" : "*");
    }

    private static void AppendEmphasis(StringBuilder builder, string text, string delimiter)
    {
        // Markdown delimiters cannot open or close against whitespace on the inside.
        int start = text.Length - text.TrimStart().Length;
        int end = text.TrimEnd().Length;
        if (end <= start)
        {
            builder.Append(text);
            return;
        }
        builder.Append(text.AsSpan(0, start)).Append(delimiter).Append(text.AsSpan(start, end - start))
            .Append(delimiter).Append(text.AsSpan(end));
    }
}
