using Text_Grab.Models;

namespace Text_Grab.Controls;

public class InlinePickerItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Optional group label used to render section headers in the picker popup
    /// (e.g. "Regions", "Saved Patterns", "Smart Patterns").
    /// </summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// For pattern items, which engine backs this entry — drives whether selection emits a
    /// <c>{p:}</c> (saved regex) or <c>{r:}</c> (recognizer) placeholder. Null for non-pattern
    /// items such as region placeholders.
    /// </summary>
    public PatternKind? Kind { get; set; }

    public InlinePickerItem() { }

    public InlinePickerItem(string displayName, string value, string group = "")
    {
        DisplayName = displayName;
        Value = value;
        Group = group;
    }

    public override string ToString() => DisplayName;
}
