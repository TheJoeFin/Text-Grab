using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

// Tesseract is a facade over two engines, chosen once by TesseractEngineModeResolver:
//   Bundled  - in-process Tesseract that ships with Text Grab (BundledTesseractBackend)
//   External - the legacy user-installed tesseract.exe (ExternalTesseractBackend, frozen)
// Tesseract docs: https://tesseract-ocr.github.io/tessdoc/

public static class TesseractHelper
{
    public static TesseractEngineMode Mode => TesseractEngineModeResolver.Current;

    /// <summary>
    /// Mode-aware gate that replaces <c>UseTesseract &amp;&amp; CanLocateTesseractExe()</c>: a bundled user
    /// has no exe to locate. Never runs the external install probe in Bundled mode.
    /// </summary>
    public static bool IsAvailable()
    {
        if (Mode == TesseractEngineMode.External)
            return ExternalTesseractBackend.CanLocateTesseractExe();

        return TessdataLocator.GetInstalledLanguages().Count > 0;
    }

    /// <summary>True when the legacy external tesseract.exe can be found. External mode only.</summary>
    public static bool CanLocateTesseractExe() => ExternalTesseractBackend.CanLocateTesseractExe();

    public static Task<string> GetTextFromImagePathAsync(string imagePath, string tessTag) =>
        Mode == TesseractEngineMode.External
            ? ExternalTesseractBackend.GetTextFromImagePathAsync(imagePath, tessTag)
            : BundledTesseractBackend.RecognizeFileAsync(imagePath, tessTag);

    public static async Task<OcrOutput> GetOcrOutputFromBitmap(Bitmap bmp, TessLang language, OcrOptions? options = null)
    {
        string rawOutput;

        if (Mode == TesseractEngineMode.External)
        {
            bmp.Save(TempImagePath(), ImageFormat.Png);
            rawOutput = await ExternalTesseractBackend.GetTextFromImagePathAsync(TempImagePath(), language.RawTag);
        }
        else
        {
            rawOutput = await BundledTesseractBackend.RecognizeAsync(bmp, language.RawTag);
        }

        OcrOutput ocrOutput = new()
        {
            Engine = OcrEngineKind.Tesseract,
            Kind = OcrOutputKind.Paragraph,
            Language = language,
            SourceBitmap = bmp,
            RawOutput = rawOutput
        };
        ocrOutput.CleanOutput(options);

        return ocrOutput;
    }

    public static Task<string> GetTextFromImagePath(string pathToFile, bool outputHocr) =>
        Mode == TesseractEngineMode.External
            ? ExternalTesseractBackend.GetTextFromImagePath(pathToFile, outputHocr)
            : BundledTesseractBackend.RecognizeFileAsync(pathToFile, "eng");

    public static string TempImagePath()
    {
        if (AutomationProfile.Current is not null)
            return Path.Combine(AutomationProfile.GetTemporaryDirectory(), "tempImage.png");

        string? exePath = Path.GetDirectoryName(AppContext.BaseDirectory);
        if (exePath is null)
        {
            string rawPath = @"%LOCALAPPDATA%\Text_Grab";
            exePath = Environment.ExpandEnvironmentVariables(rawPath);
        }

        return $"{exePath}\\tempImage.png";
    }

    public static async Task<List<string>> TesseractLanguagesAsStrings()
    {
        if (Mode == TesseractEngineMode.External)
            return await ExternalTesseractBackend.TesseractLanguagesAsStrings();

        List<string> languages = [.. TessdataLocator.GetInstalledLanguages().Select(file => file.Tag)];
        if (languages.Count == 0)
            languages.Add("eng");

        return languages;
    }

    public static async Task<List<ILanguage>> TesseractLanguages()
    {
        List<string> languageStrings = await TesseractLanguagesAsStrings();
        List<ILanguage> tesseractLanguages = new();

        foreach (string language in languageStrings)
            tesseractLanguages.Add(new TessLang(language));

        return tesseractLanguages;
    }

    /// <summary>Bundled engine health: Ready (with version), library failed to load, or no languages.</summary>
    public static Task<BundledTesseractStatus> GetBundledStatusAsync() => BundledTesseractBackend.GetStatusAsync();

    /// <summary>Call after a model was downloaded, removed or the engine mode changed.</summary>
    public static Task InvalidateBundledEngineAsync() => BundledTesseractBackend.InvalidateAsync();
}
