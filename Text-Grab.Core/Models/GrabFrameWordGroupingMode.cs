namespace Text_Grab.Models;

public enum GrabFrameWordGroupingMode
{
    /// <summary>One WordBorder per OCR line (original default).</summary>
    Line,

    /// <summary>One WordBorder per individual OCR word.</summary>
    Word,

    /// <summary>Wrapped lines merged into paragraph blocks.</summary>
    Paragraph,

    /// <summary>All OCR output in a single WordBorder.</summary>
    Window,
}
