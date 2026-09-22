using Text_Grab.Utilities;

namespace Tests;

public class ClipboardUtilitiesTests
{
    private const string SampleCfHtml = """
        Version:1.0
        StartHTML:00000097
        EndHTML:00002353
        StartFragment:00000153
        EndFragment:00002320
        <!DOCTYPE><HTML><HEAD></HEAD><BODY><!--StartFragment --><html>
            <body>
                <table>
                    <tr>
                        <td>Month</td>
                        <td>Int</td>
                        <td>Season</td>
                    </tr>
                    <tr>
                        <td>January</td>
                        <td>1</td>
                        <td>Winter</td>
                    </tr>
                    <tr>
                        <td>February</td>
                        <td>2</td>
                        <td>Winter</td>
                    </tr>
                </table>
            </body>
        </html><!--EndFragment --></BODY></HTML>
        """;

    [Fact]
    public void ConvertHtmlToTabSeparated_ParsesBasicTable()
    {
        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(SampleCfHtml);

        string[] lines = result.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("Month\tInt\tSeason", lines[0]);
        Assert.Equal("January\t1\tWinter", lines[1]);
        Assert.Equal("February\t2\tWinter", lines[2]);
    }

    [Fact]
    public void ConvertHtmlToTabSeparated_HandlesBrTag()
    {
        string html = """
            <!--StartFragment--><table>
                <tr><td>4<br/>A</td><td>Spring</td></tr>
            </table><!--EndFragment-->
            """;

        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(html);

        Assert.Equal("4 A\tSpring", result);
    }

    [Fact]
    public void ConvertHtmlToTabSeparated_ReturnsEmptyWhenNoTable()
    {
        string html = "<!--StartFragment--><p>No table here</p><!--EndFragment-->";
        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(html);
        Assert.Empty(result);
    }

    [Fact]
    public void ConvertHtmlToTabSeparated_DecodesHtmlEntities()
    {
        string html = """
            <!--StartFragment--><table>
                <tr><td>A &amp; B</td><td>&lt;tag&gt;</td></tr>
            </table><!--EndFragment-->
            """;

        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(html);

        Assert.Equal("A & B\t<tag>", result);
    }

    [Fact]
    public void ConvertHtmlToTabSeparated_HandlesThElements()
    {
        string html = """
            <!--StartFragment--><table>
                <tr><th>Name</th><th>Value</th></tr>
                <tr><td>Foo</td><td>42</td></tr>
            </table><!--EndFragment-->
            """;

        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(html);

        string[] lines = result.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Name\tValue", lines[0]);
        Assert.Equal("Foo\t42", lines[1]);
    }

    [Fact]
    public void ConvertHtmlToTabSeparated_HandlesColspan()
    {
        string html = """
            <!--StartFragment--><table>
                <tr><td colspan="2">Merged</td><td>Right</td></tr>
                <tr><td>A</td><td>B</td><td>C</td></tr>
            </table><!--EndFragment-->
            """;

        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(html);

        string[] lines = result.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Merged\tMerged\tRight", lines[0]);
        Assert.Equal("A\tB\tC", lines[1]);
    }

    [Fact]
    public void ConvertHtmlToTabSeparated_HandlesRowspan()
    {
        string html = """
            <!--StartFragment--><table>
                <tr><td rowspan="2">Tall</td><td>Top</td></tr>
                <tr><td>Bottom</td></tr>
            </table><!--EndFragment-->
            """;

        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(html);

        string[] lines = result.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Tall\tTop", lines[0]);
        Assert.Equal("Tall\tBottom", lines[1]);
    }

    [Fact]
    public void ConvertHtmlToTabSeparated_DoesNotOverwriteRowspanWithColspan()
    {
        string html = """
            <!--StartFragment--><table>
                <tr><td>Left</td><td rowspan="2">Tall</td><td>Right</td></tr>
                <tr><td colspan="2">Merged</td></tr>
            </table><!--EndFragment-->
            """;

        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(html);

        string[] lines = result.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Left\tTall\tRight", lines[0]);
        Assert.Equal("\tTall\tMerged\tMerged", lines[1]);
    }

