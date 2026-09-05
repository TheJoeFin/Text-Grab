using System.Drawing;
using System.Text;
using Text_Grab.Models;

namespace Tests;

public class ResultTableManualSeparatorTests
{
    [WpfFact]
    public void AnalyzeAsTable_ManualRowSeparatorSplitsMergedRowOutput()
    {
        List<WordBorderInfo> automaticInfos =
        [
            CreateWord("Top", left: 20, top: 10, width: 30, height: 10),
            CreateWord("Bottom", left: 20, top: 17, width: 45, height: 10)
        ];

        ResultTable automaticTable = new();
        automaticTable.AnalyzeAsTable(automaticInfos, new Rectangle(0, 0, 200, 200));

        StringBuilder automaticText = new();
        ResultTable.GetTextFromTabledWordBorders(automaticText, automaticInfos, true);
        Assert.Equal("Top Bottom", automaticText.ToString());

        List<WordBorderInfo> manualInfos =
        [
            CreateWord("Top", left: 20, top: 10, width: 30, height: 10),
            CreateWord("Bottom", left: 20, top: 17, width: 45, height: 10)
        ];

        ResultTable manualTable = new();
        manualTable.AnalyzeAsTable(
            manualInfos,
            new Rectangle(0, 0, 200, 200),
            manualRowSeparators: [18d],
            manualColumnSeparators: null);

        StringBuilder manualText = new();
        ResultTable.GetTextFromTabledWordBorders(manualText, manualInfos, true);

        Assert.Equal($"Top{Environment.NewLine}Bottom", manualText.ToString());
        Assert.Equal([18d], manualTable.ManualRowSeparators);
    }

    [WpfFact]
    public void AnalyzeAsTable_ManualColumnSeparatorSplitsMergedColumnOutput()
    {
        List<WordBorderInfo> automaticInfos =
        [
            CreateWord("LeftTop", left: 10, top: 10, width: 12, height: 10),
            CreateWord("RightTop", left: 30, top: 10, width: 18, height: 10),
            CreateWord("LeftBottom", left: 10, top: 32, width: 20, height: 10),
            CreateWord("RightBottom", left: 30, top: 32, width: 28, height: 10)
        ];

        ResultTable automaticTable = new();
        automaticTable.AnalyzeAsTable(automaticInfos, new Rectangle(0, 0, 200, 200));

        StringBuilder automaticText = new();
        ResultTable.GetTextFromTabledWordBorders(automaticText, automaticInfos, true);
        Assert.Equal($"LeftTop RightTop{Environment.NewLine}LeftBottom RightBottom", automaticText.ToString());

        List<WordBorderInfo> manualInfos =
        [
            CreateWord("LeftTop", left: 10, top: 10, width: 12, height: 10),
            CreateWord("RightTop", left: 30, top: 10, width: 18, height: 10),
            CreateWord("LeftBottom", left: 10, top: 32, width: 20, height: 10),
            CreateWord("RightBottom", left: 30, top: 32, width: 28, height: 10)
        ];

        ResultTable manualTable = new();
        manualTable.AnalyzeAsTable(
            manualInfos,
            new Rectangle(0, 0, 200, 200),
            manualRowSeparators: null,
            manualColumnSeparators: [25d]);

        StringBuilder manualText = new();
        ResultTable.GetTextFromTabledWordBorders(manualText, manualInfos, true);

        Assert.Equal($"LeftTop\tRightTop{Environment.NewLine}LeftBottom\tRightBottom", manualText.ToString());
        Assert.Equal([25d], manualTable.ManualColumnSeparators);
    }

    [WpfFact]
    public void GetTextFromTabledWordBorders_SingleRowWithDistinctColumns_StillTabSeparates()
    {
        // Regression: capturing just one row of a table (e.g. grabbing rows one at a time into
        // a spreadsheet) must not lose column structure just because that single grab only ever
        // sees one row — previously a same-row, different-column pair got glued together with
        // no separator at all ("NameAge") since tabs required 2+ rows to be detected first.
        List<WordBorderInfo> infos =
        [
            CreateWord("Name", left: 10, top: 10, width: 40, height: 10),
            CreateWord("Age", left: 200, top: 10, width: 30, height: 10)
        ];

        ResultTable table = new();
        table.AnalyzeAsTable(infos, new Rectangle(0, 0, 400, 200));

        StringBuilder text = new();
        ResultTable.GetTextFromTabledWordBorders(text, infos, true);

        Assert.Equal("Name\tAge", text.ToString());
    }

    private static WordBorderInfo CreateWord(string word, double left, double top, double width, double height)
    {
        return new WordBorderInfo
        {
            Word = word,
            BorderRect = new RectangleF((float)left, (float)top, (float)width, (float)height)
        };
    }
}
