using Markdig.Extensions.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Text_Grab.Controls;
using Text_Grab.Editing;

namespace Text_Grab.Utilities;

public static partial class MarkdownFlowDocumentUtilities
{
    private static void InitializeExtendedInlines(FlowDocument document)
    {
        for (TextPointer? position = document.ContentStart; position is not null;
            position = position.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (position.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.ElementStart)
                continue;
            switch (position.GetAdjacentElement(LogicalDirection.Forward))
            {
                case Span span:
                    ApplyExtendedEmphasisStyle(span, isLightTheme: true);
                    break;
                case InlineUIContainer { Child: MathFormula formula } container:
                    formula.ApplyTheme(document.Foreground, container.FontSize);
                    break;
            }
        }
    }

    private static void ApplyExtendedEmphasisStyle(Span span, bool isLightTheme)
    {
        switch ((string)span.GetValue(EmphasisDelimiterProperty))
        {
            case "~~":
                span.TextDecorations = TextDecorations.Strikethrough;
                break;
            case "++":
                span.TextDecorations = TextDecorations.Underline;
                break;
            case "==":
                span.Background = new SolidColorBrush(isLightTheme ? Color.FromRgb(255, 235, 130) : Color.FromRgb(100, 80, 0));
                break;
            case "~":
            case "^":
                span.BaselineAlignment = (string)span.GetValue(EmphasisDelimiterProperty) == "~"
                    ? BaselineAlignment.Subscript : BaselineAlignment.Superscript;
                span.FontSize = (span.Parent is TextElement parent ? parent.FontSize : span.FontSize) * 0.75;
                break;
        }
    }

    private const MarkdownFormattingAction ScriptActions = MarkdownFormattingAction.Superscript | MarkdownFormattingAction.Subscript;

    private static MarkdownFormattingAction GetAvailableExtendedEmphasis(
        List<Paragraph> paragraphs, TextSelection selection, bool canEmphasize)
    {
        if (selection.IsEmpty || string.IsNullOrWhiteSpace(selection.Text))
            return MarkdownFormattingAction.None;

        if (canEmphasize && (paragraphs.Count == 1 || !paragraphs.Any(paragraph => HasFormattingAncestor<TableCell>(paragraph)))
            && !paragraphs.Any(paragraph => IntersectsProtectedInline(paragraph.Inlines, selection, protectHyperlinks: true)))
            return MarkdownFormattingAction.Strikethrough | ScriptActions;

        if (paragraphs is [Paragraph singleParagraph] && FindSelectedExtendedSpan(singleParagraph.Inlines, selection) is Span span)
            return (string)span.GetValue(EmphasisDelimiterProperty) == "~~" ? MarkdownFormattingAction.Strikethrough : ScriptActions;

        return MarkdownFormattingAction.None;
    }

    private static Span? FindSelectedExtendedSpan(InlineCollection inlines, TextSelection selection)
    {
        foreach (Span span in inlines.OfType<Span>())
        {
            if (span is Hyperlink
                || selection.Start.CompareTo(span.ElementEnd) >= 0 || selection.End.CompareTo(span.ElementStart) <= 0
                || (string)span.GetValue(HtmlOpeningProperty) != string.Empty)
                continue;

            if ((selection.Start.CompareTo(span.ContentStart) < 0 && new TextRange(selection.Start, span.ContentStart).Text.Length != 0)
                || (selection.End.CompareTo(span.ContentEnd) > 0 && new TextRange(span.ContentEnd, selection.End).Text.TrimEnd('\r', '\n').Length != 0))
                continue;

            string delimiter = (string)span.GetValue(EmphasisDelimiterProperty);
            if (delimiter is "~~" or "^" or "~")
            {
                TextRange contents = new(span.ContentStart, span.ContentEnd);
                return IntersectsProtectedInline(span.Inlines, contents, protectHyperlinks: true) ? null : span;
            }

            if (delimiter.Length == 0 && FindSelectedExtendedSpan(span.Inlines, selection) is Span nested)
                return nested;
        }

        return null;
    }

