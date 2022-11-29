using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using ZXing.Windows.Compatibility;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using BitmapEncoder = System.Windows.Media.Imaging.BitmapEncoder;
using BitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using Point = System.Windows.Point;

namespace Text_Grab;

public static class ImageMethods
{
    public static Bitmap PadImage(Bitmap image, int minW = 64, int minH = 64)
    {
        if (image.Height >= minH && image.Width >= minW)
            return image;

        int width = Math.Max(image.Width + 16, minW + 16);
        int height = Math.Max(image.Height + 16, minH + 16);

        // Create a compatible bitmap
        Bitmap dest = new(width, height, image.PixelFormat);
        using Graphics gd = Graphics.FromImage(dest);

        gd.Clear(image.GetPixel(0, 0));
        gd.DrawImageUnscaled(image, 8, 8);

        return dest;
    }

    public static Bitmap BitmapImageToBitmap(BitmapImage bitmapImage)
    {
        using MemoryStream outStream = new();

        BitmapEncoder enc = new BmpBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bitmapImage));
        enc.Save(outStream);
        using Bitmap bitmap = new(outStream);
        outStream.Flush();

        return new Bitmap(bitmap);
    }

    public static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using MemoryStream memory = new();

        bitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;
        BitmapImage bitmapimage = new();
        bitmapimage.BeginInit();
        bitmapimage.StreamSource = memory;
        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapimage.EndInit();
        bitmapimage.Freeze();

        memory.Flush();

        return bitmapimage;
    }

    public static async Task<string> GetRegionsText(Window? passedWindow, Rectangle selectedRegion, Language? language)
    {
        System.Windows.Point absPosPoint;

        if (passedWindow == null)
            absPosPoint = new();
        else
            absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        Bitmap bmp = new(selectedRegion.Width, selectedRegion.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        bmp = PadImage(bmp);

        string? ocrText = await ExtractText(bmp, null, language);

        if (string.IsNullOrWhiteSpace(ocrText))
            return "";

        return ocrText.Trim();
    }

    public static Bitmap GetWindowsBoundsBitmap(Window passedWindow)
    {
        bool isGrabFrame = false;
        if (passedWindow is GrabFrame)
            isGrabFrame = true;

        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        int windowWidth = (int)(passedWindow.ActualWidth * dpi.DpiScaleX);
        int windowHeight = (int)(passedWindow.ActualHeight * dpi.DpiScaleY);

        System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)(absPosPoint.X);
        int thisCorrectedTop = (int)(absPosPoint.Y);

        if (isGrabFrame)
        {
            thisCorrectedLeft = (int)((absPosPoint.X + 2) * dpi.DpiScaleX);
            thisCorrectedTop = (int)((absPosPoint.Y + 26) * dpi.DpiScaleY);
            windowWidth -= (int)(4 * dpi.DpiScaleX);
            windowHeight -= (int)(70 * dpi.DpiScaleY);
        }

        Bitmap bmp = new(windowWidth, windowHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }

    public static ImageSource GetWindowBoundsImage(Window passedWindow)
    {
        Bitmap bmp = GetWindowsBoundsBitmap(passedWindow);
        return BitmapToImageSource(bmp);
    }

    public static async Task<string> GetClickedWord(Window passedWindow, Point clickedPoint, Language? OcrLang)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        using Bitmap bmp = new((int)(passedWindow.ActualWidth * dpi.DpiScaleX), (int)(passedWindow.ActualHeight * dpi.DpiScaleY), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        Point absPosPoint = passedWindow.GetAbsolutePosition();
        int thisCorrectedLeft = (int)absPosPoint.X;
        int thisCorrectedTop = (int)absPosPoint.Y;

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

        Point adjustedPoint = new(clickedPoint.X, clickedPoint.Y);

        string ocrText = await ExtractText(bmp, adjustedPoint, OcrLang);
        return ocrText.Trim();
    }

    public static async Task<string> ExtractText(Bitmap bmp, Point? singlePoint = null, Language? selectedLanguage = null)
    {
        if (selectedLanguage is null)
            selectedLanguage = OcrExtensions.GetOCRLanguage();

        if (singlePoint is Point point)
            return await GetTextFromClickedWord(point, bmp, selectedLanguage);

        return await GetTextFromEntireBitmap(bmp, selectedLanguage);
    }

    private async static Task<string> GetTextFromEntireBitmap(Bitmap bitmap, Language language)
    {
        double scale = await GetIdealScaleFactor(bitmap, language);
        Bitmap scaledBitmap = ScaleBitmapUniform(bitmap, scale);

        StringBuilder text = new();

        OcrResult ocrResult = await OcrExtensions.GetOcrResultFromBitmap(scaledBitmap, language);
        bool isSpaceJoiningOCRLang = IsLanguageSpaceJoining(language);

        foreach (OcrLine ocrLine in ocrResult.Lines)
            ocrLine.GetTextFromOcrLine(isSpaceJoiningOCRLang, text);

        XmlLanguage lang = XmlLanguage.GetLanguage(language.LanguageTag);
        CultureInfo culture = lang.GetEquivalentCulture();

        if (culture.TextInfo.IsRightToLeft)
            ReverseWordsForRightToLeft(text);

        if (Settings.Default.TryToReadBarcodes)
        {
            string barcodeResult = TryToReadBarcodes(scaledBitmap);

            if (!string.IsNullOrWhiteSpace(barcodeResult))
                text.AppendLine(barcodeResult);
        }

        return text.ToString();
    }

    public static bool IsLanguageSpaceJoining(Language selectedLanguage)
    {
        if (selectedLanguage.LanguageTag.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase))
            return false;
        else if (selectedLanguage.LanguageTag.Equals("ja", StringComparison.InvariantCultureIgnoreCase))
            return false;
        return true;
    }

    private static async Task<string> GetTextFromClickedWord(Point singlePoint, Bitmap bitmap, Language language)
    {
        return GetTextFromClickedWord(singlePoint, await OcrExtensions.GetOcrResultFromBitmap(bitmap, language));
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

    private static string TryToReadBarcodes(Bitmap bitmap)
    {
        BarcodeReader barcodeReader = new()
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions { TryHarder = true }
        };

        ZXing.Result result = barcodeReader.Decode(bitmap);

        if (result is null)
            return string.Empty;
        return result.Text;
    }

    private static void ReverseWordsForRightToLeft(StringBuilder text)
    {
        string[] textListLines = text.ToString().Split(new char[] { '\n', '\r' });
        Regex regexSpaceJoiningWord = new(@"(^[\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}");

        _ = text.Clear();
        foreach (string textLine in textListLines)
        {
            bool firstWord = true;
            bool isPrevWordSpaceJoining = false;
            List<string> wordArray = textLine.Split().ToList();
            wordArray.Reverse();

            foreach (string wordText in wordArray)
            {
                bool isThisWordSpaceJoining = regexSpaceJoiningWord.IsMatch(wordText);

                if (firstWord || (!isThisWordSpaceJoining && !isPrevWordSpaceJoining))
                    _ = text.Append(wordText);
                else
                    _ = text.Append(' ').Append(wordText);

                firstWord = false;
                isPrevWordSpaceJoining = isThisWordSpaceJoining;
            }

            if (textLine.Length > 0)
                _ = text.Append(Environment.NewLine);
        }
    }

    public static Bitmap ScaleBitmapUniform(Bitmap passedBitmap, double scale)
    {
        using MemoryStream memory = new();

        passedBitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;
        BitmapImage bitmapimage = new();
        bitmapimage.BeginInit();
        bitmapimage.StreamSource = memory;
        bitmapimage.CacheOption = BitmapCacheOption.None;
        bitmapimage.EndInit();
        bitmapimage.Freeze();

        memory.Flush();

        TransformedBitmap tbmpImg = new();
        tbmpImg.BeginInit();
        tbmpImg.Source = bitmapimage;
        tbmpImg.Transform = new ScaleTransform(scale, scale);
        tbmpImg.EndInit();
        tbmpImg.Freeze();
        return BitmapSourceToBitmap(tbmpImg);

    }

    public async static Task<double> GetIdealScaleFactor(Bitmap bitmap, Language selectedLanguage)
    {
        List<double> heightsList = new();
        double scaleFactor = 1.5;

        OcrResult ocrResult = await OcrExtensions.GetOcrResultFromBitmap(bitmap, selectedLanguage);

        foreach (OcrLine ocrLine in ocrResult.Lines)
            foreach (OcrWord ocrWord in ocrLine.Words)
                heightsList.Add(ocrWord.BoundingRect.Height);

        double lineHeight = 10;

        if (heightsList.Count > 0)
            lineHeight = heightsList.Average();

        // Ideal Line Height is 40px
        const double idealLineHeight = 40.0;

        scaleFactor = idealLineHeight / lineHeight;

        if (bitmap.Width * scaleFactor > OcrEngine.MaxImageDimension || bitmap.Height * scaleFactor > OcrEngine.MaxImageDimension)
        {
            int largerDim = Math.Max(bitmap.Width, bitmap.Height);
            // find the largest possible scale factor, because the ideal scale factor is too high

            scaleFactor = OcrEngine.MaxImageDimension / largerDim;
        }

        return scaleFactor;
    }

    public static Bitmap BitmapSourceToBitmap(BitmapSource source)
    {
        Bitmap bmp = new(
          source.PixelWidth,
          source.PixelHeight,
          System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        BitmapData data = bmp.LockBits(
          new Rectangle(System.Drawing.Point.Empty, bmp.Size),
          ImageLockMode.WriteOnly,
          System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        source.CopyPixels(
          Int32Rect.Empty,
          data.Scan0,
          data.Height * data.Stride,
          data.Stride);
        bmp.UnlockBits(data);
        return bmp;
    }

    public static async Task<string> OcrAbsoluteFilePath(string absolutePath)
    {
        Uri fileURI = new(absolutePath, UriKind.Absolute);
        BitmapImage droppedImage = new(fileURI);
        droppedImage.Freeze();
        Bitmap bmp = BitmapImageToBitmap(droppedImage);
        return await ExtractText(bmp);
    }
}
