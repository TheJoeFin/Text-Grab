namespace Text_Grab.Models;

public class FindResult
{
    public string Text { get; set; } = "";
    public int SelectionStart { get; set; }
    public int SelectionLength
    {
        get
        {
            return Text.Length;
        }
    }
}
