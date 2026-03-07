namespace Text_Grab.Controls;

public class InlinePickerItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Optional group label used to render section headers in the picker popup
    /// (e.g. "Regions", "Patterns").
    /// </summary>
    public string Group { get; set; } = string.Empty;

    public InlinePickerItem() { }

    public InlinePickerItem(string displayName, string value, string group = "")
    {
        DisplayName = displayName;
        Value = value;
        Group = group;
    }

    public override string ToString() => DisplayName;
}
