using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Text_Grab.Editing;

namespace Text_Grab.Controls;

public partial class MarkdownFormattingBar : UserControl
{
    private const int TablePickerSize = 8;
    private readonly Button[,] tableCells = new Button[TablePickerSize, TablePickerSize];
    private (int Column, int Row)? highlightedCell;

    internal event Action<MarkdownFormattingAction, int>? FormatRequested;
    internal event Action<int, int>? TableRequested;

    internal IEnumerable<Button> FormattingCommandButtons => FormattingButtons.Children.OfType<Button>();

    public MarkdownFormattingBar()
    {
        InitializeComponent();
        TableSizeGrid.Rows = TableSizeGrid.Columns = TablePickerSize;
        Style cellStyle = (Style)FindResource("TableSizeCellStyle");
        for (int row = 0; row < TablePickerSize; row++)
        {
            for (int column = 0; column < TablePickerSize; column++)
            {
                Button cell = new() { Style = cellStyle, Tag = (column, row) };
                AutomationProperties.SetName(cell, $"Columns: {column + 1}, rows: {row + 1}");
                AutomationProperties.SetAutomationId(cell, $"EditText.Markdown.TableSize.{column + 1}x{row + 1}");
                cell.MouseEnter += TableCell_MouseEnter;
                cell.GotKeyboardFocus += TableCell_GotKeyboardFocus;
                cell.Click += TableCell_Click;
                tableCells[column, row] = cell;
                TableSizeGrid.Children.Add(cell);
            }
        }
    }

    internal void UpdateAvailableActions(MarkdownFormattingAction available, bool canInsertTable)
    {
        BoldButton.IsEnabled = available.HasFlag(MarkdownFormattingAction.Bold);
        ItalicButton.IsEnabled = available.HasFlag(MarkdownFormattingAction.Italic);
        StrikethroughButton.IsEnabled = available.HasFlag(MarkdownFormattingAction.Strikethrough);
        SuperscriptButton.IsEnabled = available.HasFlag(MarkdownFormattingAction.Superscript);
        SubscriptButton.IsEnabled = available.HasFlag(MarkdownFormattingAction.Subscript);
        HeadingButton.IsEnabled = available.HasFlag(MarkdownFormattingAction.Heading);
        BulletsButton.IsEnabled = available.HasFlag(MarkdownFormattingAction.Bullets);
        NumberingButton.IsEnabled = available.HasFlag(MarkdownFormattingAction.Numbering);
        QuoteButton.IsEnabled = available.HasFlag(MarkdownFormattingAction.Quote);
        CodeBlockButton.IsEnabled = available.HasFlag(MarkdownFormattingAction.CodeBlock);
        TableButton.IsEnabled = canInsertTable;
        if (!canInsertTable)
            TablePicker.IsOpen = false;
    }

    private void Format_Click(object sender, RoutedEventArgs e)
    {
        MarkdownFormattingAction action = sender switch
        {
            _ when sender == BoldButton => MarkdownFormattingAction.Bold,
            _ when sender == ItalicButton => MarkdownFormattingAction.Italic,
            _ when sender == StrikethroughButton => MarkdownFormattingAction.Strikethrough,
            _ when sender == SuperscriptButton => MarkdownFormattingAction.Superscript,
            _ when sender == SubscriptButton => MarkdownFormattingAction.Subscript,
            _ when sender == BulletsButton => MarkdownFormattingAction.Bullets,
            _ when sender == NumberingButton => MarkdownFormattingAction.Numbering,
            _ when sender == QuoteButton => MarkdownFormattingAction.Quote,
            _ when sender == CodeBlockButton => MarkdownFormattingAction.CodeBlock,
            _ => throw new ArgumentOutOfRangeException(nameof(sender))
        };
        FormatRequested?.Invoke(action, 0);
    }

    private void HeadingButton_Click(object sender, RoutedEventArgs e)
    {
        HeadingButton.ContextMenu.PlacementTarget = HeadingButton;
        HeadingButton.ContextMenu.IsOpen = true;
    }

    private void Heading_Click(object sender, RoutedEventArgs e) =>
        FormatRequested?.Invoke(MarkdownFormattingAction.Heading,
            int.Parse((string)((MenuItem)sender).Tag, CultureInfo.InvariantCulture));

    private void TableButton_Click(object sender, RoutedEventArgs e) => TablePicker.IsOpen = !TablePicker.IsOpen;

    private void TablePicker_Opened(object? sender, EventArgs e)
    {
        HighlightTableSize(0, 0);
        tableCells[0, 0].Focus();
    }

    private void TableCell_MouseEnter(object sender, MouseEventArgs e)
    {
        (int column, int row) = ((int, int))((Button)sender).Tag;
        HighlightTableSize(column, row);
    }

    private void TableCell_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        (int column, int row) = ((int, int))((Button)sender).Tag;
        HighlightTableSize(column, row);
    }

    private void TableCell_Click(object sender, RoutedEventArgs e)
    {
        (int column, int row) = ((int, int))((Button)sender).Tag;
        TablePicker.IsOpen = false;
        TableRequested?.Invoke(column + 1, row + 1);
    }

    private void TablePicker_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            TablePicker.IsOpen = false;
            TableButton.Focus();
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is not Button { Tag: ValueTuple<int, int> cell })
            return;

        (int column, int row) = cell;
        switch (e.Key)
        {
            case Key.Left: column--; break;
            case Key.Right: column++; break;
            case Key.Up: row--; break;
            case Key.Down: row++; break;
            default: return;
        }

        tableCells[Math.Clamp(column, 0, TablePickerSize - 1), Math.Clamp(row, 0, TablePickerSize - 1)].Focus();
        e.Handled = true;
    }

    private void HighlightTableSize(int column, int row)
    {
        if (highlightedCell == (column, row))
            return;

        for (int r = 0; r < TablePickerSize; r++)
        {
            for (int c = 0; c < TablePickerSize; c++)
            {
                Button cell = tableCells[c, r];
                if (c <= column && r <= row)
                    cell.SetResourceReference(BackgroundProperty, "AccentFillColorDefaultBrush");
                else
                    cell.ClearValue(BackgroundProperty);
            }
        }

        highlightedCell = (column, row);
        TableSizeLabel.Text = $"{column + 1} \u00d7 {row + 1} (columns \u00d7 rows)";
    }
}
