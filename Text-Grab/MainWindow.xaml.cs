using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.System.UserProfile;
using Color = System.Drawing.Color;

namespace Text_Grab
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public List<string> InstalledLanguages => GlobalizationPreferences.Languages.ToList();

        private async void ScreenshotBTN_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Point windowPoint = screenshotGrid.PointToScreen(new System.Windows.Point(0, 0));
            Rectangle rect = new Rectangle((int)windowPoint.X + 2, (int)windowPoint.Y + 2, (int)screenshotGrid.ActualWidth - 4, (int)screenshotGrid.ActualHeight - 4);
            Bitmap bmp = new Bitmap(rect.Width, rect.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            ScreenshotImage.Source = BitmapToImageSource(bmp);

            string ocrText = await ExtractText(bmp, InstalledLanguages.FirstOrDefault());
            ocrText.Trim();

            Clipboard.SetData(DataFormats.Text, ocrText);
            MessageBox.Show(ocrText, "OCR Text", MessageBoxButton.OK);
        }

        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        public async Task<string> ExtractText(Bitmap bmp, string languageCode)
        {

            if (!GlobalizationPreferences.Languages.Contains(languageCode))
                throw new ArgumentOutOfRangeException($"{languageCode} is not installed.");

            StringBuilder text = new StringBuilder();

            await using (MemoryStream memory = new MemoryStream())
            {
                bmp.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0; 
                var bmpDecoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(memory.AsRandomAccessStream());
                var softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

                var ocrEngine = OcrEngine.TryCreateFromLanguage(new Language(languageCode));
                var ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

                foreach (var line in ocrResult.Lines) text.AppendLine(line.Text);
            }

            return text.ToString();
        }

        public async Task<string> ExtractText(string imagePath, string languageCode)
        {

          if (!GlobalizationPreferences.Languages.Contains(languageCode))
                throw new ArgumentOutOfRangeException($"{languageCode} is not installed.");

            StringBuilder text = new StringBuilder();

            await using (var fileStream = File.OpenRead(imagePath))
            {
                var bmpDecoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(fileStream.AsRandomAccessStream());
                var softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

                var ocrEngine = OcrEngine.TryCreateFromLanguage(new Language(languageCode));
                var ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

                foreach (var line in ocrResult.Lines) text.AppendLine(line.Text);
            }

            return text.ToString();
        }

        private System.Windows.Point clickedPoint;
        private bool dragging = false;

        private void mainGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            clickedPoint = e.GetPosition(this);
            dragging = true;
        }

        private void mainGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (dragging == false)
                return;

            var pos = e.GetPosition(this);
            var pos2 = new System.Windows.Point(pos.X - clickedPoint.X, pos.Y - clickedPoint.Y);

            this.Left += pos2.X;
            this.Top += pos2.Y;
        }

        private void mainGrid_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            dragging = false;
        }

        private void Rectangle_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            dragging = false;
        }
    }

    public class DpiDecorator : Decorator
    {
        public DpiDecorator()
        {
            this.Loaded += (s, e) =>
            {
                Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
                ScaleTransform dpiTransform = new ScaleTransform(1 / m.M11, 1 / m.M22);
                if (dpiTransform.CanFreeze)
                    dpiTransform.Freeze();
                this.LayoutTransform = dpiTransform;
            };
        }
    }
}
