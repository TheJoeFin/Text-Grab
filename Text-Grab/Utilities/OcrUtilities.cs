using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Controls;
using Text_Grab.Extensions;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Views;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using Point = System.Windows.Point;

namespace Text_Grab.Utilities;

public static class OcrUtilities
{
    private readonly static Settings DefaultSettings = AppUtilities.TextGrabSettings;

    public static void GetTextFromOcrLine(this OcrLine ocrLine, bool isSpaceJoiningOCRLang, StringBuilder text)
    {
        // (when OCR language is zh or ja)
        // matches words in a space-joining language, which contains:
        // - one letter that is not in "other letters" (CJK characters are "other letters")
        // - one number digit
        // - any words longer than one character
        // Chinese and Japanese characters are single-character words
        // when a word is one punctuation/symbol, join it without spaces

        if (isSpaceJoiningOCRLang)
        {
            text.AppendLine(ocrLine.Text);

            if (DefaultSettings.CorrectErrors)
                text.TryFixEveryWordLetterNumberErrors();
        }
        else
        {
            bool isFirstWord = true;
            bool isPrevWordSpaceJoining = false;

            Regex regexSpaceJoiningWord = new(@"(^[\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}");

            foreach (OcrWord ocrWord in ocrLine.Words)
            {
                string wordString = ocrWord.Text;

                bool isThisWordSpaceJoining = regexSpaceJoiningWord.IsMatch(wordString);

                if (DefaultSettings.CorrectErrors)
                    wordString = wordString.TryFixNumberLetterErrors();

                if (isFirstWord || (!isThisWordSpaceJoining && !isPrevWordSpaceJoining))
                    _ = text.Append(wordString);
                else
                    _ = text.Append(' ').Append(wordString);

                isFirstWord = false;
                isPrevWordSpaceJoining = isThisWordSpaceJoining;
            }
        }

        if (DefaultSettings.CorrectToLatin)
            text.ReplaceGreekOrCyrillicWithLatin();
    }

    public static async Task<string> GetTextFromAbsoluteRectAsync(Rect rect, Language language)
    {
        Rectangle selectedRegion = rect.AsRectangle();
        Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(selectedRegion);

        return GetStringFromOcrOutputs(await GetTextFromImageAsync(bmp, language));
    }

    public static async Task<string> GetRegionsTextAsync(Window passedWindow, Rectangle selectedRegion, Language language, string languageTag = "")
    {
        Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        Rectangle correctedRegion = new(thisCorrectedLeft, thisCorrectedTop, selectedRegion.Width, selectedRegion.Height);
        Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(correctedRegion);

        return GetStringFromOcrOutputs( await GetTextFromImageAsync(bmp, language, languageTag));
    }

