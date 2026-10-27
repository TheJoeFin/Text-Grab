using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Extensions;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

/// <summary>
/// Which corner of the table's bounding rectangle a <see cref="Thumb"/> drag handle controls.
/// </summary>
public enum TableBoundsCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>
/// Draws the visual grid lines for a <see cref="ResultTable"/> onto a WPF <see cref="Canvas"/>.
///
/// Split out of <see cref="ResultTable"/> when that class moved to Text-Grab.Core (batch 4d of
/// the Core split) - the pure clustering algorithm and table state are portable, but this
/// rendering step needs Canvas/Border/SolidColorBrush, which are not.
/// </summary>
public static class ResultTableRenderer
{
    private const double HandleSize = 10;

    public static Canvas BuildTableLines(ResultTable table, bool includeBoundsHandles = false)
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

        if (includeBoundsHandles)
        {
            tableLines.Children.Add(BuildBoundsHandle(boundingRect.Left, boundingRect.Top, TableBoundsCorner.TopLeft, tableColor));
            tableLines.Children.Add(BuildBoundsHandle(boundingRect.Right, boundingRect.Top, TableBoundsCorner.TopRight, tableColor));
            tableLines.Children.Add(BuildBoundsHandle(boundingRect.Left, boundingRect.Bottom, TableBoundsCorner.BottomLeft, tableColor));
            tableLines.Children.Add(BuildBoundsHandle(boundingRect.Right, boundingRect.Bottom, TableBoundsCorner.BottomRight, tableColor));
        }

        return tableLines;
    }

    private static Thumb BuildBoundsHandle(double centerX, double centerY, TableBoundsCorner corner, Brush fillBrush)
    {
        Thumb handle = new()
        {
            Width = HandleSize,
            Height = HandleSize,
            Tag = corner,
            Cursor = corner is TableBoundsCorner.TopLeft or TableBoundsCorner.BottomRight
                ? Cursors.SizeNWSE
                : Cursors.SizeNESW,
            Template = BuildHandleTemplate(fillBrush)
        };

        Canvas.SetLeft(handle, centerX - (HandleSize / 2));
        Canvas.SetTop(handle, centerY - (HandleSize / 2));

        return handle;
    }

    private static ControlTemplate BuildHandleTemplate(Brush fillBrush)
    {
        FrameworkElementFactory borderFactory = new(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, fillBrush);
        borderFactory.SetValue(Border.BorderBrushProperty, Brushes.White);
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));

        return new ControlTemplate(typeof(Thumb))
        {
            VisualTree = borderFactory
        };
    }
}