    private static void ToggleExtendedEmphasis(TextSelection selection, List<Paragraph> paragraphs, string delimiter)
    {
        TextPointer start = selection.Start;
        TextPointer end = selection.End;
        int startOffset = ParagraphOffset(paragraphs[0], start);
        int endOffset = ParagraphOffset(paragraphs[^1], end);
        if (paragraphs is [Paragraph paragraph] && FindSelectedExtendedSpan(paragraph.Inlines, selection) is Span span)
        {
            string previous = (string)span.GetValue(EmphasisDelimiterProperty);
            int spanStart = ParagraphOffset(paragraph, span.ContentStart);
            int spanEnd = ParagraphOffset(paragraph, span.ContentEnd);

            // Keep the unselected ends of the original span formatted when toggling only part of it.
            span.ClearValue(EmphasisDelimiterProperty);
            if (previous == "~~")
                span.ClearValue(Inline.TextDecorationsProperty);
            else
            {
                span.ClearValue(Inline.BaselineAlignmentProperty);
                span.ClearValue(TextElement.FontSizeProperty);
            }
            if (spanStart < startOffset)
                WrapExtendedEmphasis(span.Inlines, span.ContentStart, ParagraphPosition(paragraph, startOffset), previous);
            if (endOffset < spanEnd)
                WrapExtendedEmphasis(span.Inlines, ParagraphPosition(paragraph, endOffset), span.ContentEnd, previous);
            if (previous != delimiter)
                WrapExtendedEmphasis(span.Inlines, ParagraphPosition(paragraph, Math.Max(spanStart, startOffset)),
                    ParagraphPosition(paragraph, Math.Min(spanEnd, endOffset)), delimiter);

            InlineCollection siblings = span.Parent is Span parentSpan ? parentSpan.Inlines : paragraph.Inlines;
            foreach (Inline child in span.Inlines.ToArray())
            {
                span.Inlines.Remove(child);
                siblings.InsertBefore(span, child);
            }
            siblings.Remove(span);
        }
        else
        {
            for (int i = 0; i < paragraphs.Count; i++)
            {
                Paragraph selectedParagraph = paragraphs[i];
                TextPointer selectedStart = i == 0 ? ParagraphPosition(selectedParagraph, startOffset) : selectedParagraph.ContentStart;
                TextPointer selectedEnd = i == paragraphs.Count - 1 ? ParagraphPosition(selectedParagraph, endOffset) : selectedParagraph.ContentEnd;
                WrapExtendedEmphasis(selectedParagraph.Inlines, selectedStart, selectedEnd, delimiter);
            }
        }

        selection.Select(ParagraphPosition(paragraphs[0], startOffset), ParagraphPosition(paragraphs[^1], endOffset));
    }

    private static int ParagraphOffset(Paragraph paragraph, TextPointer position)
    {
        if (position.CompareTo(paragraph.ContentStart) <= 0)
            return 0;
        TextPointer end = position.CompareTo(paragraph.ContentEnd) < 0 ? position : paragraph.ContentEnd;
        return new TextRange(paragraph.ContentStart, end).Text.Length;
    }

    private static TextPointer ParagraphPosition(Paragraph paragraph, int offset)
    {
        TextPointer position = paragraph.ContentStart;
        if (offset == 0)
            return position;

        // Step across whole runs; measuring every character's prefix is quadratic on long paragraphs.
        const LogicalDirection forward = LogicalDirection.Forward;
        int currentOffset = new TextRange(position, position).Text.Length;
        while (position.CompareTo(paragraph.ContentEnd) < 0)
        {
            TextPointerContext context = position.GetPointerContext(forward);
            if (context == TextPointerContext.Text)
            {
                int length = position.GetTextRunLength(forward);
                if (offset <= currentOffset + length)
                    return position.GetPositionAtOffset(Math.Max(0, offset - currentOffset), forward)!;
                currentOffset += length;
            }
            else if (context == TextPointerContext.ElementStart
                && position.GetAdjacentElement(forward) is Inline inline && inline is LineBreak or InlineUIContainer)
            {
                position = inline.ElementEnd;
                currentOffset = ParagraphOffset(paragraph, position);
                if (offset <= currentOffset)
                    return position;
                continue;
            }

            position = position.GetNextContextPosition(forward)!;
        }

        return paragraph.ContentEnd;
    }

    private static void WrapExtendedEmphasis(InlineCollection inlines, TextPointer start, TextPointer end, string delimiter)
    {
        if (start.CompareTo(end) >= 0)
            return;

        // The range-based Span constructor can fail-fast when both endpoints are in the same Run.
        List<Inline> selected = [];
        foreach (Inline inline in inlines.ToArray())
        {
            if (inline.ContentStart.CompareTo(end) >= 0)
                break;
            if (inline.ContentEnd.CompareTo(start) <= 0)
                continue;

            if (inline.ContentStart.CompareTo(start) >= 0 && inline.ContentEnd.CompareTo(end) <= 0)
                selected.Add(inline);
            else if (inline is Run run)
            {
                int first = start.CompareTo(run.ContentStart) > 0 ? run.ContentStart.GetOffsetToPosition(start) : 0;
                int last = end.CompareTo(run.ContentEnd) < 0 ? run.ContentStart.GetOffsetToPosition(end) : run.Text.Length;
                selected.Add(SplitSelectedRun(inlines, run, first, last));
            }
            else if (inline is Span span)
            {
                WrapExtendedEmphasisGroup(inlines, selected, delimiter);
                selected.Clear();
                WrapExtendedEmphasis(span.Inlines, start, end, delimiter);
            }
            else
            {
                WrapExtendedEmphasisGroup(inlines, selected, delimiter);
                selected.Clear();
            }
        }

        WrapExtendedEmphasisGroup(inlines, selected, delimiter);
    }

