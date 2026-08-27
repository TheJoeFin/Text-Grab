using Windows.Foundation;

namespace Text_Grab.Models;

public class GeneratedOcrLinesWords : IOcrLinesWords
{
    public string Text { get; set; } = string.Empty;

    public IOcrLine[] Lines { get; set; } = [];

    public float Angle { get; set; }

    public static GeneratedOcrLinesWords FromParagraph(string text, Rect boundingBox)
    {
        string normalizedText = text?.Trim() ?? string.Empty;

        return new GeneratedOcrLinesWords
        {
            Text = normalizedText,
            Angle = 0,
            Lines = string.IsNullOrWhiteSpace(normalizedText)
                ? []
                : [GeneratedOcrLine.FromText(normalizedText, boundingBox)]
        };
    }
}

public class GeneratedOcrLine : IOcrLine
{
    public string Text { get; set; } = string.Empty;

    public IOcrWord[] Words { get; set; } = [];

    public Rect BoundingBox { get; set; }

    public static GeneratedOcrLine FromText(string text, Rect boundingBox)
    {
        return new GeneratedOcrLine
        {
            Text = text,
            BoundingBox = boundingBox,
            Words = [new GeneratedOcrWord { Text = text, BoundingBox = boundingBox }]
        };
    }
}

public class GeneratedOcrWord : IOcrWord
{
    public string Text { get; set; } = string.Empty;

    public Rect BoundingBox { get; set; }
}
