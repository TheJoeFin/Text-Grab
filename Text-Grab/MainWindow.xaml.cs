using System;
using System.Collections.Generic;
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
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.System.UserProfile;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

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

        public double WindowResizeZone { get; set; } = 32f;

        public List<string> InstalledLanguages => GlobalizationPreferences.Languages.ToList();

        private async void ScreenshotBTN_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Point windowPoint = screenshotGrid.PointToScreen(new System.Windows.Point(0, 0));
            Rectangle rect = new Rectangle((int)windowPoint.X + 2, (int)windowPoint.Y + 2, (int)screenshotGrid.ActualWidth - 4, (int)screenshotGrid.ActualHeight - 4);

            string text = await GetRegionsText(rect);

            Clipboard.SetText(text);

            MessageBox.Show(text, "OCR Text", MessageBoxButton.OK);            
        }

        private async Task<string> GetRegionsText(Rectangle selectedRegion)
        {
            Bitmap bmp = new Bitmap(selectedRegion.Width, selectedRegion.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);
            g.CopyFromScreen(selectedRegion.Left, selectedRegion.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            // ScreenshotImage.Source = BitmapToImageSource(bmp);

            string ocrText = await ExtractText(bmp, InstalledLanguages.FirstOrDefault());
            ocrText.Trim();

            return ocrText;
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
                var bmpDecoder = await BitmapDecoder.CreateAsync(memory.AsRandomAccessStream());
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
                var bmpDecoder = await BitmapDecoder.CreateAsync(fileStream.AsRandomAccessStream());
                var softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

                var ocrEngine = OcrEngine.TryCreateFromLanguage(new Language(languageCode));
                var ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

                foreach (var line in ocrResult.Lines) text.AppendLine(line.Text);
            }

            return text.ToString();
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
            this.Close();
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
                this.WindowState = WindowState.Maximized;
            else
                this.WindowState = WindowState.Normal;
        }


        private bool isSelecting = false;
        System.Windows.Point clickedPoint = new System.Windows.Point();
        Border selectBorder = new Border();

        private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isSelecting = true;
            clickedPoint = e.GetPosition(this);
            selectBorder.Height = 1;
            selectBorder.Width = 1;

            selectBorder.BorderThickness = new Thickness(1.5);
            selectBorder.BorderBrush = new SolidColorBrush(Colors.Green);
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

            double xDim = Canvas.GetLeft(selectBorder);
            double yDim = Canvas.GetTop(selectBorder);

            Rectangle region = new Rectangle((int)xDim, (int)yDim, (int)selectBorder.Width, (int)selectBorder.Height);
            Rectangle regionScaled = new Rectangle(
                (int)(region.X * m.M11),
                (int)(region.Y * m.M22),
                (int)(region.Width * m.M11),
                (int)(region.Height * m.M22) );

            string grabbedText = await GetRegionsText(regionScaled);

            RegionClickCanvas.Children.Remove(selectBorder);
            if (string.IsNullOrWhiteSpace(grabbedText) == false)
            {
                Clipboard.SetText(grabbedText);
                this.Close();
            }

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
