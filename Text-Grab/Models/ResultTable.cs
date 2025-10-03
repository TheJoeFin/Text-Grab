using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    // New: accept pure model objects
    public ResultTable(ref List<WordBorderInfo> wordBorders, DpiScale dpiScale)
    {
        int borderBuffer = 3;

        Rectangle bordersBorder = new();
        if (wordBorders.Count > 0)
        {
            double leftsMin = wordBorders.Select(x => x.BorderRect.Left).Min();
            double topsMin = wordBorders.Select(x => x.BorderRect.Top).Min();
            double rightsMax = wordBorders.Select(x => x.BorderRect.Right).Max();
            double bottomsMax = wordBorders.Select(x => x.BorderRect.Bottom).Max();

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

    public static List<WordBorderInfo> ParseOcrResultIntoWordBorderInfos(IOcrLinesWords ocrResult, DpiScale dpi)
    {
        List<WordBorderInfo> infos = [];

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

            WordBorderInfo info = new()
            {
                BorderRect = new Rect(lineRect.X, lineRect.Y, lineRect.Width, lineRect.Height),
                Word = lineText.ToString().Trim(),
                ResultRowID = 0,
                ResultColumnID = 0
            };

            infos.Add(info);
        }

        return infos;
    }

    // New core analyzer that operates on WordBorderInfo (pure model)
    public void AnalyzeAsTable(ICollection<WordBorderInfo> wordBorders, Rectangle rectCanvasSize, bool drawTable = true)
    {
        if (wordBorders == null || wordBorders.Count == 0)
        {
            Rows.Clear();
            Columns.Clear();
            return;
        }

        foreach (WordBorderInfo wb in wordBorders)
        {
            if (wb.Word is string s && s.Length > 0)
                wb.Word = s.Trim();
        }

        double medianHeight = Median(wordBorders.Select(w => w.BorderRect.Height).Where(h => h > 0));
        if (double.IsNaN(medianHeight) || medianHeight <= 0) medianHeight = 20;
        double rowCenterThreshold = Math.Max(4, medianHeight * 0.75);

        double medianWidth = Median(wordBorders.Select(w => w.BorderRect.Width).Where(w => w > 0));
        if (double.IsNaN(medianWidth) || medianWidth <= 0) medianWidth = 40;
        double columnCenterThreshold = Math.Max(24, medianWidth * 0.9);

        List<ResultRow> resultRows = BuildRowsByCenterClustering(wordBorders, rowCenterThreshold, medianHeight);
        List<ResultColumn> resultColumns = BuildColumnsByCenterClustering(wordBorders, columnCenterThreshold);

        Rows.Clear();
        Rows.AddRange(resultRows);
        Columns.Clear();
        Columns.AddRange(resultColumns);

        AssignWordBordersToFinalGrid(wordBorders);

        ParseRowAndColumnLines();
        if (drawTable)
            DrawTable();
    }

    private static List<ResultRow> BuildRowsByCenterClustering(ICollection<WordBorderInfo> wordBorders, double centerThreshold, double medianHeight)
    {
        List<WordBorderInfo> ordered = [.. wordBorders
            .OrderBy(w => w.BorderRect.Top + (w.BorderRect.Height / 2.0))
            .ThenBy(w => w.BorderRect.Left)];

        List<(double Top, double Bottom, List<WordBorderInfo> Words)> clusters = [];

        foreach (WordBorderInfo? wb in ordered)
        {
            double centerY = wb.BorderRect.Top + (wb.BorderRect.Height / 2.0);

            if (clusters.Count == 0)
            {
                clusters.Add((wb.BorderRect.Top, wb.BorderRect.Bottom, new List<WordBorderInfo> { wb }));
                continue;
            }

            (double Top, double Bottom, List<WordBorderInfo> Words) = clusters[^1];
            double lastCenter = (Top + Bottom) / 2.0;

            if (Math.Abs(centerY - lastCenter) <= centerThreshold)
            {
                Words.Add(wb);
                double newTop = Math.Min(Top, wb.BorderRect.Top);
                double newBottom = Math.Max(Bottom, wb.BorderRect.Bottom);
                clusters[^1] = (newTop, newBottom, Words);
            }
            else
            {
                clusters.Add((wb.BorderRect.Top, wb.BorderRect.Bottom, new List<WordBorderInfo> { wb }));
            }
        }

        double mergeThreshold = Math.Max(centerThreshold * 1.5, medianHeight);
        int idx = 0;
        while (idx < clusters.Count - 1)
        {
            (double Top, double Bottom, List<WordBorderInfo> Words) = clusters[idx];
            (double Top, double Bottom, List<WordBorderInfo> Words) nxt = clusters[idx + 1];
            double curCenter = (Top + Bottom) / 2.0;
            double nxtCenter = (nxt.Top + nxt.Bottom) / 2.0;
            double gap = Math.Abs(nxtCenter - curCenter);

            if (Words.Count <= 1 && gap <= mergeThreshold)
            {
                nxt.Words.InsertRange(0, Words);
                double newTop = Math.Min(Top, nxt.Top);
                double newBottom = Math.Max(Bottom, nxt.Bottom);
                clusters[idx + 1] = (newTop, newBottom, nxt.Words);
                clusters.RemoveAt(idx);
                continue;
            }
            idx++;
        }

        idx = 1;
        while (idx < clusters.Count)
        {
            (double Top, double Bottom, List<WordBorderInfo> Words) = clusters[idx - 1];
            (double Top, double Bottom, List<WordBorderInfo> Words) cur = clusters[idx];
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
            (double Top, double Bottom, List<WordBorderInfo> Words) = clusters[i];
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

    // Overload for WordBorderInfo
    private static List<ResultColumn> BuildColumnsByCenterClustering(ICollection<WordBorderInfo> wordBorders, double centerThreshold)
    {
        List<WordBorderInfo> ordered = [.. wordBorders
            .OrderBy(w => w.BorderRect.Left + (w.BorderRect.Width / 2.0))
            .ThenBy(w => w.BorderRect.Top)];

        List<(double Left, double Right, List<WordBorderInfo> Words)> clusters = [];

        foreach (WordBorderInfo? wb in ordered)
        {
            double centerX = wb.BorderRect.Left + (wb.BorderRect.Width / 2.0);

            if (clusters.Count == 0)
            {
                clusters.Add((wb.BorderRect.Left, wb.BorderRect.Right, new List<WordBorderInfo> { wb }));
                continue;
            }

            (double Left, double Right, List<WordBorderInfo> Words) = clusters[^1];
            double lastCenter = (Left + Right) / 2.0;

            if (Math.Abs(centerX - lastCenter) <= centerThreshold)
            {
                Words.Add(wb);
                double newLeft = Math.Min(Left, wb.BorderRect.Left);
                double newRight = Math.Max(Right, wb.BorderRect.Right);
                clusters[^1] = (newLeft, newRight, Words);
            }
            else
            {
                clusters.Add((wb.BorderRect.Left, wb.BorderRect.Right, new List<WordBorderInfo> { wb }));
            }
        }

        double avgWidth = clusters.Select(c => c.Right - c.Left).DefaultIfEmpty(0).Average();
        int ci = 0;
        while (ci < clusters.Count - 1)
        {
            (double Left, double Right, List<WordBorderInfo> Words) = clusters[ci];
            (double Left, double Right, List<WordBorderInfo> Words) nxt = clusters[ci + 1];
            double curWidth = Right - Left;
            double gap = nxt.Left - Right;
            if ((Words.Count <= 1 && curWidth < avgWidth * 0.4) || gap < Math.Max(6, centerThreshold * 0.25))
            {
                double newLeft = Math.Min(Left, nxt.Left);
                double newRight = Math.Max(Right, nxt.Right);
                nxt.Words.InsertRange(0, Words);
                clusters[ci + 1] = (newLeft, newRight, nxt.Words);
                clusters.RemoveAt(ci);
                continue;
            }
            ci++;
        }

        List<ResultColumn> cols = [];
        for (int i = 0; i < clusters.Count; i++)
        {
            (double Left, double Right, List<WordBorderInfo> Words) = clusters[i];
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
            else
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

    // Build text from model-only borders
    public static void GetTextFromTabledWordBorders(StringBuilder stringBuilder, List<WordBorderInfo> wordBorders, bool isSpaceJoining)
    {
        // Work on a local copy to avoid mutating caller's list
        List<WordBorderInfo> selectedBorders = [.. wordBorders];

        // Sort using a custom comparer to handle vertically stacked header text
        double leftTieThreshold = 12; // pixels considered aligned vertically
        selectedBorders.Sort((a, b) =>
        {
            int rowCmp = a.ResultRowID.CompareTo(b.ResultRowID);
            if (rowCmp != 0) return rowCmp;
            int colCmp = a.ResultColumnID.CompareTo(b.ResultColumnID);
            if (colCmp != 0) return colCmp;

            double leftDiff = Math.Abs(a.BorderRect.Left - b.BorderRect.Left);
            if (leftDiff <= leftTieThreshold)
            {
                int topCmp = a.BorderRect.Top.CompareTo(b.BorderRect.Top);
                if (topCmp != 0) return topCmp;
                return a.BorderRect.Left.CompareTo(b.BorderRect.Left);
            }
            int leftCmp = a.BorderRect.Left.CompareTo(b.BorderRect.Left);
            if (leftCmp != 0) return leftCmp;
            return a.BorderRect.Top.CompareTo(b.BorderRect.Top);
        });

        // Precompute number of distinct rows without LINQ
        int numberOfDistinctRows;
        {
            HashSet<int> rowSet = [];
            for (int i = 0; i < selectedBorders.Count; i++)
                rowSet.Add(selectedBorders[i].ResultRowID);
            numberOfDistinctRows = rowSet.Count;
        }

        // Precompute rows that contain footnote tokens like (1)
        HashSet<int> rowsWithFootnote = [];
        for (int i = 0; i < selectedBorders.Count; i++)
        {
            WordBorderInfo wb = selectedBorders[i];
            if (!rowsWithFootnote.Contains(wb.ResultRowID) && IsFootnoteToken(wb.Word))
                rowsWithFootnote.Add(wb.ResultRowID);
        }

        // Heuristic: detect the percent column using a single pass
        Dictionary<int, int> percentCounts = [];
        for (int i = 0; i < selectedBorders.Count; i++)
        {
            WordBorderInfo w = selectedBorders[i];
            string s = w.Word;
            if (s.Length == 0) continue;
            bool hasPercent = s.Contains('%') || s.Trim() == "%" || s.EndsWith("1%", StringComparison.Ordinal);
            if (hasPercent)
            {
                percentCounts.TryGetValue(w.ResultColumnID, out int cur);
                percentCounts[w.ResultColumnID] = cur + 1;
            }
        }
        int percentColumnId = -1;
        if (percentCounts.Count > 0)
        {
            int bestCol = -1;
            int bestCount = -1;
            foreach (KeyValuePair<int, int> kvp in percentCounts)
            {
                if (kvp.Value > bestCount || (kvp.Value == bestCount && kvp.Key < bestCol))
                {
                    bestCol = kvp.Key;
                    bestCount = kvp.Value;
                }
            }
            percentColumnId = bestCol;
        }

        // Build line-by-line using precise control over spaces/tabs
        StringBuilder lineSb = new();
        bool firstLineStarted = false;
        int? lastLineNum = selectedBorders.Count > 0 ? selectedBorders[0].ResultRowID : null;
        int lastColumnNum = 0;
        WordBorderInfo? prevBorderOnLine = null;

        for (int i = 0; i < selectedBorders.Count; i++)
        {
            WordBorderInfo border = selectedBorders[i];

            if (!firstLineStarted)
            {
                lastLineNum = border.ResultRowID;
                lastColumnNum = 0;
                prevBorderOnLine = null;
                lineSb.Clear();
                firstLineStarted = true;
            }

            if (border.ResultRowID != lastLineNum)
            {
                // flush previous line
                if (lineSb.Length > 0)
                {
                    string outLine = lineSb.ToString();
                    stringBuilder.Append(outLine);
                }
                stringBuilder.Append(Environment.NewLine);

                // reset for new line
                lineSb.Clear();
                lastLineNum = border.ResultRowID;
                lastColumnNum = 0;
                prevBorderOnLine = null;
            }

            if (border.ResultColumnID != lastColumnNum && numberOfDistinctRows > 1)
            {
                int numberOfOffColumns = border.ResultColumnID - lastColumnNum;
                if (numberOfOffColumns < 0)
                    lastColumnNum = 0;

                numberOfOffColumns = border.ResultColumnID - lastColumnNum;

                if (numberOfOffColumns > 0)
                {
                    for (int t = 0; t < numberOfOffColumns; t++)
                        lineSb.Append('\t');
                }
            }
            lastColumnNum = border.ResultColumnID;

            // Merge a standalone '%' with the previous token when visually adjacent in the same cell
            if (lineSb.Length > 0 && prevBorderOnLine is not null
                && prevBorderOnLine.ResultColumnID == border.ResultColumnID
                && prevBorderOnLine.ResultRowID == border.ResultRowID)
            {
                double visualGap = border.BorderRect.Left - prevBorderOnLine.BorderRect.Right;
                double tolerance = Math.Max(3, Math.Min(prevBorderOnLine.BorderRect.Height, border.BorderRect.Height) * 0.25);

                // Case 1: current is just "%"
                if (border.Word == "%")
                {
                    if (visualGap <= tolerance)
                    {
                        // append directly without a space
                        lineSb.Append('%');
                        prevBorderOnLine = border;
                        continue;
                    }
                }

                // Case 2: current is "1%" artifact immediately after a number
                if (border.Word == "1%" && visualGap <= tolerance)
                {
                    char lastCh = lineSb.Length > 0 ? lineSb[^1] : '\0';
                    if (char.IsDigit(lastCh))
                    {
                        lineSb.Append('%');
                        prevBorderOnLine = border;
                        continue;
                    }
                }
            }

            // Normalize token
            string wordToAdd = border.Word;
            if (wordToAdd.Length > 0)
                wordToAdd = wordToAdd.Replace("\\u0026", "&", StringComparison.Ordinal);

            // Normalize: if token looks like "digits  %" collapse to "digits%" unless row has a footnote
            if (wordToAdd.Contains('%'))
            {
                bool rowHasFootnote = rowsWithFootnote.Contains(border.ResultRowID);
                if (!rowHasFootnote)
                {
                    int pct = wordToAdd.LastIndexOf('%');
                    if (pct > 0)
                    {
                        int idx2 = pct - 1;
                        while (idx2 >= 0 && wordToAdd[idx2] == ' ') idx2--;
                        if (idx2 >= 0 && char.IsDigit(wordToAdd[idx2]))
                        {
                            int start = idx2 + 1;
                            int len = pct - start;
                            if (len > 0)
                                wordToAdd = wordToAdd.Remove(start, len);
                        }
                    }
                }
            }

            // Add a space between tokens in the same cell (not around tabs)
            bool sameCellAsPrev = prevBorderOnLine is not null
                && prevBorderOnLine.ResultColumnID == border.ResultColumnID
                && prevBorderOnLine.ResultRowID == border.ResultRowID;
            if (sameCellAsPrev)
            {
                if (lineSb.Length > 0)
                {
                    char lastCh = lineSb[^1];
                    if (lastCh is not '\t' and not '\n' and not '\r')
                    {
                        // glue % tokens to previous number
                        if (!(wordToAdd.Length == 1 && wordToAdd[0] == '%' && (char.IsDigit(lastCh) || lastCh == ')')))
                            lineSb.Append(' ');
                    }
                }
            }

            // Append token text
            lineSb.Append(wordToAdd);
            prevBorderOnLine = border;

            // If this is the percent column and we just added a numeric-only token without a following percent in the same cell, append '%'
            if (border.ResultColumnID == percentColumnId && LooksLikePlainNumber(wordToAdd))
            {
                bool nextHasPercent = false;
                for (int j = i + 1; j < selectedBorders.Count; j++)
                {
                    WordBorderInfo nb = selectedBorders[j];
                    if (nb.ResultRowID != border.ResultRowID) break; // next row
                    if (nb.ResultColumnID != border.ResultColumnID) break; // next column in this row
                    string nxtWord = nb.Word;
                    if (nxtWord.IndexOf('%') >= 0 || nxtWord.Trim() == "%")
                    {
                        nextHasPercent = true;
                        break;
                    }
                }

                if (!nextHasPercent)
                    lineSb.Append('%');
            }
        }

        if (lineSb.Length > 0)
        {
            string outLine = lineSb.ToString();
            stringBuilder.Append(outLine);
        }
    }

    private static bool IsFootnoteToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        token = token.Trim();
        if (token.Length >= 3 && token[0] == '(' && token[^1] == ')')
        {
            for (int i = 1; i < token.Length - 1; i++)
            {
                if (!char.IsDigit(token[i])) return false;
            }
            return true;
        }
        return false;
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
        // strip parentheses only if it's a negative number (e.g., "(-123)")
        if (t.StartsWith('(') && t.EndsWith(')') && t.Length > 3 && t[1] == '-')
            t = t[2..^1]; // remove '(' and ')' and leading '-'
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

    // Overload for WordBorderInfo
    private void AssignWordBordersToFinalGrid(ICollection<WordBorderInfo> wordBorders)
    {
        if (Rows.Count == 0 || Columns.Count == 0)
            return;

        // Precompute row and column edge arrays (sorted by ID/index)
        int rowCount = Rows.Count;
        int colCount = Columns.Count;
        double[] rowTops = new double[rowCount];
        double[] rowBottoms = new double[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            rowTops[i] = Rows[i].Top;
            rowBottoms[i] = Rows[i].Bottom;
        }
        double[] colLefts = new double[colCount];
        double[] colRights = new double[colCount];
        for (int j = 0; j < colCount; j++)
        {
            colLefts[j] = Columns[j].Left;
            colRights[j] = Columns[j].Right;
        }

        static int LowerBound(double[] arr, double value)
        {
            int lo = 0, hi = arr.Length; // [lo, hi)
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (arr[mid] <= value) lo = mid + 1; else hi = mid;
            }
            return lo - 1; // last index with arr[i] <= value, or -1 if none
        }

        foreach (WordBorderInfo wb in wordBorders)
        {
            double centerX = wb.BorderRect.Left + (wb.BorderRect.Width / 2.0);
            double centerY = wb.BorderRect.Top + (wb.BorderRect.Height / 2.0);

            // Find row by binary search on Tops then validate with Bottoms
            int rowIndex = LowerBound(rowTops, centerY);
            if (rowIndex < 0) rowIndex = 0;
            if (rowIndex >= rowCount) rowIndex = rowCount - 1;
            if (!(centerY >= rowTops[rowIndex] && centerY <= rowBottoms[rowIndex]))
            {
                // choose nearest neighbor row by distance to boundaries
                double bestDist = double.MaxValue;
                int bestIdx = rowIndex;
                // candidate current
                double dCur = centerY < rowTops[rowIndex] ? (rowTops[rowIndex] - centerY) : (centerY - rowBottoms[rowIndex]);
                if (dCur < bestDist) { bestDist = dCur; bestIdx = rowIndex; }
                // candidate previous
                if (rowIndex - 1 >= 0)
                {
                    double dPrev = centerY < rowTops[rowIndex - 1] ? (rowTops[rowIndex - 1] - centerY) : (centerY - rowBottoms[rowIndex - 1]);
                    if (dPrev < bestDist) { bestDist = dPrev; bestIdx = rowIndex - 1; }
                }
                // candidate next
                if (rowIndex + 1 < rowCount)
                {
                    double dNext = centerY < rowTops[rowIndex + 1] ? (rowTops[rowIndex + 1] - centerY) : (centerY - rowBottoms[rowIndex + 1]);
                    if (dNext < bestDist) { bestDist = dNext; bestIdx = rowIndex + 1; }
                }
                rowIndex = bestIdx;
            }

            // Find column by binary search on Lefts then validate with Rights
            int colIndex = LowerBound(colLefts, centerX);
            if (colIndex < 0) colIndex = 0;
            if (colIndex >= colCount) colIndex = colCount - 1;
            if (!(centerX >= colLefts[colIndex] && centerX <= colRights[colIndex]))
            {
                double bestDist = double.MaxValue;
                int bestIdx = colIndex;
                double dCur = centerX < colLefts[colIndex] ? (colLefts[colIndex] - centerX) : (centerX - colRights[colIndex]);
                if (dCur < bestDist) { bestDist = dCur; bestIdx = colIndex; }
                if (colIndex - 1 >= 0)
                {
                    double dPrev = centerX < colLefts[colIndex - 1] ? (colLefts[colIndex - 1] - centerX) : (centerX - colRights[colIndex - 1]);
                    if (dPrev < bestDist) { bestDist = dPrev; bestIdx = colIndex - 1; }
                }
                if (colIndex + 1 < colCount)
                {
                    double dNext = centerX < colLefts[colIndex + 1] ? (colLefts[colIndex + 1] - centerX) : (centerX - colRights[colIndex + 1]);
                    if (dNext < bestDist) { bestDist = dNext; bestIdx = colIndex + 1; }
                }
                colIndex = bestIdx;
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
