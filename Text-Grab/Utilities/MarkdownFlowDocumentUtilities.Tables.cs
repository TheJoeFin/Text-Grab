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
        FitTableColumns(document.Blocks, pageWidth - HorizontalThickness(document.PagePadding), pixelsPerDip);
    }

    private static void FitTableColumns(BlockCollection blocks, double availableWidth, double pixelsPerDip)
    {
        if (availableWidth < 1)
            return;

        foreach (Block block in blocks)
        {
            double contentWidth = availableWidth - HorizontalThickness(block.Margin) - HorizontalThickness(block.BorderThickness);
            switch (block)
            {
                case Table { Columns.Count: > 0 } table:
                    FitTable(table, Math.Max(1, contentWidth - HorizontalThickness(table.Padding)), pixelsPerDip);
                    break;

                case List list:
                    // WPF's automatic leading list padding reserves two line heights for the marker.
                    double leadingPadding = list.FlowDirection == FlowDirection.LeftToRight ? list.Padding.Left : list.Padding.Right;
                    double markerWidth = double.IsNaN(leadingPadding)
                        ? 2 * (double.IsNaN(list.LineHeight) ? list.FontFamily.LineSpacing * list.FontSize : list.LineHeight)
                        : 0;
                    foreach (ListItem item in list.ListItems)
                        FitTableColumns(item.Blocks, contentWidth - HorizontalThickness(list.Padding) - markerWidth, pixelsPerDip);
                    break;
            }
        }
    }

    private static void FitTable(Table table, double availableWidth, double pixelsPerDip)
    {
        int columnCount = table.Columns.Count;
        double[] preferred = new double[columnCount];
        foreach (TableRowGroup group in table.RowGroups)
        {
            foreach (TableRow row in group.Rows)
            {
                int column = 0;
                foreach (TableCell cell in row.Cells)
                {
                    double width = MeasureCell(cell, pixelsPerDip, availableWidth / columnCount) / cell.ColumnSpan;
                    int end = Math.Min(column + cell.ColumnSpan, columnCount);
                    for (; column < end; column++)
                        preferred[column] = Math.Max(preferred[column], width);
                }
            }
        }

        // Protect compact columns before sharing the remaining width among longer cells.
        // Cap each preference at the table width so a single long cell cannot dominate the share.
        double compactWidth = 8 * table.FontSize;
        double preferredTotal = 0;
        double compactTotal = 0;
        for (int i = 0; i < columnCount; i++)
        {
            preferred[i] = Math.Clamp(preferred[i], 1, availableWidth);
            preferredTotal += preferred[i];
            compactTotal += Math.Min(preferred[i], compactWidth);
        }

        for (int i = 0; i < columnCount; i++)
        {
            double width = preferred[i];
            if (compactTotal >= availableWidth)
            {
                width = Math.Min(width, compactWidth) * availableWidth / compactTotal;
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
    }

    private static double MeasureCell(TableCell cell, double pixelsPerDip, double fallbackObjectWidth)
    {
        double width = 0;
        foreach (Paragraph paragraph in cell.Blocks.OfType<Paragraph>())
        {
            double lineWidth = 0;
            double widestLine = 0;
            MeasureInlines(paragraph.Inlines, pixelsPerDip, fallbackObjectWidth, ref lineWidth, ref widestLine);
            double paragraphWidth = Math.Max(lineWidth, widestLine) + Math.Max(0, paragraph.TextIndent)
                + HorizontalThickness(paragraph.Margin) + HorizontalThickness(paragraph.Padding) + HorizontalThickness(paragraph.BorderThickness);
            width = Math.Max(width, paragraphWidth);
        }

        // Leave room for the fractional glyph and border rounding in WPF's table layout.
        return Math.Ceiling(width + HorizontalThickness(cell.Padding) + HorizontalThickness(cell.BorderThickness) + 2);
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
}
