using Microsoft.Windows.AI.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Services;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

namespace Text_Grab.Utilities;

/// <summary>
/// Headless OCR engine dispatch: recognize a GDI+ bitmap, a stream or an image file with the
/// Windows OCR engine, Windows AI, Tesseract or the Windows AI image description, plus barcodes.
///
/// The WPF-free half of what was <c>OcrSourceUtilities</c> (v5 F3). The app-bound half - screen
/// regions captured through the history cache, UI Automation, <c>Window</c>/<c>BitmapSource</c>
/// sources, PDFs and grab templates - stayed in the app under the old name, because 21 of the 37
/// external references pointed at it. It calls into this class.
///
/// Scaling goes through <see cref="BitmapScalingAccess"/>, which the app points at its WPF scaler.
///
/// Methods that shape text take an optional <see cref="OcrOptions"/>. <c>null</c> means "snapshot
/// the current settings", which is exactly what they did before the parameter existed.
/// <see cref="OcrOptions.LanguageTag"/> is not consulted here: every method takes its language
/// explicitly.
/// </summary>
public static class OcrEngineUtilities
{
    internal static bool IsUiAutomationLanguage(ILanguage language) => language is UiAutomationLang;
    private static bool IsWindowsAiDescriptionLanguage(ILanguage language) => language is WindowsAiDescriptionLang;

    internal static ILanguage GetCompatibleOcrLanguage(ILanguage language)
    {
        if (language is UiAutomationLang)
            return CaptureLanguageUtilities.GetUiAutomationFallbackLanguage();

        return language;
    }

    public static async Task<string> GetTextFromBitmapAsync(Bitmap bitmap, ILanguage language, OcrOptions? options = null)
    {
        options ??= OcrOptions.FromSettings(SettingsAccess.Current);

        if (IsUiAutomationLanguage(language))
        {
            if (!SettingsAccess.Current.UiAutomationFallbackToOcr)
                return string.Empty;

            language = GetCompatibleOcrLanguage(language);
        }

        return OcrUtilities.GetStringFromOcrOutputs(await GetTextFromImageAsync(bitmap, language, options), options);
    }

    public static async Task<string> GetTextFromBitmapAsTableAsync(Bitmap bitmap, ILanguage language, OcrOptions? options = null)
    {
        options ??= OcrOptions.FromSettings(SettingsAccess.Current);

        ILanguage compatibleLanguage = GetCompatibleOcrLanguage(language);
        double scale = await GetIdealScaleFactorForOcrAsync(bitmap, compatibleLanguage);
        using Bitmap scaledBitmap = BitmapScalingAccess.ScaleUniform(bitmap, scale);
        IOcrLinesWords ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, compatibleLanguage);

        List<WordBorderInfo> wordBorderInfos = OcrUtilities.ParseOcrResultIntoWordBorderInfos(
            ocrResult,
            compatibleLanguage.IsLatinBased(),
            options);

        Rectangle rectCanvasSize = new()
        {
            Width = scaledBitmap.Width,
            Height = scaledBitmap.Height,
            X = 0,
            Y = 0
        };

        ResultTable table = new();
        table.AnalyzeAsTable(wordBorderInfos, rectCanvasSize);

