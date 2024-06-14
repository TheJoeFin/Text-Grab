namespace Text_Grab;

public enum AddRemove
{
    Add = 0,
    Remove = 1,
}

public enum AppTheme
{
    System = 0,
    Dark = 1,
    Light = 2
}

public enum CurrentCase
{
    Lower = 0,
    Camel = 1,
    Upper = 2,
    Unknown = 3
}

public enum FileStorageKind
{
    Absolute = 0,
    WithExe = 1,
    WithHistory = 2,
}

public enum OpenContentKind
{
    Image = 0,
    TextFile = 1,
    Directory = 2,
}

public enum OcrEngineKind
{
    Windows = 0,
    Tesseract = 1,
}

public enum OcrOutputKind
{
    None = 0,
    Line = 1,
    Paragraph = 2,
    Barcode = 3,
}

public enum Side
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 3,
    Bottom = 4
}

public enum SpotInLine
{
    Beginning = 0,
    End = 1,
}

public enum TextGrabMode
{
    Fullscreen = 0,
    GrabFrame = 1,
    EditText = 2,
    QuickLookup = 3
}
public enum VirtualKeyCodes : short
{
    LeftButton = 0x01,
    RightButton = 0x02,
    MiddleButton = 0x04
}

public enum ScrollBehavior
{
    None = 0,
    Resize = 1,
    Zoom = 2,
}