    private static Run SplitSelectedRun(InlineCollection inlines, Run run, int first, int last)
    {
        string text = run.Text;
        if (first > 0)
            inlines.InsertBefore(run, CopyRunWithText(run, text[..first]));
        if (last < text.Length)
            inlines.InsertAfter(run, CopyRunWithText(run, text[last..]));
        run.Text = text[first..last];
        return run;
    }

    private static Run CopyRunWithText(Run source, string text)
    {
        Run copy = new(text);
        LocalValueEnumerator values = source.GetLocalValueEnumerator();
        while (values.MoveNext())
        {
            DependencyProperty property = values.Current.Property;
            if (property != Run.TextProperty && property != RawSpanStartProperty && property != RawSpanEndProperty && !property.ReadOnly)
                copy.SetValue(property, source.GetValue(property));
        }
        return copy;
    }

    private static void WrapExtendedEmphasisGroup(InlineCollection inlines, List<Inline> selected, string delimiter)
    {
        if (selected.Count == 0 || string.IsNullOrWhiteSpace(new TextRange(selected[0].ContentStart, selected[^1].ContentEnd).Text))
            return;

        Span span = new();
        span.SetValue(EmphasisDelimiterProperty, delimiter);
        inlines.InsertBefore(selected[0], span);
        foreach (Inline inline in selected)
        {
            inlines.Remove(inline);
            span.Inlines.Add(inline);
        }
        ApplyExtendedEmphasisStyle(span, isLightTheme: true);
    }

    private static Paragraph CreateMathParagraph(MathBlock block, int quoteDepth)
    {
        string source = MarkdownDocumentUtilities.GetCodeBlockText(block);
        string fence = new('$', Math.Max(2, block.OpeningFencedCharCount));
        Paragraph paragraph = new()
        {
            Margin = new Thickness(0, 8, 0, 8)
        };
        SetBlockRole(paragraph, MarkdownBlockRole.MathBlock);
        SetQuoteDepth(paragraph, quoteDepth);
        SetRawSpan(paragraph, block.Span.Start, block.Span.End + 1);
        paragraph.Inlines.Add(CreateMathInline(source, $"{fence}{Environment.NewLine}{source}{Environment.NewLine}{fence}",
            isDisplay: true, block.Span.Start, block.Span.End + 1));
        return paragraph;
    }

    private static InlineUIContainer CreateMathInline(string source, string markdown, bool isDisplay, int rawStart, int rawEnd)
    {
        MathFormula formula = new(source, markdown, isDisplay);
        formula.SetBinding(FrameworkElement.MaxWidthProperty, new Binding(nameof(FrameworkElement.ActualWidth))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(RichTextBox), 1),
            Converter = new ImageWidthConverter(),
            FallbackValue = 640d
        });
        InlineUIContainer container = new(formula) { BaselineAlignment = BaselineAlignment.Center };
        SetRawSpan(container, rawStart, rawEnd);
        return container;
    }

    private static bool TryWriteExtendedInline(StringBuilder builder, Inline inline, bool preserveLiteralMarkdown, bool isTableCell = false)
    {
        if (inline is InlineUIContainer { Child: MathFormula formula })
        {
            builder.Append(formula.Markdown);
            return true;
        }
        if (inline is Span span && (string)span.GetValue(EmphasisDelimiterProperty) is { Length: > 0 } delimiter)
        {
            AppendEmphasis(builder, SerializeInlines(span.Inlines, preserveLiteralMarkdown, isTableCell), delimiter);
            return true;
        }
        return false;
    }

    private static void WriteExtendedEmphasis(StringBuilder builder, ref Inline? inline, string delimiter,
        bool preserveLiteralMarkdown, bool isTableCell)
    {
        // Two adjacent closing/opening delimiters would be parsed as one longer delimiter run.
        StringBuilder content = new();
        while (inline is Span span && Equals(span.GetValue(EmphasisDelimiterProperty), delimiter))
        {
            content.Append(SerializeInlines(span.Inlines, preserveLiteralMarkdown, isTableCell));
            inline = inline.NextInline;
        }
        AppendEmphasis(builder, content.ToString(), delimiter);
    }

    private static readonly DependencyProperty EmphasisDelimiterProperty =
        DependencyProperty.RegisterAttached("EmphasisDelimiter", typeof(string), typeof(MarkdownFlowDocumentUtilities),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure));
}
