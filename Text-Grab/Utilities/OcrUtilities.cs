using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

public static partial class OcrUtilities
{
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    public static void GetTextFromOcrLine(this IOcrLine ocrLine, bool isSpaceJoiningOCRLang, StringBuilder text)
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

            Regex regexSpaceJoiningWord = SpaceJoiningWordRegex();

            foreach (IOcrWord ocrWord in ocrLine.Words)
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

    public static async Task<string> GetTextFromAbsoluteRectAsync(Rect rect, ILanguage language)
    {
        Rectangle selectedRegion = rect.AsRectangle();
        Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(selectedRegion);

        return GetStringFromOcrOutputs(await GetTextFromImageAsync(bmp, language));
    }

    public static async Task<string> GetRegionsTextAsync(Window passedWindow, Rectangle selectedRegion, ILanguage language)
    {
        Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        Rectangle correctedRegion = new(thisCorrectedLeft, thisCorrectedTop, selectedRegion.Width, selectedRegion.Height);
        Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(correctedRegion);

        return GetStringFromOcrOutputs(await GetTextFromImageAsync(bmp, language));
    }

    public static async Task<string> GetRegionsTextAsTableAsync(Window passedWindow, Rectangle selectedRegion, ILanguage objLang)
    {
        Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        Rectangle correctedRegion = new(thisCorrectedLeft, thisCorrectedTop, selectedRegion.Width, selectedRegion.Height);
        Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(correctedRegion);
        double scale = await GetIdealScaleFactorForOcrAsync(bmp, objLang);
        using Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bmp, scale);
        DpiScale dpiScale = VisualTreeHelper.GetDpi(passedWindow);
        IOcrLinesWords ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, objLang);

        // New model-only flow
        List<WordBorderInfo> wordBorderInfos = ResultTable.ParseOcrResultIntoWordBorderInfos(ocrResult, dpiScale);

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
        ResultTable.GetTextFromTabledWordBorders(sb, wordBorderInfos, objLang.IsSpaceJoining());
        return sb.ToString();
    }

    public static async Task<(IOcrLinesWords?, double)> GetOcrResultFromRegionAsync(Rectangle region, ILanguage language)
    {
        Bitmap bmp = ImageMethods.GetRegionOfScreenAsBitmap(region);

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

    public static async Task<IOcrLinesWords> GetOcrResultFromImageAsync(SoftwareBitmap scaledBitmap, ILanguage language)
    {
        if (language is WindowsAiLang winAiLang)
        {
            return new WinAiOcrLinesWords(await WindowsAiUtilities.GetOcrResultAsync(scaledBitmap));
        }

        if (language is not GlobalLang globalLang)
            globalLang = new GlobalLang(language.LanguageTag);

        OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(globalLang.OriginalLanguage);

        ocrEngine ??= OcrEngine.TryCreateFromLanguage(LanguageUtilities.GetCurrentInputLanguage().AsLanguage() ?? new Language("en-US"));

        return new WinRtOcrLinesWords(await ocrEngine.RecognizeAsync(scaledBitmap));
    }

    public static async Task<IOcrLinesWords> GetOcrResultFromImageAsync(Bitmap scaledBitmap, ILanguage language)
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

    public static async void GetCopyTextFromPreviousRegion()
    {
        HistoryInfo? lastFsg = Singleton<HistoryService>.Instance.GetLastFullScreenGrabInfo();

        if (lastFsg is null)
            return;

        Rect scaledRect = lastFsg.PositionRect.GetScaledUpByFraction(lastFsg.DpiScaleFactor);

        PreviousGrabWindow previousGrab = new(lastFsg.PositionRect);
        previousGrab.Show();

        ILanguage language = lastFsg.OcrLanguage ?? LanguageUtilities.GetCurrentInputLanguage();
        string grabbedText = await GetTextFromAbsoluteRectAsync(scaledRect, language);

        HistoryInfo newPrevRegionHistory = new()
        {
            ID = Guid.NewGuid().ToString(),
            CaptureDateTime = DateTimeOffset.Now,
            ImageContent = Singleton<HistoryService>.Instance.CachedBitmap,
            TextContent = grabbedText,
            PositionRect = lastFsg.PositionRect,
            LanguageTag = language.LanguageTag,
            LanguageKind = LanguageUtilities.GetLanguageKind(language),
            IsTable = lastFsg.IsTable,
            SourceMode = TextGrabMode.Fullscreen,
            DpiScaleFactor = lastFsg.DpiScaleFactor,
        };
        Singleton<HistoryService>.Instance.SaveToHistory(newPrevRegionHistory);

        OutputUtilities.HandleTextFromOcr(grabbedText, false, lastFsg.IsTable, null);
    }

    public static async Task GetTextFromPreviousFullscreenRegion(TextBox? destinationTextBox = null)
    {
        HistoryInfo? lastFsg = Singleton<HistoryService>.Instance.GetLastFullScreenGrabInfo();

        if (lastFsg is null)
            return;

        Rect scaledRect = lastFsg.PositionRect.GetScaledUpByFraction(lastFsg.DpiScaleFactor);

        PreviousGrabWindow previousGrab = new(lastFsg.PositionRect);
        previousGrab.Show();

        ILanguage language = lastFsg.OcrLanguage ?? LanguageUtilities.GetCurrentInputLanguage();
        string grabbedText = await GetTextFromAbsoluteRectAsync(scaledRect, language);

        HistoryInfo newPrevRegionHistory = new()
        {
            ID = Guid.NewGuid().ToString(),
            CaptureDateTime = DateTimeOffset.Now,
            ImageContent = Singleton<HistoryService>.Instance.CachedBitmap,
            TextContent = grabbedText,
            PositionRect = lastFsg.PositionRect,
            LanguageTag = language.LanguageTag,
            LanguageKind = LanguageUtilities.GetLanguageKind(language),
            IsTable = lastFsg.IsTable,
            SourceMode = TextGrabMode.Fullscreen,
            DpiScaleFactor = lastFsg.DpiScaleFactor,
        };
        Singleton<HistoryService>.Instance.SaveToHistory(newPrevRegionHistory);

        OutputUtilities.HandleTextFromOcr(grabbedText, false, lastFsg.IsTable, destinationTextBox);
    }

    public static async Task<List<OcrOutput>> GetTextFromRandomAccessStream(IRandomAccessStream randomAccessStream, ILanguage language)
    {
        Bitmap bitmap = ImageMethods.GetBitmapFromIRandomAccessStream(randomAccessStream);
        List<OcrOutput> outputs = await GetTextFromImageAsync(bitmap, language);

        if (DefaultSettings.TryToReadBarcodes)
        {
            OcrOutput barcodeResult = BarcodeUtilities.TryToReadBarcodes(bitmap);
            outputs.Add(barcodeResult);
        }

        return outputs;
    }

    public static async Task<List<OcrOutput>> GetTextFromWinAiAsync(Bitmap bitmap, WindowsAiLang language)
    {
        // get temp path
        string tempPath = Path.GetTempPath();
        string tempFileName = Path.GetRandomFileName() + ".bmp";
        string tempFilePath = Path.Combine(tempPath, tempFileName);
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

    public static async Task<List<OcrOutput>> GetTextFromImageAsync(Bitmap bitmap, ILanguage language)
    {
        List<OcrOutput> outputs = [];

        if (language is TessLang tessLang)
        {
            OcrOutput tesseractOutput = await TesseractHelper.GetOcrOutputFromBitmap(bitmap, tessLang);
            outputs.Add(tesseractOutput);
        }
        else if (language is WindowsAiLang winAiLang)
        {
            outputs.AddRange(await GetTextFromWinAiAsync(bitmap, winAiLang));
        }
        else
        {
            GlobalLang ocrLanguageFromILang = language as GlobalLang ?? new GlobalLang("en-US");
            double scale = await GetIdealScaleFactorForOcrAsync(bitmap, ocrLanguageFromILang);
            Bitmap scaledBitmap = ImageMethods.ScaleBitmapUniform(bitmap, scale);
            IOcrLinesWords ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(scaledBitmap, ocrLanguageFromILang);
            OcrOutput paragraphsOutput = GetTextFromOcrResult(ocrLanguageFromILang, scaledBitmap, ocrResult);
            outputs.Add(paragraphsOutput);
        }

        if (DefaultSettings.TryToReadBarcodes)
        {
            OcrOutput barcodeResult = BarcodeUtilities.TryToReadBarcodes(bitmap);
            outputs.Add(barcodeResult);
        }

        return outputs;
    }

    private static OcrOutput GetTextFromOcrResult(ILanguage language, Bitmap? scaledBitmap, IOcrLinesWords ocrResult)
    {
        StringBuilder text = new();

        bool isSpaceJoiningOCRLang = language.IsSpaceJoining();

        foreach (IOcrLine ocrLine in ocrResult.Lines)
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

    public static async Task<string> OcrAbsoluteFilePathAsync(string absolutePath, ILanguage? language = null)
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
        language ??= LanguageUtilities.GetCurrentInputLanguage();
        return GetStringFromOcrOutputs(await GetTextFromImageAsync(bmp, language));
    }

    public static async Task<string> GetClickedWordAsync(Window passedWindow, Point clickedPoint, ILanguage OcrLang)
    {
        using Bitmap bmp = ImageMethods.GetWindowsBoundsBitmap(passedWindow);
        string ocrText = await GetTextFromClickedWordAsync(clickedPoint, bmp, OcrLang);
        return ocrText.Trim();
    }

    private static async Task<string> GetTextFromClickedWordAsync(Point singlePoint, Bitmap bitmap, ILanguage language)
    {
        return GetTextFromClickedWord(singlePoint, await OcrUtilities.GetOcrResultFromImageAsync(bitmap, language));
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
        IOcrLinesWords ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(bitmap, selectedLanguage);
        return GetIdealScaleFactorForOcrResult(ocrResult, bitmap.Height, bitmap.Width);
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

        public static async Task<string> OcrFile(string path, ILanguage? selectedLanguage, OcrDirectoryOptions options)
        {
            StringBuilder returnString = new();
            if (options.OutputFileNames)
                returnString.AppendLine(Path.GetFileName(path));
            try
            {
                string ocrText = await OcrAbsoluteFilePathAsync(path, selectedLanguage);

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

        [GeneratedRegex(@"(^[\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}")]
        private static partial Regex SpaceJoiningWordRegex();
    }
