
namespace Text_Grab.Models;

public class FindResult
{
    public string Text { get; set; } = "";

    public int Count { get; set; } = 0;

    public int Index { get; set; }

    public string PreviewLeft { get; set; } = "";

    public string PreviewRight { get; set; } = "";

    public int Length
    {
        get
        {
            return Text.Length;
        }
    }
}
