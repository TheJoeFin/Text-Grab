using System.Collections.Generic;
using System.Drawing;
using Text_Grab.Models;

namespace Tests;

public class ResultTableBoundsFilterTests
{
    [WpfFact]
    public void FilterWordBordersWithinBounds_KeepsOnlyWordsCenteredInsideBounds()
    {
        WordBorderInfo inside = CreateWord("Inside", left: 20, top: 20, width: 30, height: 10);
        WordBorderInfo outsideLeft = CreateWord("OutsideLeft", left: -50, top: 20, width: 30, height: 10);
        WordBorderInfo outsideBelow = CreateWord("OutsideBelow", left: 20, top: 500, width: 30, height: 10);

        List<WordBorderInfo> all = [inside, outsideLeft, outsideBelow];
        RectangleF bounds = new(0, 0, 100, 100);

        List<WordBorderInfo> filtered = ResultTable.FilterWordBordersWithinBounds(all, bounds);

        Assert.Single(filtered);
        Assert.Same(inside, filtered[0]);
    }

    [WpfFact]
    public void FilterWordBordersWithinBounds_WordStraddlingEdge_IsKeptOnlyWhenCenterIsInside()
    {
        // Center at x=95, inside a bounds right edge of 100
        WordBorderInfo straddlingInside = CreateWord("StraddlingInside", left: 80, top: 10, width: 30, height: 10);
        // Center at x=115, outside the same bounds
        WordBorderInfo straddlingOutside = CreateWord("StraddlingOutside", left: 100, top: 10, width: 30, height: 10);

        RectangleF bounds = new(0, 0, 100, 100);

        List<WordBorderInfo> filtered = ResultTable.FilterWordBordersWithinBounds(
            [straddlingInside, straddlingOutside],
            bounds);

        Assert.Single(filtered);
        Assert.Same(straddlingInside, filtered[0]);
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
