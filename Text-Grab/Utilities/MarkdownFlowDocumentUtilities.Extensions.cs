using Markdig.Extensions.Mathematics;
using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Text_Grab.Controls;

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

    private static bool TryWriteExtendedInline(StringBuilder builder, Inline inline, bool preserveLiteralMarkdown)
    {
        if (inline is InlineUIContainer { Child: MathFormula formula })
        {
            builder.Append(formula.Markdown);
            return true;
        }
        if (inline is Span span && (string)span.GetValue(EmphasisDelimiterProperty) is { Length: > 0 } delimiter)
        {
            builder.Append(delimiter);
            builder.Append(SerializeInlines(span.Inlines, preserveLiteralMarkdown));
            builder.Append(delimiter);
            return true;
        }
        return false;
    }

    private static readonly DependencyProperty EmphasisDelimiterProperty =
        DependencyProperty.RegisterAttached("EmphasisDelimiter", typeof(string), typeof(MarkdownFlowDocumentUtilities), new PropertyMetadata(string.Empty));
}
