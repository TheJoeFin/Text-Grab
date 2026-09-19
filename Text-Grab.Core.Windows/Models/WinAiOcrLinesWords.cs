using Microsoft.Windows.AI.Imaging;
using System;
using System.Text;
using Windows.Foundation;

namespace Text_Grab.Models;
public class WinAiOcrLinesWords : IOcrLinesWords
{
    public WinAiOcrLinesWords(RecognizedText recognizedText)
    {
        OriginalRecognizedText = recognizedText;
        Angle = recognizedText.TextAngle;
        StringBuilder sb = new();

        if (recognizedText.Lines is not null)
        {
            Lines = Array.ConvertAll(recognizedText.Lines, line => new WinAiOcrLine(line));

            foreach (RecognizedLine recognizedLine in recognizedText.Lines)
                sb.AppendLine(recognizedLine.Text);
        }
        else
        {
            Lines = [];
        }

        Text = sb.ToString().Trim();
    }

    public RecognizedText OriginalRecognizedText { get; set; }

    public string Text { get; set; }
    public float Angle { get; set; }
    public IOcrLine[] Lines { get; set; }
}

public class WinAiOcrLine : IOcrLine
{
    public WinAiOcrLine(RecognizedLine recognizedLine)
    {
        OriginalLine = recognizedLine;
        Text = recognizedLine.Text;
        Words = Array.ConvertAll(recognizedLine.Words, word => new WinAiOcrWord(word));
        BoundingBox = new Rect(
            recognizedLine.BoundingBox.TopLeft,
            recognizedLine.BoundingBox.BottomRight);
    }

    public RecognizedLine OriginalLine { get; set; }

    public string Text { get; set; }
    public IOcrWord[] Words { get; set; }
    public Rect BoundingBox { get; set; }
}

public class WinAiOcrWord : IOcrWord
{
    public WinAiOcrWord(RecognizedWord recognizedWord)
    {
        OriginalWord = recognizedWord;
        Text = recognizedWord.Text;
        BoundingBox = new Rect(
            recognizedWord.BoundingBox.TopLeft,
            recognizedWord.BoundingBox.BottomRight);
    }

    public RecognizedWord OriginalWord { get; set; }

    public string Text { get; set; }
    public Rect BoundingBox { get; set; }
}
