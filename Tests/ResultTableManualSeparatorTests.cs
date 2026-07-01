using System.Drawing;
using System.Text;
using System.Windows;
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
        automaticTable.AnalyzeAsTable(automaticInfos, new Rectangle(0, 0, 200, 200), drawTable: false);

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
            manualColumnSeparators: null,
            drawTable: false);

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
        automaticTable.AnalyzeAsTable(automaticInfos, new Rectangle(0, 0, 200, 200), drawTable: false);

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
            manualColumnSeparators: [25d],
            drawTable: false);

        StringBuilder manualText = new();
        ResultTable.GetTextFromTabledWordBorders(manualText, manualInfos, true);

        Assert.Equal($"LeftTop\tRightTop{Environment.NewLine}LeftBottom\tRightBottom", manualText.ToString());
        Assert.Equal([25d], manualTable.ManualColumnSeparators);
    }

    private static WordBorderInfo CreateWord(string word, double left, double top, double width, double height)
    {
        return new WordBorderInfo
        {
            Word = word,
            BorderRect = new Rect(left, top, width, height)
        };
    }
}
