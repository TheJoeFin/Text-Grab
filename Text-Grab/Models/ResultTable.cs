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
    public List<ResultColumn> Columns { get; set; } = [];

    public List<ResultRow> Rows { get; set; } = [];

    private OcrResult? OcrResult { get; set; }

    public Rect BoundingRect { get; set; } = new();

    public List<int> ColumnLines { get; set; } = [];

    public List<int> RowLines { get; set; } = [];

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
            double leftsMin = wordBorders.Select(x => x.Left).Min();
            double topsMin = wordBorders.Select(x => x.Top).Min();
            double rightsMax = wordBorders.Select(x => x.Right).Max();
            double bottomsMax = wordBorders.Select(x => x.Bottom).Max();

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
            bottomBound = (int)Rows[^1].Bottom;
        }

        if (Columns.Count >= 1)
        {
            leftBound = (int)Columns[0].Left;
            rightBound = (int)Columns[^1].Right;
        }

        BoundingRect = new()
        {
            Width = rightBound - leftBound + 10,
            Height = bottomBound - topBound + 10,
            X = leftBound - 5,
            Y = topBound - 5
        };

        // parse columns
        ColumnLines = [];

        for (int i = 0; i < Columns.Count - 1; i++)
        {
            int columnMid = (int)(Columns[i].Right + Columns[i + 1].Left) / 2;
            ColumnLines.Add(columnMid);
        }


        // parse rows
        RowLines = [];

        for (int i = 0; i < Rows.Count - 1; i++)
        {
            int rowMid = (int)(Rows[i].Bottom + Rows[i + 1].Top) / 2;
            RowLines.Add(rowMid);
        }
    }

    private List<Rect> ParseOcrResultWordsIntoRects()
    {
        List<Rect> allBoundingRects = [];

        if (OcrResult is null)
            return allBoundingRects;

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
            }
        }

        return allBoundingRects;
    }

    public static List<WordBorder> ParseOcrResultIntoWordBorders(IOcrLinesWords ocrResult, DpiScale dpi)
    {
        List<WordBorder> wordBorders = [];
        int lineNumber = 0;

        foreach (IOcrLine ocrLine in ocrResult.Lines)
        {
            double top = ocrLine.Words.Select(x => x.BoundingBox.Top).Min();
            double bottom = ocrLine.Words.Select(x => x.BoundingBox.Bottom).Max();
            double left = ocrLine.Words.Select(x => x.BoundingBox.Left).Min();
            double right = ocrLine.Words.Select(x => x.BoundingBox.Right).Max();

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
                Width = lineRect.Width / dpi.DpiScaleX,
                Height = lineRect.Height / dpi.DpiScaleY,
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
        // New robust approach: cluster rows and columns using word center positions
        if (wordBorders == null || wordBorders.Count == 0)
        {
            Rows.Clear();
            Columns.Clear();
            return;
        }

        // Normalize content: trim each word's leading/trailing whitespace
        foreach (WordBorder wb in wordBorders)
        {
            if (wb.Word is string s && s.Length > 0)
                wb.Word = s.Trim();
        }

        // Compute adaptive thresholds
        double medianHeight = Median(wordBorders.Select(w => w.Height).Where(h => h > 0));
        if (double.IsNaN(medianHeight) || medianHeight <= 0) medianHeight = 20;
        double rowCenterThreshold = Math.Max(4, medianHeight * 0.75); // cluster rows by centerY

        double medianWidth = Median(wordBorders.Select(w => w.Width).Where(w => w > 0));
        if (double.IsNaN(medianWidth) || medianWidth <= 0) medianWidth = 40;
        double columnCenterThreshold = Math.Max(24, medianWidth * 0.9); // cluster columns by centerX

        List<ResultRow> resultRows = BuildRowsByCenterClustering(wordBorders, rowCenterThreshold, medianHeight);
        List<ResultColumn> resultColumns = BuildColumnsByCenterClustering(wordBorders, columnCenterThreshold);

        // Assign to table
        Rows.Clear();
        Rows.AddRange(resultRows);
        Columns.Clear();
        Columns.AddRange(resultColumns);

        // Final, robust assignment of each word border into the best matching row/column
        AssignWordBordersToFinalGrid(wordBorders);

        ParseRowAndColumnLines();
        DrawTable();
    }

    private static List<ResultRow> BuildRowsByCenterClustering(ICollection<WordBorder> wordBorders, double centerThreshold, double medianHeight)
    {
        // cluster by centerY regardless of gaps in other columns
        List<WordBorder> ordered = [.. wordBorders
            .OrderBy(w => w.Top + (w.Height / 2.0))
            .ThenBy(w => w.Left)];

        // Maintain working clusters with words for potential merging
        List<(double Top, double Bottom, List<WordBorder> Words)> clusters = [];

        foreach (WordBorder? wb in ordered)
        {
            double centerY = wb.Top + (wb.Height / 2.0);

            if (clusters.Count == 0)
            {
                clusters.Add((wb.Top, wb.Bottom, new List<WordBorder> { wb }));
                continue;
            }

            (double Top, double Bottom, List<WordBorder> Words) = clusters[^1];
            double lastCenter = (Top + Bottom) / 2.0;

            if (Math.Abs(centerY - lastCenter) <= centerThreshold)
            {
                // same row cluster
                Words.Add(wb);
                double newTop = Math.Min(Top, wb.Top);
                double newBottom = Math.Max(Bottom, wb.Bottom);
                clusters[^1] = (newTop, newBottom, Words);
            }
            else
            {
                clusters.Add((wb.Top, wb.Bottom, new List<WordBorder> { wb }));
            }
        }

        // Merge tiny single-word rows that are very close to an adjacent row (e.g., header split)
        double mergeThreshold = Math.Max(centerThreshold * 1.5, medianHeight); // allow a bit more than clustering threshold
        int idx = 0;
        while (idx < clusters.Count - 1)
        {
            (double Top, double Bottom, List<WordBorder> Words) = clusters[idx];
            (double Top, double Bottom, List<WordBorder> Words) nxt = clusters[idx + 1];
            double curCenter = (Top + Bottom) / 2.0;
            double nxtCenter = (nxt.Top + nxt.Bottom) / 2.0;
            double gap = Math.Abs(nxtCenter - curCenter);

            if (Words.Count <= 1 && gap <= mergeThreshold)
            {
                // merge cur into next
                nxt.Words.InsertRange(0, Words);
                double newTop = Math.Min(Top, nxt.Top);
                double newBottom = Math.Max(Bottom, nxt.Bottom);
                clusters[idx + 1] = (newTop, newBottom, nxt.Words);
                clusters.RemoveAt(idx);
                // do not increment idx; re-evaluate
                continue;
            }
            idx++;
        }

        // Also check backward merge for leading single word above a dense row
        idx = 1;
        while (idx < clusters.Count)
        {
            (double Top, double Bottom, List<WordBorder> Words) = clusters[idx - 1];
            (double Top, double Bottom, List<WordBorder> Words) cur = clusters[idx];
            double prevCenter = (Top + Bottom) / 2.0;
            double curCenter2 = (cur.Top + cur.Bottom) / 2.0;
            double gap = Math.Abs(curCenter2 - prevCenter);
            if (cur.Words.Count <= 1 && gap <= mergeThreshold)
            {
                Words.AddRange(cur.Words);
                double newTop = Math.Min(cur.Top, Top);
                double newBottom = Math.Max(cur.Bottom, Bottom);
                clusters[idx - 1] = (newTop, newBottom, Words);
                clusters.RemoveAt(idx);
                continue;
            }
            idx++;
        }

        // Convert clusters to ResultRow with sequential IDs
        List<ResultRow> rows = [];
        for (int i = 0; i < clusters.Count; i++)
        {
            (double Top, double Bottom, List<WordBorder> Words) = clusters[i];
            rows.Add(new ResultRow
            {
                ID = i,
                Top = Top,
                Bottom = Bottom,
                Height = Bottom - Top
            });
        }

        return rows;
    }

    private static List<ResultColumn> BuildColumnsByCenterClustering(ICollection<WordBorder> wordBorders, double centerThreshold)
    {
        // cluster by centerX across the table
        List<WordBorder> ordered = [.. wordBorders
            .OrderBy(w => w.Left + (w.Width / 2.0))
            .ThenBy(w => w.Top)];

        List<(double Left, double Right, List<WordBorder> Words)> clusters = [];

        foreach (WordBorder? wb in ordered)
        {
            double centerX = wb.Left + (wb.Width / 2.0);

            if (clusters.Count == 0)
            {
                clusters.Add((wb.Left, wb.Right, new List<WordBorder> { wb }));
                continue;
            }

            (double Left, double Right, List<WordBorder> Words) = clusters[^1];
            double lastCenter = (Left + Right) / 2.0;

            if (Math.Abs(centerX - lastCenter) <= centerThreshold)
            {
                // same column cluster
                Words.Add(wb);
                double newLeft = Math.Min(Left, wb.Left);
                double newRight = Math.Max(Right, wb.Right);
                clusters[^1] = (newLeft, newRight, Words);
            }
            else
            {
                clusters.Add((wb.Left, wb.Right, new List<WordBorder> { wb }));
            }
        }

        // Optional: merge very thin or low-density columns into neighbors if their span is tiny
        double avgWidth = clusters.Select(c => c.Right - c.Left).DefaultIfEmpty(0).Average();
        int ci = 0;
        while (ci < clusters.Count - 1)
        {
            (double Left, double Right, List<WordBorder> Words) = clusters[ci];
            (double Left, double Right, List<WordBorder> Words) nxt = clusters[ci + 1];
            double curWidth = Right - Left;
            double gap = nxt.Left - Right;
            // merge tiny columns or columns separated by negligible gap
            if ((Words.Count <= 1 && curWidth < avgWidth * 0.4) || gap < Math.Max(6, centerThreshold * 0.25))
            {
                // merge cur into next
                double newLeft = Math.Min(Left, nxt.Left);
                double newRight = Math.Max(Right, nxt.Right);
                nxt.Words.InsertRange(0, Words);
                clusters[ci + 1] = (newLeft, newRight, nxt.Words);
                clusters.RemoveAt(ci);
                continue;
            }
            ci++;
        }

        // Convert clusters to ResultColumn with sequential IDs
        List<ResultColumn> cols = [];
        for (int i = 0; i < clusters.Count; i++)
        {
            (double Left, double Right, List<WordBorder> Words) = clusters[i];
            cols.Add(new ResultColumn
            {
                ID = i,
                Left = Left,
                Right = Right,
                Width = Right - Left
            });
        }

        return cols;
    }

    private static double Median(IEnumerable<double> source)
    {
        if (source == null) return double.NaN;
        List<double> list = [.. source.Where(d => !double.IsNaN(d) && !double.IsInfinity(d)).OrderBy(d => d)];
        if (list.Count == 0) return double.NaN;
        int mid = list.Count / 2;
        if (list.Count % 2 == 0)
            return (list[mid - 1] + list[mid]) / 2.0;
        return list[mid];
    }

    private static List<ResultRow> CalculateResultRows(int hitGridSpacing, List<int> rowAreas)
    {
        List<ResultRow> resultRows = [];
        int rowTop = 0;
        int rowCount = 0;
        for (int i = 0; i < rowAreas.Count; i++)
        {
            int thisLine = rowAreas[i];

            // check if should set this as top
            if (i == 0)
                rowTop = thisLine;
            else if (i - 1 >= 0)
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
        List<int> rowAreas = [];

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
        // refine the rows and columns for outliers
        for (int r = 0; r < 5; r++)
        {
            // Be conservative merging rows; only merge truly empty rows
            int rowOutlierThreshold = 1;
            List<int> outlierRowIDs = FindOutlierRowIds(wordBorders, resultRows, tableIntersectionCanvas, tableBoundingRect, r, rowOutlierThreshold);

            if (outlierRowIDs.Count > 0)
                MergeTheseRowIDs(resultRows, outlierRowIDs);

            // Columns can be merged a bit more aggressively
            int columnOutlierThreshold = 2;
            List<int> outlierColumnIDs = FindOutlierColumnIds(wordBorders, tableIntersectionCanvas, resultColumns, tableBoundingRect, columnOutlierThreshold);

            if (outlierColumnIDs.Count > 0 && r != 4)
                MergetheseColumnIDs(resultColumns, outlierColumnIDs);
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
        List<int> outlierRowIDs = [];

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

            Rect rowRect = new(tableBoundingRect.X, row.Top, rowBorder.Width, rowBorder.Height);

            foreach (WordBorder wb in wordBorders)
            {
                if (wb.IntersectsWith(rowRect))
                {
                    numberOfIntersectingWords++;
                    wb.ResultRowID = row.ID;
                }
            }

            // Only consider truly empty or near-empty rows as outliers
            if (numberOfIntersectingWords < outlierThreshould && r != 4)
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
        List<int> outlierColumnIDs = [];

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

            Rect columnRect = new(column.Left, tableBoundingRect.Y, columnBorder.Width, columnBorder.Height);
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

    private static void MergetheseColumnIDs(List<ResultColumn> resultColumns, List<int> outlierColumnIDs)
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
        List<WordBorder>? selectedBorders = [.. wordBorders.Where(w => w.IsSelected)];

        if (selectedBorders.Count == 0)
            selectedBorders.AddRange(wordBorders);

        // custom comparator for natural reading order within each cell
        double leftTieThreshold = 12; // pixels considered aligned vertically
        selectedBorders = [.. selectedBorders
            .OrderBy(x => x.ResultRowID)
            .ThenBy(x => x.ResultColumnID)
            .ThenBy(x => 0)];

        // Sort using a custom comparer to handle vertically stacked header text
        selectedBorders.Sort((a, b) =>
        {
            int rowCmp = a.ResultRowID.CompareTo(b.ResultRowID);
            if (rowCmp != 0) return rowCmp;
            int colCmp = a.ResultColumnID.CompareTo(b.ResultColumnID);
            if (colCmp != 0) return colCmp;

            double leftDiff = Math.Abs(a.Left - b.Left);
            if (leftDiff <= leftTieThreshold)
            {
                // Same visual column: sort by Top first, then Left
                int topCmp = a.Top.CompareTo(b.Top);
                if (topCmp != 0) return topCmp;
                return a.Left.CompareTo(b.Left);
            }
            // Different visual columns within the cell row: sort by Left
            int leftCmp = a.Left.CompareTo(b.Left);
            if (leftCmp != 0) return leftCmp;
            return a.Top.CompareTo(b.Top);
        });

        List<string> lineList = [];
        int? lastLineNum = 0;
        int lastColumnNum = 0;
        WordBorder? prevBorderOnLine = null;

        if (selectedBorders.FirstOrDefault() != null)
            lastLineNum = selectedBorders.FirstOrDefault()!.LineNumber;

        int numberOfDistinctRows = selectedBorders.Select(x => x.ResultRowID).Distinct().Count();

        // Heuristic: detect the percent column as the one with the most percent tokens
        int percentColumnId = selectedBorders
            .GroupBy(w => w.ResultColumnID)
            .Select(g => new { Col = g.Key, Count = g.Count(w => w.Word.Contains('%') || w.Word.Trim() == "%" || w.Word.EndsWith("1%")) })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Col)
            .FirstOrDefault()?.Col ?? -1;

        for (int i = 0; i < selectedBorders.Count; i++)
        {
            WordBorder border = selectedBorders[i];

            if (lineList.Count == 0)
            {
                lastLineNum = border.ResultRowID;
                lastColumnNum = 0; // reset for a new line
                prevBorderOnLine = null;
            }

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
                lastColumnNum = 0; // reset for a new line
                prevBorderOnLine = null;
            }

            if (border.ResultColumnID != lastColumnNum && numberOfDistinctRows > 1)
            {
                int numberOfOffColumns = border.ResultColumnID - lastColumnNum;
                if (numberOfOffColumns < 0)
                    lastColumnNum = 0;

                numberOfOffColumns = border.ResultColumnID - lastColumnNum;

                if (numberOfOffColumns > 0)
                    lineList.Add(new string('\t', numberOfOffColumns));
            }
            lastColumnNum = border.ResultColumnID;

            // Merge a standalone '%' with the previous token when visually adjacent in the same cell
            if (lineList.Count > 0 && prevBorderOnLine is not null
                && prevBorderOnLine.ResultColumnID == border.ResultColumnID
                && prevBorderOnLine.ResultRowID == border.ResultRowID)
            {
                double visualGap = border.Left - prevBorderOnLine.Right;
                double tolerance = Math.Max(3, Math.Min(prevBorderOnLine.Height, border.Height) * 0.25);

                // Case 1: current is just "%"
                if (border.Word == "%")
                {
                    // If very close OR both tokens are white background, merge without space
                    System.Windows.Media.Color prevBg = prevBorderOnLine.MatchingBackground.Color;
                    System.Windows.Media.Color curBg = border.MatchingBackground.Color;
                    bool prevIsWhite = prevBg.A == 0xFF && prevBg.R == 0xFF && prevBg.G == 0xFF && prevBg.B == 0xFF;
                    bool curIsWhite = curBg.A == 0xFF && curBg.R == 0xFF && curBg.G == 0xFF && curBg.B == 0xFF;
                    if (visualGap <= tolerance || (prevIsWhite && curIsWhite))
                    {
                        lineList[^1] = lineList[^1] + "%";
                        prevBorderOnLine = border;
                        continue;
                    }
                }

                // Case 2: current is "1%" artifact immediately after a number
                if (border.Word == "1%" && visualGap <= tolerance)
                {
                    string prevText = lineList[^1];
                    if (prevText.Length > 0 && char.IsDigit(prevText[^1]))
                    {
                        lineList[^1] = prevText + "%";
                        prevBorderOnLine = border;
                        continue;
                    }
                }
            }

            // Normalize token
            string wordToAdd = border.Word;
            // Unescape any JSON-escaped ampersand sequences (e.g., "\u0026" -> "&")
            wordToAdd = wordToAdd.Replace("\\u0026", "&");

            if (wordToAdd.Contains('%') && wordToAdd.Contains(" %"))
            {
                // Only collapse the space for white-background tokens (avoid collapsing gray-highlighted ones like '61 %')
                System.Windows.Media.Color bg = border.MatchingBackground.Color;
                bool isWhiteBg = bg.A == 0xFF && bg.R == 0xFF && bg.G == 0xFF && bg.B == 0xFF;
                if (isWhiteBg)
                {
                    int idxPercent = wordToAdd.LastIndexOf('%');
                    if (idxPercent > 0 && wordToAdd[idxPercent - 1] == ' ')
                    {
                        wordToAdd = wordToAdd.Remove(idxPercent - 1, 1);
                    }
                }
            }

            // Append token now
            lineList.Add(wordToAdd);
            prevBorderOnLine = border;

            // If this is the percent column and we just added a numeric-only token without a following percent in the same cell, append '%'
            if (border.ResultColumnID == percentColumnId && LooksLikePlainNumber(wordToAdd))
            {
                // Peek next token in same row/column
                bool nextHasPercent = false;
                for (int j = i + 1; j < selectedBorders.Count; j++)
                {
                    WordBorder nb = selectedBorders[j];
                    if (nb.ResultRowID != border.ResultRowID) break; // next row
                    if (nb.ResultColumnID != border.ResultColumnID) break; // next column in this row
                    // same cell
                    string nxtWord = nb.Word;
                    if (nxtWord.Contains('%') || nxtWord.Trim() == "%")
                    {
                        nextHasPercent = true;
                        break;
                    }
                }

                if (!nextHasPercent)
                {
                    lineList[^1] = lineList[^1] + "%";
                }
            }
        }

        if (lineList.Count > 0)
        {
            if (isSpaceJoining)
                stringBuilder.Append(string.Join(' ', lineList));
            else
                stringBuilder.Append(string.Join("", lineList));
        }
    }

    private static bool LooksLikePlainNumber(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        string t = token.Trim();
        if (t.Contains('%')) return false;
        if (t.Contains('/')) return false; // N/A or dates
        if (t.Contains('$')) return false;
        if (t.Contains("N/A", StringComparison.OrdinalIgnoreCase)) return false;
        // remove commas
        t = t.Replace(",", "");
        // strip parentheses around negatives
        if (t.StartsWith('(') && t.EndsWith(')') && t.Length > 2)
            t = t[1..^1];
        return t.All(ch => char.IsDigit(ch));
    }

    private static void MergeTheseRowIDs(List<ResultRow> resultRows, List<int> outlierRowIDs)
    {
        // Merge sparse rows into adjacent rows to reduce fragmentation
        for (int i = 0; i < outlierRowIDs.Count; i++)
        {
            for (int j = 0; j < resultRows.Count; j++)
            {
                ResultRow jthRow = resultRows[j];
                if (jthRow.ID == outlierRowIDs[i])
                {
                    if (resultRows.Count == 1)
                    {
                        // nothing to merge
                        continue;
                    }

                    if (j == 0)
                    {
                        // merge with next row if possible
                        if (j + 1 < resultRows.Count)
                        {
                            ResultRow nextRow = resultRows[j + 1];
                            nextRow.Top = Math.Min(nextRow.Top, jthRow.Top);
                        }
                    }
                    else if (j == resultRows.Count - 1)
                    {
                        // merge with previous row
                        if (j - 1 >= 0)
                        {
                            ResultRow prevRow = resultRows[j - 1];
                            prevRow.Bottom = Math.Max(prevRow.Bottom, jthRow.Bottom);
                        }
                    }
                    else
                    {
                        // merge with the closest neighbor by gap distance
                        ResultRow prevRow = resultRows[j - 1];
                        ResultRow nextRow = resultRows[j + 1];
                        int distToPrev = (int)(jthRow.Top - prevRow.Bottom);
                        int distToNext = (int)(nextRow.Top - jthRow.Bottom);

                        if (distToNext < distToPrev)
                        {
                            // merge with next row
                            nextRow.Top = Math.Min(nextRow.Top, jthRow.Top);
                        }
                        else
                        {
                            // merge with prev row
                            prevRow.Bottom = Math.Max(prevRow.Bottom, jthRow.Bottom);
                        }
                    }

                    resultRows.RemoveAt(j);
                    // reindex remaining IDs to keep them sequential
                    for (int k = 0; k < resultRows.Count; k++)
                        resultRows[k].ID = k;
                    break;
                }
            }
        }
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
        List<WordBorder> smallerBorders = [];
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
        }
        ;

        ResultTable resultTable = new(ref smallerBorders, dpiScale);
        StringBuilder stringBuilder = new();
        GetTextFromTabledWordBorders(
            stringBuilder,
            smallerBorders,
            isSpaceJoining);
        return stringBuilder.ToString();
    }

    private void AssignWordBordersToFinalGrid(ICollection<WordBorder> wordBorders)
    {
        if (Rows.Count == 0 || Columns.Count == 0)
            return;

        foreach (WordBorder wb in wordBorders)
        {
            double centerX = wb.Left + (wb.Width / 2.0);
            double centerY = wb.Top + (wb.Height / 2.0);

            // Find row
            int rowIndex = -1;
            for (int i = 0; i < Rows.Count; i++)
            {
                if (centerY >= Rows[i].Top && centerY <= Rows[i].Bottom)
                {
                    rowIndex = i;
                    break;
                }
            }
            if (rowIndex == -1)
            {
                // choose closest by vertical distance
                double minDist = double.MaxValue;
                for (int i = 0; i < Rows.Count; i++)
                {
                    double dist = 0;
                    if (centerY < Rows[i].Top) dist = Rows[i].Top - centerY;
                    else if (centerY > Rows[i].Bottom) dist = centerY - Rows[i].Bottom;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        rowIndex = i;
                    }
                }
            }

            // Find column
            int colIndex = -1;
            for (int j = 0; j < Columns.Count; j++)
            {
                if (centerX >= Columns[j].Left && centerX <= Columns[j].Right)
                {
                    colIndex = j;
                    break;
                }
            }
            if (colIndex == -1)
            {
                double minDist = double.MaxValue;
                for (int j = 0; j < Columns.Count; j++)
                {
                    double dist = 0;
                    if (centerX < Columns[j].Left) dist = Columns[j].Left - centerX;
                    else if (centerX > Columns[j].Right) dist = centerX - Columns[j].Right;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        colIndex = j;
                    }
                }
            }

            wb.ResultRowID = rowIndex;
            wb.ResultColumnID = colIndex;
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