    public static async Task<string> GetRegionsTextAsTableAsync(Window passedWindow, Rectangle selectedRegion, Language language)
    {
        Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        Rectangle correctedRegion = new(thisCorrectedLeft, thisCorrectedTop, selectedRegion.Width, selectedRegion.Height);
        Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(correctedRegion);
        double scale = await GetIdealScaleFactorForOcrAsync(bmp, language);
        using Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bmp, scale);
        DpiScale dpiScale = VisualTreeHelper.GetDpi(passedWindow);
        OcrResult ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, language);
        List<WordBorder> wordBorders = ResultTable.ParseOcrResultIntoWordBorders(ocrResult, dpiScale);
        return ResultTable.GetWordsAsTable(wordBorders, dpiScale, language.IsSpaceJoining());
    }

    public static async Task<(OcrResult, double)> GetOcrResultFromRegionAsync(Rectangle region, Language language)
    {
        Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(region);

        double scale = await GetIdealScaleFactorForOcrAsync(bmp, language);
        using Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bmp, scale);

        OcrResult ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, language);

        return (ocrResult, scale);
    }

    public async static Task<OcrResult> GetOcrFromStreamAsync(MemoryStream memoryStream, Language language)
    {
        using WrappingStream wrapper = new(memoryStream);
        wrapper.Position = 0;
        BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(wrapper.AsRandomAccessStream());
        using SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

        await wrapper.FlushAsync();

        return await GetOcrResultFromImageAsync(softwareBmp, language);
    }

    public async static Task<OcrResult> GetOcrFromStreamAsync(IRandomAccessStream stream, Language language)
    {
        BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(stream);
        using SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

        return await GetOcrResultFromImageAsync(softwareBmp, language);
    }

    public async static Task<OcrResult> GetOcrResultFromImageAsync(BitmapImage scaledBitmap, Language language)
    {
        Bitmap bitmap = ImageMethods.BitmapImageToBitmap(scaledBitmap);
        return await GetOcrResultFromImageAsync(bitmap, language);
    }

    public async static Task<OcrResult> GetOcrResultFromImageAsync(SoftwareBitmap scaledBitmap, Language language)
    {
        OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(language);

        if (ocrEngine is null)
            ocrEngine = OcrEngine.TryCreateFromLanguage(LanguageUtilities.GetCurrentInputLanguage());

        return await ocrEngine.RecognizeAsync(scaledBitmap);
    }

    public async static Task<OcrResult> GetOcrResultFromImageAsync(Bitmap scaledBitmap, Language language)
    {
        await using MemoryStream memory = new();
        using WrappingStream wrapper = new(memory);

        scaledBitmap.Save(wrapper, ImageFormat.Bmp);
        wrapper.Position = 0;
        BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(wrapper.AsRandomAccessStream());
        using SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();
        await wrapper.FlushAsync();


        return await GetOcrResultFromImageAsync(softwareBmp, language);
    }

    public async static void GetCopyTextFromPreviousRegion()
    {
        HistoryInfo? lastFsg = Singleton<HistoryService>.Instance.GetLastFullScreenGrabInfo();

        if (lastFsg is null)
            return;

        Rect scaledRect = lastFsg.PositionRect.GetScaledUpByFraction(lastFsg.DpiScaleFactor);

        PreviousGrabWindow previousGrab = new(lastFsg.PositionRect);
        previousGrab.Show();

        string grabbedText = await GetTextFromAbsoluteRectAsync(scaledRect, lastFsg.OcrLanguage);

        HistoryInfo newPrevRegionHistory = new()
        {
            ID = Guid.NewGuid().ToString(),
            CaptureDateTime = DateTimeOffset.Now,
            ImageContent = Singleton<HistoryService>.Instance.CachedBitmap,
            TextContent = grabbedText,
            PositionRect = lastFsg.PositionRect,
            LanguageTag = lastFsg.OcrLanguage.LanguageTag,
            IsTable = lastFsg.IsTable,
            SourceMode = TextGrabMode.Fullscreen,
            DpiScaleFactor = lastFsg.DpiScaleFactor,
        };
        Singleton<HistoryService>.Instance.SaveToHistory(newPrevRegionHistory);

        OutputUtilities.HandleTextFromOcr(grabbedText, false, lastFsg.IsTable, null);
    }

    public async static Task GetTextFromPreviousFullscreenRegion(TextBox? destinationTextBox = null)
    {
        HistoryInfo? lastFsg = Singleton<HistoryService>.Instance.GetLastFullScreenGrabInfo();

        if (lastFsg is null)
            return;

        Rect scaledRect = lastFsg.PositionRect.GetScaledUpByFraction(lastFsg.DpiScaleFactor);

        PreviousGrabWindow previousGrab = new(lastFsg.PositionRect);
        previousGrab.Show();

        string grabbedText = await GetTextFromAbsoluteRectAsync(scaledRect, lastFsg.OcrLanguage);

        HistoryInfo newPrevRegionHistory = new()
        {
            ID = Guid.NewGuid().ToString(),
            CaptureDateTime = DateTimeOffset.Now,
            ImageContent = Singleton<HistoryService>.Instance.CachedBitmap,
            TextContent = grabbedText,
            PositionRect = lastFsg.PositionRect,
            LanguageTag = lastFsg.OcrLanguage.LanguageTag,
            IsTable = lastFsg.IsTable,
            SourceMode = TextGrabMode.Fullscreen,
            DpiScaleFactor = lastFsg.DpiScaleFactor,
        };
        Singleton<HistoryService>.Instance.SaveToHistory(newPrevRegionHistory);

        OutputUtilities.HandleTextFromOcr(grabbedText, false, lastFsg.IsTable, destinationTextBox);
    }

    public async static Task<List<OcrOutput>> GetTextFromRandomAccessStream(IRandomAccessStream randomAccessStream, Language language)
    {
        OcrResult ocrResult = await GetOcrFromStreamAsync(randomAccessStream, language);

        List<OcrOutput> outputs = new();

        OcrOutput paragraphsOutput = GetTextFromOcrResult(language, null, ocrResult);

        outputs.Add(paragraphsOutput);

        if (DefaultSettings.TryToReadBarcodes)
        {
            Bitmap bitmap = ImageMethods.GetBitmapFromIRandomAccessStream(randomAccessStream);
            OcrOutput barcodeResult = BarcodeUtilities.TryToReadBarcodes(bitmap);
            outputs.Add(barcodeResult);
        }

        return outputs;
    }

    public static Task<List<OcrOutput>> GetTextFromImageAsync(SoftwareBitmap softwareBitmap, Language language)
    {
        throw new NotImplementedException();

        // TODO:    scale software bitmaps
        //          Store software bitmaps on OcrOutput
        //          Read QR Codes from software bitmaps
    }

    public async static Task<List<OcrOutput>> GetTextFromImageAsync(BitmapImage bitmapImage, Language language)
    {
        Bitmap bitmap = ImageMethods.BitmapImageToBitmap(bitmapImage);
        return await GetTextFromImageAsync(bitmap, language);
    }

    public static Task<List<OcrOutput>> GetTextFromStreamAsync(MemoryStream stream, Language language)
    {
        throw new NotImplementedException();
    }

    public static Task<List<OcrOutput>> GetTextFromStreamAsync(IRandomAccessStream stream, Language language)
    {
        throw new NotImplementedException();
    }

    public async static Task<List<OcrOutput>> GetTextFromImageAsync(Bitmap bitmap, Language language, string tessTag = "")
    {
        List<OcrOutput> outputs = new();

        if (DefaultSettings.UseTesseract 
            && TesseractHelper.CanLocateTesseractExe() 
            && !string.IsNullOrEmpty(tessTag))
        {
            OcrOutput tesseractOutput = await TesseractHelper.GetOcrOutputFromBitmap(bitmap, language, tessTag);
            outputs.Add(tesseractOutput);

            if (DefaultSettings.TryToReadBarcodes)
            {
                OcrOutput barcodeResult = BarcodeUtilities.TryToReadBarcodes(bitmap);
                outputs.Add(barcodeResult);
            }

            return outputs;
        }

        double scale = await GetIdealScaleFactorForOcrAsync(bitmap, language);
        Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bitmap, scale);
        OcrResult ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(scaledBitmap, language);
        OcrOutput paragraphsOutput = GetTextFromOcrResult(language, scaledBitmap, ocrResult);
        outputs.Add(paragraphsOutput);

        if (DefaultSettings.TryToReadBarcodes)
        {
            OcrOutput barcodeResult = BarcodeUtilities.TryToReadBarcodes(scaledBitmap);
            outputs.Add(barcodeResult);
        }

        return outputs;
    }

    private static OcrOutput GetTextFromOcrResult(Language language, Bitmap? scaledBitmap, OcrResult ocrResult)
    {
        StringBuilder text = new();

        bool isSpaceJoiningOCRLang = language.IsSpaceJoining();

        foreach (OcrLine ocrLine in ocrResult.Lines)
            ocrLine.GetTextFromOcrLine(isSpaceJoiningOCRLang, text);

        if (language.IsRightToLeft())
            text.ReverseWordsForRightToLeft();

        OcrOutput paragraphsOutput = new()
        {
            Kind = OcrOutputKind.Paragraph,
            RawOutput = text.ToString(),
            Language = language,
            SourceBitmap = scaledBitmap,
        };
        return paragraphsOutput;
    }

    public static string GetStringFromOcrOutputs(List<OcrOutput> outputs)
    {
        StringBuilder text = new();

        foreach (OcrOutput output in outputs)
        {
            output.CleanOutput();

            if (!string.IsNullOrWhiteSpace(output.CleanedOutput))
                text.Append(output.CleanedOutput);
            else if (!string.IsNullOrWhiteSpace(output.RawOutput))
                text.Append(output.RawOutput);
        }

        return text.ToString();
    }

    public static async Task<string> OcrAbsoluteFilePathAsync(string absolutePath, Language? language = null, string tesseractLanguageTag = "")
    {
        Uri fileURI = new(absolutePath, UriKind.Absolute);
        FileInfo fileInfo = new(fileURI.LocalPath);
        RotateFlipType rotateFlipType = ImageMethods.GetRotateFlipType(absolutePath);
        BitmapImage droppedImage = new();
        droppedImage.BeginInit();
        droppedImage.UriSource = fileURI;
        ImageMethods.RotateImage(droppedImage, rotateFlipType);
        droppedImage.CacheOption = BitmapCacheOption.None;
        droppedImage.EndInit();
        droppedImage.Freeze();
        Bitmap bmp = ImageMethods.BitmapImageToBitmap(droppedImage);
        if (language is null)
            language = LanguageUtilities.GetCurrentInputLanguage();
        return GetStringFromOcrOutputs(await GetTextFromImageAsync(bmp, language, tesseractLanguageTag));
    }

    public static async Task<string> GetClickedWordAsync(Window passedWindow, Point clickedPoint, Language OcrLang)
    {
        using Bitmap bmp = ImageMethods.GetWindowsBoundsBitmap(passedWindow);
        string ocrText = await GetTextFromClickedWordAsync(clickedPoint, bmp, OcrLang);
        return ocrText.Trim();
    }

    private static async Task<string> GetTextFromClickedWordAsync(Point singlePoint, Bitmap bitmap, Language language)
    {
        return GetTextFromClickedWord(singlePoint, await OcrUtilities.GetOcrResultFromImageAsync(bitmap, language));
    }

    private static string GetTextFromClickedWord(Point singlePoint, OcrResult ocrResult)
    {
        Windows.Foundation.Point fPoint = new(singlePoint.X, singlePoint.Y);

        foreach (OcrLine ocrLine in ocrResult.Lines)
            foreach (OcrWord ocrWord in ocrLine.Words)
                if (ocrWord.BoundingRect.Contains(fPoint))
                    return ocrWord.Text;

        return string.Empty;
    }

    public async static Task<double> GetIdealScaleFactorForOcrAsync(SoftwareBitmap bitmap, Language selectedLanguage)
    {
        OcrResult ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(bitmap, selectedLanguage);

        return GetIdealScaleFactorForOcrResult(ocrResult, bitmap.PixelHeight, bitmap.PixelWidth);
    }

    public async static Task<double> GetIdealScaleFactorForOcrAsync(Bitmap bitmap, Language selectedLanguage)
    {
        OcrResult ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(bitmap, selectedLanguage);

        return GetIdealScaleFactorForOcrResult(ocrResult, bitmap.Height, bitmap.Width);
    }

    private static double GetIdealScaleFactorForOcrResult(OcrResult ocrResult, int height, int width)
    {
        List<double> heightsList = new();
        double scaleFactor = 1.5;

        foreach (OcrLine ocrLine in ocrResult.Lines)
            foreach (OcrWord ocrWord in ocrLine.Words)
                heightsList.Add(ocrWord.BoundingRect.Height);

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

    public static Rect GetBoundingRect(this OcrLine ocrLine)
    {
        double top = ocrLine.Words.Select(x => x.BoundingRect.Top).Min();
        double bottom = ocrLine.Words.Select(x => x.BoundingRect.Bottom).Max();
        double left = ocrLine.Words.Select(x => x.BoundingRect.Left).Min();
        double right = ocrLine.Words.Select(x => x.BoundingRect.Right).Max();

        return new()
        {
            X = left,
            Y = top,
            Width = Math.Abs(right - left),
            Height = Math.Abs(bottom - top)
        };
    }
}
