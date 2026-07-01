using System.Data;
using Text_Grab;
using Text_Grab.Models;

namespace Tests;

public class EditTextWindowSpreadsheetTests
{
    [Fact]
    public void ClearSpreadsheetCellValues_ClearsOnlyRequestedCells()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Columns.Add("C", typeof(string));
        dataTable.Rows.Add("a1", "b1", "c1");
        dataTable.Rows.Add("a2", "b2", "c2");

        EditTextWindow.ClearSpreadsheetCellValues(
            dataTable,
            [
                (0, 0),
                (1, 2),
                (1, 2),
                (-1, 1),
                (5, 0),
                (0, 5)
            ]);

        Assert.Equal(string.Empty, dataTable.Rows[0][0]);
        Assert.Equal("b1", dataTable.Rows[0][1]);
        Assert.Equal("c1", dataTable.Rows[0][2]);
        Assert.Equal("a2", dataTable.Rows[1][0]);
        Assert.Equal("b2", dataTable.Rows[1][1]);
        Assert.Equal(string.Empty, dataTable.Rows[1][2]);
    }

    [Fact]
    public void TryCutSpreadsheetCellValues_CopiesThenClearsRequestedCells()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Columns.Add("C", typeof(string));
        dataTable.Rows.Add("a1", "b1", "c1");
        dataTable.Rows.Add("a2", "b2", "c2");

        string clipboardText = string.Empty;

        bool didCut = EditTextWindow.TryCutSpreadsheetCellValues(
            dataTable,
            [
                (1, 2),
                (0, 1),
                (1, 0),
                (0, 1),
                (-1, 0),
                (5, 5)
            ],
            text =>
            {
                clipboardText = text;
                return true;
            });

        Assert.True(didCut);
        Assert.Equal("b1" + Environment.NewLine + "a2\tc2", clipboardText);
        Assert.Equal("a1", dataTable.Rows[0][0]);
        Assert.Equal(string.Empty, dataTable.Rows[0][1]);
        Assert.Equal("c1", dataTable.Rows[0][2]);
        Assert.Equal(string.Empty, dataTable.Rows[1][0]);
        Assert.Equal("b2", dataTable.Rows[1][1]);
        Assert.Equal(string.Empty, dataTable.Rows[1][2]);
    }

    [Fact]
    public void TryCutSpreadsheetCellValues_DoesNotClearWhenClipboardCopyFails()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Rows.Add("a1", "b1");

        bool didCut = EditTextWindow.TryCutSpreadsheetCellValues(
            dataTable,
            [
                (0, 0),
                (0, 1)
            ],
            _ => false);

        Assert.False(didCut);
        Assert.Equal("a1", dataTable.Rows[0][0]);
        Assert.Equal("b1", dataTable.Rows[0][1]);
    }

    [Fact]
    public void BuildSpreadsheetSelectionText_IncludesOnlySelectedCells()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Columns.Add("C", typeof(string));
        dataTable.Rows.Add("a1", "b1", "c1");
        dataTable.Rows.Add("a2", "b2", "c2");

        string selectionText = EditTextWindow.BuildSpreadsheetSelectionText(
            dataTable,
            [
                (1, 2),
                (0, 1),
                (1, 0),
                (0, 1),
                (-1, 0),
                (5, 5)
            ]);

        Assert.Equal("b1" + Environment.NewLine + "a2\tc2", selectionText);
    }

    [Fact]
    public void ExtractSpreadsheetSelectionNumbers_PullsNumericValuesFromSelectedCells()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Columns.Add("C", typeof(string));
        dataTable.Rows.Add("10", "$20.50", "n/a");
        dataTable.Rows.Add("1,234", "Total: 3,5", "abc");

        List<double> numbers = EditTextWindow.ExtractSpreadsheetSelectionNumbers(
            dataTable,
            [
                (0, 0),
                (0, 1),
                (1, 0),
                (1, 1),
                (1, 1),
                (-1, 0),
                (5, 5)
            ]);

        Assert.Equal([10d, 20.5d, 1234d, 3.5d], numbers);
    }

    [Fact]
    public void ExtractSpreadsheetSelectionNumbers_IgnoresNonNumericSelectedCells()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Rows.Add("hello", string.Empty);

        List<double> numbers = EditTextWindow.ExtractSpreadsheetSelectionNumbers(
            dataTable,
            [
                (0, 0),
                (0, 1)
            ]);

        Assert.Empty(numbers);
    }

    [Fact]
    public void BuildSpreadsheetSelectionNumbersPreviewText_FormatsExtractedNumbersForCalcPane()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Rows.Add("200", "17");
        dataTable.Rows.Add("7", "Total: 1");

        string previewText = EditTextWindow.BuildSpreadsheetSelectionNumbersPreviewText(
            dataTable,
            [
                (0, 0),
                (0, 1),
                (1, 0),
                (1, 1)
            ]);

        Assert.Equal("200" + Environment.NewLine + "17" + Environment.NewLine + "7" + Environment.NewLine + "1", previewText);
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(3, false, true)]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    public void ShouldHandleSpreadsheetDeleteKey_RequiresSelectionAndNoInlineEditor(int selectedCellCount, bool isCellEditorFocused, bool expected)
    {
        bool shouldHandle = EditTextWindow.ShouldHandleSpreadsheetDeleteKey(selectedCellCount, isCellEditorFocused);

        Assert.Equal(expected, shouldHandle);
    }

    [Fact]
    public void GetSelectedOrPopulatedSpreadsheetCellCoordinates_PrefersValidSelection()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Columns.Add("C", typeof(string));
        dataTable.Rows.Add("a1", string.Empty, "c1");
        dataTable.Rows.Add("a2", "b2", string.Empty);

        List<(int RowIndex, int ColumnIndex)> coordinates = EditTextWindow.GetSelectedOrPopulatedSpreadsheetCellCoordinates(
            dataTable,
            [
                (0, 1),
                (1, 2),
                (1, 2),
                (-1, 0),
                (5, 5)
            ]);

        Assert.Equal([(0, 1), (1, 2)], coordinates);
    }

    [Fact]
    public void GetSelectedOrPopulatedSpreadsheetCellCoordinates_FallsBackToPopulatedCells()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Columns.Add("C", typeof(string));
        dataTable.Rows.Add("a1", "   ", string.Empty);
        dataTable.Rows.Add(string.Empty, "b2", "c2");

        List<(int RowIndex, int ColumnIndex)> coordinates = EditTextWindow.GetSelectedOrPopulatedSpreadsheetCellCoordinates(
            dataTable,
            [
                (-1, 0),
                (10, 10)
            ]);

        Assert.Equal([(0, 0), (1, 1), (1, 2)], coordinates);
    }

    [Fact]
    public void TransformSpreadsheetDocumentCellValues_TransformsOnlyRequestedCells()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("a1\tb1\tc1\r\na2\tb2\tc2");

        EditTextWindow.TransformSpreadsheetDocumentCellValues(
            document,
            [
                (0, 0),
                (1, 2),
                (1, 2),
                (-1, 0),
                (5, 5)
            ],
            value => $"[{value}]");

        Assert.Equal("[a1]\tb1\tc1\r\na2\tb2\t[c2]", document.SerializeToText());
    }

    [Fact]
    public void SetSpreadsheetDocumentCellValues_SetsOnlyRequestedCells()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("a1\tb1\tc1\r\na2\tb2\tc2");

        EditTextWindow.SetSpreadsheetDocumentCellValues(
            document,
            [
                (0, 1, "B!"),
                (1, 0, "A!"),
                (1, 0, "A!"),
                (8, 1, "ignored")
            ]);

        Assert.Equal("a1\tB!\tc1\r\nA!\tb2\tc2", document.SerializeToText());
    }

    [Fact]
    public void SetSpreadsheetDocumentCellWrapState_UpdatesOnlyRequestedCells()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("a1\tb1\tc1\r\na2\tb2\tc2");

        EditTextWindow.SetSpreadsheetDocumentCellWrapState(
            document,
            [
                (0, 1),
                (1, 2),
                (1, 2),
                (-1, 0),
                (9, 9)
            ],
            shouldWrap: true);

        Assert.False(document.IsCellWrapped(0, 0));
        Assert.True(document.IsCellWrapped(0, 1));
        Assert.False(document.IsCellWrapped(0, 2));
        Assert.False(document.IsCellWrapped(1, 0));
        Assert.False(document.IsCellWrapped(1, 1));
        Assert.True(document.IsCellWrapped(1, 2));
    }

    [Fact]
    public void AreSpreadsheetDocumentCellsWrapped_ReturnsTrueOnlyWhenAllValidTargetsAreWrapped()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("a1\tb1\tc1\r\na2\tb2\tc2");
        document.SetCellWrap(0, 1, true);
        document.SetCellWrap(1, 2, true);

        Assert.True(EditTextWindow.AreSpreadsheetDocumentCellsWrapped(
            document,
            [
                (0, 1),
                (1, 2),
                (1, 2),
                (-1, 0)
            ]));

        Assert.False(EditTextWindow.AreSpreadsheetDocumentCellsWrapped(
            document,
            [
                (0, 1),
                (1, 1)
            ]));
    }

    [Fact]
    public void ClearSpreadsheetDocumentRowHeights_ClearsOnlyRequestedRows()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("a1\tb1\r\na2\tb2");
        document.SetRowHeight(0, 32);
        document.SetRowHeight(1, 48);

        EditTextWindow.ClearSpreadsheetDocumentRowHeights(document, [1, 1, -1, 8]);

        Assert.Equal(32, document.RowHeights[0]);
        Assert.Null(document.RowHeights[1]);
    }

    [Theory]
    [InlineData(24d, 24d)]
    [InlineData(36.5, 36.5)]
    [InlineData(double.NaN, null)]
    [InlineData(double.PositiveInfinity, null)]
    [InlineData(0d, null)]
    [InlineData(-10d, null)]
    public void GetSpreadsheetPersistedRowHeight_PersistsOnlyExplicitPositiveHeights(double rowHeight, double? expectedHeight)
    {
        Assert.Equal(expectedHeight, EditTextWindow.GetSpreadsheetPersistedRowHeight(rowHeight));
    }
}
