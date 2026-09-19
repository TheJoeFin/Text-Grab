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
    public void BuildSpreadsheetSelectionHtml_IncludesOnlySelectedCellsAsTableRows()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Columns.Add("C", typeof(string));
        dataTable.Rows.Add("a1", "b1", "c1");
        dataTable.Rows.Add("a2", "b2", "c2");

        string html = EditTextWindow.BuildSpreadsheetSelectionHtml(
            dataTable,
            [
                (0, 0),
                (0, 2),
                (1, 0),
                (1, 2),
                (-1, 0),
                (5, 5)
            ]);

        string tabSeparated = Text_Grab.Utilities.CfHtmlTableUtilities.ConvertHtmlToTabSeparated(html);

        Assert.Equal("a1\tc1" + Environment.NewLine + "a2\tc2", tabSeparated.Replace("\n", Environment.NewLine));
    }

    [Fact]
    public void BuildSpreadsheetSelectionHtml_ReturnsEmptyWhenNoValidCells()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Rows.Add("a1");

        string html = EditTextWindow.BuildSpreadsheetSelectionHtml(
            dataTable,
            [
                (-1, 0),
                (5, 5)
            ]);

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void BuildSpreadsheetSelectionMarkdown_BuildsTableFromSelectedCells()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Columns.Add("C", typeof(string));
        dataTable.Rows.Add("a1", "b1", "c1");
        dataTable.Rows.Add("a2", "b2", "c2");

        string markdown = EditTextWindow.BuildSpreadsheetSelectionMarkdown(
            dataTable,
            [
                (0, 0),
                (0, 2),
                (1, 0),
                (1, 2),
                (-1, 0),
                (5, 5)
            ]);

        string expected = string.Join(
            Environment.NewLine,
            "| a1 | c1 |",
            "| --- | --- |",
            "| a2 | c2 |");

        Assert.Equal(expected, markdown);
    }

    [Fact]
    public void BuildSpreadsheetSelectionMarkdown_EscapesPipesAndNewlines()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Columns.Add("B", typeof(string));
        dataTable.Rows.Add("has | pipe", "line1\r\nline2");

        string markdown = EditTextWindow.BuildSpreadsheetSelectionMarkdown(
            dataTable,
            [
                (0, 0),
                (0, 1)
            ]);

        string expected = string.Join(
            Environment.NewLine,
            "| has \\| pipe | line1<br />line2 |",
            "| --- | --- |");

        Assert.Equal(expected, markdown);
    }

    [Fact]
    public void BuildSpreadsheetSelectionMarkdown_ReturnsEmptyWhenNoValidCells()
    {
        DataTable dataTable = new();
        dataTable.Columns.Add("A", typeof(string));
        dataTable.Rows.Add("a1");

        string markdown = EditTextWindow.BuildSpreadsheetSelectionMarkdown(
            dataTable,
            [
                (-1, 0),
                (5, 5)
            ]);

        Assert.Equal(string.Empty, markdown);
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
    public void SearchSpreadsheetDocumentCells_SmartPatternFindsAndNarrowsCellMatches()
    {
        PatternItem emailPattern = new(
            BuiltInRecognizer.GetById("email") ?? throw new InvalidOperationException("missing email recognizer"));
        EditTextTableDocument document = EditTextTableDocument.CreateFromText(
            "Name\tEmail\r\nAlice\ta@b.com\r\nBob\tc@d.org",
            minimumRowCount: 3,
            minimumColumnCount: 2);

        List<FindResult> allMatches = EditTextWindow.SearchSpreadsheetDocumentCells(document, emailPattern);
        List<FindResult> narrowedMatches = EditTextWindow.SearchSpreadsheetDocumentCells(document, emailPattern, "C@D");

        Assert.Collection(
            allMatches,
            first =>
            {
                Assert.Equal(1, first.RowIndex);
                Assert.Equal(1, first.ColumnIndex);
                Assert.Equal("a@b.com", first.RawText);
                Assert.Equal(1, first.Count);
            },
            second =>
            {
                Assert.Equal(2, second.RowIndex);
                Assert.Equal(1, second.ColumnIndex);
                Assert.Equal("c@d.org", second.RawText);
                Assert.Equal(2, second.Count);
            });

        FindResult narrowedMatch = Assert.Single(narrowedMatches);
        Assert.Equal(2, narrowedMatch.RowIndex);
        Assert.Equal(1, narrowedMatch.ColumnIndex);
        Assert.Equal("c@d.org", narrowedMatch.RawText);
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

    [Fact]
    public void WriteGridIntoSpreadsheetDocument_WritesEachCellIntoItsOwnRowAndColumn()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText(string.Empty);
        List<string[]> grid = [["Name", "Age"], ["Joe", "42"]];

        EditTextWindow.WriteGridIntoSpreadsheetDocument(document, grid, startRow: 0, startCol: 0);

        Assert.Equal("Name", document.Rows[0][0]);
        Assert.Equal("Age", document.Rows[0][1]);
        Assert.Equal("Joe", document.Rows[1][0]);
        Assert.Equal("42", document.Rows[1][1]);
    }

    [Fact]
    public void WriteGridIntoSpreadsheetDocument_AppendsAtRequestedBottomRow_WithoutDisturbingExistingRows()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("Existing\tRow");
        int appendRow = document.GetFirstFullyEmptyRowIndex();
        List<string[]> grid = [["Name", "Age"], ["Joe", "42"]];

        EditTextWindow.WriteGridIntoSpreadsheetDocument(document, grid, startRow: appendRow, startCol: 0);

        Assert.Equal("Existing", document.Rows[0][0]);
        Assert.Equal("Row", document.Rows[0][1]);
        Assert.Equal("Name", document.Rows[appendRow][0]);
        Assert.Equal("Age", document.Rows[appendRow][1]);
        Assert.Equal("Joe", document.Rows[appendRow + 1][0]);
        Assert.Equal("42", document.Rows[appendRow + 1][1]);
    }

    [Fact]
    public void WriteGridIntoSpreadsheetDocument_ExpandsColumnsForRaggedRows_WithoutSquishingIntoOneColumn()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText(string.Empty);
        List<string[]> grid = [["A", "B", "C"], ["1", "2"]];

        EditTextWindow.WriteGridIntoSpreadsheetDocument(document, grid, startRow: 0, startCol: 0);

        Assert.True(document.ColumnCount >= 3);
        Assert.Equal("A", document.Rows[0][0]);
        Assert.Equal("B", document.Rows[0][1]);
        Assert.Equal("C", document.Rows[0][2]);
        Assert.Equal("1", document.Rows[1][0]);
        Assert.Equal("2", document.Rows[1][1]);
        Assert.Equal(string.Empty, document.Rows[1][2]);
    }

    [Fact]
    public void WriteGridIntoSpreadsheetDocument_SequentialBottomAppends_LandOnSeparateRows()
    {
        // Regression for repeated table-mode FSG grabs sent to the same Spreadsheet-mode ETW:
        // each grab must land on its own row instead of clobbering the previous one.
        EditTextTableDocument document = EditTextTableDocument.CreateFromText(string.Empty);

        int firstAppendRow = document.GetFirstFullyEmptyRowIndex();
        Assert.Equal(0, firstAppendRow);
        EditTextWindow.WriteGridIntoSpreadsheetDocument(document, [["Name", "Age"]], firstAppendRow, 0);

        int secondAppendRow = document.GetFirstFullyEmptyRowIndex();
        Assert.Equal(1, secondAppendRow);
        EditTextWindow.WriteGridIntoSpreadsheetDocument(document, [["Joe", "42"]], secondAppendRow, 0);

        int thirdAppendRow = document.GetFirstFullyEmptyRowIndex();
        Assert.Equal(2, thirdAppendRow);
        EditTextWindow.WriteGridIntoSpreadsheetDocument(document, [["Jane", "30"]], thirdAppendRow, 0);

        Assert.Equal("Name", document.Rows[0][0]);
        Assert.Equal("Age", document.Rows[0][1]);
        Assert.Equal("Joe", document.Rows[1][0]);
        Assert.Equal("42", document.Rows[1][1]);
        Assert.Equal("Jane", document.Rows[2][0]);
        Assert.Equal("30", document.Rows[2][1]);
    }

    [Fact]
    public void WriteGridIntoSpreadsheetDocument_SingleCellAtExplicitCurrentCell_DoesNotDisturbOtherRows()
    {
        // A single-cell grab result should land at the currently selected spreadsheet cell,
        // not get spliced into whatever the underlying (hidden) text box's cursor last was.
        EditTextTableDocument document = EditTextTableDocument.CreateFromText(string.Empty);
        EditTextWindow.WriteGridIntoSpreadsheetDocument(document, [["Name", "Age"]], 0, 0);

        List<string[]> singleCellGrid = EditTextTableDocument.ParseTabSeparatedRows("Joe");
        Assert.True(EditTextTableDocument.IsSingleCellGrid(singleCellGrid));

        int currentCellRow = 1;
        int currentCellColumn = 0;
        EditTextWindow.WriteGridIntoSpreadsheetDocument(document, singleCellGrid, currentCellRow, currentCellColumn);

        Assert.Equal("Name", document.Rows[0][0]);
        Assert.Equal("Age", document.Rows[0][1]);
        Assert.Equal("Joe", document.Rows[1][0]);
        Assert.Equal(string.Empty, document.Rows[1][1]);
    }
}
