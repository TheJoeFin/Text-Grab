using BenchmarkDotNet.Attributes;
using System.Drawing;
using System.Text;
using Text_Grab.Models;
using Rect = System.Windows.Rect;

namespace Tests.Benchmarks;

[MemoryDiagnoser]
public class ResultTableBenchmarks
{
    private List<WordBorderInfo> _syntheticBorders = [];
    private ResultTable _resultTable = new();
    private Rectangle _canvas;
    private readonly StringBuilder _sb = new();

    [GlobalSetup]
    public void Setup()
    {
        // Create a synthetic table-like layout: 50 rows x 6 cols
        int rows = 50;
        int cols = 6;
        double x0 = 10;
        double y0 = 10;
        double colW = 110;   // average word width
        double rowH = 24;    // average word height
        double gapX = 16;    // gap between words (within cell)
        double gapCol = 60;  // gap between columns (between cells)
        double gapRow = 14;  // gap between rows

        Random rand = new(42);
        _syntheticBorders = new List<WordBorderInfo>(rows * cols);

        for (int r = 0; r < rows; r++)
        {
            double top = y0 + (r * (rowH + gapRow));
            for (int c = 0; c < cols; c++)
            {
                double left = x0 + (c * (colW + gapCol));

                string word = (c == cols - 1)
                    ? (r % 3 == 0 ? "55 %" : r % 5 == 0 ? "48%" : (40 + (r % 60)).ToString())
                    : (c == 0 ? $"Row{r:D2}" : $"Val{r:D2}_{c:D2}");

                // create 1-2 tokens per cell to mimic multi-token cells
                string[] tokens = word.Split(' ');
                double curLeft = left;
                foreach (string token in tokens)
                {
                    WordBorderInfo w = new()
                    {
                        Word = token,
                        BorderRect = new Rect(curLeft, top, Math.Max(12, token.Length * 7), rowH)
                    };
                    _syntheticBorders.Add(w);
                    curLeft += w.BorderRect.Width + gapX;
                }
            }
        }

        _canvas = new Rectangle(0, 0, (int)(x0 + (cols * (colW + gapCol)) + 100), (int)(y0 + (rows * (rowH + gapRow)) + 100));

        // Warm-up analysis so we can benchmark text build in isolation too
        _resultTable = new ResultTable();
        _resultTable.AnalyzeAsTable(_syntheticBorders, _canvas, drawTable: false);
    }

    [Benchmark]
    public int AnalyzeAsTable_Baseline()
    {
        List<WordBorderInfo> copy = new(_syntheticBorders.Count);
        // cheap copy to simulate new OCR output without sharing instances (keeps same rects/words)
        foreach (WordBorderInfo wb in _syntheticBorders)
        {
            copy.Add(new WordBorderInfo
            {
                Word = wb.Word,
                BorderRect = wb.BorderRect
            });
        }

        ResultTable rt = new();
        rt.AnalyzeAsTable(copy, _canvas, drawTable: false);
        return rt.Rows.Count + rt.Columns.Count;
    }

    [Benchmark]
    public int GetTextFromTabledWordBorders_Baseline()
    {
        _sb.Clear();
        ResultTable.GetTextFromTabledWordBorders(_sb, _syntheticBorders, isSpaceJoining: true);
        return _sb.Length;
    }
}
