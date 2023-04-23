using System.Drawing;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.Globalization;

namespace Text_Grab.Models;

public record OcrOutput
{
    public OcrOutputKind Kind { get; set; } = OcrOutputKind.None;
    public string RawOutput { get; set; } = string.Empty;
    public string CleanedOutput { get; set; } = string.Empty;
    public Bitmap? SourceBitmap { get; set; }
    public Language? Language { get; set; }

    public void CleanOutput()
    {
        if (Settings.Default is not Settings userSettings
            || Kind == OcrOutputKind.Barcode)
            return;

        string correctingString = RawOutput;

        if (userSettings.CorrectToLatin)
            correctingString = correctingString.ReplaceGreekOrCyrillicWithLatin();

        if (userSettings.CorrectErrors)
            correctingString = correctingString.TryFixEveryWordLetterNumberErrors();

        CleanedOutput = correctingString;
    }
}
