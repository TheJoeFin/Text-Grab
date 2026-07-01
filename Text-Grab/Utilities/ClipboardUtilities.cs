using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;

namespace Text_Grab.Utilities;

public class ClipboardUtilities
{
    public static async Task<(bool, string)> TryGetClipboardText()
    {
        DataPackageView? dataPackageView = null;
        string clipboardText = "";

        try
        {
            dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        }
        catch (System.Exception ex)
        {
            return (false, ex.Message);
        }

        if (dataPackageView is null)
        {
            return (false, clipboardText);
        }

        if (dataPackageView.Contains(StandardDataFormats.Text))
        {
            try
            {
                clipboardText = await dataPackageView.GetTextAsync();
            }
            catch (System.Exception ex)
            {
                return (false, $"error with dataPackageView.GetTextAsync(). Exception Message: {ex.Message}");
            }

            return (true, clipboardText);
        }

        return (false, clipboardText);
    }

    public static (bool, ImageSource?) TryGetImageFromClipboard()
    {
        ImageSource? imageSource = null;

        if (!ClipboardContainsBase64Image())
        {
            IDataObject? clipboardData = System.Windows.Clipboard.GetDataObject();
            if (clipboardData is null
                || !clipboardData.GetDataPresent(System.Windows.Forms.DataFormats.Bitmap))
                return (false, null);

            imageSource = System.Windows.Clipboard.GetImage();
        }
        else
        {
            imageSource = GetBase64ClipboardContentAsImageSource();
        }

        if (imageSource is null)
            return (false, null);

        return (true, imageSource);
    }

    private static ImageSource? GetBase64ClipboardContentAsImageSource()
    {
        string? trimmedData = null;

        try { trimmedData = System.Windows.Clipboard.GetText().Trim(); } catch { return null; }
        trimmedData = CleanTeamsBase64Image(trimmedData);

        // used some code from https://github.com/veler/DevToys
        string base64 = trimmedData[(trimmedData.IndexOf(',') + 1)..];
        byte[] bytes = Convert.FromBase64String(base64);

        // cannot dispose of memoryStream or the BitmapImage is empty when the view trys to render
        MemoryStream ms = new(bytes, 0, bytes.Length);
        BitmapImage bmp = new();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.None;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();

        return bmp;
    }

    private static bool ClipboardContainsBase64Image()
    {
        string? trimmedData = null;

        try { trimmedData = System.Windows.Clipboard.GetText().Trim(); } catch { return false; }

        if (string.IsNullOrWhiteSpace(trimmedData))
            return false;
        trimmedData = CleanTeamsBase64Image(trimmedData);
        string fileType = base64ImageExtension(ref trimmedData);

        if (string.IsNullOrWhiteSpace(fileType))
            return false;

        return true;
    }

    private static string CleanTeamsBase64Image(string dirtyTeamsString)
    {
        // TODO: this is a bit hokey, but it works for now.
        // Maybe revist and make more robust.
        const string startingTag = "<img src=\"";
        const string endingTag = "\" alt=\"image\" iscopyblocked=\"false\">";

        if (!dirtyTeamsString.StartsWith(startingTag))
            return dirtyTeamsString;

        StringBuilder sb = new(dirtyTeamsString);
        sb.Replace(startingTag, "");
        sb.Replace(endingTag, "");
        return sb.ToString();
    }

    public static bool TryGetHtmlTableAsTabSeparated(out string tabSeparated)
    {
        tabSeparated = string.Empty;
        try
        {
            if (!System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Html))
                return false;

            string htmlData = System.Windows.Clipboard.GetData(System.Windows.DataFormats.Html) as string ?? string.Empty;
            if (string.IsNullOrEmpty(htmlData))
                return false;

            string result = ConvertHtmlToTabSeparated(htmlData);
            if (string.IsNullOrEmpty(result))
                return false;

            tabSeparated = result;
            return true;
        }
        catch
        {
            return false;
        }
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
                    // Advance past columns already occupied by rowspan carry-overs
                    while (rowData.ContainsKey(nextFreeCol))
                        nextFreeCol++;

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

        return int.TryParse(tagAttributes[valueStart..valueEnd], out int span) && span >= 1 ? span : 1;
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

    private static string base64ImageExtension(ref string base64String)
    {
        // Copied this portion of the code from https://github.com/veler/DevToys
        if (base64String!.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase))
            return ".png";
        else if (base64String!.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase))
            return ".jpeg";
        else if (base64String!.StartsWith("data:image/bmp;base64,", StringComparison.OrdinalIgnoreCase))
            return ".bmp";
        else if (base64String!.StartsWith("data:image/gif;base64,", StringComparison.OrdinalIgnoreCase))
            return ".gif";
        else if (base64String!.StartsWith("data:image/x-icon;base64,", StringComparison.OrdinalIgnoreCase))
            return ".ico";
        else if (base64String!.StartsWith("data:image/svg+xml;base64,", StringComparison.OrdinalIgnoreCase))
            return ".svg";
        else if (base64String!.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase))
            return ".webp";
        else
            return string.Empty;
    }
}
