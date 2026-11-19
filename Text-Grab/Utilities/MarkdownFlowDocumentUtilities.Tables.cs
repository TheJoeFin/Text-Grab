using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

public static partial class MarkdownFlowDocumentUtilities
{
    private const double TableActionGutterWidth = 36;

    private static Table CreateTableElement(int columnCount)
    {
        Table table = new()
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 6, TableActionGutterWidth, 6),
            TextAlignment = TextAlignment.Left
        };
        for (int column = 0; column < columnCount; column++)
            table.Columns.Add(new TableColumn());
        return table;
    }

    private static TableCell CreateTableCell(bool isHeader, Paragraph? paragraph = null)
    {
        TableCell cell = new() { Padding = new Thickness(6, 4, 6, 4) };
        SetIsTableHeader(cell, isHeader);
        if (paragraph is not null)
            cell.Blocks.Add(paragraph);
        return cell;
    }

    internal static EditTextTableDocument CreateSpreadsheetDocument(Table table)
    {
        List<List<string>> rows = [.. table.RowGroups.SelectMany(group => group.Rows).Select(row =>
            row.Cells.Select(cell => MarkdownDocumentUtilities.NormalizeNewlines(
                new TextRange(cell.ContentStart, cell.ContentEnd).Text)).ToList())];
        return new EditTextTableDocument
        {
            Format = EtwStructuredTextFormat.Tsv,
            Rows = rows,
            RowCount = rows.Count,
            ColumnCount = table.Columns.Count
        };
    }

    internal static void FitTableColumns(FlowDocument document, double viewportWidth, double pixelsPerDip)
    {
        double pageWidth = double.IsNaN(document.PageWidth) ? viewportWidth : document.PageWidth;
        double pagePadding = HorizontalThickness(document.PagePadding);
        double minimumWidth = FitTableColumns(document.Blocks, pageWidth - pagePadding, pixelsPerDip);
        double minimumPageWidth = minimumWidth > 0 ? minimumWidth + pagePadding : 0;
        if (document.MinPageWidth != minimumPageWidth)
            document.MinPageWidth = minimumPageWidth;
    }

    private static double FitTableColumns(BlockCollection blocks, double availableWidth, double pixelsPerDip)
    {
        double minimumWidth = 0;
        foreach (Block block in blocks)
        {
            double blockInsets = HorizontalThickness(block.Margin) + HorizontalThickness(block.BorderThickness);
            switch (block)
            {
                case Table { Columns.Count: > 0 } table:
                    double tableInsets = blockInsets + HorizontalThickness(table.Padding);
                    minimumWidth = Math.Max(minimumWidth, tableInsets + FitTable(table, availableWidth - tableInsets, pixelsPerDip));
                    break;

                case List list:
                    // WPF's automatic leading list padding reserves two line heights for the marker.
                    double leadingPadding = list.FlowDirection == FlowDirection.LeftToRight ? list.Padding.Left : list.Padding.Right;
                    double markerWidth = double.IsNaN(leadingPadding) ? 2 * GetLineHeight(list) : 0;
                    double listInsets = blockInsets + HorizontalThickness(list.Padding) + markerWidth;
                    foreach (ListItem item in list.ListItems)
                    {
                        double itemMinimum = FitTableColumns(item.Blocks, availableWidth - listInsets, pixelsPerDip);
                        if (itemMinimum > 0)
                            minimumWidth = Math.Max(minimumWidth, listInsets + itemMinimum);
                    }
                    break;
            }
        }
        return minimumWidth;
    }

    private static double FitTable(Table table, double availableWidth, double pixelsPerDip)
    {
        int columnCount = table.Columns.Count;
        double lineHeight = GetLineHeight(table);
        double minimumWidth = lineHeight;
        double[] preferred = new double[columnCount];
        foreach (TableRowGroup group in table.RowGroups)
        {
            foreach (TableRow row in group.Rows)
            {
                int column = 0;
                foreach (TableCell cell in row.Cells)
                {
                    (double width, double singleLineHeight) = MeasureCell(cell, lineHeight, pixelsPerDip, Math.Max(1, availableWidth) / columnCount);
                    // The smallest column is as wide as an empty one-line row is tall.
                    minimumWidth = Math.Max(minimumWidth, singleLineHeight);
                    width /= cell.ColumnSpan;
                    int end = Math.Min(column + cell.ColumnSpan, columnCount);
                    for (; column < end; column++)
                        preferred[column] = Math.Max(preferred[column], width);
                }
            }
        }

        // Protect compact columns before sharing the remaining width among longer cells.
        // Cap each preference at the table width so a single long cell cannot dominate the share.
        double minimumTotal = minimumWidth * columnCount;
        availableWidth = Math.Max(availableWidth, minimumTotal);
        double compactWidth = Math.Max(minimumWidth, 8 * table.FontSize);
        double preferredTotal = 0;
        double compactTotal = 0;
        for (int i = 0; i < columnCount; i++)
        {
            preferred[i] = Math.Clamp(preferred[i], minimumWidth, availableWidth);
            preferredTotal += preferred[i];
            compactTotal += Math.Min(preferred[i], compactWidth);
        }

        for (int i = 0; i < columnCount; i++)
        {
            double width = preferred[i];
            if (compactTotal > availableWidth)
            {
                double compact = Math.Min(width, compactWidth);
                width = minimumWidth + (compact - minimumWidth) * (availableWidth - minimumTotal) / (compactTotal - minimumTotal);
            }
            else if (preferredTotal > availableWidth)
            {
                double compact = Math.Min(width, compactWidth);
                width = compact + (width - compact) * (availableWidth - compactTotal) / (preferredTotal - compactTotal);
            }

            GridLength columnWidth = new(width);
            if (table.Columns[i].Width != columnWidth)
                table.Columns[i].Width = columnWidth;
        }
        return minimumTotal;
    }

    private static (double Width, double SingleLineHeight) MeasureCell(
        TableCell cell, double defaultLineHeight, double pixelsPerDip, double fallbackObjectWidth)
    {
        double width = 0;
        double height = defaultLineHeight;
        foreach (Paragraph paragraph in cell.Blocks.OfType<Paragraph>())
        {
            double lineWidth = 0;
            double widestLine = 0;
            MeasureInlines(paragraph.Inlines, pixelsPerDip, fallbackObjectWidth, ref lineWidth, ref widestLine);
            double paragraphWidth = Math.Max(lineWidth, widestLine) + Math.Max(0, paragraph.TextIndent)
                + HorizontalThickness(paragraph.Margin) + HorizontalThickness(paragraph.Padding) + HorizontalThickness(paragraph.BorderThickness);
            width = Math.Max(width, paragraphWidth);
            height = Math.Max(height, GetLineHeight(paragraph)
                + VerticalThickness(paragraph.Margin) + VerticalThickness(paragraph.Padding) + VerticalThickness(paragraph.BorderThickness));
        }

        // Leave room for the fractional glyph and border rounding in WPF's table layout.
        return (Math.Ceiling(width + HorizontalThickness(cell.Padding) + HorizontalThickness(cell.BorderThickness) + 2),
            height + VerticalThickness(cell.Padding) + VerticalThickness(cell.BorderThickness));
    }

    private static void MeasureInlines(InlineCollection inlines, double pixelsPerDip, double fallbackObjectWidth,
        ref double lineWidth, ref double widestLine)
    {
        foreach (Inline inline in inlines)
        {
            switch (inline)
            {
                case Run run when run.Text.Length > 0:
                    Typeface typeface = new(run.FontFamily, run.FontStyle, run.FontWeight, run.FontStretch);
                    bool first = true;
                    foreach (string part in run.Text.Split('\r', '\n'))
                    {
                        if (!first)
                        {
                            widestLine = Math.Max(widestLine, lineWidth);
                            lineWidth = 0;
                        }
                        first = false;
                        if (part.Length > 0)
                        {
                            FormattedText text = new(part, CultureInfo.CurrentCulture, run.FlowDirection, typeface,
                                run.FontSize, Brushes.Black, null, TextOptions.GetTextFormattingMode(run), pixelsPerDip);
                            lineWidth += text.WidthIncludingTrailingWhitespace;
                        }
                    }
                    break;

                case LineBreak:
                    widestLine = Math.Max(widestLine, lineWidth);
                    lineWidth = 0;
                    break;

                case Span span:
                    MeasureInlines(span.Inlines, pixelsPerDip, fallbackObjectWidth, ref lineWidth, ref widestLine);
                    break;

                case InlineUIContainer { Child: UIElement child }:
                    child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    // An image may still be loading; do not collapse its column to the text width.
                    lineWidth += child.DesiredSize.Width > 0 ? child.DesiredSize.Width : fallbackObjectWidth;
                    break;
            }
        }
    }

    private static double HorizontalThickness(Thickness value) =>
        (double.IsNaN(value.Left) ? 0 : value.Left) + (double.IsNaN(value.Right) ? 0 : value.Right);

    private static double VerticalThickness(Thickness value) =>
        (double.IsNaN(value.Top) ? 0 : value.Top) + (double.IsNaN(value.Bottom) ? 0 : value.Bottom);

    private static double GetLineHeight(Block block) =>
        double.IsNaN(block.LineHeight) ? block.FontFamily.LineSpacing * block.FontSize : block.LineHeight;
}
