namespace Text_Grab.Models;

public class LookupItem
{
    public string shortValue { get; set; } = string.Empty;
    public string longValue { get; set; } = string.Empty;

    public LookupItem()
    {

    }

    public LookupItem(string sv, string lv)
    {
        shortValue = sv;
        longValue = lv;
    }

    public override string ToString() => $"{shortValue} {longValue}";

    public string ToCSVString() => $"{shortValue},{longValue}";
}