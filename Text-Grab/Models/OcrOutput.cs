using System.Drawing;
using Text_Grab.Interfaces;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.Graphics.Imaging;

namespace Text_Grab.Models;

public record OcrOutput
{
    public OcrEngineKind Engine { get; set; } = OcrEngineKind.Windows;
    public OcrOutputKind Kind { get; set; } = OcrOutputKind.None;
    public string RawOutput { get; set; } = string.Empty;
    public string CleanedOutput { get; set; } = string.Empty;
    public Bitmap? SourceBitmap { get; set; }
    public SoftwareBitmap? SourceSoftwareBitmap { get; set; }
    public ILanguage? Language { get; set; }

    public void CleanOutput()
    {
        if (AppUtilities.TextGrabSettings is not Settings userSettings
            || Kind == OcrOutputKind.Barcode)
            return;

        string correctingString = RawOutput;

        if (userSettings.CorrectToLatin && Language?.IsLatinBased() == true)
            correctingString = correctingString.ReplaceGreekOrCyrillicWithLatin();

        if (userSettings.CorrectErrors)
            correctingString = correctingString.TryFixEveryWordLetterNumberErrors();

        CleanedOutput = correctingString;
    }
}
