using System;
using System.Windows;
using Text_Grab.Controls;

namespace Text_Grab.Models;

public class WordBorderInfo
{
    public string Word { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public Rect BorderRect { get; set; } = Rect.Empty;
    public double DisplayLineHeight { get; set; } = 0;
    public bool KeepSingleLineOutput { get; set; } = false;
    public int LineNumber { get; set; } = 0;
    public int ResultColumnID { get; set; } = 0;
    public int ResultRowID { get; set; } = 0;
    public string MatchingBackground { get; set; } = "Transparent";
    public bool IsBarcode { get; set; } = false;

    public WordBorderInfo()
    {

    }

    public WordBorderInfo(WordBorder wordBorder)
    {
        Word = wordBorder.Word;
        DisplayText = wordBorder.KeepSingleLineOutput || !string.Equals(wordBorder.DisplayText, wordBorder.Word, StringComparison.Ordinal)
            ? wordBorder.DisplayText
            : string.Empty;
        DisplayLineHeight = wordBorder.DisplayLineHeight;
        KeepSingleLineOutput = wordBorder.KeepSingleLineOutput;
        LineNumber = wordBorder.LineNumber;
        ResultColumnID = wordBorder.ResultColumnID;
        ResultRowID = wordBorder.ResultRowID;
        MatchingBackground = wordBorder.MatchingBackground.ToString();
        IsBarcode = wordBorder.IsBarcode;
        BorderRect = new()
        {
            X = wordBorder.Left,
            Y = wordBorder.Top,
            Width = wordBorder.Width,
            Height = wordBorder.Height
        };
    }
}
