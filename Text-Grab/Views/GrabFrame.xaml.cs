using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Text_Grab.Utilities;
using Windows.Media.Ocr;

namespace Text_Grab.Views
{
    /// <summary>
    /// Interaction logic for PersistentWindow.xaml
    /// </summary>
    public partial class GrabFrame : Window
    {
        private bool isDrawing = false;
        private OcrResult ocrResultOfWindow;
        private List<string> matchesList= new List<string>();

        public GrabFrame()
        {
            InitializeComponent();
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void GrabBTN_Click(object sender, RoutedEventArgs e)
        {
            string frameText = "";

            if(matchesList.Count == 0)
            {
                Point windowPosition = this.GetAbsolutePosition();
                var dpi = VisualTreeHelper.GetDpi(this);
                System.Drawing.Rectangle rectCanvasSize = new System.Drawing.Rectangle
                {
                    Width = (int)((this.ActualWidth + 2) * dpi.DpiScaleX),
                    Height = (int)((this.Height - 64) * dpi.DpiScaleY),
                    X = (int)((windowPosition.X - 2) * dpi.DpiScaleX),
                    Y = (int)((windowPosition.Y + 24) * dpi.DpiScaleY)
                };
                frameText = await ImageMethods.GetRegionsText(null, rectCanvasSize);
            }

            if (matchesList.Count > 0)
                frameText = string.Join('\n', matchesList.ToArray());

            NotificationUtilities.ShowToast(frameText);
        }

        private void ResetGrabFrame()
        {
            ocrResultOfWindow = null;
            RectanglesCanvas.Children.Clear();
            matchesList.Clear();
            MatchesTXTBLK.Text = "Matches: 0";
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (this.IsLoaded == false)
                return;

            ResetGrabFrame();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.IsLoaded == false)
                return;

            ResetGrabFrame();
        }

        private void GrabFrameWindow_Deactivated(object sender, EventArgs e)
        {
            ResetGrabFrame();
        }

        private async Task DrawRectanglesAroundWords(string searchWord)
        {
            if (isDrawing == true)
                return;

            isDrawing = true;

            RectanglesCanvas.Children.Clear();
            matchesList.Clear();

            Point windowPosition = this.GetAbsolutePosition();
            var dpi = VisualTreeHelper.GetDpi(this);
            System.Drawing.Rectangle rectCanvasSize = new System.Drawing.Rectangle
            {
                Width = (int)((this.ActualWidth + 2) * dpi.DpiScaleX),
                Height = (int)((this.Height - 64) * dpi.DpiScaleY),
                X = (int)((windowPosition.X - 2) * dpi.DpiScaleX),
                Y = (int)((windowPosition.Y + 24) * dpi.DpiScaleY)
            };

            if(ocrResultOfWindow == null || ocrResultOfWindow.Lines.Count == 0)
                ocrResultOfWindow = await ImageMethods.GetOcrResultFromRegion(rectCanvasSize);

            int numberOfMatches = 0;

            foreach (OcrLine ocrLine in ocrResultOfWindow.Lines)
            {
                foreach (OcrWord ocrWord in ocrLine.Words)
                {
                    Border wordborder = new Border
                    {
                        Width = (ocrWord.BoundingRect.Width / dpi.DpiScaleX) + 6,
                        Height = (ocrWord.BoundingRect.Height / dpi.DpiScaleY) + 6,
                        BorderBrush = new SolidColorBrush(Colors.Teal),
                        BorderThickness = new Thickness(2)
                    };

                    if((bool)ExactMatchChkBx.IsChecked)
                    {
                        if(ocrWord.Text == searchWord)
                        {
                            wordborder.BorderThickness = new Thickness(2);
                            wordborder.BorderBrush = new SolidColorBrush(Colors.Yellow);
                            numberOfMatches++;
                            matchesList.Add(ocrWord.Text);
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrWhiteSpace(searchWord) 
                            && ocrWord.Text.ToLower().Contains(searchWord.ToLower()))
                        {
                            wordborder.BorderThickness = new Thickness(2);
                            wordborder.BorderBrush = new SolidColorBrush(Colors.Yellow);
                            numberOfMatches++;
                            matchesList.Add(ocrWord.Text);
                        }
                    }

                    RectanglesCanvas.Children.Add(wordborder);
                    Canvas.SetLeft(wordborder, (ocrWord.BoundingRect.Left / dpi.DpiScaleX) - 3);
                    Canvas.SetTop(wordborder, (ocrWord.BoundingRect.Top / dpi.DpiScaleY) - 3);
                }
            }

            if(ocrResultOfWindow != null && ocrResultOfWindow.TextAngle != null)
            {
                RotateTransform transform = new RotateTransform((double)ocrResultOfWindow.TextAngle);
                transform.CenterX = (this.Width - 4) / 2;
                transform.CenterY = (this.Height -60) / 2;
                RectanglesCanvas.RenderTransform = transform;
            }
            MatchesTXTBLK.Text = $"Matches: {numberOfMatches}";
            isDrawing = false;
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox searchBox = sender as TextBox;

            if(searchBox != null)
                await DrawRectanglesAroundWords(searchBox.Text);
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = sender as TextBox;
            searchBox.Text = "";
        }

        private async void ExactMatchChkBx_Click(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = SearchBox;

            if (searchBox != null)
                await DrawRectanglesAroundWords(searchBox.Text);
        }

        private void ClearBTN_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
        }

        private async void RefreshBTN_Click(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = SearchBox;
            ResetGrabFrame();

            await Task.Delay(200);

            if (searchBox != null)
                await DrawRectanglesAroundWords(searchBox.Text);
        }
    }
}
