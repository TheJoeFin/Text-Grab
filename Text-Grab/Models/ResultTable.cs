using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Text_Grab.Controls;
using Text_Grab.Utilities;
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

    public ResultTable()
    {

    }

    public ResultTable(ref List<WordBorder> wordBorders, DpiScale dpiScale)
    {
        int borderBuffer = 3;

        Rectangle bordersBorder = new();
        if (wordBorders.Count > 0)
        {
            var leftsMin = wordBorders.Select(x => x.Left).Min();
            var topsMin = wordBorders.Select(x => x.Top).Min();
            var rightsMax = wordBorders.Select(x => x.Right).Max();
            var bottomsMax = wordBorders.Select(x => x.Bottom).Max();

            bordersBorder = new()
            {
                X = (int)leftsMin - borderBuffer,
                Y = (int)topsMin - borderBuffer,
                Width = (int)(rightsMax + borderBuffer),
                Height = (int)(bottomsMax + borderBuffer)
            };
        }

        bordersBorder.Width = (int)(bordersBorder.Width * dpiScale.DpiScaleX);
        bordersBorder.Height = (int)(bordersBorder.Height * dpiScale.DpiScaleY);

        AnalyzeAsTable(wordBorders, bordersBorder);
    }

    private void ParseRowAndColumnLines()
    {
        // Draw Bounding Rect
        int topBound = 0;
        int bottomBound = topBound;
        int leftBound = 0;
        int rightBound = leftBound;

        if (Rows.Count >= 1)
        {
            topBound = (int)Rows[0].Top;
            bottomBound = (int)Rows[Rows.Count - 1].Bottom;
        }

        if (Columns.Count >= 1)
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

    private List<Rect> ParseOcrResultWordsIntoRects()
    {
        List<Rect> allBoundingRects = new();

        if (OcrResult is null)
            return allBoundingRects;

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

        return allBoundingRects;
    }

    public static List<WordBorder> ParseOcrResultIntoWordBorders(OcrResult ocrResult, DpiScale dpi)
    {
        List<WordBorder> wordBorders = new();
        int lineNumber = 0;

        foreach (OcrLine ocrLine in ocrResult.Lines)
        {
            double top = ocrLine.Words.Select(x => x.BoundingRect.Top).Min();
            double bottom = ocrLine.Words.Select(x => x.BoundingRect.Bottom).Max();
            double left = ocrLine.Words.Select(x => x.BoundingRect.Left).Min();
            double right = ocrLine.Words.Select(x => x.BoundingRect.Right).Max();

            Rect lineRect = new()
            {
                X = left,
                Y = top,
                Width = Math.Abs(right - left),
                Height = Math.Abs(bottom - top)
            };

            StringBuilder lineText = new();
            ocrLine.GetTextFromOcrLine(true, lineText);

            WordBorder wordBorderBox = new()
            {
                Width = lineRect.Width / (dpi.DpiScaleX),
                Height = lineRect.Height / (dpi.DpiScaleY),
                Top = lineRect.Y,
                Left = lineRect.X,
                Word = lineText.ToString().Trim(),
                ToolTip = ocrLine.Text,
                LineNumber = lineNumber,
            };
            wordBorders.Add(wordBorderBox);

            lineNumber++;
        }

        return wordBorders;
    }

    public void AnalyzeAsTable(ICollection<WordBorder> wordBorders, Rectangle rectCanvasSize)
    {
        int hitGridSpacing = 3;

        int numberOfVerticalLines = rectCanvasSize.Width / hitGridSpacing;
        int numberOfHorizontalLines = rectCanvasSize.Height / hitGridSpacing;

        Canvas tableIntersectionCanvas = new();

        List<int> rowAreas = CalculateRowAreas(rectCanvasSize, hitGridSpacing, numberOfHorizontalLines, tableIntersectionCanvas, wordBorders);
        List<ResultRow> resultRows = CalculateResultRows(hitGridSpacing, rowAreas);

        List<int> columnAreas = CalculateColumnAreas(rectCanvasSize, hitGridSpacing, numberOfVerticalLines, tableIntersectionCanvas, wordBorders);
        List<ResultColumn> resultColumns = CalculateResultColumns(hitGridSpacing, columnAreas);

        Rect tableBoundingRect = new()
        {
            X = columnAreas.FirstOrDefault(),
            Y = rowAreas.FirstOrDefault(),
            Width = columnAreas.LastOrDefault() - columnAreas.FirstOrDefault(),
            Height = rowAreas.LastOrDefault() - rowAreas.FirstOrDefault()
        };

        wordBorders = CombineOutliers(wordBorders, resultRows, tableIntersectionCanvas, resultColumns, tableBoundingRect);

        Rows.Clear();
        Rows.AddRange(resultRows);
        Columns.Clear();
        Columns.AddRange(resultColumns);

        ParseRowAndColumnLines();
        DrawTable();
    }

    private static List<ResultRow> CalculateResultRows(int hitGridSpacing, List<int> rowAreas)
    {
        List<ResultRow> resultRows = new();
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

        return resultRows;
    }

    private static List<int> CalculateRowAreas(Rectangle rectCanvasSize, int hitGridSpacing, int numberOfHorizontalLines, Canvas tableIntersectionCanvas, ICollection<WordBorder> wordBorders)
    {
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

            CheckInersectionsWithWordBorders(hitGridSpacing, wordBorders, rowAreas, i, horzLineRect);
        }

        return rowAreas;
    }

    private static void CheckInersectionsWithWordBorders(int hitGridSpacing, ICollection<WordBorder> wordBorders, ICollection<int> rowAreas, int i, Rect horzLineRect)
    {
        foreach (WordBorder wb in wordBorders)
        {
            if (wb.IntersectsWith(horzLineRect))
            {
                rowAreas.Add(i * hitGridSpacing);
                break;
            }
        }
    }

    private static ICollection<WordBorder> CombineOutliers(ICollection<WordBorder> wordBorders, List<ResultRow> resultRows, Canvas tableIntersectionCanvas, List<ResultColumn> resultColumns, Rect tableBoundingRect)
    {
        // try 4 times to refine the rows and columns for outliers
        // on the fifth time set the word boundery properties
        for (int r = 0; r < 5; r++)
        {
            int outlierThreshould = 2;
            List<int> outlierRowIDs = FindOutlierRowIds(wordBorders, resultRows, tableIntersectionCanvas, tableBoundingRect, r, outlierThreshould);

            if (outlierRowIDs.Count > 0)
                mergeTheseRowIDs(resultRows, outlierRowIDs);


            List<int> outlierColumnIDs = FindOutlierColumnIds(wordBorders, tableIntersectionCanvas, resultColumns, tableBoundingRect, outlierThreshould);

            if (outlierColumnIDs.Count > 0 && r != 4)
                mergetheseColumnIDs(resultColumns, outlierColumnIDs);
        }

        return wordBorders;
    }

    private static List<int> FindOutlierRowIds(
        ICollection<WordBorder> wordBorders,
        ICollection<ResultRow> resultRows,
        Canvas tableIntersectionCanvas,
        Rect tableBoundingRect,
        int r,
        int outlierThreshould)
    {
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
                if (wb.IntersectsWith(rowRect))
                {
                    numberOfIntersectingWords++;
                    wb.ResultRowID = row.ID;
                }
            }

            if (numberOfIntersectingWords <= outlierThreshould && r != 4)
                outlierRowIDs.Add(row.ID);
        }

        return outlierRowIDs;
    }

    private static List<int> FindOutlierColumnIds(
        ICollection<WordBorder> wordBorders,
        Canvas tableIntersectionCanvas,
        List<ResultColumn> resultColumns,
        Rect tableBoundingRect,
        int outlierThreshould)
    {
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
                if (wb.IntersectsWith(columnRect))
                {
                    numberOfIntersectingWords++;
                    wb.ResultColumnID = column.ID;
                }
            }

            if (numberOfIntersectingWords <= outlierThreshould)
                outlierColumnIDs.Add(column.ID);
        }

        return outlierColumnIDs;
    }

    private static List<ResultColumn> CalculateResultColumns(int hitGridSpacing, List<int> columnAreas)
    {
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

        return resultColumns;
    }

    private static List<int> CalculateColumnAreas(Rectangle rectCanvasSize, int hitGridSpacing, int numberOfVerticalLines, Canvas tableIntersectionCanvas, ICollection<WordBorder> wordBorders)
    {
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

            foreach (WordBorder wb in wordBorders)
            {
                if (wb.IntersectsWith(vertLineRect))
                {
                    columnAreas.Add(i * hitGridSpacing);
                    break;
                }
            }
        }

        return columnAreas;
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

    public static void GetTextFromTabledWordBorders(StringBuilder stringBuilder, List<WordBorder> wordBorders, bool isSpaceJoining)
    {
        List<WordBorder>? selectedBorders = wordBorders.Where(w => w.IsSelected).ToList();

        if (selectedBorders.Count == 0)
            selectedBorders.AddRange(wordBorders);

        List<string> lineList = new();
        int? lastLineNum = 0;
        int lastColumnNum = 0;

        if (selectedBorders.FirstOrDefault() != null)
            lastLineNum = selectedBorders.FirstOrDefault()!.LineNumber;

        selectedBorders = selectedBorders.OrderBy(x => x.ResultColumnID).ToList();
        selectedBorders = selectedBorders.OrderBy(x => x.ResultRowID).ToList();

        int numberOfDistinctRows = selectedBorders.Select(x => x.ResultRowID).Distinct().Count();

        foreach (WordBorder border in selectedBorders)
        {
            if (lineList.Count == 0)
                lastLineNum = border.ResultRowID;

            if (border.ResultRowID != lastLineNum)
            {
                if (isSpaceJoining)
                    stringBuilder.Append(string.Join(' ', lineList));
                else
                    stringBuilder.Append(string.Join("", lineList));

                stringBuilder.Replace(" \t ", "\t");
                stringBuilder.Replace("\t ", "\t");
                stringBuilder.Replace(" \t", "\t");
                stringBuilder.Append(Environment.NewLine);
                lineList.Clear();
                lastLineNum = border.ResultRowID;
            }

            if (border.ResultColumnID != lastColumnNum && numberOfDistinctRows > 1)
            {
                string borderWord = border.Word;
                int numberOfOffColumns = border.ResultColumnID - lastColumnNum;
                if (numberOfOffColumns < 0)
                    lastColumnNum = 0;

                numberOfOffColumns = border.ResultColumnID - lastColumnNum;

                if (numberOfOffColumns > 0)
                    lineList.Add(new string('\t', numberOfOffColumns));
            }
            lastColumnNum = border.ResultColumnID;

            lineList.Add(border.Word);
        }

        stringBuilder.Append(string.Join("", lineList));
    }

    private static void mergeTheseRowIDs(List<ResultRow> resultRows, List<int> outlierRowIDs)
    {

    }

    private void DrawTable()
    {
        // Draw the lines and bounds of the table
        SolidColorBrush tableColor = new(System.Windows.Media.Color.FromArgb(255, 40, 118, 126));

        TableLines = new Canvas()
        {
            Tag = "TableLines"
        };

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

    public static string GetWordsAsTable(List<WordBorder> wordBorders, DpiScale dpiScale, bool isSpaceJoining)
    {
        List<WordBorder> smallerBorders = new();
        foreach (WordBorder originalWB in wordBorders)
        {
            WordBorder newWB = new()
            {
                Word = originalWB.Word,
                Left = originalWB.Left,
                Top = originalWB.Top,
                Width = originalWB.Width > 10 ? originalWB.Width - 6 : originalWB.Width,
                Height = originalWB.Height > 10 ? originalWB.Height - 6 : originalWB.Height,
                ResultRowID = originalWB.ResultRowID,
                ResultColumnID = originalWB.ResultColumnID,
            };
            smallerBorders.Add(newWB);
        };

        ResultTable resultTable = new(ref smallerBorders, dpiScale);
        StringBuilder stringBuilder = new();
        GetTextFromTabledWordBorders(
            stringBuilder,
            smallerBorders,
            isSpaceJoining);
        return stringBuilder.ToString();
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
