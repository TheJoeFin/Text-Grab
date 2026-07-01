
namespace Text_Grab.Models;

public class FindResult
{
    public string Text { get; set; } = "";

    public int Count { get; set; } = 0;

    public int Index { get; set; }

    public string PreviewLeft { get; set; } = "";

    public string PreviewRight { get; set; } = "";

    public int Length { get; set; }

    public int? RowIndex { get; set; }

    public int? ColumnIndex { get; set; }

    public string CellAddress
    {
        get
        {
            if (RowIndex is null || ColumnIndex is null) return string.Empty;
            string colLabel = EditTextTableDocument.GetSpreadsheetColumnLabel(ColumnIndex.Value);
            return $"Cell: {colLabel}{RowIndex.Value + 1}";
        }
    }

    public string LocationDisplay =>
        CellAddress.Length > 0 ? CellAddress : $"At index: {Index}";
}
