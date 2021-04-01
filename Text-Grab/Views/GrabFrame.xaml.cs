using Fasetto.Word;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Text_Grab.Controls;
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
        private ObservableCollection<WordBorder> wordBorders = new ObservableCollection<WordBorder>();

        public GrabFrame()
        {
            InitializeComponent();

            var resizer = new WindowResizer(this);
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void GrabBTN_Click(object sender, RoutedEventArgs e)
        {
            string frameText = "";

            if(wordBorders.Where(w => w.IsSelected == true).ToList().Count == 0)
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

            if (wordBorders.Count > 0)
            {
                var selectedBorders = wordBorders.Where(w => w.IsSelected == true).ToList();
                List<string> wordsList = new List<string>();
                foreach (WordBorder border in selectedBorders)
                {
                    wordsList.Add(border.Word);
                }
                frameText = string.Join('\n', wordsList);
            }

            Clipboard.SetText(frameText);

            if(App.AppSettings.ShowToast)
                NotificationUtilities.ShowToast(frameText);
        }

        private void ResetGrabFrame()
        {
            ocrResultOfWindow = null;
            RectanglesCanvas.Children.Clear();
            wordBorders.Clear();
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
            wordBorders.Clear();

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
                    WordBorder wordBorderBox = new WordBorder
                    {
                        Width = (ocrWord.BoundingRect.Width / dpi.DpiScaleX) + 6,
                        Height = (ocrWord.BoundingRect.Height / dpi.DpiScaleY) + 6,
                        Word = ocrWord.Text,
                        ToolTip = ocrWord.Text
                    };

                    if((bool)ExactMatchChkBx.IsChecked)
                    {
                        if(ocrWord.Text == searchWord)
                        {
                            wordBorderBox.Select();
                            numberOfMatches++;
                        }
                    }
                    else
                    {
                        if (!String.IsNullOrWhiteSpace(searchWord) 
                            && ocrWord.Text.ToLower().Contains(searchWord.ToLower()))
                        {
                            wordBorderBox.Select();
                            numberOfMatches++;
                        }
                    }

                    wordBorders.Add(wordBorderBox);
                    RectanglesCanvas.Children.Add(wordBorderBox);
                    Canvas.SetLeft(wordBorderBox, (ocrWord.BoundingRect.Left / dpi.DpiScaleX) - 3);
                    Canvas.SetTop(wordBorderBox, (ocrWord.BoundingRect.Top / dpi.DpiScaleY) - 3);
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
