using System.Text.Json;
using Text_Grab.Models;

namespace Text_Grab.Tests.Core;

public class EditTextTableDocumentTests
{
    [Fact]
    public void Tsv_RoundTrips_WithoutMinimumGridPadding()
    {
        const string input = "Name\tValue\r\nAlpha\t42";

        EditTextTableDocument document = EditTextTableDocument.CreateFromText(input);

        Assert.Equal(EtwStructuredTextFormat.Tsv, document.Format);
        Assert.Equal("\r\n", document.NewLineSequence);
        Assert.Equal(input, document.SerializeToText());
    }

    [Fact]
    public void Csv_QuotedFields_RoundTrip()
    {
        const string input = "Name,Notes\r\nJoe,\"Hello, \"\"world\"\"\"";

        EditTextTableDocument document = EditTextTableDocument.CreateFromText(input);

        Assert.Equal(EtwStructuredTextFormat.Csv, document.Format);
        Assert.Equal(input, document.SerializeToText());
    }

    [Fact]
    public void Xml_FlattensRows_AndSerializesAttributesAndChildren()
    {
        const string input = "<items><item id=\"1\"><name>Alpha</name><value>42</value></item><item id=\"2\"><name>Beta</name><value>99</value></item></items>";

        EditTextTableDocument document = EditTextTableDocument.CreateFromText(input);

        Assert.Equal(EtwStructuredTextFormat.Xml, document.Format);
        Assert.Equal(["@id", "name", "value"], document.ColumnNames.Take(3).ToList());
        Assert.Equal("1", document.Rows[0][0]);
        Assert.Equal("Alpha", document.Rows[0][1]);
        Assert.Contains("id=\"1\"", document.SerializeToText());
        Assert.Contains("<name>Alpha</name>", document.SerializeToText());
    }

    [Fact]
    public void PlainText_PreservesNewLineStyle()
    {
        const string input = "first\nsecond\nthird";

        EditTextTableDocument document = EditTextTableDocument.CreateFromText(input);

        Assert.Equal(EtwStructuredTextFormat.PlainText, document.Format);
        Assert.Equal("\n", document.NewLineSequence);
        Assert.Equal(input, document.SerializeToText());
    }

    [Fact]
    public void AddedRowsAndColumns_ExpandSerializedDocument_NotMinimumCapacity()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("A\tB");

        document.InsertColumn(2);
        document.InsertRow(1);
        document.Rows[0][2] = "C";
        document.Rows[1][0] = "D";
        document.Rows[1][1] = "E";
        document.Rows[1][2] = "F";

