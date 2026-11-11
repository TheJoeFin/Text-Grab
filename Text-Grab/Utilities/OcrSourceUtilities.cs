using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Text_Grab.Controls;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Point = System.Windows.Point;

namespace Text_Grab.Utilities;

/// <summary>
/// OCR entry points bound to app-side image sources: screen regions, windows, BitmapSource,
/// PDFs and grab templates.
///
/// The app-coupled half of what used to be OcrUtilities (batch 4c of the Core split), split again
/// in v5 F3: engine dispatch - recognizing a GDI+ bitmap, stream or image file - moved to
/// Text-Grab.Core.Windows as <see cref="OcrEngineUtilities"/>, and this half kept the name because
/// most external references point here.
///
/// What holds it here: System.Windows.Window, Rect, Point and BitmapSource; UI Automation, which
/// is app-only (Core split B4); screen capture through ImageMethods.GetRegionOfScreenAsBitmap,
/// which caches into HistoryService; PdfDocumentRenderer (BitmapSource); GrabTemplateExecutor;
/// and the replay entry points, which show windows.
/// </summary>
public static class OcrSourceUtilities
{
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

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
        if (OcrEngineUtilities.IsUiAutomationLanguage(language))
        {
            string uiAutomationText = await UIAutomationUtilities.GetTextFromRegionAsync(rect, excludedHandles);
            if (!string.IsNullOrWhiteSpace(uiAutomationText) || !DefaultSettings.UiAutomationFallbackToOcr)
                return uiAutomationText;

            language = OcrEngineUtilities.GetCompatibleOcrLanguage(language);
        }

        Bitmap bmp = preCapturedBitmap ?? ImageMethods.GetRegionOfScreenAsBitmap(rect.AsRectangle());