    // The Text Grab browser extension's Table mode (including its layout
    // reconstruction fallback for non-<table> grids) writes a clean
    // <table><tr><td>…</td></tr></table> to the clipboard with <br> for
    // newlines and &amp;-style entity escaping, then hands off via
    // text-grab://paste-spreadsheet. This pins compatibility with that exact
    // output (see Text-Grab-Extension/lib/formats.js -> toCleanHtmlTable).
    private const string ExtensionRegionTableCfHtml = """
        Version:0.9
        StartHTML:00000097
        EndHTML:00000260
        StartFragment:00000131
        EndFragment:00000224
        <html><body>
        <!--StartFragment--><table><tr><td>Product</td><td>Qty</td><td>Unit price</td></tr><tr><td>USB-C hub</td><td>12</td><td>$24.50</td></tr><tr><td>Monitor<br>arm</td><td>5</td><td>$130 &amp; up</td></tr></table><!--EndFragment-->
        </body></html>
        """;

    [Fact]
    public void ConvertHtmlToTabSeparated_ParsesBrowserExtensionRegionTable()
    {
        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(ExtensionRegionTableCfHtml);

        string[] lines = result.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal("Product\tQty\tUnit price", lines[0]);
        Assert.Equal("USB-C hub\t12\t$24.50", lines[1]);
        // <br> collapses to a space; &amp; decodes to &.
        Assert.Equal("Monitor arm\t5\t$130 & up", lines[2]);
    }

    [Fact]
    public void BuildCfHtmlTable_RoundTripsThroughConvertHtmlToTabSeparated()
    {
        string cfHtml = CfHtmlTableUtilities.BuildCfHtmlTable(
            [
                ["Month", "Int", "Season"],
                ["January", "1", "Winter"],
            ]);

        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(cfHtml);

        string[] lines = result.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Month\tInt\tSeason", lines[0]);
        Assert.Equal("January\t1\tWinter", lines[1]);
    }

    [Fact]
    public void BuildCfHtmlTable_HeaderOffsetsPointAtFragmentBoundaries()
    {
        string cfHtml = CfHtmlTableUtilities.BuildCfHtmlTable([["a", "b"]]);

        int startHtml = int.Parse(cfHtml.Substring(cfHtml.IndexOf("StartHTML:") + "StartHTML:".Length, 10));
        int endHtml = int.Parse(cfHtml.Substring(cfHtml.IndexOf("EndHTML:") + "EndHTML:".Length, 10));
        int startFragment = int.Parse(cfHtml.Substring(cfHtml.IndexOf("StartFragment:") + "StartFragment:".Length, 10));
        int endFragment = int.Parse(cfHtml.Substring(cfHtml.IndexOf("EndFragment:") + "EndFragment:".Length, 10));

        byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(cfHtml);

        Assert.True(startHtml < startFragment);
        Assert.True(startFragment < endFragment);
        Assert.True(endFragment <= endHtml);
        Assert.True(endHtml <= utf8Bytes.Length);

        string fragment = System.Text.Encoding.UTF8.GetString(utf8Bytes, startFragment, endFragment - startFragment);
        Assert.Equal("<table border=\"1\" style=\"border-collapse:collapse\"><tr><td>a</td><td>b</td></tr></table>", fragment);
    }

    [Fact]
    public void BuildCfHtmlTable_EscapesHtmlAndConvertsNewlinesToBreaks()
    {
        string cfHtml = CfHtmlTableUtilities.BuildCfHtmlTable([["<b>A & B</b>", "line1\r\nline2"]]);

        string result = CfHtmlTableUtilities.ConvertHtmlToTabSeparated(cfHtml);

        Assert.Equal("<b>A & B</b>\tline1 line2", result);
    }

    [Fact]
    public void BuildCfHtmlTable_ReturnsEmptyForNoRows()
    {
        Assert.Equal(string.Empty, CfHtmlTableUtilities.BuildCfHtmlTable([]));
    }
}
