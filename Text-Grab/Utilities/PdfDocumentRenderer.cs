using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using OcrEngine = Windows.Media.Ocr.OcrEngine;
using PigPdfDocument = UglyToad.PdfPig.PdfDocument;
using PdfPageRenderOptions = Windows.Data.Pdf.PdfPageRenderOptions;
using WinPdfDocument = Windows.Data.Pdf.PdfDocument;
using WinPdfPage = Windows.Data.Pdf.PdfPage;

namespace Text_Grab.Utilities;

internal sealed class PdfPageContent
{
    public PdfPageContent(
        int pageIndex,
        BitmapSource renderedPage,
        IReadOnlyList<PdfPageTextLine> nativeLines,
        IReadOnlyList<Windows.Foundation.Rect> imageRegions)
    {
        PageIndex = pageIndex;
        RenderedPage = renderedPage;
        NativeLines = nativeLines;
        ImageRegions = imageRegions;
    }

    public bool HasNativeText => NativeLines.Count > 0;

    public IReadOnlyList<Windows.Foundation.Rect> ImageRegions { get; }

    public IReadOnlyList<PdfPageTextLine> NativeLines { get; }

    public int PageIndex { get; }

    public BitmapSource RenderedPage { get; }
}

internal sealed class PdfPageTextLine
{
    public PdfPageTextLine(Windows.Foundation.Rect sourceRect, string text, bool isNativeText)
    {
        SourceRect = sourceRect;
        Text = text;
        IsNativeText = isNativeText;
    }

    public bool IsNativeText { get; }

    public Windows.Foundation.Rect SourceRect { get; }

    public string Text { get; }
}

internal sealed class PdfDocumentRenderer : IDisposable
{
    private const double DefaultRenderScale = 2.0;
    private const int MaxCachedPages = 10;
    private readonly WinPdfDocument renderDocument;
    private readonly PigPdfDocument textDocument;
    private readonly Dictionary<int, PdfPageContent> pageCache = [];
    private readonly LinkedList<int> cacheOrder = new();

    private PdfDocumentRenderer(string filePath, WinPdfDocument renderDocument, PigPdfDocument textDocument)
    {
        FilePath = filePath;
        this.renderDocument = renderDocument;
        this.textDocument = textDocument;
    }

    public string FilePath { get; }

    public int PageCount => (int)renderDocument.PageCount;

    public void Dispose()
    {
        textDocument.Dispose();
    }

    public async Task<string> ExtractTextAsync(ILanguage? language = null, GrabTemplate? grabTemplate = null)
    {
        ILanguage resolvedLanguage = language ?? LanguageUtilities.GetCurrentInputLanguage();
        StringBuilder extractedText = new();

        for (int pageIndex = 0; pageIndex < PageCount; pageIndex++)
        {
            string pageText;
            if (grabTemplate is not null)
            {
                BitmapSource pageImage = await RenderPageAsync(pageIndex);
                using Bitmap pageBitmap = ImageMethods.BitmapSourceToBitmap(pageImage);
                pageText = await GrabTemplateExecutor.ExecuteTemplateOnBitmapAsync(grabTemplate, pageBitmap, resolvedLanguage);
            }
            else
            {
                IReadOnlyList<PdfPageTextLine> lines = await GetSelectableLinesAsync(pageIndex, resolvedLanguage);
                pageText = string.Join(Environment.NewLine, lines.Select(line => line.Text));
            }

            if (string.IsNullOrWhiteSpace(pageText))
                continue;

            if (extractedText.Length > 0)
                extractedText.AppendLine().AppendLine();

            extractedText.Append(pageText.Trim());
        }

        return extractedText.ToString();
    }

    public async Task<PdfPageContent> GetPageContentAsync(int pageIndex)
    {
        ValidatePageIndex(pageIndex);

        if (pageCache.TryGetValue(pageIndex, out PdfPageContent? cachedPage))
        {
            cacheOrder.Remove(pageIndex);
            cacheOrder.AddLast(pageIndex);
            return cachedPage;
        }

        WinPdfPage renderPage = renderDocument.GetPage((uint)pageIndex);
        try
        {
            BitmapImage renderedPage = await RenderPageBitmapAsync(renderPage);
            Page textPage = textDocument.GetPage(pageIndex + 1);

            List<PdfPageTextLine> nativeLines = ExtractNativeLines(textPage, renderedPage.PixelWidth, renderedPage.PixelHeight);
            List<Windows.Foundation.Rect> imageRegions = ExtractImageRegions(textPage, renderedPage.PixelWidth, renderedPage.PixelHeight);

            PdfPageContent pageContent = new(pageIndex, renderedPage, nativeLines, imageRegions);

            if (pageCache.Count >= MaxCachedPages && cacheOrder.First is LinkedListNode<int> oldest)
            {
                pageCache.Remove(oldest.Value);
                cacheOrder.RemoveFirst();
            }

            pageCache[pageIndex] = pageContent;
            cacheOrder.AddLast(pageIndex);
            return pageContent;
        }
        finally
        {
            (renderPage as IDisposable)?.Dispose();
        }
    }

