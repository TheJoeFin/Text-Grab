using Text_Grab.Utilities;
using Windows.Foundation;
using Windows.Media.Ocr;

namespace Text_Grab.Models;
public class WinRtOcrLinesWords : IOcrLinesWords
{

    public WinRtOcrLinesWords(OcrResult ocrResult)
    {
        OriginalOcrResult = ocrResult;
        Angle = (float)(ocrResult.TextAngle ?? 0.0f);

        Lines = new WinRtOcrLine[ocrResult.Lines.Count];

        for (int i = 0; i < ocrResult.Lines.Count; i++)
        {
            OcrLine line = ocrResult.Lines[i];
            Lines[i] = new WinRtOcrLine(line);
        }

        Text = ocrResult.Text;
    }
    public OcrResult OriginalOcrResult { get; set; }
    public string Text { get; set; }
    public float Angle { get; set; }
    public IOcrLine[] Lines { get; set; }
}

public class WinRtOcrLine : IOcrLine
{
    public WinRtOcrLine(OcrLine ocrLine)
    {
        OriginalLine = ocrLine;
        Text = ocrLine.Text;
        Words = new WinRtOcrWord[ocrLine.Words.Count];

        for (int i = 0; i < ocrLine.Words.Count; i++)
        {
            OcrWord word = ocrLine.Words[i];
            Words[i] = new WinRtOcrWord(word);
        }

        System.Windows.Rect bRect = ocrLine.GetBoundingRect();

        BoundingBox = new Rect(bRect.Left, bRect.Top, bRect.Width, bRect.Height);
    }

    public OcrLine OriginalLine { get; set; }

    public string Text { get; set; }
    public IOcrWord[] Words { get; set; }
    public Rect BoundingBox { get; set; }
}

public class WinRtOcrWord : IOcrWord
{
    public WinRtOcrWord(OcrWord ocrWord)
    {
        OriginalWord = ocrWord;
        Text = ocrWord.Text;
        BoundingBox = ocrWord.BoundingRect;
    }

    public OcrWord OriginalWord { get; set; }

    public string Text { get; set; }
    public Rect BoundingBox { get; set; }
}
