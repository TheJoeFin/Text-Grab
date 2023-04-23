namespace Text_Grab;

public enum DefaultLaunchSetting
{
    Fullscreen = 0,
    GrabFrame = 1,
    EditText = 2,
    QuickLookup = 3
}

public enum AddRemove
{
    Add = 0,
    Remove = 1,
}

public enum SpotInLine
{
    Beginning = 0,
    End = 1,
}

public enum CurrentCase
{
    Lower = 0,
    Camel = 1,
    Upper = 2,
    Unknown = 3
}

public enum Side
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 3,
    Bottom = 4
}

public enum OcrOutputKind
{
    None = 0,
    Line = 1,
    Paragraph = 2,
    Barcode = 3,
}

public enum AppTheme
{
    System = 0,
    Dark = 1,
    Light = 2
}