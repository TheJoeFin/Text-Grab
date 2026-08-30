using Microsoft.Windows.AI.Imaging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Controls;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using Point = System.Windows.Point;

namespace Text_Grab.Utilities;

/// <summary>
/// OCR entry points bound to app-side image sources: screen regions, windows, BitmapSource,
/// files and streams - plus engine dispatch.
///
/// The app-coupled half of what used to be OcrUtilities (batch 4c of the Core split). The text
/// assembly it feeds - line and word joining, furigana filtering, paragraph grouping - moved to
/// Text-Grab.Core.Windows keeping the OcrUtilities name, so the call sites there resolve
/// unchanged; this half took the new name instead.
///
/// What holds it here: System.Windows.Window and BitmapSource throughout, and engine dispatch
/// via WindowsAiUtilities and LanguageUtilities, neither of which has moved (WindowsAiUtilities
/// is blocked on SoftwareBitmapExtensions in wave 5a). LoadBitmapFromFile stays deferred on its
/// own account - it builds a WPF BitmapImage to apply EXIF rotation, and decoupling it means a
/// GDI+/WIC rewrite, which takes OcrAbsoluteFilePathAsync and OcrFile with it.
/// </summary>
public static class OcrSourceUtilities
{
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    private static bool IsUiAutomationLanguage(ILanguage language) => language is UiAutomationLang;
    private static bool IsWindowsAiDescriptionLanguage(ILanguage language) => language is WindowsAiDescriptionLang;

    private static ILanguage GetCompatibleOcrLanguage(ILanguage language)
    {
        if (language is UiAutomationLang)
            return CaptureLanguageUtilities.GetUiAutomationFallbackLanguage();

        return language;
    }

    private static IReadOnlyCollection<IntPtr>? GetExcludedWindowHandles(Window passedWindow)
    {
        IntPtr handle = new System.Windows.Interop.WindowInteropHelper(passedWindow).Handle;
        return handle == IntPtr.Zero ? null : [handle];
    }

    public static async Task<string> GetTextFromAbsoluteRectAsync(
        Rect rect,
        ILanguage language,
        IReadOnlyCollection<IntPtr>? excludedHandles = null,
        Bitmap? preCapturedBitmap = null)
    {
        if (IsUiAutomationLanguage(language))
        {
            string uiAutomationText = await UIAutomationUtilities.GetTextFromRegionAsync(rect, excludedHandles);
            if (!string.IsNullOrWhiteSpace(uiAutomationText) || !DefaultSettings.UiAutomationFallbackToOcr)
                return uiAutomationText;

            language = GetCompatibleOcrLanguage(language);
        }

        Bitmap bmp = preCapturedBitmap ?? ImageMethods.GetRegionOfScreenAsBitmap(rect.AsRectangle());

        return OcrUtilities.GetStringFromOcrOutputs(await GetTextFromImageAsync(bmp, language));
    }

    public static async Task<string> GetRegionsTextAsync(Window passedWindow, Rectangle selectedRegion, ILanguage language)
    {
        Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        Rectangle correctedRegion = new(thisCorrectedLeft, thisCorrectedTop, selectedRegion.Width, selectedRegion.Height);
        return await GetTextFromAbsoluteRectAsync(correctedRegion.AsRect(), language, GetExcludedWindowHandles(passedWindow));
    }

