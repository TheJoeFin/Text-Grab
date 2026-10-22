using Text_Grab;
using Text_Grab.Models;

namespace Tests;

public class EditTextWindowAggregateResultTests
{
    [Theory]
    [InlineData("One summary.", 0, 0)]
    [InlineData("One summary.", 1, 2)]
    [InlineData("# Meeting notes\r\n- First decision\r\n- Owner\tAction", 0, 0)]
    [InlineData("# Meeting notes\r\n- First decision\r\n- Owner\tAction", 1, 2)]
    public void WriteAggregateResultIntoSpreadsheetDocument_ReplacesOnlyAnchorCell(
        string result,
        int targetRow,
        int targetColumn)
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("a1\tb1\tc1\r\na2\tb2\tc2");
        string[][] original = [.. document.Rows.Select(row => row.ToArray())];

        EditTextWindow.WriteAggregateResultIntoSpreadsheetDocument(document, result, targetRow, targetColumn);

        Assert.Equal(original.Length, document.Rows.Count);
        for (int row = 0; row < original.Length; row++)
        {
            Assert.Equal(original[row].Length, document.Rows[row].Count);
            for (int column = 0; column < original[row].Length; column++)
            {
                string expected = row == targetRow && column == targetColumn ? result : original[row][column];
                Assert.Equal(expected, document.Rows[row][column]);
            }
        }

        Assert.Single(document.Rows.SelectMany(row => row).Where(value => value == result));
    }

    [Fact]
    public void WriteAggregateResultIntoSpreadsheetDocument_PreservesMultilineResultThroughDocumentPersistence()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText("source\tkeep");
        const string result = "## Decisions\r\n- Keep\tthis together\r\n\r\n## Actions\r\n- Follow up";

        EditTextWindow.WriteAggregateResultIntoSpreadsheetDocument(document, result, 0, 0);
        EditTextTableDocument restored = Assert.IsType<EditTextTableDocument>(
            EditTextTableDocument.TryDeserialize(document.SerializeToJson()));

        Assert.Equal(result, restored.Rows[0][0]);
        Assert.Equal("keep", restored.Rows[0][1]);
        Assert.Single(restored.Rows.SelectMany(row => row).Where(value => value == result));
    }

    [Fact]
    public void WriteAggregateResultIntoSpreadsheetDocument_CreatesOneResultInEmptySheet()
    {
        EditTextTableDocument document = EditTextTableDocument.CreateFromText(string.Empty);

        EditTextWindow.WriteAggregateResultIntoSpreadsheetDocument(document, "Summary", 0, 0);

        Assert.Equal("Summary", document.Rows[0][0]);
        Assert.Single(document.Rows.SelectMany(row => row).Where(value => !string.IsNullOrEmpty(value)));
    }
}
