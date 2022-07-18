using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Text_Grab.Views;

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

    private bool isComboBoxReady = false;

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

        SetImageToBackground();

        if (Settings.Default.FSGMakeSingleLineToggle == true)
            SingleLineMenuItem.IsChecked = true;

        TopButtonsStackPanel.Visibility = Visibility.Visible;

        LoadOcrLanguages();
    }

    private void LoadOcrLanguages()
    {
        if (LanguagesComboBox.Items.Count > 0)
            return;

        IReadOnlyList<Language> possibleOCRLangs = OcrEngine.AvailableRecognizerLanguages;
        Language? firstLang = ImageMethods.GetOCRLanguage();

        int count = 0;

        foreach (Language language in possibleOCRLangs)
        {
            LanguagesComboBox.Items.Add(language);

            if (language.LanguageTag == firstLang?.LanguageTag)
                LanguagesComboBox.SelectedIndex = count;
            
            count++;
        }

        isComboBoxReady = true;
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
        WindowUtilities.FullscreenKeyDown(e.Key);
    }

    private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.RightButton == MouseButtonState.Pressed)
            return;

        isSelecting = true;
        TopButtonsStackPanel.Visibility = Visibility.Collapsed;
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
        bool isActive = false;
        if (sender is ToggleButton tb && tb.IsChecked is not null)
            isActive = tb.IsChecked.Value;
        else if (sender is MenuItem mi)
            isActive = mi.IsChecked;

        WindowUtilities.FullscreenKeyDown(Key.G, isActive);
    }

    private void NewEditTextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
        WindowUtilities.CloseAllFullscreenGrabs();
    }

    private void FreezeMenuItem_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        bool isActive = false;
        if (sender is ToggleButton tb && tb.IsChecked is not null)
            isActive = tb.IsChecked.Value;
        else if (sender is MenuItem mi)
            isActive = mi.IsChecked;

        WindowUtilities.FullscreenKeyDown(Key.F, isActive);
    }

    private async void FreezeUnfreeze(bool Activate)
    {
        if (FreezeMenuItem.IsChecked == true)
        {
            TopButtonsStackPanel.Visibility = Visibility.Collapsed;
            BackgroundBrush.Opacity = 0;
            RegionClickCanvas.ContextMenu.IsOpen = false;
            await Task.Delay(150);
            SetImageToBackground();
            TopButtonsStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            BackgroundImage.Source = null;
        }
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

    internal void KeyPressed(Key key, bool? isActive = null)
    {
        switch (key)
        {
            // This case is handled in the WindowUtilities.FullscreenKeyDown
            // case Key.Escape:
            //     WindowUtilities.CloseAllFullscreenGrabs();
            //     break;
            case Key.G:
                if (isActive == null)
                    NewGrabFrameMenuItem.IsChecked = !NewGrabFrameMenuItem.IsChecked;
                else
                    NewGrabFrameMenuItem.IsChecked = isActive.Value;
                break;
            case Key.S:
                if (isActive == null)
                    SingleLineMenuItem.IsChecked = !SingleLineMenuItem.IsChecked;
                else
                    SingleLineMenuItem.IsChecked = isActive.Value;

                Settings.Default.FSGMakeSingleLineToggle = SingleLineMenuItem.IsChecked;
                Settings.Default.Save();
                break;
            case Key.F:
                if (isActive == null)
                    FreezeMenuItem.IsChecked = !FreezeMenuItem.IsChecked;
                else
                    FreezeMenuItem.IsChecked = isActive.Value;

                FreezeUnfreeze(FreezeMenuItem.IsChecked);
                break;
            case Key.D1:
            case Key.D2:
            case Key.D3:
            case Key.D4:
            case Key.D5:
            case Key.D6:
            case Key.D7:
            case Key.D8:
            case Key.D9:
                int numberPressed = (int)key - 34; // D1 casts to 35, D2 to 36, etc.
                int numberOfLanguages = LanguagesComboBox.Items.Count;

                if (numberPressed <= numberOfLanguages 
                    && numberPressed - 1 >= 0
                    && numberPressed - 1 != LanguagesComboBox.SelectedIndex 
                    && isComboBoxReady == true)
                    LanguagesComboBox.SelectedIndex = numberPressed - 1;
                break;
            default:
                break;
        }
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
        TopButtonsStackPanel.Visibility = Visibility.Visible;

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
            Language? selectedOcrLang = LanguagesComboBox.SelectedItem as Language;
            grabbedText = await ImageMethods.GetClickedWord(this, new System.Windows.Point(xDimScaled, yDimScaled), selectedOcrLang);
        }
        else
        {
            Language? selectedOcrLang = LanguagesComboBox.SelectedItem as Language;
            grabbedText = await ImageMethods.GetRegionsText(this, regionScaled, selectedOcrLang);
        }

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

    private void SingleLineMenuItem_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        bool isActive = false;
        if (sender is ToggleButton tb && tb.IsChecked is not null)
            isActive = tb.IsChecked.Value;
        else if (sender is MenuItem mi)
            isActive = mi.IsChecked;

        WindowUtilities.FullscreenKeyDown(Key.S, isActive);
    }

    private void LanguagesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox languageCmbBox || isComboBoxReady == false)
            return;

        int selection = languageCmbBox.SelectedIndex;

        switch (selection)
        {
            case 0:
                WindowUtilities.FullscreenKeyDown(Key.D1);
                break;
            case 1:
                WindowUtilities.FullscreenKeyDown(Key.D2);
                break;
            case 2:
                WindowUtilities.FullscreenKeyDown(Key.D3);
                break;
            case 3:
                WindowUtilities.FullscreenKeyDown(Key.D4);
                break;
            case 4:
                WindowUtilities.FullscreenKeyDown(Key.D5);
                break;
            case 5:
                WindowUtilities.FullscreenKeyDown(Key.D6);
                break;
            case 6:
                WindowUtilities.FullscreenKeyDown(Key.D7);
                break;
            case 7:
                WindowUtilities.FullscreenKeyDown(Key.D8);
                break;
            case 8:
                WindowUtilities.FullscreenKeyDown(Key.D9);
                break;
            default:
                break;
        }
    }
}
