using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Text_Grab.Controls;
using Windows.Foundation;
using Windows.Media.Ocr;
using Rect = System.Windows.Rect;

namespace Text_Grab.Models;

public class ResultTable
{
    public List<ResultColumn> Columns { get; set; } = new();

    public List<ResultRow> Rows { get; set; } = new();

    private OcrResult? OcrResult { get; set; }

    public Rect BoundingRect { get; set; } = new();

    public List<int> ColumnLines { get; set; } = new();

    public List<int> RowLines { get; set; } = new();

    public Canvas? TableLines { get; set; } = null;

    public ResultTable(OcrResult ocrResultParam)
    {
        OcrResult = ocrResultParam;
        ParseOcrResultIntoResultTable();
    }

    public ResultTable()
    {

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
                Rect ocrWordRect = new(
                    ocrWord.BoundingRect.X, 
                    ocrWord.BoundingRect.Y, 
                    ocrWord.BoundingRect.Width, 
                    ocrWord.BoundingRect.Height);

                allBoundingRects.Add(ocrWordRect);
                // Debug.WriteLine($"{ocrWord.Text}\t{ocrWord.BoundingRect.Height}\t{ocrWord.BoundingRect.Width}\t{ocrWord.BoundingRect.X}\t{ocrWord.BoundingRect.Y}");
            }
        }
    }

    public void AnalyzeAsTable(List<WordBorder> wordBorders, Rectangle rectCanvasSize)
    {
        int hitGridSpacing = 3;

        int numberOfVerticalLines = rectCanvasSize.Width / hitGridSpacing;
        int numberOfHorizontalLines = rectCanvasSize.Height / hitGridSpacing;

        List<ResultRow> resultRows = new();
        Canvas tableIntersectionCanvas = new();

        List<Rect> wbRects = new();
        foreach (WordBorder wb in wordBorders)
        {
            Rect wbRect = new Rect(Canvas.GetLeft(wb), Canvas.GetTop(wb), wb.Width, wb.Height);
            wbRects.Add(wbRect);
        }

        List<int> rowAreas = new();

        for (int i = 0; i < numberOfHorizontalLines; i++)
        {
            Border horzLine = new()
            {
                Height = 1,
                Width = rectCanvasSize.Width,
                Opacity = 0,
                Background = new SolidColorBrush(Colors.Gray)
            };
            Rect horzLineRect = new(0, i * hitGridSpacing, horzLine.Width, horzLine.Height);
            _ = tableIntersectionCanvas.Children.Add(horzLine);
            Canvas.SetTop(horzLine, i * 3);

            foreach (Rect wbRect in wbRects)
            {
                if (horzLineRect.IntersectsWith(wbRect))
                {
                    rowAreas.Add(i * hitGridSpacing);
                    break;
                }
            }

            //foreach (var child in tableIntersectionCanvas.Children)
            //{
            //    if (child is WordBorder wb)
            //    {
            //        Rect wbRect = new Rect(Canvas.GetLeft(wb), Canvas.GetTop(wb), wb.Width, wb.Height);
            //        if (horzLineRect.IntersectsWith(wbRect))
            //        {
            //            rowAreas.Add(i * hitGridSpacing);
            //            break;
            //        }
            //    }
            //}
        }

        int rowTop = 0;
        int rowCount = 0;
        for (int i = 0; i < rowAreas.Count; i++)
        {
            int thisLine = rowAreas[i];

            // check if should set this as top
            if (i == 0)
                rowTop = thisLine;
            else if (i - 1 > 0)
            {
                int prevRow = rowAreas[i - 1];
                if (thisLine - prevRow != hitGridSpacing)
                {
                    rowTop = thisLine;
                }
            }

            // check to see if at bottom of row
            if (i == rowAreas.Count - 1)
            {
                resultRows.Add(new ResultRow { Top = rowTop, Bottom = thisLine, ID = rowCount });
                rowCount++;
            }
            else if (i + 1 < rowAreas.Count)
            {
                int nextRow = rowAreas[i + 1];
                if (nextRow - thisLine != hitGridSpacing)
                {
                    resultRows.Add(new ResultRow { Top = rowTop, Bottom = thisLine, ID = rowCount });
                    rowCount++;
                }
            }
        }

        List<int> columnAreas = new();
        for (int i = 0; i < numberOfVerticalLines; i++)
        {
            Border vertLine = new()
            {
                Height = rectCanvasSize.Height,
                Width = 1,
                Opacity = 0,
                Background = new SolidColorBrush(Colors.Gray)
            };
            _ = tableIntersectionCanvas.Children.Add(vertLine);
            Canvas.SetLeft(vertLine, i * hitGridSpacing);

            Rect vertLineRect = new(i * hitGridSpacing, 0, vertLine.Width, vertLine.Height);

            foreach (Rect wbRect in wbRects)
            {
                if (vertLineRect.IntersectsWith(wbRect))
                {
                    columnAreas.Add(i * hitGridSpacing);
                    break;
                }
            }

            //foreach (var child in tableIntersectionCanvas.Children)
            //{
            //    if (child is WordBorder wb)
            //    {
            //        Rect wbRect = new Rect(Canvas.GetLeft(wb), Canvas.GetTop(wb), wb.Width, wb.Height);
            //        if (vertLineRect.IntersectsWith(wbRect))
            //        {
            //            columnAreas.Add(i * hitGridSpacing);
            //            break;
            //        }
            //    }
            //}
        }

        List<ResultColumn> resultColumns = new();
        int columnLeft = 0;
        int columnCount = 0;
        for (int i = 0; i < columnAreas.Count; i++)
        {
            int thisLine = columnAreas[i];

            // check if should set this as top
            if (i == 0)
                columnLeft = thisLine;
            else if (i - 1 > 0)
            {
                int prevColumn = columnAreas[i - 1];
                if (thisLine - prevColumn != hitGridSpacing)
                {
                    columnLeft = thisLine;
                }
            }

            // check to see if at last Column
            if (i == columnAreas.Count - 1)
            {
                resultColumns.Add(new ResultColumn { Left = columnLeft, Right = thisLine, ID = columnCount });
                columnCount++;
            }
            else if (i + 1 < columnAreas.Count)
            {
                int nextColumn = columnAreas[i + 1];
                if (nextColumn - thisLine != hitGridSpacing)
                {
                    resultColumns.Add(new ResultColumn { Left = columnLeft, Right = thisLine, ID = columnCount });
                    columnCount++;
                }
            }
        }

        Rect tableBoundingRect = new()
        {
            X = columnAreas.FirstOrDefault(),
            Y = rowAreas.FirstOrDefault(),
            Width = columnAreas.LastOrDefault() - columnAreas.FirstOrDefault(),
            Height = rowAreas.LastOrDefault() - rowAreas.FirstOrDefault()
        };

        // try 4 times to refine the rows and columns for outliers
        // on the fifth time set the word boundery properties
        for (int r = 0; r < 5; r++)
        {
            int outlierThreshould = 2;

            List<int> outlierRowIDs = new();

            foreach (ResultRow row in resultRows)
            {
                int numberOfIntersectingWords = 0;
                Border rowBorder = new()
                {
                    Height = row.Bottom - row.Top,
                    Width = tableBoundingRect.Width,
                    Background = new SolidColorBrush(Colors.Red),
                    Tag = row.ID
                };
                tableIntersectionCanvas.Children.Add(rowBorder);
                Canvas.SetLeft(rowBorder, tableBoundingRect.X);
                Canvas.SetTop(rowBorder, row.Top);

                Rect rowRect = new Rect(tableBoundingRect.X, row.Top, rowBorder.Width, rowBorder.Height);

                foreach (WordBorder wb in wordBorders)
                {
                    Rect wbRect = new Rect(Canvas.GetLeft(wb), Canvas.GetTop(wb), wb.Width, wb.Height);
                    if (rowRect.IntersectsWith(wbRect))
                    {
                        numberOfIntersectingWords++;
                        wb.ResultRowID = row.ID;
                    }
                }

                if (numberOfIntersectingWords <= outlierThreshould && r != 4)
                    outlierRowIDs.Add(row.ID);
            }

            if (outlierRowIDs.Count > 0)
                mergeTheseRowIDs(resultRows, outlierRowIDs);


            List<int> outlierColumnIDs = new();

            foreach (ResultColumn column in resultColumns)
            {
                int numberOfIntersectingWords = 0;
                Border columnBorder = new()
                {
                    Height = tableBoundingRect.Height,
                    Width = column.Right - column.Left,
                    Background = new SolidColorBrush(Colors.Blue),
                    Opacity = 0.2,
                    Tag = column.ID
                };
                tableIntersectionCanvas.Children.Add(columnBorder);
                Canvas.SetLeft(columnBorder, column.Left);
                Canvas.SetTop(columnBorder, tableBoundingRect.Y);

                Rect columnRect = new Rect(column.Left, tableBoundingRect.Y, columnBorder.Width, columnBorder.Height);
                foreach (WordBorder wb in wordBorders)
                {
                    Rect wbRect = new Rect(Canvas.GetLeft(wb), Canvas.GetTop(wb), wb.Width, wb.Height);
                    if (columnRect.IntersectsWith(wbRect))
                    {
                        numberOfIntersectingWords++;
                        wb.ResultColumnID = column.ID;
                    }                }

                if (numberOfIntersectingWords <= outlierThreshould)
                    outlierColumnIDs.Add(column.ID);
            }

            if (outlierColumnIDs.Count > 0 && r != 4)
                mergetheseColumnIDs(resultColumns, outlierColumnIDs);
        }

        Rows.Clear();
        Rows.AddRange(resultRows);
        Columns.Clear();
        Columns.AddRange(resultColumns);

        ParseRowAndColumnLines();
        DrawTable();

        // foreach (ResultRow row in resultRows)
        // {
        //     Border rowBorder = new()
        //     {
        //         Height = row.Bottom - row.Top,
        //         Width = tableBoundingRect.Width,
        //         Background = new SolidColorBrush(Colors.Red),
        //         Opacity = 0.2,
        //         Tag = row.ID
        //     };
        //     RectanglesCanvas.Children.Add(rowBorder);
        //     Canvas.SetLeft(rowBorder, tableBoundingRect.X);
        //     Canvas.SetTop(rowBorder, row.Top);
        // }

        // foreach (ResultColumn column in resultColumns)
        // {
        //     Border columnBorder = new()
        //     {
        //         Height = tableBoundingRect.Height,
        //         Width = column.Right - column.Left,
        //         Background = new SolidColorBrush(Colors.Blue),
        //         Opacity = 0.2,
        //         Tag = column.ID
        //     };
        //     RectanglesCanvas.Children.Add(columnBorder);
        //     Canvas.SetLeft(columnBorder, column.Left);
        //     Canvas.SetTop(columnBorder, tableBoundingRect.Y);
        // }
    }

    private static void mergetheseColumnIDs(List<ResultColumn> resultColumns, List<int> outlierColumnIDs)
    {
        for (int i = 0; i < outlierColumnIDs.Count; i++)
        {
            for (int j = 0; j < resultColumns.Count; j++)
            {
                ResultColumn jthColumn = resultColumns[j];
                if (jthColumn.ID == outlierColumnIDs[i])
                {
                    if (j == 0)
                    {
                        // merge with next column if possible
                        if (j + 1 < resultColumns.Count)
                        {
                            ResultColumn nextColumn = resultColumns[j + 1];
                            nextColumn.Left = jthColumn.Left;
                        }
                    }
                    else if (j == resultColumns.Count - 1)
                    {
                        // merge with previous column
                        if (j - 1 >= 0)
                        {
                            ResultColumn prevColumn = resultColumns[j - 1];
                            prevColumn.Right = jthColumn.Right;
                        }
                    }
                    else
                    {
                        // merge with closet column
                        ResultColumn prevColumn = resultColumns[j - 1];
                        ResultColumn nextColumn = resultColumns[j + 1];
                        int distToPrev = (int)(jthColumn.Left - prevColumn.Right);
                        int distToNext = (int)(nextColumn.Left - jthColumn.Right);

                        if (distToNext < distToPrev)
                        {
                            // merge with next column
                            nextColumn.Left = jthColumn.Left;
                        }
                        else
                        {
                            // merge with prev column
                            prevColumn.Right = jthColumn.Right;
                        }
                    }
                    resultColumns.RemoveAt(j);
                }
            }
        }
    }

    private static void mergeTheseRowIDs(List<ResultRow> resultRows, List<int> outlierRowIDs)
    {

    }



    private void DrawTable()
    {
        // Draw the lines and bounds of the table
        SolidColorBrush tableColor = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(255, 40, 118, 126));

        TableLines = new Canvas();

        Border tableOutline = new()
        {
            Width = this.BoundingRect.Width,
            Height = this.BoundingRect.Height,
            BorderThickness = new Thickness(3),
            BorderBrush = tableColor
        };
        TableLines.Children.Add(tableOutline);
        Canvas.SetTop(tableOutline, this.BoundingRect.Y);
        Canvas.SetLeft(tableOutline, this.BoundingRect.X);

        foreach (int columnLine in this.ColumnLines)
        {
            Border vertLine = new()
            {
                Width = 2,
                Height = this.BoundingRect.Height,
                Background = tableColor
            };
            TableLines.Children.Add(vertLine);
            Canvas.SetTop(vertLine, this.BoundingRect.Y);
            Canvas.SetLeft(vertLine, columnLine);
        }

        foreach (int rowLine in this.RowLines)
        {
            Border horzLine = new()
            {
                Height = 2,
                Width = this.BoundingRect.Width,
                Background = tableColor
            };
            TableLines.Children.Add(horzLine);
            Canvas.SetTop(horzLine, rowLine);
            Canvas.SetLeft(horzLine, this.BoundingRect.X);
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