        StringBuilder textBuilder = new();
        ResultTable.GetTextFromTabledWordBorders(textBuilder, wordBorderInfos, compatibleLanguage.IsSpaceJoining());
        return textBuilder.ToString();
    }

    public static async Task<(IOcrLinesWords?, double)> GetOcrResultFromBitmapAsync(Bitmap bmp, ILanguage language)
    {
        language = GetCompatibleOcrLanguage(language);

        if (IsWindowsAiDescriptionLanguage(language))
            return (await GetOcrResultFromImageAsync(bmp, language), 1.0);

        if (language is WindowsAiLang)
            return (await WindowsAiUtilities.GetOcrResultAsync(bmp), 1.0);

        if (language is not GlobalLang globalLang)
            globalLang = new GlobalLang(language.LanguageTag);

        double scale = await GetIdealScaleFactorForOcrAsync(bmp, language);
        using Bitmap scaledBitmap = BitmapScalingAccess.ScaleUniform(bmp, scale);
        IOcrLinesWords ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, globalLang);
        return (ocrResult, scale);
    }

    public static async Task<IOcrLinesWords> GetOcrResultFromImageAsync(SoftwareBitmap scaledBitmap, ILanguage language)
    {
        language = GetCompatibleOcrLanguage(language);

        if (IsWindowsAiDescriptionLanguage(language))
            return await GetWindowsAiDescriptionOcrResultAsync(scaledBitmap);

        if (language is WindowsAiLang winAiLang)
        {
            RecognizedText? recognizedText = await WindowsAiUtilities.GetOcrResultAsync(scaledBitmap);
            if (recognizedText is not null)
                return new WinAiOcrLinesWords(recognizedText);

            language = LanguageUtilities.GetCurrentInputLanguage().AsLanguage() is Language fallbackLanguage
                ? new GlobalLang(fallbackLanguage)
                : new GlobalLang("en-US");
        }

        if (language is not GlobalLang globalLang)
            globalLang = new GlobalLang(language.LanguageTag);

        OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(globalLang.OriginalLanguage);

        ocrEngine ??= OcrEngine.TryCreateFromLanguage(LanguageUtilities.GetCurrentInputLanguage().AsLanguage() ?? new Language("en-US"));

        return new WinRtOcrLinesWords(await ocrEngine.RecognizeAsync(scaledBitmap));
    }

    public static async Task<IOcrLinesWords> GetOcrResultFromImageAsync(Bitmap scaledBitmap, ILanguage language)
    {
        language = GetCompatibleOcrLanguage(language);
        await using MemoryStream memory = new();
        using WrappingStream wrapper = new(memory);

        scaledBitmap.Save(wrapper, ImageFormat.Bmp);
        wrapper.Position = 0;
        BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(wrapper.AsRandomAccessStream());
        using SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();
        await wrapper.FlushAsync();


        return await GetOcrResultFromImageAsync(softwareBmp, language);
    }

    public static async Task<List<OcrOutput>> GetTextFromRandomAccessStream(IRandomAccessStream randomAccessStream, ILanguage language, OcrOptions? options = null)
    {
        Bitmap bitmap = BitmapUtilities.GetBitmapFromIRandomAccessStream(randomAccessStream);
        List<OcrOutput> outputs = await GetTextFromImageAsync(bitmap, language, options);
        return outputs;
    }

    public static async Task<List<OcrOutput>> GetTextFromWinAiAsync(Bitmap bitmap, WindowsAiLang language, OcrOptions? options = null)
    {
        options ??= OcrOptions.FromSettings(SettingsAccess.Current);

        if (OcrUtilities.ShouldUseParagraphDetection(language.IsSpaceJoining(), options: options))
        {
            WinAiOcrLinesWords? ocrResult = await WindowsAiUtilities.GetOcrResultAsync(bitmap);
            if (ocrResult is not null)
                return [GetTextFromOcrResult(language, bitmap, ocrResult, options)];
        }

        // get temp path
        string tempPath = AutomationProfile.GetTemporaryDirectory();
        string tempFileName = Path.GetRandomFileName() + ".bmp";
        string tempFilePath = Path.Combine(tempPath, tempFileName);
        try
        {
            bitmap.Save(tempFilePath, ImageFormat.Bmp);

            string result = await WindowsAiUtilities.GetTextWithWinAI(tempFilePath);

            OcrOutput paragraphsOutput = new()
            {
                Kind = OcrOutputKind.Paragraph,
                RawOutput = result,
                Language = language,
                SourceBitmap = bitmap,
            };

            List<OcrOutput> outputs = [paragraphsOutput];
            return outputs;
        }
        finally
        {
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
    }

    public static async Task<List<OcrOutput>> GetTextFromWinAiDescriptionAsync(Bitmap bitmap, WindowsAiDescriptionLang language, OcrOptions? options = null)
    {
        options ??= OcrOptions.FromSettings(SettingsAccess.Current);

        IOcrLinesWords descriptionResult = await GetOcrResultFromImageAsync(bitmap, language);
        return [GetTextFromOcrResult(language, bitmap, descriptionResult, options)];
    }

    public static async Task<List<OcrOutput>> GetTextFromImageAsync(Bitmap bitmap, ILanguage language, OcrOptions? options = null)
    {
        options ??= OcrOptions.FromSettings(SettingsAccess.Current);

        List<OcrOutput> outputs = [];

        if (IsUiAutomationLanguage(language))
        {
            if (!SettingsAccess.Current.UiAutomationFallbackToOcr)
                return outputs;

            language = GetCompatibleOcrLanguage(language);
        }

        if (language is TessLang tessLang)
        {
            OcrOutput tesseractOutput = await TesseractHelper.GetOcrOutputFromBitmap(bitmap, tessLang, options);
            outputs.Add(tesseractOutput);
        }
        else if (language is WindowsAiLang winAiLang)
        {
            outputs.AddRange(await GetTextFromWinAiAsync(bitmap, winAiLang, options));
        }
        else if (language is WindowsAiDescriptionLang windowsAiDescriptionLang)
        {
            outputs.AddRange(await GetTextFromWinAiDescriptionAsync(bitmap, windowsAiDescriptionLang, options));
        }
        else
        {
            GlobalLang ocrLanguageFromILang = language as GlobalLang ?? new GlobalLang("en-US");
            double scale = await GetIdealScaleFactorForOcrAsync(bitmap, ocrLanguageFromILang);
            using Bitmap scaledBitmap = BitmapScalingAccess.ScaleUniform(bitmap, scale);
            IOcrLinesWords ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, ocrLanguageFromILang);
            OcrOutput paragraphsOutput = GetTextFromOcrResult(ocrLanguageFromILang, new Bitmap(scaledBitmap), ocrResult, options);
            outputs.Add(paragraphsOutput);
        }

        if (options.TryToReadBarcodes)
            outputs.AddRange(BarcodeUtilities.TryToReadBarcodes(bitmap));

        return outputs;
    }

    private static OcrOutput GetTextFromOcrResult(ILanguage language, Bitmap? scaledBitmap, IOcrLinesWords ocrResult, OcrOptions options)
    {
        OcrOutput paragraphsOutput = new()
        {
            Kind = OcrOutputKind.Paragraph,
            RawOutput = OcrUtilities.BuildTextFromOcrLines(language, ocrResult, options),
            Language = language,
            SourceBitmap = scaledBitmap,
        };
        return paragraphsOutput;
    }

    /// <summary>
    /// OCRs an image file (not a PDF) and returns the joined text. Applies the file's EXIF
    /// rotation first; see <see cref="LoadBitmapFromFile"/>.
    /// </summary>
    public static async Task<string> OcrImageFileAsync(string absolutePath, ILanguage language, OcrOptions? options = null)
    {
        options ??= OcrOptions.FromSettings(SettingsAccess.Current);

        using Bitmap bmp = LoadBitmapFromFile(absolutePath);
        return OcrUtilities.GetStringFromOcrOutputs(await GetTextFromImageAsync(bmp, language, options), options);
    }

    /// <summary>
    /// Loads an image file as a 32bpp ARGB bitmap, rotated upright by its EXIF orientation.
    ///
    /// The file is read into memory first: a GDI+ Bitmap built from a path or a file stream keeps
    /// the file locked until it is disposed. Copying through <c>new Bitmap(Image)</c> both detaches
    /// the result from that stream and normalizes it to 32bpp ARGB - GDI+ decodes a JPEG as 24bpp,
    /// and the OCR scaling path copies pixels as 32 bits each.
    ///
    /// Only the three pure rotations are applied, exactly as the WPF <c>RotateImage</c> switch this
    /// replaced did. Mirrored orientations (EXIF 2, 4, 5, 7) were never flipped, and 5 and 7 were
    /// not rotated either; ExifOrientationTests pins that.
    /// </summary>
    internal static Bitmap LoadBitmapFromFile(string absolutePath)
    {
        RotateFlipType rotateFlipType = BitmapUtilities.GetRotateFlipType(absolutePath);

        using MemoryStream fileContents = new(File.ReadAllBytes(absolutePath));
        using Bitmap decoded = new(fileContents);
        Bitmap bitmap = new(decoded);

        if (rotateFlipType is RotateFlipType.Rotate90FlipNone
            or RotateFlipType.Rotate180FlipNone
            or RotateFlipType.Rotate270FlipNone)
        {
            bitmap.RotateFlip(rotateFlipType);
        }

        return bitmap;
    }

    public static async Task<double> GetIdealScaleFactorForOcrAsync(Bitmap bitmap, ILanguage selectedLanguage)
    {
        if (IsWindowsAiDescriptionLanguage(selectedLanguage))
            return 1.0;

        selectedLanguage = GetCompatibleOcrLanguage(selectedLanguage);
        IOcrLinesWords ocrResult = await GetOcrResultFromImageAsync(bitmap, selectedLanguage);
        return GetIdealScaleFactorForOcrResult(ocrResult, bitmap.Height, bitmap.Width);
    }

    private static async Task<IOcrLinesWords> GetWindowsAiDescriptionOcrResultAsync(SoftwareBitmap softwareBitmap)
    {
        string description = await WindowsAiUtilities.GetTextDescriptionWithWinAI(softwareBitmap);
        Windows.Foundation.Rect fullBounds = new(0, 0, softwareBitmap.PixelWidth, softwareBitmap.PixelHeight);
        return GeneratedOcrLinesWords.FromParagraph(description, fullBounds);
    }

    private static double GetIdealScaleFactorForOcrResult(IOcrLinesWords ocrResult, int height, int width)
    {
        List<double> heightsList = [];
        double scaleFactor = 1.5;

        foreach (IOcrLine ocrLine in ocrResult.Lines)
            foreach (IOcrWord ocrWord in ocrLine.Words)
                heightsList.Add(ocrWord.BoundingBox.Height);

        double lineHeight = 10;

        if (heightsList.Count > 0)
            lineHeight = heightsList.Average();

        // Ideal Line Height is 40px
        const double idealLineHeight = 40.0;

        scaleFactor = idealLineHeight / lineHeight;

        if (width * scaleFactor > OcrEngine.MaxImageDimension || height * scaleFactor > OcrEngine.MaxImageDimension)
        {
            int largerDim = Math.Max(width, height);
            // find the largest possible scale factor, because the ideal scale factor is too high

            scaleFactor = OcrEngine.MaxImageDimension / largerDim;
        }

        return scaleFactor;
    }
}
