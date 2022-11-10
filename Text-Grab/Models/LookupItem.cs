using System;

namespace Text_Grab.Models;

public class LookupItem : IEquatable<LookupItem>
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

    public bool Equals(LookupItem? other)
    {
        if (other is null)
            return false;

        if (other.ToString() == ToString())
            return true;

        return false;
    }
}