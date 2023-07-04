using Text_Grab.Controls;

namespace Text_Grab.Models;

public class WordBorderInfo
{
    public string Word { get; set; } = string.Empty;
    public double Left { get; set; } = 0.0;
    public double Top { get; set; } = 0.0;
    public int LineNumber { get; set; } = 0;
    public int ResultColumnID { get; set; } = 0;
    public int ResultRowID { get; set; } = 0;
    public string MatchingBackground { get; set; } = "Transparent";

    public WordBorderInfo()
    {

    }

    public WordBorderInfo(WordBorder wordBorder)
    {
        Word = wordBorder.Word;
        Left = wordBorder.Left;
        Top = wordBorder.Top;
        LineNumber = wordBorder.LineNumber;
        ResultColumnID = wordBorder.ResultColumnID;
        ResultRowID = wordBorder.ResultRowID;
        MatchingBackground = wordBorder.MatchingBackground.ToString();
    }
}