    public async Task<IReadOnlyList<PdfPageTextLine>> GetSelectableLinesAsync(int pageIndex, ILanguage? language = null)
    {
        PdfPageContent pageContent = await GetPageContentAsync(pageIndex);
        ILanguage resolvedLanguage = language ?? LanguageUtilities.GetCurrentInputLanguage();

        if (!pageContent.HasNativeText)
            return await GetOcrLinesAsync(pageContent.RenderedPage, resolvedLanguage);

        if (pageContent.ImageRegions.Count == 0)
            return pageContent.NativeLines;

        List<PdfPageTextLine> combinedLines = [.. pageContent.NativeLines];
        IReadOnlyList<Windows.Foundation.Rect> nativeRects = [.. pageContent.NativeLines.Select(l => l.SourceRect)];
        IReadOnlyList<PdfPageTextLine> imageOcrLines = await GetOcrLinesAsync(
            pageContent.RenderedPage,
            resolvedLanguage,
            sourceRect => ShouldIncludeOcrLine(sourceRect, pageContent.ImageRegions)
                       && !ShouldIncludeOcrLine(sourceRect, nativeRects));

        combinedLines.AddRange(imageOcrLines);
        return SortLines(combinedLines);
    }

    public async Task<BitmapSource> RenderPageAsync(int pageIndex)
    {
        PdfPageContent pageContent = await GetPageContentAsync(pageIndex);
        return pageContent.RenderedPage;
    }

    public static async Task<PdfDocumentRenderer> LoadAsync(string filePath)
    {
        if (!IoUtilities.IsPdfFileExtension(Path.GetExtension(filePath)))
            throw new InvalidOperationException("The provided path is not a PDF document.");

        string absolutePath = Path.GetFullPath(filePath);
        StorageFile storageFile = await StorageFile.GetFileFromPathAsync(absolutePath);
        WinPdfDocument renderDocument = await WinPdfDocument.LoadFromFileAsync(storageFile);
        PigPdfDocument textDocument = PigPdfDocument.Open(absolutePath);

        return new PdfDocumentRenderer(absolutePath, renderDocument, textDocument);
    }

