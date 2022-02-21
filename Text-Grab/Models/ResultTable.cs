using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Windows.Foundation;
using Windows.Media.Ocr;

namespace Text_Grab.Models;

public class ResultTable
{
    public List<ResultColumn> Columns { get; set; } = new();

    public List<ResultRow> Rows { get; set; } = new();

    private OcrResult? OcrResult { get; set; }

    public Rect? BoundingRect { get; set; }

    public List<int>? ColumnLines;

    public List<int>? RowLines;

    public ResultTable(List<ResultColumn> ColumnsArgs, List<ResultRow> RowsArgs)
    {
        Columns.Clear();
        Columns.AddRange(ColumnsArgs);
        Rows.Clear();
        Rows.AddRange(RowsArgs);
    }

    public ResultTable(OcrResult ocrResultParam)
    {
        OcrResult = ocrResultParam;
        ParseOcrResultIntoResultTable();
    }

    private void ParseOcrResultIntoResultTable()
    {
        if (OcrResult is null)
            return;

        List<Rect> allBoundingRects = new();
        // Debug.WriteLine("Table debug:");
        // Debug.WriteLine("Word Text\tHeight\tWidth\tX\tY");
        foreach (OcrLine ocrLine in OcrResult.Lines)
        {
            foreach (OcrWord ocrWord in ocrLine.Words)
            {
                allBoundingRects.Add(ocrWord.BoundingRect);
                // Debug.WriteLine($"{ocrWord.Text}\t{ocrWord.BoundingRect.Height}\t{ocrWord.BoundingRect.Width}\t{ocrWord.BoundingRect.X}\t{ocrWord.BoundingRect.Y}");
            }
        }
    }
}


public class ResultColumn
{
    public double Width { get; set; } = 0;

    public double Left { get; set; } = 0;

    public double Right { get; set; } = 0;

    public int ID { get; set; } = 0;
}

public class ResultRow
{
    public double Height { get; set; } = 0;

    public double Top { get; set; } = 0;

    public double Bottom { get; set; } = 0;

    public int ID { get; set; } = 0;
}

public class ResultTableCell
{
    public int ResultRowID { get; set; } = 0;
    public int ResultColumnID { get; set; } = 0;

    public string ResultText { get; set; } = "";
}
