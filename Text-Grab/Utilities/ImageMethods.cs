using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Drawing;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Markup;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using Windows.Media.Ocr;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using BitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using BitmapEncoder = System.Windows.Media.Imaging.BitmapEncoder;

namespace Text_Grab
{
    public static class ImageMethods
    {
        public static Bitmap PadImage(Bitmap image, int minW = 64, int minH = 64)
        {
            if (image.Height >= minH && image.Width >= minW)
                return image;

            int width = Math.Max(image.Width + 16, minW + 16);
            int height = Math.Max(image.Height + 16, minH + 16);

            // Create a compatible bitmap
            Bitmap dest = new Bitmap(width, height, image.PixelFormat);
            using (Graphics gd = Graphics.FromImage(dest))
            {
                gd.Clear(image.GetPixel(0, 0));
                gd.DrawImageUnscaled(image, 8, 8);
            }
            return dest;
        }

        internal static Bitmap BitmapImageToBitmap(BitmapImage bitmapImage)
        {
            // BitmapImage bitmapImage = new BitmapImage(new Uri("../Images/test.png", UriKind.Relative));

            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                Bitmap bitmap = new Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

        internal static BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        internal static async Task<string> GetRegionsText(Window passedWindow, Rectangle selectedRegion)
        {
            Bitmap bmp = new Bitmap(selectedRegion.Width, selectedRegion.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);

            System.Windows.Point absPosPoint;

            if (passedWindow == null)
                absPosPoint = new System.Windows.Point();
            else
                absPosPoint = passedWindow.GetAbsolutePosition();

            int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
            int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

            g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            bmp = PadImage(bmp);

            string ocrText = await ExtractText(bmp);

            return ocrText;
        }


        internal static async Task<string> GetClickedWord(Window passedWindow, System.Windows.Point clickedPoint)
        {
            DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
            Bitmap bmp = new Bitmap((int)(passedWindow.ActualWidth * dpi.DpiScaleX), (int)(passedWindow.ActualHeight * dpi.DpiScaleY), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);

            System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();
            int thisCorrectedLeft = (int)absPosPoint.X;
            int thisCorrectedTop = (int)absPosPoint.Y;

            g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            // var bmpImage = BitmapToImageSource(bmp);
            // DebugImage.Source = bmpImage;

            System.Windows.Point adjustedPoint = new System.Windows.Point(clickedPoint.X, clickedPoint.Y);

            string ocrText = await ExtractText(bmp, adjustedPoint);
            return ocrText.Trim();
        }

        public static async Task<string> ExtractText(Bitmap bmp, System.Windows.Point? singlePoint = null)
        {
            Language selectedLanguage = GetOCRLanguage();
            if (selectedLanguage == null)
            {
                return null;
            }

            XmlLanguage lang = XmlLanguage.GetLanguage(selectedLanguage.LanguageTag);
            CultureInfo culture = lang.GetEquivalentCulture();

            bool scaleBMP = true;

            if (singlePoint != null
                || bmp.Width * 1.5 > OcrEngine.MaxImageDimension)
            {
                scaleBMP = false;
            }

            Bitmap scaledBitmap;
            if (scaleBMP)
                scaledBitmap = ScaleBitmapUniform(bmp, 1.5);
            else
                scaledBitmap = ScaleBitmapUniform(bmp, 1.0);

            StringBuilder text = new StringBuilder();

            await using (MemoryStream memory = new MemoryStream())
            {
                scaledBitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(memory.AsRandomAccessStream());
                SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

                OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(selectedLanguage);
                OcrResult ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

                if (singlePoint == null)
                {
                    foreach (OcrLine line in ocrResult.Lines) text.AppendLine(line.Text);
                }
                else
                {
                    Windows.Foundation.Point fPoint = new Windows.Foundation.Point(singlePoint.Value.X, singlePoint.Value.Y);
                    foreach (OcrLine ocrLine in ocrResult.Lines)
                    {
                        foreach (OcrWord ocrWord in ocrLine.Words)
                        {
                            if (ocrWord.BoundingRect.Contains(fPoint))
                                _ = text.Append(ocrWord.Text);
                        }
                    }
                }
            }
            if (culture.TextInfo.IsRightToLeft)
            {
                List<string> textListLines = text.ToString().Split(new char[] { '\n', '\r' }).ToList();

                _ = text.Clear();
                foreach (string textLine in textListLines)
                {
                    List<string> wordArray = textLine.Split().ToList();
                    wordArray.Reverse();
                    _ = text.Append(string.Join(' ', wordArray));

                    if (textLine.Length > 0)
                        _ = text.Append('\n');
                }
                return text.ToString();
            }
            else
            {
                return text.ToString();
            }
        }

        public static async Task<OcrResult> GetOcrResultFromRegion(Rectangle region)
        {
            Language selectedLanguage = GetOCRLanguage();
            if (selectedLanguage == null)
            {
                return null;
            }
            
            Bitmap bmp = new Bitmap(region.Width, region.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);

            g.CopyFromScreen(region.Left, region.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

            OcrResult ocrResult;
            await using (MemoryStream memory = new MemoryStream())
            {
                bmp.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(memory.AsRandomAccessStream());
                SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

                OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(selectedLanguage);
                ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);
            }
            return ocrResult;
        }

        public static Bitmap ScaleBitmapUniform(Bitmap passedBitmap, double scale)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                passedBitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                TransformedBitmap tbmpImg = new TransformedBitmap();
                tbmpImg.BeginInit();
                tbmpImg.Source = bitmapimage;
                tbmpImg.Transform = new ScaleTransform(scale, scale);
                tbmpImg.EndInit();
                return BitmapSourceToBitmap(tbmpImg.Source);
            }
        }

        public static Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            Bitmap bmp = new Bitmap(
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

        private static Language GetOCRLanguage()
        {
            // use currently selected Language
            string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;

            Language selectedLanguage = new Language(inputLang);
            List<Language> possibleOCRLangs = OcrEngine.AvailableRecognizerLanguages.ToList();

            if (possibleOCRLangs.Count < 1)
            {
                MessageBox.Show("No possible OCR languages are installed.", "Text Grab");
                return null;
            }

            if (possibleOCRLangs.All(l => l.LanguageTag != selectedLanguage.LanguageTag))
            {
                List<Language> similarLanguages = possibleOCRLangs.Where(
                    la => la.AbbreviatedName == selectedLanguage.AbbreviatedName).ToList();

                selectedLanguage = similarLanguages.Count > 0
                    ? similarLanguages.FirstOrDefault()
                    : possibleOCRLangs.FirstOrDefault();
            }

            return selectedLanguage;
        }
    }
}
