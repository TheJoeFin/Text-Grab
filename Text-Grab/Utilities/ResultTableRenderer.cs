using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Text_Grab.Extensions;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

/// <summary>
/// Draws the visual grid lines for a <see cref="ResultTable"/> onto a WPF <see cref="Canvas"/>.
///
/// Split out of <see cref="ResultTable"/> when that class moved to Text-Grab.Core (batch 4d of
/// the Core split) - the pure clustering algorithm and table state are portable, but this
/// rendering step needs Canvas/Border/SolidColorBrush, which are not.
/// </summary>
public static class ResultTableRenderer
{
    public static Canvas BuildTableLines(ResultTable table)
    {
        Rect boundingRect = table.BoundingRect.AsRect();

        // Draw the lines and bounds of the table
        SolidColorBrush tableColor = new(Color.FromArgb(255, 40, 118, 126));

        Canvas tableLines = new()
        {
            Tag = "TableLines"
        };

        Border tableOutline = new()
        {
            Width = boundingRect.Width,
            Height = boundingRect.Height,
            BorderThickness = new Thickness(3),
            BorderBrush = tableColor
        };
        tableLines.Children.Add(tableOutline);
        Canvas.SetTop(tableOutline, boundingRect.Y);
        Canvas.SetLeft(tableOutline, boundingRect.X);

        foreach (double columnLine in table.ColumnLines)
        {
            Border vertLine = new()
            {
                Width = 2,
                Height = boundingRect.Height,
                Background = tableColor
            };
            tableLines.Children.Add(vertLine);
            Canvas.SetTop(vertLine, boundingRect.Y);
            Canvas.SetLeft(vertLine, columnLine);
        }

        foreach (double rowLine in table.RowLines)
        {
            Border horzLine = new()
            {
                Height = 2,
                Width = boundingRect.Width,
                Background = tableColor
            };
            tableLines.Children.Add(horzLine);
            Canvas.SetTop(horzLine, rowLine);
            Canvas.SetLeft(horzLine, boundingRect.X);
        }

        return tableLines;
    }
}