    public static async Task<string> GetRegionsTextAsTableAsync(Window passedWindow, Rectangle selectedRegion, ILanguage objLang)
    {
        ILanguage compatibleLanguage = GetCompatibleOcrLanguage(objLang);
        Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        Rectangle correctedRegion = new(thisCorrectedLeft, thisCorrectedTop, selectedRegion.Width, selectedRegion.Height);
        using Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(correctedRegion);
        double scale = await GetIdealScaleFactorForOcrAsync(bmp, compatibleLanguage);
        using Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bmp, scale);
        IOcrLinesWords ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, compatibleLanguage);

        // New model-only flow
        List<WordBorderInfo> wordBorderInfos = OcrUtilities.ParseOcrResultIntoWordBorderInfos(
            ocrResult,
            compatibleLanguage.IsLatinBased());

        Rectangle rectCanvasSize = new()
        {
            Width = scaledBitmap.Width,
            Height = scaledBitmap.Height,
            X = 0,
            Y = 0
        };

        ResultTable table = new();
        table.AnalyzeAsTable(wordBorderInfos, rectCanvasSize);

        StringBuilder sb = new();
        ResultTable.GetTextFromTabledWordBorders(sb, wordBorderInfos, compatibleLanguage.IsSpaceJoining());
        return sb.ToString();
    }

    public static async Task<string> GetTextFromBitmapAsync(Bitmap bitmap, ILanguage language)
    {
        if (IsUiAutomationLanguage(language))
        {
            if (!DefaultSettings.UiAutomationFallbackToOcr)
                return string.Empty;

            language = GetCompatibleOcrLanguage(language);
        }

        return OcrUtilities.GetStringFromOcrOutputs(await GetTextFromImageAsync(bitmap, language));
    }

    public static async Task<string> GetTextFromBitmapSourceAsync(BitmapSource bitmapSource, ILanguage language)
    {
        using Bitmap bitmap = ImageMethods.BitmapSourceToBitmap(bitmapSource);
        return await GetTextFromBitmapAsync(bitmap, language);
    }

    public static async Task<string> GetTextFromBitmapAsTableAsync(Bitmap bitmap, ILanguage language)
    {
        ILanguage compatibleLanguage = GetCompatibleOcrLanguage(language);
        double scale = await GetIdealScaleFactorForOcrAsync(bitmap, compatibleLanguage);
        using Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bitmap, scale);
        IOcrLinesWords ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, compatibleLanguage);

        List<WordBorderInfo> wordBorderInfos = OcrUtilities.ParseOcrResultIntoWordBorderInfos(
            ocrResult,
            compatibleLanguage.IsLatinBased());

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

    public static async Task<string> GetTextFromBitmapSourceAsTableAsync(BitmapSource bitmapSource, ILanguage language)
    {
        using Bitmap bitmap = ImageMethods.BitmapSourceToBitmap(bitmapSource);
        return await GetTextFromBitmapAsTableAsync(bitmap, language);
    }

    public static async Task<(IOcrLinesWords?, double)> GetOcrResultFromRegionAsync(Rectangle region, ILanguage language)
    {
        language = GetCompatibleOcrLanguage(language);
        using Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(region);

        if (IsWindowsAiDescriptionLanguage(language))
            return (await GetOcrResultFromImageAsync(bmp, language), 1.0);

        if (language is WindowsAiLang)
        {
            return (await WindowsAiUtilities.GetOcrResultAsync(bmp), 1.0);
        }

        if (language is not GlobalLang globalLang)
            globalLang = new GlobalLang(language.LanguageTag);

        double scale = await GetIdealScaleFactorForOcrAsync(bmp, language);
        using Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bmp, scale);

        IOcrLinesWords ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, globalLang);

        return (ocrResult, scale);

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
        using Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bmp, scale);
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

    public static async void GetCopyTextFromPreviousRegion()
    {
        HistoryInfo? lastFsg = Singleton<HistoryService>.Instance.GetLastFullScreenGrabInfo();

        if (lastFsg is null)
            return;

        if (!await CanReplayPreviousFullscreenSelection(lastFsg))
            return;

        Rect scaledRect = lastFsg.PositionRect.GetScaledUpByFraction(lastFsg.DpiScaleFactor);
        ILanguage language = lastFsg.OcrLanguage ?? LanguageUtilities.GetCurrentInputLanguage();

        // Capture the region before showing the loading indicator so the overlay itself
        // isn't baked into the region's screenshot (issue #662).
        Bitmap preCapturedBitmap = ImageMethods.GetRegionOfScreenAsBitmap(scaledRect.AsRectangle());

        PreviousGrabWindow previousGrab = new(lastFsg.PositionRect, PreviousGrabIndicator.Loading);
        previousGrab.Show();

        try
        {
            string grabbedText = await GetTextFromAbsoluteRectAsync(scaledRect, language, preCapturedBitmap: preCapturedBitmap);
            (string languageTag, LanguageKind languageKind, bool usedUiAutomation) =
                LanguageUtilities.GetPersistedLanguageIdentity(language);

            HistoryInfo newPrevRegionHistory = new()
            {
                ID = Guid.NewGuid().ToString(),
                CaptureDateTime = DateTimeOffset.Now,
                ImageContent = Singleton<HistoryService>.Instance.CachedBitmap,
                TextContent = grabbedText,
                PositionRect = lastFsg.PositionRect,
                LanguageTag = languageTag,
                LanguageKind = languageKind,
                UsedUiAutomation = usedUiAutomation,
                IsTable = lastFsg.IsTable,
                SourceMode = TextGrabMode.Fullscreen,
                DpiScaleFactor = lastFsg.DpiScaleFactor,
            };
            Singleton<HistoryService>.Instance.SaveToHistory(newPrevRegionHistory);

            OutputUtilities.HandleTextFromOcr(grabbedText, false, lastFsg.IsTable, null);
            previousGrab.ShowSuccess();
        }
        catch
        {
            previousGrab.Close();
            throw;
        }
    }

    public static async Task GetTextFromPreviousFullscreenRegion(TextBox? destinationTextBox = null)
    {
        HistoryInfo? lastFsg = Singleton<HistoryService>.Instance.GetLastFullScreenGrabInfo();

        if (lastFsg is null)
            return;

        if (!await CanReplayPreviousFullscreenSelection(lastFsg))
            return;

        Rect scaledRect = lastFsg.PositionRect.GetScaledUpByFraction(lastFsg.DpiScaleFactor);
        ILanguage language = lastFsg.OcrLanguage ?? LanguageUtilities.GetCurrentInputLanguage();

        // Capture the region before showing the loading indicator so the overlay itself
        // isn't baked into the region's screenshot (issue #662).
        Bitmap preCapturedBitmap = ImageMethods.GetRegionOfScreenAsBitmap(scaledRect.AsRectangle());

        PreviousGrabWindow previousGrab = new(lastFsg.PositionRect, PreviousGrabIndicator.Loading);
        previousGrab.Show();

        try
        {
            string grabbedText = await GetTextFromAbsoluteRectAsync(scaledRect, language, preCapturedBitmap: preCapturedBitmap);
            (string languageTag, LanguageKind languageKind, bool usedUiAutomation) =
                LanguageUtilities.GetPersistedLanguageIdentity(language);

            HistoryInfo newPrevRegionHistory = new()
            {
                ID = Guid.NewGuid().ToString(),
                CaptureDateTime = DateTimeOffset.Now,
                ImageContent = Singleton<HistoryService>.Instance.CachedBitmap,
                TextContent = grabbedText,
                PositionRect = lastFsg.PositionRect,
                LanguageTag = languageTag,
                LanguageKind = languageKind,
                UsedUiAutomation = usedUiAutomation,
                IsTable = lastFsg.IsTable,
                SourceMode = TextGrabMode.Fullscreen,
                DpiScaleFactor = lastFsg.DpiScaleFactor,
            };
            Singleton<HistoryService>.Instance.SaveToHistory(newPrevRegionHistory);

            OutputUtilities.HandleTextFromOcr(grabbedText, false, lastFsg.IsTable, destinationTextBox);
            previousGrab.ShowSuccess();
        }
        catch
        {
            previousGrab.Close();
            throw;
        }
    }

    public static async Task<List<OcrOutput>> GetTextFromRandomAccessStream(IRandomAccessStream randomAccessStream, ILanguage language)
    {
        Bitmap bitmap = BitmapUtilities.GetBitmapFromIRandomAccessStream(randomAccessStream);
        List<OcrOutput> outputs = await GetTextFromImageAsync(bitmap, language);
        return outputs;
    }

    public static async Task<List<OcrOutput>> GetTextFromWinAiAsync(Bitmap bitmap, WindowsAiLang language)
    {
        if (OcrUtilities.ShouldUseParagraphDetection(language.IsSpaceJoining()))
        {
            WinAiOcrLinesWords? ocrResult = await WindowsAiUtilities.GetOcrResultAsync(bitmap);
            if (ocrResult is not null)
                return [GetTextFromOcrResult(language, bitmap, ocrResult)];
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

    public static async Task<List<OcrOutput>> GetTextFromWinAiDescriptionAsync(Bitmap bitmap, WindowsAiDescriptionLang language)
    {
        IOcrLinesWords descriptionResult = await GetOcrResultFromImageAsync(bitmap, language);
        return [GetTextFromOcrResult(language, bitmap, descriptionResult)];
    }

    public static async Task<List<OcrOutput>> GetTextFromImageAsync(Bitmap bitmap, ILanguage language)
    {
        List<OcrOutput> outputs = [];

        if (IsUiAutomationLanguage(language))
        {
            if (!DefaultSettings.UiAutomationFallbackToOcr)
                return outputs;

            language = GetCompatibleOcrLanguage(language);
        }

        if (language is TessLang tessLang)
        {
            OcrOutput tesseractOutput = await TesseractHelper.GetOcrOutputFromBitmap(bitmap, tessLang);
            outputs.Add(tesseractOutput);
        }
        else if (language is WindowsAiLang winAiLang)
        {
            outputs.AddRange(await GetTextFromWinAiAsync(bitmap, winAiLang));
        }
        else if (language is WindowsAiDescriptionLang windowsAiDescriptionLang)
        {
            outputs.AddRange(await GetTextFromWinAiDescriptionAsync(bitmap, windowsAiDescriptionLang));
        }
        else
        {
            GlobalLang ocrLanguageFromILang = language as GlobalLang ?? new GlobalLang("en-US");
            double scale = await GetIdealScaleFactorForOcrAsync(bitmap, ocrLanguageFromILang);
            using Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bitmap, scale);
            IOcrLinesWords ocrResult = await OcrSourceUtilities.GetOcrResultFromImageAsync(scaledBitmap, ocrLanguageFromILang);
            OcrOutput paragraphsOutput = GetTextFromOcrResult(ocrLanguageFromILang, new Bitmap(scaledBitmap), ocrResult);
            outputs.Add(paragraphsOutput);
        }

        if (DefaultSettings.TryToReadBarcodes)
            outputs.AddRange(BarcodeUtilities.TryToReadBarcodes(bitmap));

        return outputs;
    }

    private static OcrOutput GetTextFromOcrResult(ILanguage language, Bitmap? scaledBitmap, IOcrLinesWords ocrResult)
    {
        OcrOutput paragraphsOutput = new()
        {
            Kind = OcrOutputKind.Paragraph,
            RawOutput = OcrUtilities.BuildTextFromOcrLines(language, ocrResult),
            Language = language,
            SourceBitmap = scaledBitmap,
        };
        return paragraphsOutput;
    }

    public static async Task<string> OcrAbsoluteFilePathAsync(string absolutePath, ILanguage? language = null)
    {
        language ??= LanguageUtilities.GetCurrentInputLanguage();

        if (IoUtilities.IsPdfFileExtension(Path.GetExtension(absolutePath)))
        {
            using PdfDocumentRenderer pdfDocument = await PdfDocumentRenderer.LoadAsync(absolutePath);
            return await pdfDocument.ExtractTextAsync(language);
        }

        using Bitmap bmp = LoadBitmapFromFile(absolutePath);
        return OcrUtilities.GetStringFromOcrOutputs(await GetTextFromImageAsync(bmp, language));
    }

    private static Bitmap LoadBitmapFromFile(string absolutePath)
    {
        Uri fileURI = new(absolutePath, UriKind.Absolute);
        RotateFlipType rotateFlipType = BitmapUtilities.GetRotateFlipType(absolutePath);
        BitmapImage droppedImage = new();
        droppedImage.BeginInit();
        droppedImage.UriSource = fileURI;
        ImageMethods.RotateImage(droppedImage, rotateFlipType);
        droppedImage.CacheOption = BitmapCacheOption.None;
        droppedImage.EndInit();
        droppedImage.Freeze();
        return ImageMethods.BitmapImageToBitmap(droppedImage);
    }

    public static async Task<string> GetClickedWordAsync(Window passedWindow, Point clickedPoint, ILanguage OcrLang)
    {
        if (IsUiAutomationLanguage(OcrLang))
        {
            Point absoluteWindowPosition = passedWindow.GetAbsolutePosition();
            Point absoluteClickedPoint = new(absoluteWindowPosition.X + clickedPoint.X, absoluteWindowPosition.Y + clickedPoint.Y);
            string uiAutomationText = await UIAutomationUtilities.GetTextFromPointAsync(absoluteClickedPoint, GetExcludedWindowHandles(passedWindow));
            if (!string.IsNullOrWhiteSpace(uiAutomationText) || !DefaultSettings.UiAutomationFallbackToOcr)
                return uiAutomationText.Trim();

            OcrLang = GetCompatibleOcrLanguage(OcrLang);
        }

        using Bitmap bmp = ImageMethods.GetWindowsBoundsBitmap(passedWindow);
        string ocrText = await GetTextFromClickedWordAsync(clickedPoint, bmp, OcrLang);
        return ocrText.Trim();
    }

    private static async Task<string> GetTextFromClickedWordAsync(Point singlePoint, Bitmap bitmap, ILanguage language)
    {
        return GetTextFromClickedWord(singlePoint, await OcrSourceUtilities.GetOcrResultFromImageAsync(bitmap, language));
    }

    private static string GetTextFromClickedWord(Point singlePoint, IOcrLinesWords ocrResult)
    {
        Windows.Foundation.Point fPoint = new(singlePoint.X, singlePoint.Y);

        foreach (IOcrLine ocrLine in ocrResult.Lines)
            foreach (IOcrWord ocrWord in ocrLine.Words)
                if (ocrWord.BoundingBox.Contains(fPoint))
                    return ocrWord.Text;

        return string.Empty;
    }

    public static async Task<double> GetIdealScaleFactorForOcrAsync(Bitmap bitmap, ILanguage selectedLanguage)
    {
        if (IsWindowsAiDescriptionLanguage(selectedLanguage))
            return 1.0;

        selectedLanguage = GetCompatibleOcrLanguage(selectedLanguage);
        IOcrLinesWords ocrResult = await OcrSourceUtilities.GetOcrResultFromImageAsync(bitmap, selectedLanguage);
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

    public static async Task<string> OcrFile(string path, ILanguage? selectedLanguage, OcrDirectoryOptions options)
    {
        StringBuilder returnString = new();
        if (options.OutputFileNames)
            returnString.AppendLine(Path.GetFileName(path));
        try
        {
            string ocrText;
            if (options.GrabTemplate is GrabTemplate grabTemplate)
            {
                if (IoUtilities.IsPdfFileExtension(Path.GetExtension(path)))
                {
                    using PdfDocumentRenderer pdfDocument = await PdfDocumentRenderer.LoadAsync(path);
                    ocrText = await pdfDocument.ExtractTextAsync(selectedLanguage, grabTemplate);
                }
                else
                {
                    using Bitmap bmp = LoadBitmapFromFile(path);
                    ocrText = await GrabTemplateExecutor.ExecuteTemplateOnBitmapAsync(grabTemplate, bmp, selectedLanguage);
                }
            }
            else
                ocrText = await OcrAbsoluteFilePathAsync(path, selectedLanguage);

            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                returnString.AppendLine(ocrText);

                if (options.WriteTxtFiles && Path.GetDirectoryName(path) is string dir)
                {
                    using StreamWriter outputFile = new(Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(path)}.txt"));
                    outputFile.WriteLine(ocrText);
                }
            }
            else
                returnString.AppendLine($"----- No Text Extracted{Environment.NewLine}");

        }
        catch (Exception ex)
        {
            returnString.AppendLine($"Failed to read {path}: {ex.Message}{Environment.NewLine}");
        }

        return returnString.ToString();
    }

    private static async Task<bool> CanReplayPreviousFullscreenSelection(HistoryInfo history)
    {
        if (history.SelectionStyle is FsgSelectionStyle.Region or FsgSelectionStyle.AdjustAfter)
            return true;

        await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Text Grab",
            Content = "Repeat previous fullscreen capture is currently available only for Region and Adjust After selections.",
            CloseButtonText = "OK"
        }.ShowDialogAsync();
        return false;
    }
}
