
using Windows.Foundation;

namespace Text_Grab.Models;
public interface IOcrLinesWords
{
    string Text { get; set; }

    IOcrLine[] Lines { get; set; }

    float Angle { get; set; }
}

public interface IOcrLine
{
    string Text { get; set; }

    IOcrWord[] Words { get; set; }

    Rect BoundingBox { get; set; }
}

public interface IOcrWord
{
    string Text { get; set; }

    Rect BoundingBox { get; set; }
}
