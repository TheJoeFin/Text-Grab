using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.System.UserProfile;
using Windows.UI.Notifications;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using Microsoft.Toolkit.Uwp.Notifications;
using Text_Grab.Properties;
using System.Globalization;
using System.Windows.Markup;
using System.Drawing.Imaging;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isSelecting = false;
        System.Windows.Point clickedPoint = new System.Windows.Point();
        Border selectBorder = new Border();

        System.Windows.Point GetMousePos() => this.PointToScreen(Mouse.GetPosition(this));
        public List<string> InstalledLanguages => GlobalizationPreferences.Languages.ToList();

        public MainWindow()
        {
            InitializeComponent();
        }

        // add padding to image to reach a minimum size
        private Bitmap PadImage(Bitmap image, int minW = 64, int minH = 64)
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

        private async Task<string> GetRegionsText(Rectangle selectedRegion)
        {
            Bitmap bmp = new Bitmap(selectedRegion.Width, selectedRegion.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);

            System.Windows.Point absPosPoint = this.GetAbsolutePosition();
            int thisCorrectedLeft = (int)(absPosPoint.X) + selectedRegion.Left;
            int thisCorrectedTop = (int)(absPosPoint.Y) + selectedRegion.Top;

            g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            bmp = PadImage(bmp);

            // use currently selected Language
            string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;

            string ocrText = await ExtractText(bmp, inputLang);
            return ocrText.Trim();
        }

        private async Task<string> GetClickedWord(System.Windows.Point clickedPoint)
        {
            // Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
            var dpi = VisualTreeHelper.GetDpi(this);
            Bitmap bmp = new Bitmap((int)(this.ActualWidth * dpi.DpiScaleX), (int)(this.ActualHeight * dpi.DpiScaleY), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);

            System.Windows.Point absPosPoint = this.GetAbsolutePosition();
            int thisCorrectedLeft = (int)(absPosPoint.X);
            int thisCorrectedTop = (int)(absPosPoint.Y);

            g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            // var bmpImage = BitmapToImageSource(bmp);
            // DebugImage.Source = bmpImage;

            System.Windows.Point adjustedPoint = new System.Windows.Point(clickedPoint.X, clickedPoint.Y);

            // use currently selected Language
            string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;
            if (!InstalledLanguages.Contains(inputLang))
                inputLang = InstalledLanguages.FirstOrDefault();

            string ocrText = await ExtractText(bmp, inputLang, adjustedPoint);
            return ocrText.Trim();
        }

        BitmapImage BitmapToImageSource(Bitmap bitmap)
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

        private Bitmap BitmapImageToBitmap(BitmapImage bitmapImage)
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

        Bitmap BitmapSourceToBitmap(BitmapSource source)
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

        private Bitmap ScaleBitmapUniform(Bitmap passedBitmap, double scale)
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

        public async Task<string> ExtractText(Bitmap bmp, string languageCode, System.Windows.Point? singlePoint = null)
        {
            Language selectedLanguage = new Language(languageCode);
            List<Language> possibleOCRLangs = OcrEngine.AvailableRecognizerLanguages.ToList();

            if(possibleOCRLangs.Count < 1)
                throw new ArgumentOutOfRangeException($"No possible OCR languages are installed.");

            if (possibleOCRLangs.Where(l => l.LanguageTag == selectedLanguage.LanguageTag).Count() < 1)
            {
                List<Language> similarLanguages = possibleOCRLangs.Where(la => la.AbbreviatedName == selectedLanguage.AbbreviatedName).ToList();
                if (similarLanguages.Count() > 0)
                    selectedLanguage = similarLanguages.FirstOrDefault();
                else
                    selectedLanguage = possibleOCRLangs.FirstOrDefault();
            }

            bool scaleBMP = true;

            if (singlePoint != null
                || bmp.Width * 1.5 > OcrEngine.MaxImageDimension)
                scaleBMP = false;

            Bitmap scaledBitmap;
            if (scaleBMP)
                scaledBitmap = ScaleBitmapUniform(bmp, 1.5);
            else
                scaledBitmap = ScaleBitmapUniform(bmp, 1.0);

            StringBuilder text = new StringBuilder();

            XmlLanguage lang = XmlLanguage.GetLanguage(languageCode);
            CultureInfo culture = lang.GetEquivalentCulture();

            await using (MemoryStream memory = new MemoryStream())
            {
                scaledBitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(memory.AsRandomAccessStream());
                Windows.Graphics.Imaging.SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

                OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(selectedLanguage);
                OcrResult ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

                if(singlePoint == null)
                    foreach (OcrLine line in ocrResult.Lines) text.AppendLine(line.Text);
                else
                {
                    Windows.Foundation.Point fPoint = new Windows.Foundation.Point(singlePoint.Value.X, singlePoint.Value.Y);
                    foreach (OcrLine ocrLine in ocrResult.Lines)
                    {
                        foreach (OcrWord ocrWord in ocrLine.Words)
                        {
                            if (ocrWord.BoundingRect.Contains(fPoint))
                                text.Append(ocrWord.Text);
                        }
                    }
                }
            }
            if (culture.TextInfo.IsRightToLeft)
            {
                List<string> textListLines = text.ToString().Split(Environment.NewLine).ToList();

                text.Clear();
                foreach (var textLine in textListLines)
                {
                    List<string> wordArray = textLine.Split().ToList();
                    wordArray.Reverse();
                    text.Append(string.Join(' ', wordArray));
                    text.Append(Environment.NewLine);
                }
                return text.ToString();
            }
            else
                return text.ToString();

        }

        private void ShowToast(string copiedText)
        {
            string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;
            // Construct the content
            ToastContent content = new ToastContentBuilder()
                .AddToastActivationInfo(copiedText + ',' + inputLang, ToastActivationType.Foreground)
                .SetBackgroundActivation()
                .AddText(copiedText)
                .GetToastContent();
            content.Duration = ToastDuration.Short;

            // Create the toast notification
            var toastNotif = new ToastNotification(content.GetXml());

            // And send the notification
            try 
            { 
                ToastNotificationManager.CreateToastNotifier().Show(toastNotif); 
            } 
            catch (Exception) 
            {
                Settings.Default.ShowToast = false;
                Settings.Default.Save();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;

            RoutedCommand newCmd = new RoutedCommand();
            newCmd.InputGestures.Add(new KeyGesture(Key.Escape));
            CommandBindings.Add(new CommandBinding(newCmd, escape_Keyed));
        }

        private void escape_Keyed(object sender, ExecutedRoutedEventArgs e)
        {
            App.Current.Shutdown();
        }

        private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isSelecting = true;
            clickedPoint = e.GetPosition(this);
            selectBorder.Height = 1;
            selectBorder.Width = 1;

            try { RegionClickCanvas.Children.Remove(selectBorder); } catch (Exception) { }

            selectBorder.BorderThickness = new Thickness(2);
            System.Windows.Media.Color borderColor = System.Windows.Media.Color.FromArgb(255, 40, 118, 126);
            selectBorder.BorderBrush = new SolidColorBrush(borderColor);
            RegionClickCanvas.Children.Add(selectBorder);
            Canvas.SetLeft(selectBorder, clickedPoint.X);
            Canvas.SetTop(selectBorder, clickedPoint.Y);
        }

        private void RegionClickCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting == false)
                return;

            System.Windows.Point movingPoint = e.GetPosition(this);

            double xDelta = movingPoint.X - clickedPoint.X;
            double yDelta = movingPoint.Y - clickedPoint.Y;

            // X and Y postive
            if(xDelta > 0 && yDelta > 0)
            {
                selectBorder.Width = Math.Abs(movingPoint.X - clickedPoint.X);
                selectBorder.Height = Math.Abs(movingPoint.Y - clickedPoint.Y);
            }
            // X negative Y positive
            if(xDelta < 0 && yDelta > 0)
            {
                Canvas.SetLeft(selectBorder, clickedPoint.X - Math.Abs(xDelta));

                selectBorder.Width = Math.Abs(movingPoint.X - clickedPoint.X);
                selectBorder.Height = Math.Abs(movingPoint.Y - clickedPoint.Y);
            }
            // X postive Y negative
            if(xDelta > 0 && yDelta < 0)
            {
                Canvas.SetTop(selectBorder, clickedPoint.Y - Math.Abs(yDelta));

                selectBorder.Width = Math.Abs(movingPoint.X - clickedPoint.X);
                selectBorder.Height = Math.Abs(movingPoint.Y - clickedPoint.Y);
            }
            // X and Y negative
            if(xDelta < 0 && yDelta < 0)
            {
                Canvas.SetLeft(selectBorder, clickedPoint.X - Math.Abs(xDelta));
                Canvas.SetTop(selectBorder, clickedPoint.Y - Math.Abs(yDelta));
                
                selectBorder.Width = Math.Abs(movingPoint.X - clickedPoint.X);
                selectBorder.Height = Math.Abs(movingPoint.Y - clickedPoint.Y);
            }
        }

        private async void RegionClickCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isSelecting = false;
            Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;

            var mPt = GetMousePos();
            System.Windows.Point movingPoint = e.GetPosition(this);
            movingPoint.X *= m.M11;
            movingPoint.Y *= m.M22;

            movingPoint.X = Math.Round(movingPoint.X);
            movingPoint.Y = Math.Round(movingPoint.Y);

            if (mPt == movingPoint)
                Debug.WriteLine("Probably on Screen 1");

            double correctedLeft = this.Left;
            double correctedTop = this.Top;

            if (correctedLeft < 0)
                correctedLeft = 0;

            if (correctedTop < 0)
                correctedTop = 0;

            double xDimScaled = (Canvas.GetLeft(selectBorder) * m.M11);
            double yDimScaled = (Canvas.GetTop(selectBorder) * m.M22);

            Rectangle regionScaled = new Rectangle(
                (int)xDimScaled,
                (int)yDimScaled,
                (int)(selectBorder.Width * m.M11),
                (int)(selectBorder.Height * m.M22) );

            string grabbedText = "";

            // remove selectBorder before capture - force screen Re-render to actually remove it
            try { RegionClickCanvas.Children.Remove(selectBorder); } catch { }
            RegionClickCanvas.Background.Opacity = 0;
            RegionClickCanvas.UpdateLayout();
            RegionClickCanvas.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

            if (regionScaled.Width < 3 || regionScaled.Height < 3)
                grabbedText = await GetClickedWord(new System.Windows.Point(xDimScaled, yDimScaled));
            else
                grabbedText = await GetRegionsText(regionScaled);

            if (string.IsNullOrWhiteSpace(grabbedText) == false)
            {
                Clipboard.SetText(grabbedText);
                if(Settings.Default.ShowToast)
                    ShowToast(grabbedText);
                App.Current.Shutdown();
            }
            else
                RegionClickCanvas.Background.Opacity = .2;
        }
    }
}
