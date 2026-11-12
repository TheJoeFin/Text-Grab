using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Text_Grab;

public partial class EditTextWindow
{
    private sealed class SpreadsheetCellTextWrappingConverter(EditTextWindow owner, int columnIndex) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return owner.GetSpreadsheetCellTextWrapping(value, columnIndex);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Windows.Data.Binding.DoNothing;
        }
    }

    internal void EnterSpreadsheetMode() => SetEditorMode(EtwEditorMode.Spreadsheet);

    /// <summary>
    /// Pastes the clipboard into the spreadsheet editor, parsing HTML tables
    /// (including colspan/rowspan) when present. Used by the text-grab://
    /// paste-spreadsheet protocol activation.
    /// </summary>
    internal void PasteClipboardIntoSpreadsheet() => PasteIntoSpreadsheet();

    private void ApplySpreadsheetDocumentChange(
        Action<EditTextTableDocument> changeAction,
        int? focusRow = null,
        int? focusColumn = null,
        bool beginEdit = true)
    {
        CommitSpreadsheetEditsAndCapturePendingHistory();
        SpreadsheetUndoState? beforeChange = CreateCurrentSpreadsheetUndoState(syncFromTable: true);

        if (tableDocument is null)
            return;

        changeAction(tableDocument);
        tableDocument.EnsureMinimumSize();
        RecordSpreadsheetUndoChange(beforeChange, CreateCurrentSpreadsheetUndoState(syncFromTable: false));
        RebuildSpreadsheetTable();
        UpdateTextFromSpreadsheetDocument();

        if (focusRow.HasValue && focusColumn.HasValue)
        {
            Dispatcher.BeginInvoke(
                () => FocusSpreadsheetCell(focusRow.Value, focusColumn.Value, beginEdit),
                DispatcherPriority.Background);
        }
    }

    private SpreadsheetUndoState? CreateCurrentSpreadsheetUndoState(bool syncFromTable = false)
    {
        if (syncFromTable && editorMode == EtwEditorMode.Spreadsheet)
            SyncSpreadsheetDocumentFromTable(writeText: false);

        if (tableDocument is null)
            return null;

        tableDocument.EnsureMinimumSize();
        return new SpreadsheetUndoState(
            tableDocument.SerializeToJson(),
            GetSpreadsheetCurrentRowIndex(),
            GetSpreadsheetCurrentColumnIndex());
    }

    private int? GetSpreadsheetCurrentRowIndex()
    {
        int rowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        return rowIndex >= 0 ? rowIndex : null;
    }

    private int? GetSpreadsheetCurrentColumnIndex()
    {
        return SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex;
    }

    private void CommitSpreadsheetEditsAndCapturePendingHistory()
    {
        if (editorMode != EtwEditorMode.Spreadsheet)
            return;

        _ = SpreadsheetDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        _ = SpreadsheetDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        CaptureCommittedSpreadsheetEditIfPending();
    }

    private void CaptureCommittedSpreadsheetEditIfPending()
    {
        if (pendingSpreadsheetUndoState is null || isRestoringSpreadsheetUndoState)
            return;

        SpreadsheetUndoState beforeChange = pendingSpreadsheetUndoState;
        pendingSpreadsheetUndoState = null;
        RecordSpreadsheetUndoChange(beforeChange, CreateCurrentSpreadsheetUndoState(syncFromTable: true));
    }

    private void RecordSpreadsheetUndoChange(SpreadsheetUndoState? beforeChange, SpreadsheetUndoState? afterChange)
    {
        spreadsheetUndoHistory.RecordChange(beforeChange, afterChange);
        CommandManager.InvalidateRequerySuggested();
    }

    private void ResetSpreadsheetUndoHistory()
    {
        bool wasNonEmpty = spreadsheetUndoHistory.CanUndo || spreadsheetUndoHistory.CanRedo;
        spreadsheetUndoHistory.Clear();
        pendingSpreadsheetUndoState = null;
        // Only invalidate when the undo/redo availability actually changed to avoid
        // triggering all CanExecute handlers on every text change in text mode.
        if (wasNonEmpty)
            CommandManager.InvalidateRequerySuggested();
    }

    private void RestoreSpreadsheetUndoState(SpreadsheetUndoState stateToRestore)
    {
        EditTextTableDocument? restoredDocument = EditTextTableDocument.TryDeserialize(stateToRestore.DocumentJson);
        if (restoredDocument is null)
            return;

        isRestoringSpreadsheetUndoState = true;
        try
        {
            pendingSpreadsheetUndoState = null;
            tableDocument = restoredDocument;
            RebuildSpreadsheetTable();
            UpdateTextFromSpreadsheetDocument();
        }
        finally
        {
            isRestoringSpreadsheetUndoState = false;
        }

        if (SpreadsheetDataGrid.Items.Count == 0 || SpreadsheetDataGrid.Columns.Count == 0)
        {
            UpdateLineAndColumnText();
            return;
        }

        int focusRow = Math.Clamp(stateToRestore.FocusRow ?? 0, 0, SpreadsheetDataGrid.Items.Count - 1);
        int focusColumn = Math.Clamp(stateToRestore.FocusColumn ?? 0, 0, SpreadsheetDataGrid.Columns.Count - 1);

        Dispatcher.BeginInvoke(
            () => FocusSpreadsheetCell(focusRow, focusColumn, beginEdit: false),
            DispatcherPriority.Background);
        UpdateLineAndColumnText();
    }

    private void CopySpreadsheetColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int columnIndex =
            spreadsheetContextColumnIndex
            ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex
            ?? -1;

        if (columnIndex < 0)
            return;
        List<string> values = [];

        foreach (DataRow row in spreadsheetTable.Rows)
        {
            if (columnIndex >= spreadsheetTable.Columns.Count)
                break;

            values.Add(row[columnIndex]?.ToString() ?? string.Empty);
        }

        TrySetClipboardText(string.Join(Environment.NewLine, values));
    }

    private void CopySpreadsheetRowsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        List<DataRowView> selectedRows = [.. SpreadsheetDataGrid.SelectedItems.OfType<DataRowView>()];

        if (selectedRows.Count == 0 && SpreadsheetDataGrid.CurrentItem is DataRowView currentRow)
            selectedRows.Add(currentRow);

        if (selectedRows.Count == 0)
            return;

        string rowText = string.Join(
            Environment.NewLine,
            selectedRows.Select(row => string.Join("\t", row.Row.ItemArray.Select(value => value?.ToString() ?? string.Empty))));

        TrySetClipboardText(rowText);
    }

    private void CopySpreadsheetSelectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = TryCopySpreadsheetSelectionToClipboard(GetSelectedSpreadsheetCellCoordinates());
    }

    private void CopySpreadsheetAsMarkdownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string markdownText = BuildSpreadsheetSelectionMarkdown(
            spreadsheetTable,
            GetSelectedOrPopulatedSpreadsheetCellCoordinates());

        if (!string.IsNullOrEmpty(markdownText))
            TrySetClipboardText(markdownText);
    }

    private void AddSpreadsheetColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int currentColumnIndex =
            spreadsheetContextColumnIndex
            ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex
            ?? ((tableDocument?.ColumnCount ?? 1) - 1);
        int insertIndex = Math.Clamp(currentColumnIndex + 1, 0, Math.Max(tableDocument?.ColumnCount ?? 0, 0));

        ApplySpreadsheetDocumentChange(
            document => document.InsertColumn(insertIndex),
            SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem),
            insertIndex);
    }

    private void AddSpreadsheetRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int currentRowIndex = spreadsheetContextRowIndex ?? SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        if (currentRowIndex < 0)
            currentRowIndex = (tableDocument?.RowCount ?? 1) - 1;

        int insertIndex = Math.Clamp(currentRowIndex + 1, 0, Math.Max(tableDocument?.RowCount ?? 0, 0));
        int focusColumn = SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0;

        ApplySpreadsheetDocumentChange(
            document => document.InsertRow(insertIndex),
            insertIndex,
            focusColumn);
    }

    private void SplitSpreadsheetCellsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet)
            return;

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrCurrentSpreadsheetCellCoordinates();
        if (targetCells.Count == 0)
            return;

        (int rowIndex, int columnIndex) = targetCells[0];
        string sampleText = string.Empty;
        if (rowIndex >= 0 && rowIndex < spreadsheetTable.Rows.Count
            && columnIndex >= 0 && columnIndex < spreadsheetTable.Columns.Count)
        {
            sampleText = spreadsheetTable.Rows[rowIndex][columnIndex]?.ToString() ?? string.Empty;
        }

        SplitColumnWindow splitColumnWindow = new()
        {
            Owner = this,
            SampleText = sampleText
        };
        splitColumnWindow.ShowDialog();
    }

    internal void SplitSelectedSpreadsheetCells(SplitColumnOptions options)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || tableDocument is null)
            return;

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrCurrentSpreadsheetCellCoordinates();
        if (targetCells.Count == 0)
            return;

        ApplySpreadsheetDocumentChange(document =>
        {
            // Compute the split for each target cell before mutating the table.
            List<(int RowIndex, int ColumnIndex, IReadOnlyList<string> Parts)> splits = [];
            int maxRequiredColumns = document.ColumnCount;

            foreach ((int rowIndex, int columnIndex) in targetCells)
            {
                if (rowIndex < 0 || rowIndex >= document.Rows.Count
                    || columnIndex < 0 || columnIndex >= document.ColumnCount)
                {
                    continue;
                }

                string cellValue = document.Rows[rowIndex][columnIndex] ?? string.Empty;
                IReadOnlyList<string> parts = ColumnSplitUtilities.SplitCell(cellValue, options);
                splits.Add((rowIndex, columnIndex, parts));
                maxRequiredColumns = Math.Max(maxRequiredColumns, columnIndex + parts.Count);
            }

            // Grow the table so every row has room for the widest split, then pad all rows.
            document.ColumnCount = Math.Max(document.ColumnCount, maxRequiredColumns);
            document.MinimumColumnCount = Math.Max(document.MinimumColumnCount, maxRequiredColumns);
            document.EnsureMinimumSize();

            // Overwrite the original cell and the cells to its right with the resulting parts.
            foreach ((int rowIndex, int columnIndex, IReadOnlyList<string> parts) in splits)
            {
                for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                {
                    int targetColumn = columnIndex + partIndex;
                    if (rowIndex < document.Rows.Count && targetColumn < document.Rows[rowIndex].Count)
                        document.Rows[rowIndex][targetColumn] = parts[partIndex];
                }
            }
        });
    }

    private void TransposeTableExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        int currentRowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        int currentColumnIndex = SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0;

        ApplySpreadsheetDocumentChange(document => document.Transpose());

        if (SpreadsheetDataGrid.Items.Count == 0 || SpreadsheetDataGrid.Columns.Count == 0)
            return;

        int focusRow = Math.Clamp(currentColumnIndex, 0, SpreadsheetDataGrid.Items.Count - 1);
        int focusColumn = Math.Clamp(Math.Max(0, currentRowIndex), 0, SpreadsheetDataGrid.Columns.Count - 1);

        Dispatcher.BeginInvoke(
            () => FocusSpreadsheetCell(focusRow, focusColumn),
            DispatcherPriority.Background);
    }

    private void DeleteSpreadsheetColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int columnIndex = spreadsheetContextColumnIndex ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? -1;
        if (columnIndex < 0)
            return;

        int nextColumnIndex = Math.Max(0, Math.Min(columnIndex, (tableDocument?.ColumnCount ?? 1) - 2));
        int rowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);

        ApplySpreadsheetDocumentChange(
            document => document.DeleteColumn(columnIndex),
            Math.Max(0, rowIndex),
            nextColumnIndex);
    }

    private void DeleteSpreadsheetRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        int rowIndex = spreadsheetContextRowIndex ?? SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        if (rowIndex < 0)
            return;

        int nextRowIndex = Math.Max(0, Math.Min(rowIndex, (tableDocument?.RowCount ?? 1) - 2));
        int columnIndex = SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0;

        ApplySpreadsheetDocumentChange(
            document => document.DeleteRow(rowIndex),
            nextRowIndex,
            columnIndex);
    }

    private void EnsureSpreadsheetDocumentFromText()
    {
        if (tableDocument is not null)
        {
            tableDocument.EnsureMinimumSize();
            return;
        }

        tableDocument = EditTextTableDocument.CreateFromText(Session.Text);
    }

    private void FocusSpreadsheetCell(int rowIndex, int columnIndex, bool beginEdit = true)
    {
        if (rowIndex < 0
            || columnIndex < 0
            || rowIndex >= SpreadsheetDataGrid.Items.Count
            || columnIndex >= SpreadsheetDataGrid.Columns.Count)
        {
            return;
        }

        object rowItem = SpreadsheetDataGrid.Items[rowIndex];
        DataGridColumn column = SpreadsheetDataGrid.Columns[columnIndex];

        SelectSpreadsheetCell(rowItem, column, clearExistingSelection: true);

        if (beginEdit)
            SpreadsheetDataGrid.BeginEdit();
    }

    private void MoveSpreadsheetColumnLeftMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveSpreadsheetColumn(-1);
    }

    private void MoveSpreadsheetColumnRightMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveSpreadsheetColumn(1);
    }

    private void MoveSpreadsheetColumn(int direction)
    {
        int fromIndex = spreadsheetContextColumnIndex ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? -1;
        if (fromIndex < 0)
            return;

        int toIndex = fromIndex + direction;
        if (toIndex < 0 || toIndex >= (tableDocument?.ColumnCount ?? 0))
            return;

        int rowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        ApplySpreadsheetDocumentChange(
            document => document.MoveColumn(fromIndex, toIndex),
            Math.Max(0, rowIndex),
            toIndex);
    }

    private void MoveSpreadsheetRowDownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveSpreadsheetRow(1);
    }

    private void MoveSpreadsheetRowUpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveSpreadsheetRow(-1);
    }

    private void MoveSpreadsheetRow(int direction)
    {
        int fromIndex = spreadsheetContextRowIndex ?? SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        if (fromIndex < 0)
            return;

        int toIndex = fromIndex + direction;
        if (toIndex < 0 || toIndex >= (tableDocument?.RowCount ?? 0))
            return;

        int columnIndex = SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0;
        ApplySpreadsheetDocumentChange(
            document => document.MoveRow(fromIndex, toIndex),
            toIndex,
            columnIndex);
    }

    private void RebuildSpreadsheetTable()
    {
        if (tableDocument is null)
            return;

        DetachSpreadsheetColumnWidthTracking();
        isApplyingSpreadsheetLayout = true;
        spreadsheetTable.BeginInit();
        spreadsheetTable.Clear();
        spreadsheetTable.Columns.Clear();

        foreach (string columnName in tableDocument.ColumnNames)
            spreadsheetTable.Columns.Add(columnName, typeof(string));

        foreach (List<string> row in tableDocument.Rows)
        {
            DataRow dataRow = spreadsheetTable.NewRow();
            for (int columnIndex = 0; columnIndex < tableDocument.ColumnNames.Count; columnIndex++)
                dataRow[columnIndex] = columnIndex < row.Count ? row[columnIndex] ?? string.Empty : string.Empty;

            spreadsheetTable.Rows.Add(dataRow);
        }

        spreadsheetTable.EndInit();

        SpreadsheetDataGrid.ItemsSource = spreadsheetTable.DefaultView;
        selectedSpreadsheetCellCoordinates = [];
        SpreadsheetDataGrid.Columns.Clear();

        for (int columnIndex = 0; columnIndex < spreadsheetTable.Columns.Count; columnIndex++)
        {
            DataColumn column = spreadsheetTable.Columns[columnIndex];
            double width = tableDocument.ColumnWidths.ElementAtOrDefault(columnIndex) ?? SpreadsheetDefaultColumnWidth;
            DataGridTextColumn gridColumn = new()
            {
                Header = EditTextTableDocument.GetSpreadsheetColumnLabel(columnIndex),
                Binding = new System.Windows.Data.Binding($"[{column.ColumnName}]"),
                ElementStyle = CreateSpreadsheetDisplayTextStyle(columnIndex),
                EditingElementStyle = CreateSpreadsheetEditingTextStyle(columnIndex),
                MinWidth = SpreadsheetDefaultColumnWidth,
                Width = new DataGridLength(Math.Max(SpreadsheetDefaultColumnWidth, width)),
            };

            SpreadsheetDataGrid.Columns.Add(gridColumn);
            TrackSpreadsheetColumnWidth(gridColumn);
        }

        SpreadsheetDataGrid.Items.Refresh();
        isApplyingSpreadsheetLayout = false;
    }

    /// <summary>
    /// <see cref="Editing.SpreadsheetSurface"/>'s half of syncing the spreadsheet from the text: rebuilds the
    /// table document (keeping its column widths and row heights) and the grid. The session skips it
    /// while the text is being written from the spreadsheet.
    /// </summary>
    private void RefreshSpreadsheetFromText(string text)
    {
        EditTextTableDocument? existingDocument = tableDocument;
        tableDocument = EditTextTableDocument.CreateFromText(
            text,
            existingDocument?.MinimumRowCount ?? EditTextTableDocument.DefaultMinimumRowCount,
            existingDocument?.MinimumColumnCount ?? EditTextTableDocument.DefaultMinimumColumnCount);

        if (existingDocument is not null)
            tableDocument.ApplyViewMetricsFrom(existingDocument);

        RebuildSpreadsheetTable();
        UpdateLineAndColumnText();
    }

    /// <summary>
    /// <see cref="Editing.SpreadsheetSurface"/>'s half of syncing the text from the spreadsheet: commits the
    /// grid into the table document and serializes it.
    /// </summary>
    private string? GetSpreadsheetTextFromTable()
    {
        SyncSpreadsheetDocumentFromTable(writeText: false);
        return tableDocument?.SerializeToText();
    }

    private void SpreadsheetDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (isRestoringSpreadsheetUndoState)
            return;

        pendingSpreadsheetUndoState = CreateCurrentSpreadsheetUndoState(syncFromTable: true);
    }

    private void SpreadsheetDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        DetachSpreadsheetCellEditor();

        if (e.EditAction == DataGridEditAction.Cancel)
        {
            pendingSpreadsheetUndoState = null;
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                CaptureCommittedSpreadsheetEditIfPending();
                UpdateLineAndColumnText();
            },
            DispatcherPriority.Background);
    }

    private void SpreadsheetDataGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        DetachSpreadsheetCellEditor();

        if (e.EditingElement is not System.Windows.Controls.TextBox cellTextBox)
            return;

        activeSpreadsheetCellEditor = cellTextBox;
        lastSpreadsheetCellEditorSelectedText = cellTextBox.SelectedText;
        cellTextBox.SelectionChanged += SpreadsheetCellEditor_SelectionChanged;
    }

    private void SpreadsheetCellEditor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox cellTextBox)
            lastSpreadsheetCellEditorSelectedText = cellTextBox.SelectedText;
    }

    private void DetachSpreadsheetCellEditor()
    {
        activeSpreadsheetCellEditor?.SelectionChanged -= SpreadsheetCellEditor_SelectionChanged;

        activeSpreadsheetCellEditor = null;
    }

    private void SpreadsheetDataGrid_CurrentCellChanged(object sender, EventArgs e)
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            UpdateLineAndColumnText();
    }

    private void SpreadsheetDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        int rowIndex = e.Row.GetIndex();
        e.Row.Header = (rowIndex + 1).ToString(CultureInfo.InvariantCulture);
        e.Row.SizeChanged -= SpreadsheetRow_SizeChanged;
        e.Row.SizeChanged += SpreadsheetRow_SizeChanged;

        double? rowHeight = tableDocument?.RowHeights.ElementAtOrDefault(rowIndex);
        if (rowHeight.HasValue)
            e.Row.Height = rowHeight.Value;
        else
            e.Row.ClearValue(HeightProperty);
    }

    private void SpreadsheetDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            if (!ShouldHandleSpreadsheetDeleteKey(SpreadsheetDataGrid.SelectedCells.Count, IsSpreadsheetCellEditorFocused()))
                return;

            e.Handled = true;
            ClearSelectedSpreadsheetCellValues();
            return;
        }

        if (e.Key == Key.C
            && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            && !IsSpreadsheetCellEditorFocused())
        {
            e.Handled = true;
            _ = TryCopySpreadsheetSelectionToClipboard(GetSelectedSpreadsheetCellCoordinates());
            return;
        }

        if (e.Key == Key.X
            && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            && !IsSpreadsheetCellEditorFocused())
        {
            e.Handled = true;
            _ = TryCutSelectedSpreadsheetCellValues();
            return;
        }

        if (e.Key == Key.V
            && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            && !IsSpreadsheetCellEditorFocused())
        {
            e.Handled = true;
            PasteIntoSpreadsheet();
            return;
        }

    }

    private void SpreadsheetDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        spreadsheetContextRowIndex = null;
        spreadsheetContextColumnIndex = null;

        if (FindVisualParent<Thumb>(e.OriginalSource as DependencyObject) is not null)
            return;

        if (FindVisualParent<DataGridColumnHeader>(e.OriginalSource as DependencyObject) is DataGridColumnHeader columnHeader
            && columnHeader.Column is DataGridColumn dataGridColumn)
        {
            SelectSpreadsheetColumn(dataGridColumn.DisplayIndex);
            e.Handled = true;
            return;
        }

        if (FindVisualParent<DataGridRowHeader>(e.OriginalSource as DependencyObject) is DataGridRowHeader rowHeader
            && rowHeader.DataContext is not null)
        {
            SelectSpreadsheetRow(rowHeader.DataContext);
            e.Handled = true;
        }
    }

    private void SpreadsheetDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        spreadsheetContextRowIndex = null;
        spreadsheetContextColumnIndex = null;

        if (FindVisualParent<Thumb>(e.OriginalSource as DependencyObject) is not null)
            return;

        if (FindVisualParent<DataGridColumnHeader>(e.OriginalSource as DependencyObject) is DataGridColumnHeader columnHeader
            && columnHeader.Column is DataGridColumn dataGridColumn)
        {
            spreadsheetContextColumnIndex = dataGridColumn.DisplayIndex;
            SelectSpreadsheetColumn(dataGridColumn.DisplayIndex);
            SpreadsheetDataGrid.ContextMenu = FindResource("SpreadsheetColumnHeaderContextMenu") as ContextMenu;
            return;
        }

        if (FindVisualParent<DataGridRowHeader>(e.OriginalSource as DependencyObject) is DataGridRowHeader rowHeader
            && rowHeader.DataContext is not null)
        {
            spreadsheetContextRowIndex = SpreadsheetDataGrid.Items.IndexOf(rowHeader.DataContext);
            SelectSpreadsheetRow(rowHeader.DataContext);
            SpreadsheetDataGrid.ContextMenu = FindResource("SpreadsheetRowHeaderContextMenu") as ContextMenu;
            return;
        }

        if (FindVisualParent<System.Windows.Controls.DataGridCell>(e.OriginalSource as DependencyObject) is System.Windows.Controls.DataGridCell dataGridCell
            && dataGridCell.DataContext is not null
            && dataGridCell.Column is DataGridColumn clickedCellColumn)
        {
            spreadsheetContextRowIndex = SpreadsheetDataGrid.Items.IndexOf(dataGridCell.DataContext);
            spreadsheetContextColumnIndex = clickedCellColumn.DisplayIndex;

            bool isCellAlreadySelected = GetSelectedSpreadsheetCellCoordinates().Contains((spreadsheetContextRowIndex.Value, spreadsheetContextColumnIndex.Value));
            SelectSpreadsheetCell(dataGridCell.DataContext, clickedCellColumn, clearExistingSelection: !isCellAlreadySelected);
            SpreadsheetDataGrid.ContextMenu = FindResource("SpreadsheetContextMenu") as ContextMenu;
            return;
        }

        SpreadsheetDataGrid.ContextMenu = FindResource("SpreadsheetContextMenu") as ContextMenu;
    }

    private void SpreadsheetDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        if (activeSpreadsheetCellEditor is null)
            lastSpreadsheetCellEditorSelectedText = string.Empty;

        UpdateSelectedSpreadsheetCellCoordinates();

        if (editorMode == EtwEditorMode.Spreadsheet)
        {
            UpdateLineAndColumnText();

            if (CalcResultsTextControl.Visibility == Visibility.Visible)
            {
                UpdateCalcPaneTextDisplay();
                UpdateAggregateStatusDisplay();
            }
        }
    }

    private void ClearSelectedSpreadsheetCellValues()
    {
        ClearSpreadsheetCellValuesAndSync(GetSelectedSpreadsheetCellCoordinates());
    }

    internal static void ClearSpreadsheetCellValues(DataTable dataTable, IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        foreach ((int rowIndex, int columnIndex) in cellCoordinates.Distinct())
        {
            if (rowIndex < 0
                || rowIndex >= dataTable.Rows.Count
                || columnIndex < 0
                || columnIndex >= dataTable.Columns.Count)
            {
                continue;
            }

            dataTable.Rows[rowIndex][columnIndex] = string.Empty;
        }
    }

    internal static bool TryCutSpreadsheetCellValues(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates,
        Func<string, bool> trySetClipboardText)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(cellCoordinates);
        ArgumentNullException.ThrowIfNull(trySetClipboardText);

        string selectionText = BuildSpreadsheetSelectionText(dataTable, cellCoordinates);
        if (string.IsNullOrEmpty(selectionText) || !trySetClipboardText(selectionText))
            return false;

        ClearSpreadsheetCellValues(dataTable, cellCoordinates);
        return true;
    }

    private void PasteIntoSpreadsheet()
    {
        string clipboardText;
        try
        {
            if (!ClipboardUtilities.TryGetHtmlTableAsTabSeparated(out clipboardText))
                clipboardText = System.Windows.Clipboard.GetText();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PasteIntoSpreadsheet: clipboard read failed. {ex.Message}");
            return;
        }

        if (string.IsNullOrEmpty(clipboardText))
            return;

        if (AppUtilities.TextGrabSettings.EtwNormalizeLineEndingsOnPaste)
            clipboardText = NormalizeLineEndings(clipboardText);

        int startRow = Math.Max(0, SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem));
        int startCol = Math.Max(0, SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0);

        List<string[]> pastedRows = EditTextTableDocument.ParseTabSeparatedRows(clipboardText);
        if (pastedRows.Count == 0)
            return;

        ApplySpreadsheetDocumentChange(
            document => WriteGridIntoSpreadsheetDocument(document, pastedRows, startRow, startCol),
            startRow,
            startCol);
    }

    internal static void WriteGridIntoSpreadsheetDocument(
        EditTextTableDocument document,
        List<string[]> gridRows,
        int startRow,
        int startCol)
    {
        int maxGridCols = gridRows.Max(row => row.Length);

        // Expand the document to fit the incoming data if necessary
        int requiredRows = startRow + gridRows.Count;
        int requiredCols = startCol + maxGridCols;
        document.RowCount = Math.Max(document.RowCount, requiredRows);
        document.ColumnCount = Math.Max(document.ColumnCount, requiredCols);
        document.MinimumRowCount = Math.Max(document.MinimumRowCount, requiredRows);
        document.MinimumColumnCount = Math.Max(document.MinimumColumnCount, requiredCols);
        document.EnsureMinimumSize();

        // Write values into the target cells
        for (int r = 0; r < gridRows.Count; r++)
        {
            int targetRow = startRow + r;
            for (int c = 0; c < gridRows[r].Length; c++)
            {
                int targetCol = startCol + c;
                if (targetRow < document.Rows.Count && targetCol < document.Rows[targetRow].Count)
                    document.Rows[targetRow][targetCol] = gridRows[r][c];
            }
        }
    }

    internal static void WriteAggregateResultIntoSpreadsheetDocument(
        EditTextTableDocument document,
        string resultText,
        int targetRow,
        int targetColumn)
    {
        // An aggregate is one result, not a per-cell transform or a pasted table. Keep even
        // multiline/tabbed output in the anchor cell, leaving every other cell untouched.
        WriteGridIntoSpreadsheetDocument(document, [[resultText]], targetRow, targetColumn);
    }

    /// <summary>
    /// Inserts an OCR grab result into this Spreadsheet-mode window through the structured
    /// table model rather than splicing raw text into the (hidden, while in this mode)
    /// underlying text box — that box's selection/cursor doesn't track the DataGrid's current
    /// cell, so splicing into it corrupts whatever row happens to sit at that stale position.
    /// A single cell (no row/column structure) replaces the currently selected spreadsheet
    /// cell; a real multi-cell table is appended as new rows at the bottom instead, landing
    /// column-aware. Returns true when it handled the insert itself.
    /// </summary>
    internal bool TryInsertGrabbedTextIntoSpreadsheet(string grabbedText)
    {
        if (editorMode != EtwEditorMode.Spreadsheet)
            return false;

        List<string[]> parsedRows = EditTextTableDocument.ParseTabSeparatedRows(grabbedText);
        if (parsedRows.Count == 0)
            return false;

        if (EditTextTableDocument.IsSingleCellGrid(parsedRows))
        {
            int targetRow = Math.Max(0, GetSpreadsheetCurrentRowIndex() ?? 0);
            int targetColumn = Math.Max(0, GetSpreadsheetCurrentColumnIndex() ?? 0);

            ApplySpreadsheetDocumentChange(document =>
                WriteGridIntoSpreadsheetDocument(document, parsedRows, targetRow, targetColumn));

            return true;
        }

        ApplySpreadsheetDocumentChange(document =>
            WriteGridIntoSpreadsheetDocument(document, parsedRows, document.GetFirstFullyEmptyRowIndex(), 0));

        return true;
    }

    internal static string BuildSpreadsheetSelectionText(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        List<(int RowIndex, int ColumnIndex)> validCoordinates = [.. cellCoordinates
            .Distinct()
            .Where(cell => cell.RowIndex >= 0
                && cell.RowIndex < dataTable.Rows.Count
                && cell.ColumnIndex >= 0
                && cell.ColumnIndex < dataTable.Columns.Count)];

        if (validCoordinates.Count == 0)
            return string.Empty;

        return string.Join(
            Environment.NewLine,
            validCoordinates
                .GroupBy(cell => cell.RowIndex)
                .OrderBy(group => group.Key)
                .Select(group => string.Join(
                    "\t",
                    group.OrderBy(cell => cell.ColumnIndex)
                        .Select(cell => dataTable.Rows[cell.RowIndex][cell.ColumnIndex]?.ToString() ?? string.Empty))));
    }

    internal static string BuildSpreadsheetSelectionHtml(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        List<(int RowIndex, int ColumnIndex)> validCoordinates = [.. cellCoordinates
            .Distinct()
            .Where(cell => cell.RowIndex >= 0
                && cell.RowIndex < dataTable.Rows.Count
                && cell.ColumnIndex >= 0
                && cell.ColumnIndex < dataTable.Columns.Count)];

        if (validCoordinates.Count == 0)
            return string.Empty;

        List<List<string>> rows = [.. validCoordinates
            .GroupBy(cell => cell.RowIndex)
            .OrderBy(group => group.Key)
            .Select(group => group
                .OrderBy(cell => cell.ColumnIndex)
                .Select(cell => dataTable.Rows[cell.RowIndex][cell.ColumnIndex]?.ToString() ?? string.Empty)
                .ToList())];

        return CfHtmlTableUtilities.BuildCfHtmlTable(rows);
    }

    internal static string BuildSpreadsheetSelectionMarkdown(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        List<(int RowIndex, int ColumnIndex)> validCoordinates = [.. cellCoordinates
            .Distinct()
            .Where(cell => cell.RowIndex >= 0
                && cell.RowIndex < dataTable.Rows.Count
                && cell.ColumnIndex >= 0
                && cell.ColumnIndex < dataTable.Columns.Count)];

        if (validCoordinates.Count == 0)
            return string.Empty;

        List<int> rowIndices = [.. validCoordinates.Select(cell => cell.RowIndex).Distinct().OrderBy(index => index)];
        List<int> columnIndices = [.. validCoordinates.Select(cell => cell.ColumnIndex).Distinct().OrderBy(index => index)];

        StringBuilder builder = new();

        for (int rowPosition = 0; rowPosition < rowIndices.Count; rowPosition++)
        {
            int rowIndex = rowIndices[rowPosition];
            IEnumerable<string> cellTexts = columnIndices.Select(columnIndex =>
                EscapeMarkdownTableCell(dataTable.Rows[rowIndex][columnIndex]?.ToString() ?? string.Empty));

            builder.Append($"| {string.Join(" | ", cellTexts)} |");
            builder.Append(Environment.NewLine);

            if (rowPosition == 0)
            {
                builder.Append($"| {string.Join(" | ", Enumerable.Repeat("---", columnIndices.Count))} |");
                builder.Append(Environment.NewLine);
            }
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    internal static List<double> ExtractSpreadsheetSelectionNumbers(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        List<double> numbers = [];

        foreach ((int rowIndex, int columnIndex) in cellCoordinates.Distinct())
        {
            if (rowIndex < 0
                || rowIndex >= dataTable.Rows.Count
                || columnIndex < 0
                || columnIndex >= dataTable.Columns.Count)
            {
                continue;
            }

            string cellText = dataTable.Rows[rowIndex][columnIndex]?.ToString() ?? string.Empty;
            if (NumericUtilities.TryExtractFirstDouble(cellText, out double number))
                numbers.Add(number);
        }

        return numbers;
    }

    internal static string BuildSpreadsheetSelectionNumbersPreviewText(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        return string.Join(
            Environment.NewLine,
            ExtractSpreadsheetSelectionNumbers(dataTable, cellCoordinates)
                .Select(NumericUtilities.FormatNumber));
    }

    internal static List<(int RowIndex, int ColumnIndex)> GetSelectedOrPopulatedSpreadsheetCellCoordinates(
        DataTable dataTable,
        IEnumerable<(int RowIndex, int ColumnIndex)> selectedCellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(dataTable);
        ArgumentNullException.ThrowIfNull(selectedCellCoordinates);

        List<(int RowIndex, int ColumnIndex)> validSelectedCoordinates = [.. selectedCellCoordinates
            .Distinct()
            .Where(cell => cell.RowIndex >= 0
                && cell.RowIndex < dataTable.Rows.Count
                && cell.ColumnIndex >= 0
                && cell.ColumnIndex < dataTable.Columns.Count)];

        if (validSelectedCoordinates.Count > 0)
            return validSelectedCoordinates;

        List<(int RowIndex, int ColumnIndex)> populatedCoordinates = [];

        for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < dataTable.Columns.Count; columnIndex++)
            {
                string cellValue = dataTable.Rows[rowIndex][columnIndex]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(cellValue))
                    populatedCoordinates.Add((rowIndex, columnIndex));
            }
        }

        return populatedCoordinates;
    }

    internal static void TransformSpreadsheetDocumentCellValues(
        EditTextTableDocument document,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates,
        Func<string, string> transform)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cellCoordinates);
        ArgumentNullException.ThrowIfNull(transform);

        document.EnsureMinimumSize();

        foreach ((int rowIndex, int columnIndex) in cellCoordinates.Distinct())
        {
            if (rowIndex < 0
                || rowIndex >= document.Rows.Count
                || columnIndex < 0
                || document.Rows[rowIndex] is null
                || columnIndex >= document.Rows[rowIndex].Count)
            {
                continue;
            }

            string updatedValue = transform(document.Rows[rowIndex][columnIndex] ?? string.Empty);
            ArgumentNullException.ThrowIfNull(updatedValue);
            document.Rows[rowIndex][columnIndex] = updatedValue;
        }
    }

    internal static void SetSpreadsheetDocumentCellValues(
        EditTextTableDocument document,
        IEnumerable<(int RowIndex, int ColumnIndex, string Value)> cellValues)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cellValues);

        document.EnsureMinimumSize();

        foreach ((int rowIndex, int columnIndex, string value) in cellValues.Distinct())
        {
            if (rowIndex < 0
                || rowIndex >= document.Rows.Count
                || columnIndex < 0
                || document.Rows[rowIndex] is null
                || columnIndex >= document.Rows[rowIndex].Count)
            {
                continue;
            }

            document.Rows[rowIndex][columnIndex] = value ?? string.Empty;
        }
    }

    internal static bool AreSpreadsheetDocumentCellsWrapped(
        EditTextTableDocument document,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        document.EnsureMinimumSize();

        List<(int RowIndex, int ColumnIndex)> validCoordinates = [.. cellCoordinates
            .Distinct()
            .Where(cell => cell.RowIndex >= 0
                && cell.RowIndex < document.Rows.Count
                && cell.ColumnIndex >= 0
                && cell.ColumnIndex < document.ColumnNames.Count)];

        return validCoordinates.Count > 0
            && validCoordinates.All(cell => document.IsCellWrapped(cell.RowIndex, cell.ColumnIndex));
    }

    internal static void SetSpreadsheetDocumentCellWrapState(
        EditTextTableDocument document,
        IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates,
        bool shouldWrap)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cellCoordinates);

        document.EnsureMinimumSize();

        foreach ((int rowIndex, int columnIndex) in cellCoordinates.Distinct())
            document.SetCellWrap(rowIndex, columnIndex, shouldWrap);
    }

    internal static void ClearSpreadsheetDocumentRowHeights(EditTextTableDocument document, IEnumerable<int> rowIndices)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(rowIndices);

        document.EnsureMinimumSize();

        foreach (int rowIndex in rowIndices.Distinct())
            document.SetRowHeight(rowIndex, null);
    }

    internal static double? GetSpreadsheetPersistedRowHeight(double rowHeight)
    {
        if (double.IsNaN(rowHeight) || double.IsInfinity(rowHeight) || rowHeight <= 0)
            return null;

        return rowHeight;
    }

    private void UpdateSelectedSpreadsheetCellCoordinates()
    {
        selectedSpreadsheetCellCoordinates = [.. SpreadsheetDataGrid.SelectedCells
            .Select(cell => (
                RowIndex: SpreadsheetDataGrid.Items.IndexOf(cell.Item),
                ColumnIndex: cell.Column?.DisplayIndex ?? -1))
            .Where(cell => cell.RowIndex >= 0 && cell.ColumnIndex >= 0)
            .Distinct()];
    }

    private List<(int RowIndex, int ColumnIndex)> GetSelectedSpreadsheetCellCoordinates()
    {
        return [.. selectedSpreadsheetCellCoordinates];
    }

    private List<(int RowIndex, int ColumnIndex)> GetSelectedOrCurrentSpreadsheetCellCoordinates()
    {
        List<(int RowIndex, int ColumnIndex)> selectedCells = GetSelectedSpreadsheetCellCoordinates();
        if (selectedCells.Count > 0)
            return selectedCells;

        int rowIndex = spreadsheetContextRowIndex ?? SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        int columnIndex = spreadsheetContextColumnIndex ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? -1;

        if (rowIndex < 0 || columnIndex < 0)
            return [];

        return [(rowIndex, columnIndex)];
    }

    private List<(int RowIndex, int ColumnIndex)> GetSelectedOrPopulatedSpreadsheetCellCoordinates()
    {
        return GetSelectedOrPopulatedSpreadsheetCellCoordinates(spreadsheetTable, GetSelectedSpreadsheetCellCoordinates());
    }

    private IEnumerable<string> GetSelectedOrPopulatedSpreadsheetCellTexts()
    {
        foreach ((int rowIndex, int columnIndex) in GetSelectedOrPopulatedSpreadsheetCellCoordinates())
            yield return spreadsheetTable.Rows[rowIndex][columnIndex]?.ToString() ?? string.Empty;
    }

    private bool TryApplySpreadsheetTextTransform(Func<string, string> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (editorMode != EtwEditorMode.Spreadsheet)
            return false;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        EnsureSpreadsheetDocumentFromText();

        if (tableDocument is null)
            return true;

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrPopulatedSpreadsheetCellCoordinates();
        if (targetCells.Count == 0)
            return true;

        int focusRow = Math.Max(0, SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem));
        int focusColumn = Math.Max(0, SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0);

        ApplySpreadsheetDocumentChange(
            document => TransformSpreadsheetDocumentCellValues(document, targetCells, transform),
            focusRow,
            focusColumn);
        UpdateLineAndColumnText();
        return true;
    }

    private async Task<bool> TryApplySpreadsheetTextTransformAsync(Func<string, Task<string>> transformAsync)
    {
        ArgumentNullException.ThrowIfNull(transformAsync);

        if (editorMode != EtwEditorMode.Spreadsheet)
            return false;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        EnsureSpreadsheetDocumentFromText();

        if (tableDocument is null)
            return true;

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrPopulatedSpreadsheetCellCoordinates();
        if (targetCells.Count == 0)
            return true;

        int focusRow = Math.Max(0, SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem));
        int focusColumn = Math.Max(0, SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0);
        List<(int RowIndex, int ColumnIndex, string Value)> transformedCells = [];

        foreach ((int rowIndex, int columnIndex) in targetCells)
        {
            if (rowIndex < 0
                || rowIndex >= tableDocument.Rows.Count
                || columnIndex < 0
                || columnIndex >= tableDocument.Rows[rowIndex].Count)
            {
                continue;
            }

            string updatedValue = await transformAsync(tableDocument.Rows[rowIndex][columnIndex] ?? string.Empty);
            ArgumentNullException.ThrowIfNull(updatedValue);
            transformedCells.Add((rowIndex, columnIndex, updatedValue));
        }

        ApplySpreadsheetDocumentChange(
            document => SetSpreadsheetDocumentCellValues(document, transformedCells),
            focusRow,
            focusColumn);
        UpdateLineAndColumnText();
        return true;
    }

    private void SpreadsheetUndoCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        e.CanExecute = spreadsheetUndoHistory.CanUndo;
        e.Handled = true;
    }

    private void SpreadsheetCopyCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        e.CanExecute = GetSelectedSpreadsheetCellCoordinates().Count > 0;
        e.Handled = true;
    }

    private void SpreadsheetPasteCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        e.CanExecute = true;
        e.Handled = true;
    }

    private void SpreadsheetRedoCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        e.CanExecute = spreadsheetUndoHistory.CanRedo;
        e.Handled = true;
    }

    private void SpreadsheetUndoExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        SpreadsheetUndoState? previousState = spreadsheetUndoHistory.Undo(CreateCurrentSpreadsheetUndoState(syncFromTable: true));
        if (previousState is null)
            return;

        RestoreSpreadsheetUndoState(previousState);
        CommandManager.InvalidateRequerySuggested();
        e.Handled = true;
    }

    private void SpreadsheetRedoExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        SpreadsheetUndoState? nextState = spreadsheetUndoHistory.Redo(CreateCurrentSpreadsheetUndoState(syncFromTable: true));
        if (nextState is null)
            return;

        RestoreSpreadsheetUndoState(nextState);
        CommandManager.InvalidateRequerySuggested();
        e.Handled = true;
    }

    private void SpreadsheetCopyExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        _ = TryCopySpreadsheetSelectionToClipboard(GetSelectedSpreadsheetCellCoordinates());
        e.Handled = true;
    }

    private void SpreadsheetCutExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        _ = TryCutSelectedSpreadsheetCellValues();
        e.Handled = true;
    }

    private void SpreadsheetPasteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (editorMode != EtwEditorMode.Spreadsheet || IsSpreadsheetCellEditorFocused())
            return;

        PasteIntoSpreadsheet();
        e.Handled = true;
    }

    private bool IsSpreadsheetCellEditorFocused()
    {
        if (Keyboard.FocusedElement is not DependencyObject focusedElement)
            return false;

        return FindVisualParent<System.Windows.Controls.DataGridCell>(focusedElement) is not null
            && FindVisualParent<System.Windows.Controls.TextBox>(focusedElement) is not null;
    }

    private string GetSpreadsheetSelectedText()
    {
        if (activeSpreadsheetCellEditor is System.Windows.Controls.TextBox cellTextBox)
            return cellTextBox.SelectedText;

        if (lastSpreadsheetCellEditorSelectedText.Length > 0)
            return lastSpreadsheetCellEditorSelectedText;

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrCurrentSpreadsheetCellCoordinates();
        if (targetCells.Count != 1)
            return string.Empty;

        (int rowIndex, int columnIndex) = targetCells[0];
        if (rowIndex < 0 || rowIndex >= spreadsheetTable.Rows.Count
            || columnIndex < 0 || columnIndex >= spreadsheetTable.Columns.Count)
        {
            return string.Empty;
        }

        return spreadsheetTable.Rows[rowIndex][columnIndex]?.ToString() ?? string.Empty;
    }

    internal static bool ShouldHandleSpreadsheetDeleteKey(int selectedCellCount, bool isCellEditorFocused)
    {
        return !isCellEditorFocused && selectedCellCount > 0;
    }

    private void SpreadsheetColumnWidthChanged(object? sender, EventArgs e)
    {
        if (isApplyingSpreadsheetLayout || tableDocument is null || sender is not DataGridColumn column)
            return;

        int columnIndex = SpreadsheetDataGrid.Columns.IndexOf(column);
        if (columnIndex < 0)
            return;

        double width = column.ActualWidth > 0 ? column.ActualWidth : column.Width.DisplayValue;
        tableDocument.SetColumnWidth(columnIndex, width);
    }

    private void SpreadsheetRow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (isApplyingSpreadsheetLayout || tableDocument is null || sender is not DataGridRow row)
            return;

        int rowIndex = row.GetIndex();
        if (rowIndex < 0)
            return;

        double? height = GetSpreadsheetPersistedRowHeight(row.Height);
        if (!height.HasValue)
            return;

        tableDocument.SetRowHeight(rowIndex, height.Value);
    }

    private void EnterSpreadsheetMode_Click(object sender, RoutedEventArgs e) => SetEditorMode(EtwEditorMode.Spreadsheet);

    private void SyncSpreadsheetDocumentFromTable(bool writeText = true)
    {
        tableDocument ??= EditTextTableDocument.CreateFromText(Session.Text);

        tableDocument.ColumnNames = [.. spreadsheetTable.Columns
            .Cast<DataColumn>()
            .Select(column => column.ColumnName)];

        tableDocument.Rows = [.. spreadsheetTable.Rows
            .Cast<DataRow>()
            .Select(row => spreadsheetTable.Columns
                .Cast<DataColumn>()
                .Select(column => row[column]?.ToString() ?? string.Empty)
                .ToList())];

        int furthestNonEmptyRowIndex = -1;
        int furthestNonEmptyColumnIndex = -1;

        for (int rowIndex = 0; rowIndex < tableDocument.Rows.Count; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < tableDocument.Rows[rowIndex].Count; columnIndex++)
            {
                if (string.IsNullOrWhiteSpace(tableDocument.Rows[rowIndex][columnIndex]))
                    continue;

                furthestNonEmptyRowIndex = Math.Max(furthestNonEmptyRowIndex, rowIndex);
                furthestNonEmptyColumnIndex = Math.Max(furthestNonEmptyColumnIndex, columnIndex);
            }
        }

        tableDocument.RowCount = Math.Max(tableDocument.RowCount, furthestNonEmptyRowIndex + 1);
        tableDocument.ColumnCount = Math.Max(tableDocument.ColumnCount, furthestNonEmptyColumnIndex + 1);
        tableDocument.MinimumColumnCount = Math.Max(tableDocument.MinimumColumnCount, spreadsheetTable.Columns.Count);
        tableDocument.MinimumRowCount = Math.Max(tableDocument.MinimumRowCount, spreadsheetTable.Rows.Count);
        CaptureSpreadsheetLayoutMetrics();
        tableDocument.EnsureMinimumSize();

        if (writeText)
            UpdateTextFromSpreadsheetDocument();
    }

    private void CaptureSpreadsheetLayoutMetrics()
    {
        if (tableDocument is null)
            return;

        for (int columnIndex = 0; columnIndex < SpreadsheetDataGrid.Columns.Count; columnIndex++)
        {
            DataGridColumn column = SpreadsheetDataGrid.Columns[columnIndex];
            double width = column.ActualWidth > 0 ? column.ActualWidth : column.Width.DisplayValue;
            tableDocument.SetColumnWidth(columnIndex, width);
        }

        foreach (object item in SpreadsheetDataGrid.Items)
        {
            if (item == CollectionView.NewItemPlaceholder)
                continue;

            if (SpreadsheetDataGrid.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row)
                continue;

            int rowIndex = row.GetIndex();
            if (rowIndex < 0)
                continue;

            double? height = GetSpreadsheetPersistedRowHeight(row.Height);
            tableDocument.SetRowHeight(rowIndex, height);
        }
    }

    private void DetachSpreadsheetColumnWidthTracking()
    {
        foreach (DataGridColumn column in trackedSpreadsheetColumns)
            DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))?.RemoveValueChanged(column, SpreadsheetColumnWidthChanged);

        trackedSpreadsheetColumns.Clear();
    }

    private void TrackSpreadsheetColumnWidth(DataGridColumn column)
    {
        trackedSpreadsheetColumns.Add(column);
        DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn))?.AddValueChanged(column, SpreadsheetColumnWidthChanged);
    }

    private bool TrySetSpreadsheetClipboardSelection(string plainText, string htmlClipboardData)
    {
        if (string.IsNullOrEmpty(plainText))
            return false;

        try
        {
            System.Windows.DataObject dataObject = new();
            dataObject.SetText(plainText);
            if (!string.IsNullOrEmpty(htmlClipboardData))
                dataObject.SetData(System.Windows.DataFormats.Html, htmlClipboardData);

            System.Windows.Clipboard.SetDataObject(dataObject, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryCopySpreadsheetSelectionToClipboard(IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        List<(int RowIndex, int ColumnIndex)> coordinates = [.. cellCoordinates];
        string selectionText = BuildSpreadsheetSelectionText(spreadsheetTable, coordinates);
        string selectionHtml = BuildSpreadsheetSelectionHtml(spreadsheetTable, coordinates);
        return TrySetSpreadsheetClipboardSelection(selectionText, selectionHtml);
    }

    private void ClearSpreadsheetCellValuesAndSync(IEnumerable<(int RowIndex, int ColumnIndex)> cellCoordinates)
    {
        List<(int RowIndex, int ColumnIndex)> targetCellCoordinates = [.. cellCoordinates];
        if (targetCellCoordinates.Count == 0)
            return;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        SpreadsheetUndoState? beforeChange = CreateCurrentSpreadsheetUndoState(syncFromTable: true);

        ClearSpreadsheetCellValues(spreadsheetTable, targetCellCoordinates);
        SyncSpreadsheetDocumentFromTable();
        RecordSpreadsheetUndoChange(beforeChange, CreateCurrentSpreadsheetUndoState(syncFromTable: false));
        UpdateLineAndColumnText();
    }

    private bool TryCutSelectedSpreadsheetCellValues()
    {
        List<(int RowIndex, int ColumnIndex)> selectedCellCoordinates = GetSelectedSpreadsheetCellCoordinates();
        if (selectedCellCoordinates.Count == 0)
            return false;

        CommitSpreadsheetEditsAndCapturePendingHistory();
        SpreadsheetUndoState? beforeChange = CreateCurrentSpreadsheetUndoState(syncFromTable: true);

        string selectionHtml = BuildSpreadsheetSelectionHtml(spreadsheetTable, selectedCellCoordinates);
        if (!TryCutSpreadsheetCellValues(
                spreadsheetTable,
                selectedCellCoordinates,
                text => TrySetSpreadsheetClipboardSelection(text, selectionHtml)))
            return false;

        SyncSpreadsheetDocumentFromTable();
        RecordSpreadsheetUndoChange(beforeChange, CreateCurrentSpreadsheetUndoState(syncFromTable: false));
        UpdateLineAndColumnText();
        return true;
    }

    private void UpdateSpreadsheetModeUi()
    {
        bool isSpreadsheetMode = editorMode == EtwEditorMode.Spreadsheet;
        bool isMarkdownMode = editorMode == EtwEditorMode.Markdown;

        AddSpreadsheetRowButton.Visibility = isSpreadsheetMode ? Visibility.Visible : Visibility.Collapsed;
        AddSpreadsheetColumnButton.Visibility = isSpreadsheetMode ? Visibility.Visible : Visibility.Collapsed;
        AddSpreadsheetRowMenuItem.Visibility = isSpreadsheetMode ? Visibility.Visible : Visibility.Collapsed;
        AddSpreadsheetColumnMenuItem.Visibility = isSpreadsheetMode ? Visibility.Visible : Visibility.Collapsed;
        RawTextModeMenuItem.IsChecked = editorMode == EtwEditorMode.Text;
        SpreadsheetModeMenuItem.IsChecked = isSpreadsheetMode;
        MarkdownModeMenuItem.IsChecked = isMarkdownMode;
        bool isCodeMode = editorMode == EtwEditorMode.Code;
        CodeModeMenuItem.IsChecked = isCodeMode;
        CodeLanguageMenuItem.Visibility = isCodeMode ? Visibility.Visible : Visibility.Collapsed;
        CodeLanguageButton.Visibility = isCodeMode ? Visibility.Visible : Visibility.Collapsed;
        CommandManager.InvalidateRequerySuggested();
    }

    private System.Windows.Data.Binding CreateSpreadsheetCellTextWrappingBinding(int columnIndex)
    {
        return new System.Windows.Data.Binding
        {
            Converter = new SpreadsheetCellTextWrappingConverter(this, columnIndex),
            Mode = BindingMode.OneWay
        };
    }

    private Style CreateSpreadsheetDisplayTextStyle(int columnIndex)
    {
        Style style = new(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, CreateSpreadsheetCellTextWrappingBinding(columnIndex)));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Top));
        return style;
    }

    private Style CreateSpreadsheetEditingTextStyle(int columnIndex)
    {
        Style style = new(typeof(System.Windows.Controls.TextBox));
        style.Setters.Add(new Setter(System.Windows.Controls.TextBox.TextWrappingProperty, CreateSpreadsheetCellTextWrappingBinding(columnIndex)));
        style.Setters.Add(new Setter(System.Windows.Controls.TextBox.AcceptsReturnProperty, false));
        style.Setters.Add(new Setter(System.Windows.Controls.TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Top));
        return style;
    }

    private TextWrapping GetSpreadsheetCellTextWrapping(object? rowItem, int columnIndex)
    {
        if (tableDocument is null || rowItem is not DataRowView dataRowView)
            return TextWrapping.NoWrap;

        int rowIndex = dataRowView.Row.Table.Rows.IndexOf(dataRowView.Row);
        if (rowIndex < 0)
            return TextWrapping.NoWrap;

        return tableDocument.IsCellWrapped(rowIndex, columnIndex)
            ? TextWrapping.Wrap
            : TextWrapping.NoWrap;
    }

    private static MenuItem? GetContextMenuItem(ContextMenu contextMenu, string itemTag)
    {
        return contextMenu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, itemTag, StringComparison.Ordinal));
    }

    private void SelectSpreadsheetColumn(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= SpreadsheetDataGrid.Columns.Count)
            return;

        SpreadsheetDataGrid.SelectedItems.Clear();
        SpreadsheetDataGrid.SelectedCells.Clear();

        DataGridColumn column = SpreadsheetDataGrid.Columns[columnIndex];
        object? firstRowItem = null;

        foreach (object item in SpreadsheetDataGrid.Items)
        {
            if (ReferenceEquals(item, CollectionView.NewItemPlaceholder))
                continue;

            firstRowItem ??= item;
            SpreadsheetDataGrid.SelectedCells.Add(new DataGridCellInfo(item, column));
        }

        if (firstRowItem is not null)
        {
            SpreadsheetDataGrid.CurrentCell = new DataGridCellInfo(firstRowItem, column);
            SpreadsheetDataGrid.ScrollIntoView(firstRowItem, column);
        }

        UpdateSelectedSpreadsheetCellCoordinates();
        SpreadsheetDataGrid.Focus();
        UpdateLineAndColumnText();
    }

    private void SelectSpreadsheetCell(object rowItem, DataGridColumn column, bool clearExistingSelection)
    {
        if (clearExistingSelection)
        {
            SpreadsheetDataGrid.SelectedItems.Clear();
            SpreadsheetDataGrid.SelectedCells.Clear();
        }

        SpreadsheetDataGrid.CurrentCell = new DataGridCellInfo(rowItem, column);

        if (!SpreadsheetDataGrid.SelectedCells.Any(cell => ReferenceEquals(cell.Item, rowItem) && cell.Column == column))
            SpreadsheetDataGrid.SelectedCells.Add(new DataGridCellInfo(rowItem, column));

        SpreadsheetDataGrid.ScrollIntoView(rowItem, column);
        UpdateSelectedSpreadsheetCellCoordinates();
        SpreadsheetDataGrid.Focus();
        UpdateLineAndColumnText();
    }

    private void SelectSpreadsheetRow(object rowItem)
    {
        SpreadsheetDataGrid.SelectedItems.Clear();
        SpreadsheetDataGrid.SelectedCells.Clear();

        if (SpreadsheetDataGrid.Columns.Count == 0)
            return;

        DataGridColumn firstColumn = SpreadsheetDataGrid.Columns[0];
        foreach (DataGridColumn column in SpreadsheetDataGrid.Columns)
            SpreadsheetDataGrid.SelectedCells.Add(new DataGridCellInfo(rowItem, column));

        SpreadsheetDataGrid.CurrentCell = new DataGridCellInfo(rowItem, firstColumn);
        SpreadsheetDataGrid.ScrollIntoView(rowItem, firstColumn);
        UpdateSelectedSpreadsheetCellCoordinates();
        SpreadsheetDataGrid.Focus();
        UpdateLineAndColumnText();
    }

    /// <summary>
    /// Copies the table document, serialized, into the text. For callers that just changed the
    /// document themselves, so the grid has nothing newer to commit.
    /// </summary>
    private void UpdateTextFromSpreadsheetDocument()
    {
        if (tableDocument is null)
            return;

        SessionImpl.SetTextFromEditor(EtwEditorMode.Spreadsheet, tableDocument.SerializeToText());
    }

    private static string GetSpreadsheetSaveExtension(EditTextTableDocument? tableDocument)
    {
        if (tableDocument is null)
            return ".tsv";

        return tableDocument.Format switch
        {
            EtwStructuredTextFormat.Csv => ".csv",
            EtwStructuredTextFormat.Tsv => ".tsv",
            EtwStructuredTextFormat.DelimitedText when string.Equals(tableDocument.Delimiter, ",", StringComparison.Ordinal) => ".csv",
            _ => ".tsv"
        };
    }

    public void CommitSpreadsheetAndSync()
    {
        CommitSpreadsheetEditsAndCapturePendingHistory();
        SyncSpreadsheetDocumentFromTable(writeText: false);
    }

    public void NavigateToSpreadsheetCell(int rowIndex, int columnIndex)
    {
        Dispatcher.BeginInvoke(
            () => FocusSpreadsheetCell(rowIndex, columnIndex, beginEdit: false),
            DispatcherPriority.Background);
    }

    public List<FindResult> SearchSpreadsheetCells(Regex pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        return tableDocument is null
            ? []
            : FindSpreadsheetDocumentMatches(
                tableDocument,
                cellValue =>
                [
                    .. pattern.Matches(cellValue)
                        .Cast<Match>()
                        .Select(match => new RecognizerMatch(match.Index, match.Length, match.Value, match.Value))
                ]);
    }

    public List<FindResult> SearchSpreadsheetCells(PatternItem pattern, string narrowText = "")
    {
        ArgumentNullException.ThrowIfNull(pattern);

        return tableDocument is null
            ? []
            : SearchSpreadsheetDocumentCells(tableDocument, pattern, narrowText);
    }

    internal static List<FindResult> SearchSpreadsheetDocumentCells(
        EditTextTableDocument document,
        PatternItem pattern,
        string narrowText = "")
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pattern);

        return FindSpreadsheetDocumentMatches(
            document,
            cellValue =>
            [
                .. PatternExecutor.GetMatches(pattern, cellValue)
                    .Where(match => string.IsNullOrEmpty(narrowText)
                        || match.Text.Contains(narrowText, StringComparison.CurrentCultureIgnoreCase))
            ]);
    }

    private static List<FindResult> FindSpreadsheetDocumentMatches(
        EditTextTableDocument document,
        Func<string, IReadOnlyList<RecognizerMatch>> getMatches)
    {
        document.EnsureMinimumSize();
        List<FindResult> results = [];
        int count = 1;

        for (int row = 0; row < document.RowCount; row++)
        {
            List<string> rowData = document.Rows[row];
            for (int col = 0; col < document.ColumnCount; col++)
            {
                string cellValue = col < rowData.Count ? rowData[col] ?? string.Empty : string.Empty;
                foreach (RecognizerMatch match in getMatches(cellValue))
                {
                    int previewStart = Math.Max(0, match.Start - 12);
                    int previewEnd = Math.Min(cellValue.Length, match.Start + match.Length + 12);
                    results.Add(new FindResult
                    {
                        RowIndex = row,
                        ColumnIndex = col,
                        Index = match.Start,
                        Text = TextSearchUtilities.FormatMatchTextForDisplay(match.Text),
                        RawText = match.Text,
                        PreviewLeft = cellValue[previewStart..match.Start],
                        PreviewRight = cellValue[(match.Start + match.Length)..previewEnd],
                        Length = match.Length,
                        Count = count++
                    });
                }
            }
        }

        return results;
    }

    public void ReplaceInSpreadsheetCells(
        IEnumerable<FindResult> targets,
        string replaceWith,
        Regex pattern)
    {
        CommitSpreadsheetEditsAndCapturePendingHistory();
        SyncSpreadsheetDocumentFromTable(writeText: false);

        if (tableDocument is null) return;

        SpreadsheetUndoState? beforeState = CreateCurrentSpreadsheetUndoState(syncFromTable: false);

        IEnumerable<(int RowIndex, int ColumnIndex, string Value)> updates = targets
            .Where(r => r.RowIndex.HasValue && r.ColumnIndex.HasValue)
            .GroupBy(r => (r.RowIndex!.Value, r.ColumnIndex!.Value))
            .Select(g =>
            {
                int row = g.Key.Item1, col = g.Key.Item2;
                string oldValue = row < tableDocument.Rows.Count && col < tableDocument.Rows[row].Count
                    ? tableDocument.Rows[row][col] ?? string.Empty : string.Empty;

                HashSet<int> indicesToReplace = [.. g.Select(r => r.Index)];
                string newValue = pattern.Replace(oldValue, m =>
                    indicesToReplace.Contains(m.Index) ? m.Result(replaceWith) : m.Value);

                return (RowIndex: row, ColumnIndex: col, Value: newValue);
            });

        SetSpreadsheetDocumentCellValues(tableDocument, updates);
        RebuildSpreadsheetTable();
        UpdateTextFromSpreadsheetDocument();
        RecordSpreadsheetUndoChange(beforeState, CreateCurrentSpreadsheetUndoState(syncFromTable: false));
    }

    private string GetSpreadsheetAwareSelectedText()
    {
        if (editorMode == EtwEditorMode.Spreadsheet)
            return GetSpreadsheetSelectedText();

        return Session.SelectedText;
    }

    private void OpenNewWindowWithSpreadsheetSelection()
    {
        CommitSpreadsheetEditsAndCapturePendingHistory();

        List<(int RowIndex, int ColumnIndex)> selectedCellCoordinates = GetSelectedSpreadsheetCellCoordinates();
        string selectedText = BuildSpreadsheetSelectionText(spreadsheetTable, selectedCellCoordinates);
        bool hasSelection = selectedCellCoordinates.Count > 0;

        EditTextWindow newEtwWithText = CreateSelectionWindow(
            selectedText,
            hasSelection ? EtwEditorMode.Spreadsheet : EtwEditorMode.Text);
        newEtwWithText.Show();
    }

    private bool TrySelectSpreadsheetWord()
    {
        if (editorMode != EtwEditorMode.Spreadsheet)
            return false;

        if (TryGetFocusedSpreadsheetCellEditor(out System.Windows.Controls.TextBox? focusedEditor))
        {
            (int editorWordStart, int editorWordLength) = focusedEditor.Text.CursorWordBoundaries(focusedEditor.CaretIndex);
            focusedEditor.Select(editorWordStart, editorWordLength);
            return true;
        }

        int rowIndex = SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem);
        int? columnIndex = SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex;
        if (rowIndex < 0
            || columnIndex is null
            || rowIndex >= spreadsheetTable.Rows.Count
            || columnIndex.Value < 0
            || columnIndex.Value >= spreadsheetTable.Columns.Count)
        {
            return true;
        }

        string cellText = spreadsheetTable.Rows[rowIndex][columnIndex.Value]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cellText))
            return true;

        (int wordStart, int wordLength) = cellText.CursorWordBoundaries(0);
        FocusSpreadsheetCell(rowIndex, columnIndex.Value);

        Dispatcher.BeginInvoke(
            () =>
            {
                if (TryGetFocusedSpreadsheetCellEditor(out System.Windows.Controls.TextBox? editor))
                    editor.Select(wordStart, wordLength);
            },
            DispatcherPriority.Background);

        return true;
    }

    private bool TryGetFocusedSpreadsheetCellEditor([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out System.Windows.Controls.TextBox? editor)
    {
        editor = null;

        if (Keyboard.FocusedElement is not DependencyObject focusedElement)
            return false;

        if (FindVisualParent<System.Windows.Controls.DataGridCell>(focusedElement) is null)
            return false;

        editor = FindVisualParent<System.Windows.Controls.TextBox>(focusedElement);
        return editor is not null;
    }

    private bool TryGetSpreadsheetSelectionNumbersPreviewText(out string previewText)
    {
        previewText = string.Empty;

        if (editorMode != EtwEditorMode.Spreadsheet)
            return false;

        List<(int RowIndex, int ColumnIndex)> selectedCellCoordinates = GetSelectedSpreadsheetCellCoordinates();
        if (selectedCellCoordinates.Count == 0)
            return false;

        previewText = BuildSpreadsheetSelectionNumbersPreviewText(spreadsheetTable, selectedCellCoordinates);
        return !string.IsNullOrEmpty(previewText);
    }

    private void SpreadsheetContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu contextMenu)
            return;

        MenuItem? wrapTextMenuItem = GetContextMenuItem(contextMenu, "SpreadsheetWrapTextToggle");
        if (wrapTextMenuItem is null)
            return;

        EnsureSpreadsheetDocumentFromText();

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrCurrentSpreadsheetCellCoordinates();
        bool hasTargetCells = tableDocument is not null && targetCells.Count > 0;

        wrapTextMenuItem.IsEnabled = hasTargetCells;
        wrapTextMenuItem.IsChecked = hasTargetCells && AreSpreadsheetDocumentCellsWrapped(tableDocument!, targetCells);
    }

    private void ToggleSpreadsheetWrapTextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        List<(int RowIndex, int ColumnIndex)> targetCells = GetSelectedOrCurrentSpreadsheetCellCoordinates();
        if (targetCells.Count == 0)
            return;

        int focusRow = Math.Max(0, spreadsheetContextRowIndex ?? SpreadsheetDataGrid.Items.IndexOf(SpreadsheetDataGrid.CurrentItem));
        int focusColumn = Math.Max(0, spreadsheetContextColumnIndex ?? SpreadsheetDataGrid.CurrentCell.Column?.DisplayIndex ?? 0);

        ApplySpreadsheetDocumentChange(
            document =>
            {
                SetSpreadsheetDocumentCellWrapState(document, targetCells, menuItem.IsChecked);
                ClearSpreadsheetDocumentRowHeights(document, targetCells.Select(cell => cell.RowIndex));
            },
            focusRow,
            focusColumn,
            beginEdit: false);
    }
}
