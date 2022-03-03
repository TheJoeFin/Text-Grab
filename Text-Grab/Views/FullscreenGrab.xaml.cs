using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        private System.Windows.Forms.Screen? currentScreen { get; set; }

        private DpiScale? dpiScale;

        private System.Windows.Point GetMousePos() => this.PointToScreen(Mouse.GetPosition(this));

        double selectLeft;
        double selectTop;

        double xShiftDelta;
        double yShiftDelta;

        public EditTextWindow? EditWindow { get; set; }

        public string? textFromOCR;

        public bool IsFreeze { get; set; } = false;

        public FullscreenGrab()
        {
            InitializeComponent();
        }

        public void SetImageToBackground()
        {
            BackgroundImage.Source = ImageMethods.GetWindowBoundsImage(this);
            BackgroundBrush.Opacity = 0.2;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Maximized;
            FullWindow.Rect = new System.Windows.Rect(0, 0, Width, Height);
            this.KeyDown += FullscreenGrab_KeyDown;
            this.KeyUp += FullscreenGrab_KeyUp;

            if (IsFreeze == false)
                BackgroundBrush.Opacity = 0.2;

            if (Settings.Default.FSGMakeSingleLineToggle == true)
                SingleLineMenuItem.IsChecked = true;
        }

        private void FullscreenGrab_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.LeftShift:
                    isShiftDown = false;
                    clickedPoint = new System.Windows.Point(clickedPoint.X + xShiftDelta, clickedPoint.Y + yShiftDelta);
                    break;
                case Key.RightShift:
                    isShiftDown = false;
                    clickedPoint = new System.Windows.Point(clickedPoint.X + xShiftDelta, clickedPoint.Y + yShiftDelta);
                    break;
                default:
                    break;
            }
        }

        private void FullscreenGrab_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    WindowUtilities.CloseAllFullscreenGrabs();
                    break;
                default:
                    break;
            }
        }

        private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
                return;

            isSelecting = true;
            RegionClickCanvas.CaptureMouse();
            CursorClipper.ClipCursor(this);
            clickedPoint = e.GetPosition(this);
            selectBorder.Height = 1;
            selectBorder.Width = 1;

            dpiScale = VisualTreeHelper.GetDpi(this);

            try { RegionClickCanvas.Children.Remove(selectBorder); } catch (Exception) { }

            selectBorder.BorderThickness = new Thickness(2);
            System.Windows.Media.Color borderColor = System.Windows.Media.Color.FromArgb(255, 40, 118, 126);
            selectBorder.BorderBrush = new SolidColorBrush(borderColor);
            _ = RegionClickCanvas.Children.Add(selectBorder);
            Canvas.SetLeft(selectBorder, clickedPoint.X);
            Canvas.SetTop(selectBorder, clickedPoint.Y);

            var screens = System.Windows.Forms.Screen.AllScreens;
            System.Drawing.Point formsPoint = new System.Drawing.Point((int)clickedPoint.X, (int)clickedPoint.Y);
            foreach (var scr in screens)
            {
                if (scr.Bounds.Contains(formsPoint))
                    currentScreen = scr;
            }

            if (currentScreen is not null)
            {
                Debug.WriteLine($"Current screen: Left{currentScreen.Bounds.Left} Right{currentScreen.Bounds.Right} Top{currentScreen.Bounds.Top} Bottom{currentScreen.Bounds.Bottom}");
                Debug.WriteLine($"ClickedPoint X{clickedPoint.X} Y{clickedPoint.Y}");
            }

        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            WindowUtilities.OpenOrActivateWindow<SettingsWindow>();
            WindowUtilities.CloseAllFullscreenGrabs();
        }

        private void NewGrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // WindowUtilities.OpenOrActivateWindow<GrabFrame>();
            // WindowUtilities.CloseAllFullscreenGrabs();
        }

        private void NewEditTextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
            WindowUtilities.CloseAllFullscreenGrabs();
        }

        private async void FreezeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            BackgroundBrush.Opacity = 0;
            RegionClickCanvas.ContextMenu.IsOpen = false;
            await Task.Delay(150);
            SetImageToBackground();
        }

        private void CancelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            WindowUtilities.CloseAllFullscreenGrabs();
        }

        private void RegionClickCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isSelecting)
            {
                return;
            }

            System.Windows.Point movingPoint = e.GetPosition(this);

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                if (!isShiftDown)
                {
                    shiftPoint = movingPoint;
                    selectLeft = Canvas.GetLeft(selectBorder);
                    selectTop = Canvas.GetTop(selectBorder);
                }

                isShiftDown = true;
                xShiftDelta = (movingPoint.X - shiftPoint.X);
                yShiftDelta = (movingPoint.Y - shiftPoint.Y);

                double leftValue = selectLeft + xShiftDelta;
                double topValue = selectTop + yShiftDelta;

                if (currentScreen is not null && dpiScale is not null)
                {
                    double currentScreenLeft = currentScreen.Bounds.Left; // Should always be 0
                    double currentScreenRight = currentScreen.Bounds.Right / dpiScale.Value.DpiScaleX;
                    double currentScreenTop = currentScreen.Bounds.Top; // Should always be 0
                    double currentScreenBottom = currentScreen.Bounds.Bottom / dpiScale.Value.DpiScaleY;

                    leftValue = Math.Clamp(leftValue, currentScreenLeft, (currentScreenRight - selectBorder.Width));
                    topValue = Math.Clamp(topValue, currentScreenTop, (currentScreenBottom - selectBorder.Height));
                }

                clippingGeometry.Rect = new Rect(
                    new System.Windows.Point(leftValue, topValue),
                    new System.Windows.Size(selectBorder.Width - 2, selectBorder.Height - 2));
                Canvas.SetLeft(selectBorder, leftValue - 1);
                Canvas.SetTop(selectBorder, topValue - 1);
                return;
            }

            isShiftDown = false;

            var left = Math.Min(clickedPoint.X, movingPoint.X);
            var top = Math.Min(clickedPoint.Y, movingPoint.Y);

            selectBorder.Height = Math.Max(clickedPoint.Y, movingPoint.Y) - top;
            selectBorder.Width = Math.Max(clickedPoint.X, movingPoint.X) - left;
            selectBorder.Height = selectBorder.Height + 2;
            selectBorder.Width = selectBorder.Width + 2;

            clippingGeometry.Rect = new Rect(
                new System.Windows.Point(left, top),
                new System.Windows.Size(selectBorder.Width - 2, selectBorder.Height - 2));
            Canvas.SetLeft(selectBorder, left - 1);
            Canvas.SetTop(selectBorder, top - 1);
        }

        private async void RegionClickCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isSelecting == false)
                return;

            isSelecting = false;
            currentScreen = null;
            CursorClipper.UnClipCursor();
            RegionClickCanvas.ReleaseMouseCapture();
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

            if (NewGrabFrameMenuItem.IsChecked == true)
            {
                // Make a new GrabFrame and show it on screen
                // Then place it where the user just drew the region
                // Add space around the window to account for titlebar
                // bottom bar and width of GrabFrame
                System.Windows.Point absPosPoint = this.GetAbsolutePosition();
                DpiScale dpi = VisualTreeHelper.GetDpi(this);
                int firstScreenBPP = System.Windows.Forms.Screen.AllScreens[0].BitsPerPixel;
                GrabFrame grabFrame = new();
                grabFrame.Show();
                double posLeft = Canvas.GetLeft(selectBorder); // * dpi.DpiScaleX;
                double posTop = Canvas.GetTop(selectBorder); // * dpi.DpiScaleY;
                grabFrame.Left = posLeft + (absPosPoint.X / dpi.PixelsPerDip);
                grabFrame.Top = posTop + (absPosPoint.Y / dpi.PixelsPerDip);

                grabFrame.Left -= (2 / dpi.PixelsPerDip);
                grabFrame.Top -= (34 / dpi.PixelsPerDip);
                // if (grabFrame.Top < 0)
                //     grabFrame.Top = 0;

                if (selectBorder.Width > 20 && selectBorder.Height > 20)
                {
                    grabFrame.Width = selectBorder.Width + 4;
                    grabFrame.Height = selectBorder.Height + 72;
                }
                grabFrame.Activate();
                WindowUtilities.CloseAllFullscreenGrabs();
                return;
            }

            try { RegionClickCanvas.Children.Remove(selectBorder); } catch { }

            if (regionScaled.Width < 3 || regionScaled.Height < 3)
            {
                BackgroundBrush.Opacity = 0;
                grabbedText = await ImageMethods.GetClickedWord(this, new System.Windows.Point(xDimScaled, yDimScaled));
            }
            else
                grabbedText = await ImageMethods.GetRegionsText(this, regionScaled);

            if (Settings.Default.CorrectErrors)
                grabbedText.TryFixEveryWordLetterNumberErrors();

            if (SingleLineMenuItem.IsChecked == true)
                grabbedText = grabbedText.MakeStringSingleLine();

            if (string.IsNullOrWhiteSpace(grabbedText) == false)
            {
                textFromOCR = grabbedText;

                if (Settings.Default.NeverAutoUseClipboard == false
                    && EditWindow is null)
                    Clipboard.SetText(grabbedText);

                if (Settings.Default.ShowToast
                    && EditWindow is null)
                    NotificationUtilities.ShowToast(grabbedText);

                if (EditWindow is not null)
                    EditWindow.AddThisText(grabbedText);

                WindowUtilities.CloseAllFullscreenGrabs();
            }
            else
            {
                BackgroundBrush.Opacity = .2;
                clippingGeometry.Rect = new Rect(
                new System.Windows.Point(0, 0),
                new System.Windows.Size(0, 0));
            }
        }

        private void SingleLineMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem singleLineMenuItem)
            {
                Settings.Default.FSGMakeSingleLineToggle = singleLineMenuItem.IsChecked;
            }
        }
    }
}
