using System.Drawing;

namespace Text_Grab.Models;

public class WordBorderInfo
{
    public string Word { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public RectangleF BorderRect { get; set; } = RectangleF.Empty;
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
}
