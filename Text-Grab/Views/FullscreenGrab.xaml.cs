using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class FullscreenGrab : Window
    {
        private bool isSelecting = false;
        private bool isShiftDown = false;
        private System.Windows.Point clickedPoint = new System.Windows.Point();
        private System.Windows.Point shiftPoint = new System.Windows.Point();
        private Border selectBorder = new Border();

        private System.Windows.Point GetMousePos() => this.PointToScreen(Mouse.GetPosition(this));

        public bool IsFromEditWindow { get; set; } = false;

        public FullscreenGrab()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Maximized;

            this.KeyDown += FullscreenGrab_KeyDown;
            this.KeyUp += FullscreenGrab_KeyUp;
        }

        private void FullscreenGrab_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.LeftShift:
                    isShiftDown = false;
                    break;
                case Key.RightShift:
                    isShiftDown = false;
                    break;
                default:
                    break;
            }
        }

        private void FullscreenGrab_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.LeftShift:
                    isShiftDown = true;
                    shiftPoint = Mouse.GetPosition(this);
                    break;
                case Key.RightShift:
                    isShiftDown = true;
                    shiftPoint = Mouse.GetPosition(this);
                    break;
                case Key.Escape:
                    WindowUtilities.CloseAllFullscreenGrabs();
                    break;
                default:
                    break;
            }
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
            _ = RegionClickCanvas.Children.Add(selectBorder);
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

            if (isShiftDown == true)
            {
                Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
                double xShiftDelta = (movingPoint.X - shiftPoint.X) / m.M11;
                double yShiftDelta = (movingPoint.Y - shiftPoint.Y) / m.M22;
                double selectLeft = Canvas.GetLeft(selectBorder);
                double selectTop = Canvas.GetTop(selectBorder);
                Canvas.SetLeft(selectBorder, selectLeft + xShiftDelta);
                Canvas.SetTop(selectBorder, selectTop + yShiftDelta);
                return;
            }

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
        }

        private async void RegionClickCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isSelecting = false;
            Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;

            System.Windows.Point mPt = GetMousePos();
            System.Windows.Point movingPoint = e.GetPosition(this);
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

            Rectangle regionScaled = new Rectangle(
                (int)xDimScaled,
                (int)yDimScaled,
                (int)(selectBorder.Width * m.M11),
                (int)(selectBorder.Height * m.M22));

            string grabbedText = "";

            // remove selectBorder before capture - force screen Re-render to actually remove it
            try { RegionClickCanvas.Children.Remove(selectBorder); } catch { }
            RegionClickCanvas.Background.Opacity = 0;
            RegionClickCanvas.UpdateLayout();
            RegionClickCanvas.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

            if (regionScaled.Width < 3 || regionScaled.Height < 3)
                grabbedText = await ImageMethods.GetClickedWord(this, new System.Windows.Point(xDimScaled, yDimScaled));
            else
                grabbedText = await ImageMethods.GetRegionsText(this, regionScaled);

            if (Settings.Default.CorrectErrors)
                grabbedText.TryFixEveryWordLetterNumberErrors();

            if (string.IsNullOrWhiteSpace(grabbedText) == false)
            {
                Clipboard.SetText(grabbedText);
                if (Settings.Default.ShowToast && IsFromEditWindow == false)
                    NotificationUtilities.ShowToast(grabbedText);

                if (IsFromEditWindow == true)
                    WindowUtilities.AddTextToOpenWindow(grabbedText);

                WindowUtilities.CloseAllFullscreenGrabs();
            }
            else
            {
                RegionClickCanvas.Background.Opacity = .2;
            }
        }
    }
}