        return OcrUtilities.GetStringFromOcrOutputs(await OcrEngineUtilities.GetTextFromImageAsync(bmp, language));
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
        ILanguage compatibleLanguage = OcrEngineUtilities.GetCompatibleOcrLanguage(objLang);
        Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        Rectangle correctedRegion = new(thisCorrectedLeft, thisCorrectedTop, selectedRegion.Width, selectedRegion.Height);
        using Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(correctedRegion);
        double scale = await OcrEngineUtilities.GetIdealScaleFactorForOcrAsync(bmp, compatibleLanguage);
        using Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bmp, scale);
        IOcrLinesWords ocrResult = await OcrEngineUtilities.GetOcrResultFromImageAsync(scaledBitmap, compatibleLanguage);

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

    public static async Task<string> GetTextFromBitmapSourceAsync(BitmapSource bitmapSource, ILanguage language)
    {
        using Bitmap bitmap = ImageMethods.BitmapSourceToBitmap(bitmapSource);
        return await OcrEngineUtilities.GetTextFromBitmapAsync(bitmap, language);
    }

    public static async Task<string> GetTextFromBitmapSourceAsTableAsync(BitmapSource bitmapSource, ILanguage language)
    {
        using Bitmap bitmap = ImageMethods.BitmapSourceToBitmap(bitmapSource);
        return await OcrEngineUtilities.GetTextFromBitmapAsTableAsync(bitmap, language);
    }

    /// <summary>
    /// Captures <paramref name="region"/> through the history-caching screen capture, then OCRs it
    /// exactly as <see cref="OcrEngineUtilities.GetOcrResultFromBitmapAsync"/> does - the two
    /// bodies were identical after the capture.
    /// </summary>
    public static async Task<(IOcrLinesWords?, double)> GetOcrResultFromRegionAsync(Rectangle region, ILanguage language)
    {
        using Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(region);
        return await OcrEngineUtilities.GetOcrResultFromBitmapAsync(bmp, language);
    }

    public static async void GetCopyTextFromPreviousRegion()
    {
        HistoryInfo? lastFsg = Singleton<HistoryService>.Instance.GetLastFullScreenGrabInfo();

        if (lastFsg is null)
            return;

        if (!await CanReplayPreviousFullscreenSelection(lastFsg))
            return;

        Rect scaledRect = lastFsg.PositionRect.GetScaledUpByFraction(lastFsg.DpiScaleFactor).AsRect();
        ILanguage language = lastFsg.OcrLanguage ?? LanguageUtilities.GetCurrentInputLanguage();

        // Capture the region before showing the loading indicator so the overlay itself
        // isn't baked into the region's screenshot (issue #662).
        Bitmap preCapturedBitmap = ImageMethods.GetRegionOfScreenAsBitmap(scaledRect.AsRectangle());

        PreviousGrabWindow previousGrab = new(lastFsg.PositionRect.AsRect(), PreviousGrabIndicator.Loading);
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

        Rect scaledRect = lastFsg.PositionRect.GetScaledUpByFraction(lastFsg.DpiScaleFactor).AsRect();
        ILanguage language = lastFsg.OcrLanguage ?? LanguageUtilities.GetCurrentInputLanguage();

        // Capture the region before showing the loading indicator so the overlay itself
        // isn't baked into the region's screenshot (issue #662).
        Bitmap preCapturedBitmap = ImageMethods.GetRegionOfScreenAsBitmap(scaledRect.AsRectangle());

        PreviousGrabWindow previousGrab = new(lastFsg.PositionRect.AsRect(), PreviousGrabIndicator.Loading);
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

    /// <summary>
    /// OCRs an image or PDF file. PDFs need <see cref="PdfDocumentRenderer"/>, which is app-side
    /// (its pages are BitmapSource); images go to <see cref="OcrEngineUtilities.OcrImageFileAsync"/>.
    /// </summary>
    public static async Task<string> OcrAbsoluteFilePathAsync(string absolutePath, ILanguage? language = null)
    {
        language ??= LanguageUtilities.GetCurrentInputLanguage();

        if (IoUtilities.IsPdfFileExtension(Path.GetExtension(absolutePath)))
        {
            using PdfDocumentRenderer pdfDocument = await PdfDocumentRenderer.LoadAsync(absolutePath);
            return await pdfDocument.ExtractTextAsync(language);
        }

        return await OcrEngineUtilities.OcrImageFileAsync(absolutePath, language);
    }

    public static async Task<string> GetClickedWordAsync(Window passedWindow, Point clickedPoint, ILanguage OcrLang)
    {
        if (OcrEngineUtilities.IsUiAutomationLanguage(OcrLang))
        {
            Point absoluteWindowPosition = passedWindow.GetAbsolutePosition();
            Point absoluteClickedPoint = new(absoluteWindowPosition.X + clickedPoint.X, absoluteWindowPosition.Y + clickedPoint.Y);
            string uiAutomationText = await UIAutomationUtilities.GetTextFromPointAsync(absoluteClickedPoint, GetExcludedWindowHandles(passedWindow));
            if (!string.IsNullOrWhiteSpace(uiAutomationText) || !DefaultSettings.UiAutomationFallbackToOcr)
                return uiAutomationText.Trim();

            OcrLang = OcrEngineUtilities.GetCompatibleOcrLanguage(OcrLang);
        }

        using Bitmap bmp = ImageMethods.GetWindowsBoundsBitmap(passedWindow);
        string ocrText = await GetTextFromClickedWordAsync(clickedPoint, bmp, OcrLang);
        return ocrText.Trim();
    }

    private static async Task<string> GetTextFromClickedWordAsync(Point singlePoint, Bitmap bitmap, ILanguage language)
    {
        return GetTextFromClickedWord(singlePoint, await OcrEngineUtilities.GetOcrResultFromImageAsync(bitmap, language));
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

    /// <summary>
    /// OCRs one file for "Read folder of images". Stays app-side because both of its special
    /// branches are: PDFs need <see cref="PdfDocumentRenderer"/> and templates need
    /// <see cref="GrabTemplateExecutor"/>. A plain image goes through
    /// <see cref="OcrAbsoluteFilePathAsync"/> to <see cref="OcrEngineUtilities"/>.
    /// </summary>
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
                    using Bitmap bmp = OcrEngineUtilities.LoadBitmapFromFile(path);
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
