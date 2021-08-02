using Fasetto.Word;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Properties;
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
        private DispatcherTimer reDrawTimer = new DispatcherTimer();
        private bool isSelecting;
        private Point clickedPoint;
        private Border selectBorder = new Border();
        private System.Windows.Point GetMousePos() => this.PointToScreen(Mouse.GetPosition(this));

        public bool IsfromEditWindow { get; set; } = false;

        public GrabFrame()
        {
            InitializeComponent();

            WindowResizer resizer = new WindowResizer(this);
            reDrawTimer.Interval = new TimeSpan(0, 0, 0, 0, 1200);
            reDrawTimer.Tick += ReDrawTimer_Tick;

            RoutedCommand newCmd = new RoutedCommand();
            _ = newCmd.InputGestures.Add(new KeyGesture(Key.Escape));
            _ = CommandBindings.Add(new CommandBinding(newCmd, Escape_Keyed));
        }

        private void Escape_Keyed(object sender, ExecutedRoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text) == false && SearchBox.Text != "Search For Text...")
                SearchBox.Text = "";
            else if (RectanglesCanvas.Children.Count > 0)
                ResetGrabFrame();
            else
                Close();
        }

        private async void ReDrawTimer_Tick(object sender, EventArgs e)
        {
            reDrawTimer.Stop();
            ResetGrabFrame();

            TextBox searchBox = SearchBox;

            if (searchBox != null)
                await DrawRectanglesAroundWords(searchBox.Text);
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void GrabBTN_Click(object sender, RoutedEventArgs e)
        {
            string frameText = "";

            if (wordBorders.Where(w => w.IsSelected == true).ToList().Count == 0)
            {
                Point windowPosition = this.GetAbsolutePosition();
                DpiScale dpi = VisualTreeHelper.GetDpi(this);
                System.Drawing.Rectangle rectCanvasSize = new System.Drawing.Rectangle
                {
                    Width = (int)((ActualWidth + 2) * dpi.DpiScaleX),
                    Height = (int)((Height - 64) * dpi.DpiScaleY),
                    X = (int)((windowPosition.X - 2) * dpi.DpiScaleX),
                    Y = (int)((windowPosition.Y + 24) * dpi.DpiScaleY)
                };
                frameText = await ImageMethods.GetRegionsText(null, rectCanvasSize);
            }

            if (wordBorders.Count > 0)
            {
                List<WordBorder> selectedBorders = wordBorders.Where(w => w.IsSelected == true).ToList();

                if (selectedBorders.Count == 0)
                    selectedBorders.AddRange(wordBorders);

                List<string> wordsList = new List<string>();
                int lastLineNum = selectedBorders.FirstOrDefault().LineNumber;
                foreach (WordBorder border in selectedBorders)
                {
                    if (border.LineNumber != lastLineNum)
                    {
                        wordsList.Add(Environment.NewLine);
                        lastLineNum = border.LineNumber;
                    }

                    wordsList.Add(border.Word);
                }
                frameText = string.Join(' ', wordsList);
            }

            Clipboard.SetText(frameText);

            if (Settings.Default.ShowToast
                && IsfromEditWindow == false
                && string.IsNullOrWhiteSpace(frameText) == false)
                NotificationUtilities.ShowToast(frameText);

            if (IsfromEditWindow == true && string.IsNullOrWhiteSpace(frameText) == false)
                WindowUtilities.AddTextToOpenWindow(frameText);
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
            if (IsLoaded == false)
                return;

            ResetGrabFrame();

            reDrawTimer.Start();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsLoaded == false)
                return;

            ResetGrabFrame();

            reDrawTimer.Start();
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
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            System.Drawing.Rectangle rectCanvasSize = new System.Drawing.Rectangle
            {
                Width = (int)((ActualWidth + 2) * dpi.DpiScaleX),
                Height = (int)((ActualHeight - 64) * dpi.DpiScaleY),
                X = (int)((windowPosition.X - 2) * dpi.DpiScaleX),
                Y = (int)((windowPosition.Y + 24) * dpi.DpiScaleY)
            };

            if (ocrResultOfWindow == null || ocrResultOfWindow.Lines.Count == 0)
                ocrResultOfWindow = await ImageMethods.GetOcrResultFromRegion(rectCanvasSize);

            int numberOfMatches = 0;
            int lineNumber = 0;

            foreach (OcrLine ocrLine in ocrResultOfWindow.Lines)
            {
                foreach (OcrWord ocrWord in ocrLine.Words)
                {
                    string wordString = ocrWord.Text;

                    if (Settings.Default.CorrectErrors)
                        wordString = wordString.TryFixEveryWordLetterNumberErrors();

                    WordBorder wordBorderBox = new WordBorder
                    {
                        Width = (ocrWord.BoundingRect.Width / dpi.DpiScaleX) + 6,
                        Height = (ocrWord.BoundingRect.Height / dpi.DpiScaleY) + 6,
                        Word = wordString,
                        ToolTip = wordString,
                        LineNumber = lineNumber,
                        IsFromEditWindow = IsfromEditWindow
                    };

                    if ((bool)ExactMatchChkBx.IsChecked)
                    {
                        if (wordString == searchWord)
                        {
                            wordBorderBox.Select();
                            numberOfMatches++;
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(searchWord)
                            && wordString.ToLower().Contains(searchWord.ToLower()))
                        {
                            wordBorderBox.Select();
                            numberOfMatches++;
                        }
                    }

                    wordBorders.Add(wordBorderBox);
                    _ = RectanglesCanvas.Children.Add(wordBorderBox);
                    Canvas.SetLeft(wordBorderBox, (ocrWord.BoundingRect.Left / dpi.DpiScaleX) - 3);
                    Canvas.SetTop(wordBorderBox, (ocrWord.BoundingRect.Top / dpi.DpiScaleY) - 3);
                }

                lineNumber++;
            }

            if (ocrResultOfWindow != null && ocrResultOfWindow.TextAngle != null)
            {
                RotateTransform transform = new RotateTransform((double)ocrResultOfWindow.TextAngle)
                {
                    CenterX = (Width - 4) / 2,
                    CenterY = (Height - 60) / 2
                };
                RectanglesCanvas.RenderTransform = transform;
            }
            MatchesTXTBLK.Text = $"Matches: {numberOfMatches}";
            isDrawing = false;
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded == false)
                return;

            if (sender is TextBox searchBox)
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

        private void GrabFrameWindow_Activated(object sender, EventArgs e)
        {
            reDrawTimer.Start();
        }

        private void RectanglesCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isSelecting = true;
            clickedPoint = e.GetPosition(RectanglesCanvas);
            selectBorder.Height = 1;
            selectBorder.Width = 1;

            try { RectanglesCanvas.Children.Remove(selectBorder); } catch (Exception) { }

            selectBorder.BorderThickness = new Thickness(2);
            System.Windows.Media.Color borderColor = System.Windows.Media.Color.FromArgb(255, 40, 118, 126);
            selectBorder.BorderBrush = new SolidColorBrush(borderColor);
            _ = RectanglesCanvas.Children.Add(selectBorder);
            Canvas.SetLeft(selectBorder, clickedPoint.X);
            Canvas.SetTop(selectBorder, clickedPoint.Y);
        }

        private void RectanglesCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isSelecting = false;
            Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;

            System.Windows.Point mPt = GetMousePos();
            System.Windows.Point movingPoint = e.GetPosition(RectanglesCanvas);
            movingPoint.X *= m.M11;
            movingPoint.Y *= m.M22;

            movingPoint.X = Math.Round(movingPoint.X);
            movingPoint.Y = Math.Round(movingPoint.Y);

            if (mPt == movingPoint)
                Debug.WriteLine("Probably on Screen 1");

            double correctedLeft = Left;
            double correctedTop = Top;

            if (correctedLeft < 0)
                correctedLeft = 0;

            if (correctedTop < 0)
                correctedTop = 0;

            double xDimScaled = Canvas.GetLeft(selectBorder) * m.M11;
            double yDimScaled = Canvas.GetTop(selectBorder) * m.M22;

            System.Drawing.Rectangle regionScaled = new System.Drawing.Rectangle(
                (int)xDimScaled,
                (int)yDimScaled,
                (int)(selectBorder.Width * m.M11),
                (int)(selectBorder.Height * m.M22));

            try { RectanglesCanvas.Children.Remove(selectBorder); } catch { }
        }

        private void RectanglesCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting == false)
                return;

            System.Windows.Point movingPoint = e.GetPosition(RectanglesCanvas);
            
            double xDelta = movingPoint.X - clickedPoint.X;
            double yDelta = movingPoint.Y - clickedPoint.Y;

            // X and Y postive
            if (xDelta > 0 && yDelta > 0)
            {
                selectBorder.Width = Math.Abs(movingPoint.X - clickedPoint.X);
                selectBorder.Height = Math.Abs(movingPoint.Y - clickedPoint.Y);
            }
            // X negative Y positive
            if (xDelta < 0 && yDelta > 0)
            {
                Canvas.SetLeft(selectBorder, clickedPoint.X - Math.Abs(xDelta));

                selectBorder.Width = Math.Abs(movingPoint.X - clickedPoint.X);
                selectBorder.Height = Math.Abs(movingPoint.Y - clickedPoint.Y);
            }
            // X postive Y negative
            if (xDelta > 0 && yDelta < 0)
            {
                Canvas.SetTop(selectBorder, clickedPoint.Y - Math.Abs(yDelta));

                selectBorder.Width = Math.Abs(movingPoint.X - clickedPoint.X);
                selectBorder.Height = Math.Abs(movingPoint.Y - clickedPoint.Y);
            }
            // X and Y negative
            if (xDelta < 0 && yDelta < 0)
            {
                Canvas.SetLeft(selectBorder, clickedPoint.X - Math.Abs(xDelta));
                Canvas.SetTop(selectBorder, clickedPoint.Y - Math.Abs(yDelta));

                selectBorder.Width = Math.Abs(movingPoint.X - clickedPoint.X);
                selectBorder.Height = Math.Abs(movingPoint.Y - clickedPoint.Y);
            }

            CheckSelectBorderIntersections();
        }

        private void CheckSelectBorderIntersections()
        {
            Rect rectSelect = new Rect(Canvas.GetLeft(selectBorder), Canvas.GetTop(selectBorder), selectBorder.Width, selectBorder.Height);

            foreach (WordBorder wordBorder in wordBorders)
            {
                Rect wbRect= new Rect(Canvas.GetLeft(wordBorder), Canvas.GetTop(wordBorder), wordBorder.Width, wordBorder.Height);

                if (rectSelect.IntersectsWith(wbRect))
                    wordBorder.Select();
                else
                    wordBorder.Deselect();
            }
        }
    }
}
