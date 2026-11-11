using System.Drawing;
using Text_Grab;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Tests;

/// <summary>
/// The scaler seam the OCR dispatch uses (v5 F3). In the app the WPF scaler is registered, so grabs
/// scale exactly as before the dispatch moved to Core.Windows; headless hosts fall back to GDI+.
/// </summary>
public class BitmapScalingAccessTests
{
    [Fact]
    public void ModuleInitializer_RegistersTheWpfScalerWithoutAnyStartupCall()
    {
        // The test host never raises App's WPF Startup event, so this passing is the proof that
        // the registration lives in a module initializer.
        Assert.True(BitmapScalingAccess.IsConfigured);
    }

    [WpfFact]
    public void ScaleUniform_InTheApp_IsTheWpfScaler()
    {
        using Bitmap source = OcrEngineUtilities.LoadBitmapFromFile(FileUtilities.GetPathToLocalFile(@".\Images\font_sample.png"));

        using Bitmap viaSeam = BitmapScalingAccess.ScaleUniform(source, 1.7);
        using Bitmap direct = ImageMethods.ScaleBitmapUniform(source, 1.7);

        Assert.Equal(direct.Size, viaSeam.Size);
        Assert.Equal(direct.PixelFormat, viaSeam.PixelFormat);
        for (int y = 0; y < direct.Height; y += 7)
            for (int x = 0; x < direct.Width; x += 7)
                Assert.Equal(direct.GetPixel(x, y), viaSeam.GetPixel(x, y));
    }

    /// <summary>
    /// A headless host scales with the GDI+ default instead of WPF. This pins that the two give the
    /// engine the same text on the OCR fixtures, without touching the process-global registration.
    /// </summary>
    [WpfTheory]
    [InlineData(@".\Images\font_sample.png")]
    [InlineData(@".\Images\FontTest.png")]
    [InlineData(@".\Images\paragraph-test-image.png")]
    public async Task DefaultScaler_GivesTheEngineTheSameTextAsTheWpfScaler(string relativePath)
    {
        ILanguage language = new GlobalLang("en-US");
        OcrOptions options = new(
            CorrectErrors: true, CorrectToLatin: true, RemoveFurigana: true,
            ParagraphDetection: true, TryToReadBarcodes: false, LanguageTag: "en-US");
        using Bitmap source = OcrEngineUtilities.LoadBitmapFromFile(FileUtilities.GetPathToLocalFile(relativePath));
        double scale = await OcrEngineUtilities.GetIdealScaleFactorForOcrAsync(source, language);

        using Bitmap wpfScaled = ImageMethods.ScaleBitmapUniform(source, scale);
        using Bitmap gdiScaled = BitmapScalingAccess.DefaultScaleUniform(source, scale);

        string wpfText = OcrUtilities.BuildTextFromOcrLines(
            language, await OcrEngineUtilities.GetOcrResultFromImageAsync(wpfScaled, language), options);
        string gdiText = OcrUtilities.BuildTextFromOcrLines(
            language, await OcrEngineUtilities.GetOcrResultFromImageAsync(gdiScaled, language), options);

        Assert.False(string.IsNullOrWhiteSpace(wpfText));
        Assert.Equal(wpfText, gdiText);
    }
}
