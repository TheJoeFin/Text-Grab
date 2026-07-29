using Microsoft.Windows.AI.Imaging;
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

    // Cache the SpaceJoiningWordRegex to avoid creating it on every method call
    private static readonly Regex _cachedSpaceJoiningWordRegex = SpaceJoiningWordRegex();

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
            // For CJK languages, filter out likely furigana (small ruby-text
            // characters above the main text) before merging the words. This is
            // opt-in via the RemoveFurigana setting.
            IEnumerable<IOcrWord> words = DefaultSettings.RemoveFurigana
                ? FilterFurigana([.. ocrLine.Words])
                : ocrLine.Words;

            bool isFirstWord = true;
            bool isPrevWordSpaceJoining = false;

            foreach (IOcrWord ocrWord in words)
            {
                string wordString = ocrWord.Text;

                bool isThisWordSpaceJoining = _cachedSpaceJoiningWordRegex.IsMatch(wordString);

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

    /// <summary>
    /// Removes words that are likely furigana: small ruby-text characters
    /// rendered above the main text in Japanese. A word is treated as furigana
    /// when it is noticeably shorter than the line's median word height and sits
    /// directly above a larger word that overlaps it horizontally.
    /// </summary>
    internal static List<IOcrWord> FilterFurigana(List<IOcrWord> words)
    {
        if (words.Count == 0)
            return words;

        // Furigana is typically around half the height of the main text.
        List<double> heights = [.. words.Select(w => w.BoundingBox.Height).OrderBy(h => h)];
        double medianHeight = heights[heights.Count / 2];
        double furiganaThreshold = medianHeight * 0.6;

        List<IOcrWord> filteredWords = [];

        for (int i = 0; i < words.Count; i++)
        {
            IOcrWord word = words[i];
            bool isProbablyFurigana = false;

            if (word.BoundingBox.Height < furiganaThreshold)
            {
                // Only treat it as furigana when a larger word sits below it and
                // overlaps horizontally (i.e. the kanji it annotates).
                for (int j = 0; j < words.Count; j++)
                {
                    if (i == j)
                        continue;

                    IOcrWord otherWord = words[j];

                    bool isBelow = otherWord.BoundingBox.Top > word.BoundingBox.Bottom;
                    bool overlapsHorizontally = !(otherWord.BoundingBox.Right < word.BoundingBox.Left
                        || otherWord.BoundingBox.Left > word.BoundingBox.Right);
                    bool isLarger = otherWord.BoundingBox.Height > furiganaThreshold;

                    if (isBelow && overlapsHorizontally && isLarger)
                    {
                        isProbablyFurigana = word.Text.Length <= 2;
                        break;
                    }
                }
            }

            if (!isProbablyFurigana)
                filteredWords.Add(word);
        }

        // If everything was filtered, fall back to the original words to avoid
        // dropping the whole line.
        return filteredWords.Count > 0 ? filteredWords : words;
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

        return GetStringFromOcrOutputs(await GetTextFromImageAsync(bmp, language));
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
        DpiScale dpiScale = VisualTreeHelper.GetDpi(passedWindow);
        IOcrLinesWords ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, compatibleLanguage);

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

        return GetStringFromOcrOutputs(await GetTextFromImageAsync(bitmap, language));
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
        DpiScale bitmapDpiScale = new(1.0, 1.0);

        List<WordBorderInfo> wordBorderInfos = ResultTable.ParseOcrResultIntoWordBorderInfos(ocrResult, bitmapDpiScale);

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
        Bitmap bitmap = ImageMethods.GetBitmapFromIRandomAccessStream(randomAccessStream);
        List<OcrOutput> outputs = await GetTextFromImageAsync(bitmap, language);
        return outputs;
    }

    public static async Task<List<OcrOutput>> GetTextFromWinAiAsync(Bitmap bitmap, WindowsAiLang language)
    {
        if (ShouldUseParagraphDetection(language.IsSpaceJoining()))
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
            IOcrLinesWords ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(scaledBitmap, ocrLanguageFromILang);
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
            RawOutput = BuildTextFromOcrLines(language, ocrResult),
            Language = language,
            SourceBitmap = scaledBitmap,
        };
        return paragraphsOutput;
    }

    internal readonly record struct PositionedOcrLine(int LineNumber, string Text, Windows.Foundation.Rect BoundingBox);

    internal sealed class GroupedOcrLines(IReadOnlyList<PositionedOcrLine> lines, Windows.Foundation.Rect boundingBox)
    {
        public Windows.Foundation.Rect BoundingBox { get; } = boundingBox;

        public IReadOnlyList<PositionedOcrLine> Lines { get; } = lines;

        public int StartingLineNumber => Lines.Count == 0 ? 0 : Lines[0].LineNumber;

        public string DisplayText => string.Join(Environment.NewLine, Lines.Select(static line => line.Text.MakeStringSingleLine()));

        public string SingleLineText => string.Join(" ", Lines.Select(static line => line.Text.MakeStringSingleLine()).Where(static text => !string.IsNullOrWhiteSpace(text)));
    }

    internal static string BuildTextFromOcrLines(ILanguage language, IOcrLinesWords ocrResult)
    {
        StringBuilder text = new();

        bool isSpaceJoiningOCRLang = language.IsSpaceJoining();
        IOcrLine[] lines = ocrResult.Lines;

        if (ShouldUseParagraphDetection(isSpaceJoiningOCRLang) && lines.Length > 0)
        {
            List<GroupedOcrLines> groupedLines =
            [
                .. GroupWrappedParagraphLines(
                    [.. lines.Select((line, index) => new PositionedOcrLine(index, line.Text, line.BoundingBox))])
            ];

            for (int i = 0; i < groupedLines.Count; i++)
            {
                if (i > 0)
                    text.AppendLine();

                text.Append(groupedLines[i].SingleLineText);
            }
        }
        else
        {
            // Windows OCR returns CJK lines - especially furigana ruby lines and
            // stray fragments - in an order that does not follow the page's reading
            // flow, so re-sort by geometry (top-to-bottom, then left-to-right)
            // before joining. Space-joining languages keep the engine order because
            // paragraph detection above already handles their layout.
            IReadOnlyList<IOcrLine> orderedLines = isSpaceJoiningOCRLang
                ? lines
                : OrderLinesForReadingFlow(lines);

            // Windows OCR emits furigana (Japanese ruby readings) as their own
            // short lines sitting directly above the kanji they annotate, so the
            // word-level filter above never catches them. Drop those whole lines
            // when furigana removal is enabled.
            if (!isSpaceJoiningOCRLang && DefaultSettings.RemoveFurigana)
                orderedLines = FilterFuriganaLines(orderedLines);

            foreach (IOcrLine ocrLine in orderedLines)
                ocrLine.GetTextFromOcrLine(isSpaceJoiningOCRLang, text);
        }

        if (language.IsRightToLeft())
            text.ReverseWordsForRightToLeft();

        return text.ToString();
    }

    /// <summary>
    /// Re-orders OCR lines into natural reading flow: groups lines that share a
    /// horizontal row (their vertical extents overlap), orders rows top-to-bottom,
    /// and orders the lines within each row left-to-right. Windows OCR frequently
    /// returns CJK lines out of order (furigana above kanji, trailing fragments),
    /// which scrambles the concatenated text without this pass.
    /// </summary>
    internal static IReadOnlyList<IOcrLine> OrderLinesForReadingFlow(IReadOnlyList<IOcrLine> lines)
    {
        if (lines.Count <= 1)
            return lines;

        // Stable sort by the top edge so rows are discovered top-to-bottom.
        List<IOcrLine> byTop = [.. lines.OrderBy(line => line.BoundingBox.Top)];

        List<List<IOcrLine>> rows = [];
        double currentRowTop = 0;
        double currentRowBottom = 0;

        foreach (IOcrLine line in byTop)
        {
            Windows.Foundation.Rect box = line.BoundingBox;

            if (rows.Count > 0)
            {
                double overlap = Math.Min(currentRowBottom, box.Bottom) - Math.Max(currentRowTop, box.Top);
                double minHeight = Math.Min(currentRowBottom - currentRowTop, box.Height);

                // A line joins the current row when it overlaps the row's vertical
                // band by more than half of the shorter of the two heights.
                if (minHeight > 0 && overlap > minHeight * 0.5)
                {
                    rows[^1].Add(line);
                    currentRowTop = Math.Min(currentRowTop, box.Top);
                    currentRowBottom = Math.Max(currentRowBottom, box.Bottom);
                    continue;
                }
            }

            rows.Add([line]);
            currentRowTop = box.Top;
            currentRowBottom = box.Bottom;
        }

        List<IOcrLine> ordered = [];
        foreach (List<IOcrLine> row in rows)
            ordered.AddRange(row.OrderBy(line => line.BoundingBox.Left));

        return ordered;
    }

    /// <summary>
    /// Removes whole OCR lines that are likely furigana: short ruby-reading lines
    /// that sit directly above a substantially taller line overlapping them
    /// horizontally (the kanji they annotate). Windows OCR returns furigana as
    /// their own lines, so this complements the word-level <see cref="FilterFurigana"/>.
    /// The heuristic is intentionally conservative and geometry-only; it can miss
    /// mis-detected readings and is offered as an opt-in, experimental setting.
    /// </summary>
    internal static IReadOnlyList<IOcrLine> FilterFuriganaLines(IReadOnlyList<IOcrLine> lines)
    {
        if (lines.Count < 2)
            return lines;

        List<IOcrLine> kept = [];

        for (int i = 0; i < lines.Count; i++)
        {
            Windows.Foundation.Rect box = lines[i].BoundingBox;
            bool isFurigana = false;

            for (int j = 0; j < lines.Count; j++)
            {
                if (i == j)
                    continue;

                Windows.Foundation.Rect other = lines[j].BoundingBox;

                bool isBelow = other.Top >= box.Bottom;
                bool overlapsHorizontally = !(other.Right < box.Left || other.Left > box.Right);
                // The annotated kanji is markedly taller than its reading.
                bool isSubstantiallyTaller = other.Height > box.Height * 1.4;
                // Ruby text hugs the top of its character; a large vertical gap
                // means these are separate lines of body text, not a reading.
                bool isCloseAbove = other.Top - box.Bottom < box.Height;

                if (isBelow && overlapsHorizontally && isSubstantiallyTaller && isCloseAbove)
                {
                    isFurigana = true;
                    break;
                }
            }

            if (!isFurigana)
                kept.Add(lines[i]);
        }

        // Never drop everything - fall back to the input if the heuristic would
        // erase the whole result.
        return kept.Count > 0 ? kept : lines;
    }

    internal static bool ShouldUseParagraphDetection(bool isSpaceJoiningLanguage, bool isTableMode = false)
    {
        return DefaultSettings.ParagraphDetection && isSpaceJoiningLanguage && !isTableMode;
    }

    internal static List<GroupedOcrLines> GroupWrappedParagraphLines(IReadOnlyList<PositionedOcrLine> lines)
    {
        List<GroupedOcrLines> groupedLines = [];

        if (lines.Count == 0)
            return groupedLines;

        List<PositionedOcrLine> currentGroup = [lines[0]];
        Windows.Foundation.Rect currentBounds = lines[0].BoundingBox;

        for (int i = 1; i < lines.Count; i++)
        {
            PositionedOcrLine previousLine = currentGroup[^1];
            PositionedOcrLine currentLine = lines[i];

            if (IsWrappedParagraph(
                previousLine.BoundingBox.Y,
                previousLine.BoundingBox.Height,
                currentLine.BoundingBox.Y,
                currentLine.BoundingBox.Height))
            {
                currentGroup.Add(currentLine);
                currentBounds = UnionRectangles(currentBounds, currentLine.BoundingBox);
                continue;
            }

            groupedLines.Add(new GroupedOcrLines([.. currentGroup], currentBounds));
            currentGroup = [currentLine];
            currentBounds = currentLine.BoundingBox;
        }

        groupedLines.Add(new GroupedOcrLines([.. currentGroup], currentBounds));
        return groupedLines;
    }

    private static Windows.Foundation.Rect UnionRectangles(Windows.Foundation.Rect current, Windows.Foundation.Rect next)
    {
        if (current.IsEmpty)
            return next;

        if (next.IsEmpty)
            return current;

        double left = Math.Min(current.X, next.X);
        double top = Math.Min(current.Y, next.Y);
        double right = Math.Max(current.X + current.Width, next.X + next.Width);
        double bottom = Math.Max(current.Y + current.Height, next.Y + next.Height);
        return new Windows.Foundation.Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Determines whether two consecutive lines belong to the same wrapped paragraph
    /// by comparing the vertical gap between them relative to the average line height.
    /// Returns true if the lines should be joined with a space (same paragraph, wrapped),
    /// false if they should be separated by a newline (different paragraphs).
    /// </summary>
    internal static bool IsWrappedLine(IOcrLine currentLine, IOcrLine nextLine)
    {
        if (currentLine.BoundingBox.IsEmpty || nextLine.BoundingBox.IsEmpty)
            return false;

        return IsWrappedParagraph(
            currentLine.BoundingBox.Y,
            currentLine.BoundingBox.Height,
            nextLine.BoundingBox.Y,
            nextLine.BoundingBox.Height);
    }

    /// <summary>
    /// Core paragraph-wrap heuristic: returns true when the vertical gap between two
    /// lines is small enough (less than 60 % of the average line height) that they
    /// belong to the same wrapped paragraph, and their heights are similar (ratio ≤ 1.5).
    /// Works for any coordinate space — ratios are scale-invariant.
    /// </summary>
    internal static bool IsWrappedParagraph(
        double currentTop, double currentHeight,
        double nextTop, double nextHeight)
    {
        if (currentHeight <= 0 || nextHeight <= 0)
            return false;

        // Lines with significantly different heights are likely different content blocks
        double minHeight = Math.Min(currentHeight, nextHeight);
        double maxHeight = Math.Max(currentHeight, nextHeight);
        if (maxHeight / minHeight > 1.5)
            return false;

        // Consecutive OCR entries must advance to a distinct visual row. Without
        // this guard, duplicate or horizontally split entries on the same row have
        // a negative gap and are incorrectly merged into a one-line-tall paragraph.
        if (nextTop - currentTop < minHeight * 0.5)
            return false;

        // If the vertical gap between line bounding boxes is less than 0.6× the average line
        // height, the lines are part of the same paragraph (normal line spacing); otherwise
        // the extra whitespace signals a paragraph break.
        double gap = nextTop - (currentTop + currentHeight);
        double avgLineHeight = (currentHeight + nextHeight) / 2.0;
        return gap < avgLineHeight * 0.6;
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
        language ??= LanguageUtilities.GetCurrentInputLanguage();

        if (IoUtilities.IsPdfFileExtension(Path.GetExtension(absolutePath)))
        {
            using PdfDocumentRenderer pdfDocument = await PdfDocumentRenderer.LoadAsync(absolutePath);
            return await pdfDocument.ExtractTextAsync(language);
        }

        using Bitmap bmp = LoadBitmapFromFile(absolutePath);
        return GetStringFromOcrOutputs(await GetTextFromImageAsync(bmp, language));
    }

    private static Bitmap LoadBitmapFromFile(string absolutePath)
    {
        Uri fileURI = new(absolutePath, UriKind.Absolute);
        RotateFlipType rotateFlipType = ImageMethods.GetRotateFlipType(absolutePath);
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
        if (IsWindowsAiDescriptionLanguage(selectedLanguage))
            return 1.0;

        selectedLanguage = GetCompatibleOcrLanguage(selectedLanguage);
        IOcrLinesWords ocrResult = await OcrUtilities.GetOcrResultFromImageAsync(bitmap, selectedLanguage);
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

    [GeneratedRegex(@"(^[\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}")]
    private static partial Regex SpaceJoiningWordRegex();

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
