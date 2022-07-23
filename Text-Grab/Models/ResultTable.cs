using System.Collections.Generic;
using Windows.Foundation;
using Windows.Media.Ocr;

namespace Text_Grab.Models;

public class ResultTable
{
    public List<ResultColumn> Columns { get; set; } = new();

    public List<ResultRow> Rows { get; set; } = new();

    private OcrResult? OcrResult { get; set; }

    public Rect BoundingRect { get; set; } = new();

    public List<int> ColumnLines { get; set; } = new();

    public List<int> RowLines { get; set; } = new();

    public ResultTable(List<ResultColumn> ColumnsArgs, List<ResultRow> RowsArgs)
    {
        Columns.Clear();
        Columns.AddRange(ColumnsArgs);
        Rows.Clear();
        Rows.AddRange(RowsArgs);

        ParseRowAndColumnLines();
    }

    public ResultTable(OcrResult ocrResultParam)
    {
        OcrResult = ocrResultParam;
        ParseOcrResultIntoResultTable();
    }

    private void ParseRowAndColumnLines()
    {
        // Draw Bounding Rect
        int topBound = 0;
        int bottomBound = topBound;
        int leftBound = 0;
        int rightBound = leftBound;

        if (Rows.Count == 1)
        {
            topBound = (int)Rows[0].Top;
            bottomBound = (int)Rows[0].Bottom;
        }
        else if (Rows.Count > 1)
        {
            topBound = (int)Rows[0].Top;
            bottomBound = (int)Rows[Rows.Count - 1].Bottom;
        }

        if (Columns.Count == 1)
        {
            leftBound = (int)Columns[0].Left;
            rightBound = (int)Columns[0].Right;
        }
        else if (Columns.Count > 1)
        {
            leftBound = (int)Columns[0].Left;
            rightBound = (int)Columns[Columns.Count - 1].Right;
        }

        BoundingRect = new()
        {
            Width = (rightBound - leftBound) + 10,
            Height = (bottomBound - topBound) + 10,
            X = leftBound - 5,
            Y = topBound - 5
        };

        // parse columns
        ColumnLines = new();

        for (int i = 0; i < Columns.Count - 1; i++)
        {
            int columnMid = (int)(Columns[i].Right + Columns[i + 1].Left) / 2;
            ColumnLines.Add(columnMid);
        }


        // parse rows
        RowLines = new();

        for (int i = 0; i < Rows.Count - 1; i++)
        {
            int rowMid = (int)(Rows[i].Bottom + Rows[i + 1].Top) / 2;
            RowLines.Add(rowMid);
        }
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