        Assert.Equal("A\tB\tC\r\nD\tE\tF", document.SerializeToText());
    }

    [Fact]
    public void SerializedJson_RestoresLogicalDimensions()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("left\tright");
        document.InsertColumn(2);
        document.Rows[0][2] = "extra";
        document.SetColumnWidth(0, 180);
        document.SetRowHeight(0, 36);
        document.SetCellWrap(0, 1, true);

        string json = document.SerializeToJson();
        EditTextTableDocument? restored = EditTextTableDocument.TryDeserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(document.RowCount, restored!.RowCount);
        Assert.Equal(document.ColumnCount, restored.ColumnCount);
        Assert.Equal(document.SerializeToText(), restored.SerializeToText());
        Assert.Equal(180, restored.ColumnWidths[0]);
        Assert.Equal(36, restored.RowHeights[0]);
        Assert.True(restored.IsCellWrapped(0, 1));
        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("ColumnCount", out _));
    }

    [Fact]
    public void MoveAndDeleteRow_UpdateLogicalOrdering()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("A\t1\r\nB\t2\r\nC\t3");

        document.MoveRow(2, 0);
        document.DeleteRow(1);

        Assert.Equal("C\t3\r\nB\t2", document.SerializeToText());
    }

    [Fact]
    public void MoveAndDeleteColumn_UpdateLogicalOrdering()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("A\tB\tC");

        document.MoveColumn(2, 0);
        document.DeleteColumn(1);

        Assert.Equal("C\tB", document.SerializeToText());
    }

    [Fact]
    public void ViewMetrics_MoveWithRowsAndColumns()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("A\tB\r\nC\tD");
        document.SetColumnWidth(0, 140);
        document.SetColumnWidth(1, 220);
        document.SetRowHeight(0, 30);
        document.SetRowHeight(1, 44);

        document.MoveColumn(1, 0);
        document.MoveRow(1, 0);

        Assert.Equal(220, document.ColumnWidths[0]);
        Assert.Equal(140, document.ColumnWidths[1]);
        Assert.Equal(44, document.RowHeights[0]);
        Assert.Equal(30, document.RowHeights[1]);
    }

    [Fact]
    public void ApplyViewMetricsFrom_PreservesExistingSizing()
    {
        EditTextTableDocument source = EditTextTableDocument.CreateFromText("A\tB\r\nC\tD");
        source.SetColumnWidth(0, 160);
        source.SetColumnWidth(1, 240);
        source.SetRowHeight(0, 28);
        source.SetRowHeight(1, 40);
        source.SetCellWrap(1, 1, true);

        EditTextTableDocument target = EditTextTableDocument.CreateFromText("1\t2\r\n3\t4\r\n5\t6");
        target.ApplyViewMetricsFrom(source);

        Assert.Equal(160, target.ColumnWidths[0]);
        Assert.Equal(240, target.ColumnWidths[1]);
        Assert.Equal(28, target.RowHeights[0]);
        Assert.Equal(40, target.RowHeights[1]);
        Assert.Null(target.RowHeights[2]);
        Assert.True(target.IsCellWrapped(1, 1));
    }

    [Fact]
    public void Transpose_SwapsRowsAndColumns_AndResetsViewMetrics()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText(
            "A\tB\tC\r\n1\t2\t3",
            minimumRowCount: 2,
            minimumColumnCount: 3);
        document.SetColumnWidth(0, 180);
        document.SetRowHeight(0, 36);
        document.SetCellWrap(0, 2, true);

        document.Transpose();

        Assert.Equal("A\t1\r\nB\t2\r\nC\t3", document.SerializeToText());
        Assert.Equal(3, document.RowCount);
        Assert.Equal(2, document.ColumnCount);
        Assert.Equal(3, document.MinimumRowCount);
        Assert.Equal(2, document.MinimumColumnCount);
        Assert.All(document.ColumnWidths.Take(document.ColumnCount), width => Assert.Null(width));
        Assert.All(document.RowHeights.Take(document.RowCount), height => Assert.Null(height));
        Assert.True(document.IsCellWrapped(2, 0));
    }

    [Fact]
    public void WrappedCells_MoveWithInsertedMovedAndDeletedRowsAndColumns()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("A\tB\tC\r\n1\t2\t3\r\nx\ty\tz");
        document.SetCellWrap(1, 1, true);

        document.InsertRow(1);
        document.InsertColumn(1);
        Assert.True(document.IsCellWrapped(2, 2));

        document.MoveRow(2, 0);
        document.MoveColumn(2, 0);
        Assert.True(document.IsCellWrapped(0, 0));

        document.DeleteRow(0);
        document.DeleteColumn(0);
        Assert.False(document.WrappedCells.Any());
    }

    [Theory]
    [InlineData("Alpha", 1, 1)]
    [InlineData("Alpha\tBeta", 1, 2)]
    [InlineData("Alpha\tBeta\r\n1\t2", 2, 2)]
    [InlineData("Alpha\r\nBeta", 2, 1)]
    public void ParseTabSeparatedRows_SplitsRowsAndColumns(string input, int expectedRowCount, int expectedColumnCount)
    {
        List<string[]> rows = EditTextTableDocument.ParseTabSeparatedRows(input);

        Assert.Equal(expectedRowCount, rows.Count);
        Assert.All(rows, row => Assert.Equal(expectedColumnCount, row.Length));
    }

    [Fact]
    public void ParseTabSeparatedRows_TrimsTrailingNewlineArtifact()
    {
        List<string[]> rows = EditTextTableDocument.ParseTabSeparatedRows("A\tB\r\n1\t2\r\n");

        Assert.Equal(2, rows.Count);
        Assert.Equal(["1", "2"], rows[1]);
    }

    [Fact]
    public void ParseTabSeparatedRows_EmptyInput_ReturnsNoRows()
    {
        Assert.Empty(EditTextTableDocument.ParseTabSeparatedRows(string.Empty));
        Assert.Empty(EditTextTableDocument.ParseTabSeparatedRows(null));
    }

    [Theory]
    [InlineData("Alpha", true)]
    [InlineData("Alpha\tBeta", false)]
    [InlineData("Alpha\r\nBeta", false)]
    [InlineData("Alpha\tBeta\r\n1\t2", false)]
    public void IsSingleCellGrid_OnlyTrueForOneRowOneColumn(string input, bool expected)
    {
        List<string[]> rows = EditTextTableDocument.ParseTabSeparatedRows(input);

        Assert.Equal(expected, EditTextTableDocument.IsSingleCellGrid(rows));
    }

    [Fact]
    public void GetFirstFullyEmptyRowIndex_ReturnsIndexAfterLastPopulatedRow()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("A\tB\r\nC\tD");

        Assert.Equal(2, document.GetFirstFullyEmptyRowIndex());
    }

    [Fact]
    public void GetFirstFullyEmptyRowIndex_AllRowsEmpty_ReturnsZero()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText(string.Empty);

        Assert.Equal(0, document.GetFirstFullyEmptyRowIndex());
    }

    [Fact]
    public void GetFirstFullyEmptyRowIndex_IgnoresBlankRowsBelowPopulatedData()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText(
            "A\tB",
            minimumRowCount: 10,
            minimumColumnCount: 2);
        document.Rows[5][0] = "populated";

        Assert.Equal(6, document.GetFirstFullyEmptyRowIndex());
    }
}
