using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Text_Grab.Utilities;

/// <summary>
/// Pure CF_HTML table parsing and serialization - no clipboard, WPF, WinRT or GDI+ dependency.
/// Builds the CF_HTML fragment Windows' clipboard format expects and parses one back into a
/// tab-separated grid. Split out of ClipboardUtilities, whose remaining clipboard-touching
/// methods call into this for the actual table encoding/decoding.
/// </summary>
public static class CfHtmlTableUtilities
{
    private const int MaxHtmlTableSpan = 16_384;

    public static string BuildCfHtmlTable(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows is null || rows.Count == 0)
            return string.Empty;

        StringBuilder table = new();
        table.Append("<table border=\"1\" style=\"border-collapse:collapse\">");

        foreach (IReadOnlyList<string> row in rows)
        {
            table.Append("<tr>");
            foreach (string cell in row)
            {
                table.Append("<td>");
                table.Append(WebUtility.HtmlEncode(cell ?? string.Empty).Replace("\r\n", "<br>").Replace("\n", "<br>"));
                table.Append("</td>");
            }
            table.Append("</tr>");
        }

        table.Append("</table>");

        return WrapHtmlFragmentAsCfHtml(table.ToString());
    }

    internal static string WrapHtmlFragmentAsCfHtml(string htmlFragment)
    {
        const string htmlPrefix = "<html>\r\n<body>\r\n<!--StartFragment-->";
        const string htmlSuffix = "<!--EndFragment-->\r\n</body>\r\n</html>\r\n";

        // CF_HTML header fields are 10-digit, zero-padded byte offsets into the UTF-8
        // encoded clipboard payload. See https://learn.microsoft.com/windows/win32/dataxchg/html-clipboard-format
        static string BuildHeader(int startHtml, int endHtml, int startFragment, int endFragment) =>
            "Version:0.9\r\n" +
            $"StartHTML:{startHtml:D10}\r\n" +
            $"EndHTML:{endHtml:D10}\r\n" +
            $"StartFragment:{startFragment:D10}\r\n" +
            $"EndFragment:{endFragment:D10}\r\n";

        int headerByteLength = Encoding.UTF8.GetByteCount(BuildHeader(0, 0, 0, 0));
        int startHtmlOffset = headerByteLength;
        int startFragmentOffset = startHtmlOffset + Encoding.UTF8.GetByteCount(htmlPrefix);
        int endFragmentOffset = startFragmentOffset + Encoding.UTF8.GetByteCount(htmlFragment);
        int endHtmlOffset = endFragmentOffset + Encoding.UTF8.GetByteCount(htmlSuffix);

        return BuildHeader(startHtmlOffset, endHtmlOffset, startFragmentOffset, endFragmentOffset)
            + htmlPrefix + htmlFragment + htmlSuffix;
    }

    internal static string ConvertHtmlToTabSeparated(string cfHtml)
    {
        string fragment = ExtractHtmlFragment(cfHtml);
        List<List<string>> table = ParseHtmlTableToGrid(fragment);
        if (table.Count == 0)
            return string.Empty;

        StringBuilder sb = new();
        for (int r = 0; r < table.Count; r++)
        {
            if (r > 0) sb.Append('\n');
            sb.Append(string.Join("\t", table[r]));
        }
        return sb.ToString();
    }

    private static string ExtractHtmlFragment(string cfHtml)
    {
        int startPos = cfHtml.IndexOf("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase);
        if (startPos < 0)
            startPos = cfHtml.IndexOf("<!--StartFragment -->", StringComparison.OrdinalIgnoreCase);

        int endPos = cfHtml.IndexOf("<!--EndFragment-->", StringComparison.OrdinalIgnoreCase);
        if (endPos < 0)
            endPos = cfHtml.IndexOf("<!--EndFragment -->", StringComparison.OrdinalIgnoreCase);

        if (startPos >= 0 && endPos > startPos)
        {
            int fragmentStart = cfHtml.IndexOf("-->", startPos) + 3;
            return cfHtml[fragmentStart..endPos];
        }

        // Fall back to byte-offset headers (StartFragment:/EndFragment:)
        const string startKey = "StartFragment:";
        const string endKey = "EndFragment:";
        int sfIdx = cfHtml.IndexOf(startKey, StringComparison.OrdinalIgnoreCase);
        int efIdx = cfHtml.IndexOf(endKey, StringComparison.OrdinalIgnoreCase);

        if (sfIdx >= 0 && efIdx >= 0)
        {
            int sfNumStart = sfIdx + startKey.Length;
            int sfLineEnd = cfHtml.IndexOf('\n', sfNumStart);
            int efNumStart = efIdx + endKey.Length;
            int efLineEnd = cfHtml.IndexOf('\n', efNumStart);

            if (sfLineEnd > sfNumStart && efLineEnd > efNumStart
                && int.TryParse(cfHtml[sfNumStart..sfLineEnd].Trim(), out int sfOff)
                && int.TryParse(cfHtml[efNumStart..efLineEnd].Trim(), out int efOff)
                && sfOff >= 0 && efOff > sfOff && efOff <= cfHtml.Length)
            {
                return cfHtml[sfOff..efOff];
            }
        }

        return cfHtml;
    }

    private static List<List<string>> ParseHtmlTableToGrid(string html)
    {
        List<List<string>> result = [];
        int tableStart = html.IndexOf("<table", StringComparison.OrdinalIgnoreCase);
        if (tableStart < 0) return result;

        int tableEnd = html.LastIndexOf("</table>", StringComparison.OrdinalIgnoreCase);
        tableEnd = tableEnd >= 0 ? tableEnd + 8 : html.Length;

        string tableHtml = html[tableStart..tableEnd];

        // Tracks cells that span into future rows: col -> (remaining rows to fill, cell content)
        Dictionary<int, (int RemainingRows, string Content)> rowspanMap = [];

        int pos = 0;
        while (pos < tableHtml.Length)
        {
            int rowStart = tableHtml.IndexOf("<tr", pos, StringComparison.OrdinalIgnoreCase);
            if (rowStart < 0) break;

            int rowEnd = tableHtml.IndexOf("</tr>", rowStart, StringComparison.OrdinalIgnoreCase);
            rowEnd = rowEnd >= 0 ? rowEnd + 5 : tableHtml.Length;

            List<(string Text, int ColSpan, int RowSpan)> parsedCells =
                ParseHtmlRowCells(tableHtml[rowStart..rowEnd]);

            if (parsedCells.Count > 0 || rowspanMap.Count > 0)
            {
                // Build a sparse column map for this row
                Dictionary<int, string> rowData = [];

                // Apply rowspan carry-overs from previous rows first
                foreach (int col in rowspanMap.Keys.OrderBy(k => k).ToList())
                {
                    (int rem, string content) = rowspanMap[col];
                    rowData[col] = content;
                    if (rem > 1)
                        rowspanMap[col] = (rem - 1, content);
                    else
                        rowspanMap.Remove(col);
                }

                // Place each parsed cell in the next free column(s)
                int nextFreeCol = 0;
                foreach ((string text, int colspan, int rowspan) in parsedCells)
                {
                    nextFreeCol = FindNextFreeColumnRange(rowData, nextFreeCol, colspan);

                    for (int cs = 0; cs < colspan; cs++)
                        rowData[nextFreeCol + cs] = text;

                    if (rowspan > 1)
                        for (int cs = 0; cs < colspan; cs++)
                            rowspanMap[nextFreeCol + cs] = (rowspan - 1, text);

                    nextFreeCol += colspan;
                }

                if (rowData.Count > 0)
                {
                    int colCount = rowData.Keys.Max() + 1;
                    List<string> row = [];
                    for (int c = 0; c < colCount; c++)
                        row.Add(rowData.TryGetValue(c, out string? cell) ? cell : string.Empty);
                    result.Add(row);
                }
            }

            pos = rowEnd;
        }

        return result;
    }

    private static int FindNextFreeColumnRange(
        IReadOnlyDictionary<int, string> rowData,
        int startColumn,
        int columnCount)
    {
        int candidate = Math.Max(0, startColumn);

        while (true)
        {
            bool foundOccupiedColumn = false;
            for (int offset = 0; offset < columnCount; offset++)
            {
                if (!rowData.ContainsKey(candidate + offset))
                    continue;

                candidate += offset + 1;
                foundOccupiedColumn = true;
                break;
            }

            if (!foundOccupiedColumn)
                return candidate;
        }
    }

    private static List<(string Text, int ColSpan, int RowSpan)> ParseHtmlRowCells(string rowHtml)
    {
        List<(string, int, int)> cells = [];
        int pos = 0;

        while (pos < rowHtml.Length)
        {
            int tdPos = rowHtml.IndexOf("<td", pos, StringComparison.OrdinalIgnoreCase);
            int thPos = rowHtml.IndexOf("<th", pos, StringComparison.OrdinalIgnoreCase);

            if (tdPos < 0 && thPos < 0) break;

            int cellStart;
            string endTag;
            if (tdPos >= 0 && (thPos < 0 || tdPos <= thPos))
            {
                cellStart = tdPos;
                endTag = "</td>";
            }
            else
            {
                cellStart = thPos;
                endTag = "</th>";
            }

            int openEnd = rowHtml.IndexOf('>', cellStart);
            if (openEnd < 0) break;

            string tagAttributes = rowHtml[(cellStart + 3)..openEnd];
            int colspan = ParseSpanAttribute(tagAttributes, "colspan");
            int rowspan = ParseSpanAttribute(tagAttributes, "rowspan");

            int contentStart = openEnd + 1;
            int contentEnd = rowHtml.IndexOf(endTag, contentStart, StringComparison.OrdinalIgnoreCase);
            contentEnd = contentEnd >= 0 ? contentEnd : rowHtml.Length;

            cells.Add((CleanHtmlCellContent(rowHtml[contentStart..contentEnd]), colspan, rowspan));
            pos = contentEnd + endTag.Length;
        }

        return cells;
    }

    private static int ParseSpanAttribute(string tagAttributes, string attributeName)
    {
        int attrPos = tagAttributes.IndexOf(attributeName, StringComparison.OrdinalIgnoreCase);
        if (attrPos < 0) return 1;

        int eqPos = tagAttributes.IndexOf('=', attrPos + attributeName.Length);
        if (eqPos < 0) return 1;

        int valueStart = eqPos + 1;
        while (valueStart < tagAttributes.Length && tagAttributes[valueStart] is ' ' or '"' or '\'')
            valueStart++;

        int valueEnd = valueStart;
        while (valueEnd < tagAttributes.Length && char.IsDigit(tagAttributes[valueEnd]))
            valueEnd++;

        if (valueEnd == valueStart) return 1;

        return int.TryParse(tagAttributes[valueStart..valueEnd], out int span) && span >= 1
            ? Math.Min(span, MaxHtmlTableSpan)
            : 1;
    }

    private static string CleanHtmlCellContent(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        html = Regex.Replace(html, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<[^>]*>", string.Empty);
        html = WebUtility.HtmlDecode(html);

        return html.Trim();
    }
}
