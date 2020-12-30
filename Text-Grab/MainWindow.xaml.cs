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

        private async Task<string> GetRegionsText(Rectangle selectedRegion)
        {
            Bitmap bmp = new Bitmap(selectedRegion.Width, selectedRegion.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);
            g.CopyFromScreen(selectedRegion.Left, selectedRegion.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

            string ocrText = await ExtractText(bmp, InstalledLanguages.FirstOrDefault());
            ocrText.Trim();

            return ocrText;
        }

        private async Task<string> GetClickedWord(System.Windows.Point clickedPoint)
        {
            Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
            Bitmap bmp = new Bitmap((int)(this.ActualWidth * m.M11), (int)(this.ActualHeight * m.M22), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmp);
            g.CopyFromScreen(0, 0, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

            string ocrText = await ExtractText(bmp, InstalledLanguages.FirstOrDefault(), clickedPoint);
            ocrText.Trim();

            return ocrText;
        }

        public async Task<string> ExtractText(Bitmap bmp, string languageCode, System.Windows.Point? singlePoint = null)
        {

            if (!GlobalizationPreferences.Languages.Contains(languageCode))
                throw new ArgumentOutOfRangeException($"{languageCode} is not installed.");

            StringBuilder text = new StringBuilder();

            await using (MemoryStream memory = new MemoryStream())
            {
                bmp.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(memory.AsRandomAccessStream());
                Windows.Graphics.Imaging.SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

                OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(new Language(languageCode));
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

            string grabbedText = "";

            RegionClickCanvas.Background.Opacity = 0;

            if (regionScaled.Width < 3 || regionScaled.Height < 3)
                grabbedText = await GetClickedWord(new System.Windows.Point(clickedPoint.X * m.M11, clickedPoint.Y * m.M22));
            else
                grabbedText = await GetRegionsText(regionScaled);

            RegionClickCanvas.Children.Remove(selectBorder);
            if (string.IsNullOrWhiteSpace(grabbedText) == false)
            {
                Clipboard.SetText(grabbedText);
                this.Close();
            }
            else
                RegionClickCanvas.Background.Opacity = .2;
        }
    }
}