    internal static Windows.Foundation.Rect ConvertPdfRectToImageRect(
        PdfRectangle pdfRect,
        double pageWidthPoints,
        double pageHeightPoints,
        double renderedWidth,
        double renderedHeight)
    {
        if (pageWidthPoints <= 0 || pageHeightPoints <= 0 || renderedWidth <= 0 || renderedHeight <= 0)
            return new Windows.Foundation.Rect(0, 0, 0, 0);

        PdfPoint[] points =
        [
            pdfRect.TopLeft,
            pdfRect.TopRight,
            pdfRect.BottomLeft,
            pdfRect.BottomRight
        ];

        List<double> xs = [];
        List<double> ys = [];

        foreach (PdfPoint point in points)
        {
            double x = (double)point.X / pageWidthPoints * renderedWidth;
            double y = (1 - ((double)point.Y / pageHeightPoints)) * renderedHeight;
            xs.Add(x);
            ys.Add(y);
        }

        double left = xs.Min();
        double top = ys.Min();
        double right = xs.Max();
        double bottom = ys.Max();

        return new Windows.Foundation.Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    internal static IReadOnlyList<PdfPageTextLine> GroupWordsIntoLines(IEnumerable<(Windows.Foundation.Rect SourceRect, string Text)> words)
    {
        List<(Windows.Foundation.Rect SourceRect, string Text)> orderedWords = [.. words
            .Where(word => !string.IsNullOrWhiteSpace(word.Text) && word.SourceRect.Width > 0 && word.SourceRect.Height > 0)
            .OrderBy(word => word.SourceRect.Y)
            .ThenBy(word => word.SourceRect.X)];

        if (orderedWords.Count == 0)
            return [];

        List<List<(Windows.Foundation.Rect SourceRect, string Text)>> groups = [];

        foreach ((Windows.Foundation.Rect SourceRect, string Text) word in orderedWords)
        {
            if (groups.Count == 0)
            {
                groups.Add([word]);
                continue;
            }

            List<(Windows.Foundation.Rect SourceRect, string Text)> currentGroup = groups[^1];
            Windows.Foundation.Rect currentBounds = GetBounds(currentGroup.Select(item => item.SourceRect));
            double currentCenterY = currentBounds.Y + (currentBounds.Height / 2);
            double wordCenterY = word.SourceRect.Y + (word.SourceRect.Height / 2);
            double lineHeight = Math.Max(currentBounds.Height, word.SourceRect.Height);
            double maxGap = lineHeight * 6;
            double horizontalGap = Math.Max(0, word.SourceRect.X - currentBounds.Right);
            bool sameBaseline = Math.Abs(wordCenterY - currentCenterY) <= lineHeight * 0.6;

            if (sameBaseline && horizontalGap <= maxGap)
                currentGroup.Add(word);
            else
                groups.Add([word]);
        }

        List<PdfPageTextLine> lines = [];
        foreach (List<(Windows.Foundation.Rect SourceRect, string Text)> group in groups)
        {
            List<(Windows.Foundation.Rect SourceRect, string Text)> orderedGroup = [.. group.OrderBy(item => item.SourceRect.X)];
            Windows.Foundation.Rect lineBounds = GetBounds(orderedGroup.Select(item => item.SourceRect));
            string text = string.Join(" ", orderedGroup.Select(item => item.Text.Trim()));
            lines.Add(new PdfPageTextLine(lineBounds, text, isNativeText: true));
        }

        return SortLines(lines);
    }

    internal static (uint Width, uint Height) GetRenderDimensions(double pageWidth, double pageHeight, double scaleFactor = DefaultRenderScale)
    {
        if (!double.IsFinite(pageWidth) || pageWidth <= 0 || !double.IsFinite(pageHeight) || pageHeight <= 0)
            return (1, 1);

        double scaledWidth = Math.Max(1, pageWidth * scaleFactor);
        double scaledHeight = Math.Max(1, pageHeight * scaleFactor);
        double maxDimension = Math.Max(scaledWidth, scaledHeight);

        if (maxDimension > OcrEngine.MaxImageDimension)
        {
            double scaleDownRatio = OcrEngine.MaxImageDimension / maxDimension;
            scaledWidth *= scaleDownRatio;
            scaledHeight *= scaleDownRatio;
        }

        return ((uint)Math.Max(1, Math.Round(scaledWidth)), (uint)Math.Max(1, Math.Round(scaledHeight)));
    }

    internal static bool ShouldIncludeOcrLine(Windows.Foundation.Rect sourceRect, IReadOnlyList<Windows.Foundation.Rect> imageRegions)
    {
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            return false;

        double sourceArea = sourceRect.Width * sourceRect.Height;
        if (sourceArea <= 0)
            return false;

        foreach (Windows.Foundation.Rect imageRegion in imageRegions)
        {
            double intersectionLeft = Math.Max(sourceRect.Left, imageRegion.Left);
            double intersectionTop = Math.Max(sourceRect.Top, imageRegion.Top);
            double intersectionRight = Math.Min(sourceRect.Right, imageRegion.Right);
            double intersectionBottom = Math.Min(sourceRect.Bottom, imageRegion.Bottom);

            double intersectionWidth = Math.Max(0, intersectionRight - intersectionLeft);
            double intersectionHeight = Math.Max(0, intersectionBottom - intersectionTop);
            double intersectionArea = intersectionWidth * intersectionHeight;

            if (intersectionArea / sourceArea >= 0.25)
                return true;
        }

        return false;
    }

    private static PdfPageRenderOptions CreateRenderOptions(WinPdfPage page)
    {
        (uint width, uint height) = GetRenderDimensions(page.Size.Width, page.Size.Height);

        return new PdfPageRenderOptions
        {
            BackgroundColor = new Windows.UI.Color { A = byte.MaxValue, R = byte.MaxValue, G = byte.MaxValue, B = byte.MaxValue },
            BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
            DestinationWidth = width,
            DestinationHeight = height,
            IsIgnoringHighContrast = true
        };
    }

    private static List<Windows.Foundation.Rect> ExtractImageRegions(Page textPage, int renderedWidth, int renderedHeight)
    {
        return [.. textPage.GetImages()
            .Select(image => ConvertPdfRectToImageRect(image.BoundingBox, (double)textPage.Width, (double)textPage.Height, renderedWidth, renderedHeight))
            .Where(rect => rect.Width > 0 && rect.Height > 0)];
    }

    private static List<PdfPageTextLine> ExtractNativeLines(Page textPage, int renderedWidth, int renderedHeight)
    {
        List<(Windows.Foundation.Rect SourceRect, string Text)> words = [.. textPage
            .GetWords(NearestNeighbourWordExtractor.Instance)
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .Select(word => (
                SourceRect: ConvertPdfRectToImageRect(word.BoundingBox, (double)textPage.Width, (double)textPage.Height, renderedWidth, renderedHeight),
                Text: word.Text.Trim()))
            .Where(word => word.SourceRect.Width > 0 && word.SourceRect.Height > 0)];

        return [.. GroupWordsIntoLines(words)];
    }

    private static Windows.Foundation.Rect GetBounds(IEnumerable<Windows.Foundation.Rect> rects)
    {
        List<Windows.Foundation.Rect> rectList = [.. rects.Where(rect => rect.Width > 0 && rect.Height > 0)];
        if (rectList.Count == 0)
            return new Windows.Foundation.Rect(0, 0, 0, 0);

        double left = rectList.Min(rect => rect.Left);
        double top = rectList.Min(rect => rect.Top);
        double right = rectList.Max(rect => rect.Right);
        double bottom = rectList.Max(rect => rect.Bottom);

        return new Windows.Foundation.Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private async Task<IReadOnlyList<PdfPageTextLine>> GetOcrLinesAsync(
        BitmapSource renderedPage,
        ILanguage language,
        Func<Windows.Foundation.Rect, bool>? sourceRectPredicate = null)
    {
        using Bitmap bitmap = ImageMethods.BitmapSourceToBitmap(renderedPage);
        (IOcrLinesWords? ocrResult, double scale) = await OcrUtilities.GetOcrResultFromBitmapAsync(bitmap, language);
        if (ocrResult is null || ocrResult.Lines.Length == 0)
            return [];

        return ConvertOcrLines(ocrResult, scale, language, sourceRectPredicate);
    }

    private static IReadOnlyList<PdfPageTextLine> ConvertOcrLines(
        IOcrLinesWords ocrResult,
        double scale,
        ILanguage language,
        Func<Windows.Foundation.Rect, bool>? sourceRectPredicate)
    {
        List<PdfPageTextLine> lines = [];
        bool isSpaceJoiningLanguage = language.IsSpaceJoining();

        foreach (IOcrLine ocrLine in ocrResult.Lines)
        {
            StringBuilder textBuilder = new();
            ocrLine.GetTextFromOcrLine(isSpaceJoiningLanguage, textBuilder);
            textBuilder.RemoveTrailingNewlines();

            string lineText = textBuilder.ToString();
            if (string.IsNullOrWhiteSpace(lineText))
                continue;

            Windows.Foundation.Rect scaledRect = ocrLine.BoundingBox;
            Windows.Foundation.Rect sourceRect = new(
                scaledRect.X / scale,
                scaledRect.Y / scale,
                scaledRect.Width / scale,
                scaledRect.Height / scale);

            if (sourceRectPredicate is not null && !sourceRectPredicate(sourceRect))
                continue;

            lines.Add(new PdfPageTextLine(sourceRect, lineText.Trim(), isNativeText: false));
        }

        return SortLines(lines);
    }

    private static List<PdfPageTextLine> SortLines(IEnumerable<PdfPageTextLine> lines)
    {
        return [.. lines.OrderBy(line => line.SourceRect.Y).ThenBy(line => line.SourceRect.X)];
    }

    private static async Task<BitmapImage> RenderPageBitmapAsync(WinPdfPage page)
    {
        using InMemoryRandomAccessStream renderedStream = new();
        PdfPageRenderOptions renderOptions = CreateRenderOptions(page);

        await page.RenderToStreamAsync(renderedStream, renderOptions);
        renderedStream.Seek(0);

        using Bitmap renderedBitmap = ImageMethods.GetBitmapFromIRandomAccessStream(renderedStream);
        return ImageMethods.BitmapToImageSource(renderedBitmap);
    }

    private void ValidatePageIndex(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, "Page index is outside the document bounds.");
    }
}
