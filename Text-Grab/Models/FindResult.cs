
namespace Text_Grab.Models;

public class FindResult
{
    public string Text { get; set; } = "";

    public int Count { get; set; } = 0;

    public int SelectionStart { get; set; }

    public string PreviewLeft { get; set; } = "";

    public string PreviewRight { get; set; } = "";

    public int SelectionLength
    {
        get
        {
            return Text.Length;
        }
    }
}
