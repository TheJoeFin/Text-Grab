namespace Text_Grab.Controls;

public class InlinePickerItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public InlinePickerItem() { }

    public InlinePickerItem(string displayName, string value)
    {
        DisplayName = displayName;
        Value = value;
    }

    public override string ToString() => DisplayName;
}
