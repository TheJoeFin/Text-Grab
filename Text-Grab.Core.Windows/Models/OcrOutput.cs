using System.Drawing;
using Text_Grab.Interfaces;
using Text_Grab.Services;
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

    /// <param name="options">Per-call overrides; <c>null</c> uses the current user settings.</param>
    public void CleanOutput(OcrOptions? options = null)
    {
        if (Kind == OcrOutputKind.Barcode)
            return;

        options ??= OcrOptions.FromSettings(SettingsAccess.Current);

        string correctingString = RawOutput;

        if (options.CorrectToLatin && Language?.IsLatinBased() == true)
            correctingString = correctingString.ReplaceGreekOrCyrillicWithLatin();

        if (options.CorrectErrors)
            correctingString = correctingString.TryFixEveryWordLetterNumberErrors();

        CleanedOutput = correctingString;
    }
}
