using System.Text.RegularExpressions;

namespace Text_Grab.Utilities;

public class TessOcrLine
{
    public int Height { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Width { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public static class HocrReader
{
    private static readonly string[] separator = ["<span class='ocr_line'", "</span>"];

    public static List<TessOcrLine> ReadLines(string hocrText)
    {
        // Create a list to hold the OcrLine objects
        List<TessOcrLine> lines = new();

        // Split the hOCR text into lines
        string[] hocrLines = hocrText.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        // Iterate through the lines
        foreach (string hocrLineText in hocrLines)
        {
            // Extract the line information
            TessOcrLine line = ReadLine(hocrLineText);

            // Add the line to the list
            lines.Add(line);
        }

        return lines;
    }

    private static TessOcrLine ReadLine(string hocrLineText)
    {
        // Create a new OcrLine object
        TessOcrLine line = new();

        // Extract the text of the line from the hOCR text
        Match textMatch = Regex.Match(hocrLineText, "<span class='ocr_line'[^>]*>(.*?)</span>");
        line.Text = textMatch.Groups[1].Value;

        // Extract the bounding box coordinates from the hOCR text
        Match bboxMatch = Regex.Match(hocrLineText, "bbox (\\d+) (\\d+) (\\d+) (\\d+)");
        line.X = int.Parse(bboxMatch.Groups[1].Value);
        line.Y = int.Parse(bboxMatch.Groups[2].Value);
        line.Width = int.Parse(bboxMatch.Groups[3].Value);
        line.Height = int.Parse(bboxMatch.Groups[4].Value);

        return line;
    }
}
