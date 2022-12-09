using System;
using System.Collections.Generic;
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

    public TextBox? DestinationTextBox { get; set; }

    public string? textFromOCR { get; set; }

    public bool IsFreeze { get; set; } = false;

    private bool isComboBoxReady = false;

    public FullscreenGrab()
    {
        InitializeComponent();
    }

    public void SetImageToBackground()
    {
        BackgroundImage.Source = null;
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

        if (Settings.Default.FSGMakeSingleLineToggle)
            SingleLineMenuItem.IsChecked = true;

        TopButtonsStackPanel.Visibility = Visibility.Visible;

        LoadOcrLanguages();
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        BackgroundImage.Source = null;
        BackgroundImage.UpdateLayout();
        // EditWindow = null;
        currentScreen = null;
        dpiScale = null;
        Language = null;
        textFromOCR = null;

        this.Loaded -= Window_Loaded;
        this.Unloaded -= Window_Unloaded;

        RegionClickCanvas.MouseDown -= RegionClickCanvas_MouseDown;
        RegionClickCanvas.MouseMove -= RegionClickCanvas_MouseMove;
        RegionClickCanvas.MouseUp -= RegionClickCanvas_MouseUp;

        SingleLineMenuItem.Click -= SingleLineMenuItem_Click;
        FreezeMenuItem.Click -= FreezeMenuItem_Click;
        NewGrabFrameMenuItem.Click -= NewGrabFrameMenuItem_Click;
        NewEditTextMenuItem.Click -= NewEditTextMenuItem_Click;
        SettingsMenuItem.Click -= SettingsMenuItem_Click;
        CancelMenuItem.Click -= CancelMenuItem_Click;

        LanguagesComboBox.SelectionChanged -= LanguagesComboBox_SelectionChanged;

        SingleLineToggleButton.Click -= SingleLineMenuItem_Click;
        FreezeToggleButton.Click -= FreezeMenuItem_Click;
        NewGrabFrameToggleButton.Click -= NewGrabFrameMenuItem_Click;
        NewEditTextButton.Click -= NewEditTextMenuItem_Click;
        SettingsButton.Click -= SettingsMenuItem_Click;
        CancelButton.Click -= CancelMenuItem_Click;

        this.KeyDown -= FullscreenGrab_KeyDown;
        this.KeyUp -= FullscreenGrab_KeyUp;
    }

    private void LoadOcrLanguages()
    {
        if (LanguagesComboBox.Items.Count > 0)
            return;

        IReadOnlyList<Language> possibleOCRLangs = OcrEngine.AvailableRecognizerLanguages;
        Language firstLang = LanguageUtilities.GetOCRLanguage();

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

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<SettingsWindow>();
        WindowUtilities.CloseAllFullscreenGrabs();
    }

    private void SingleLineMenuItem_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);

        WindowUtilities.FullscreenKeyDown(Key.S, isActive);
    }

    private void NewGrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);

        WindowUtilities.FullscreenKeyDown(Key.G, isActive);
    }

    private static bool CheckIfCheckingOrUnchecking(object? sender)
    {
        bool isActive = false;
        if (sender is ToggleButton tb && tb.IsChecked is not null)
            isActive = tb.IsChecked.Value;
        else if (sender is MenuItem mi)
            isActive = mi.IsChecked;
        return isActive;
    }

    private void NewEditTextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
        WindowUtilities.CloseAllFullscreenGrabs();
    }

    private void FreezeMenuItem_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);

        WindowUtilities.FullscreenKeyDown(Key.F, isActive);
    }

    private async void FreezeUnfreeze(bool Activate)
    {
        if (FreezeMenuItem.IsChecked is true)
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
        System.Drawing.Point formsPoint = new((int)clickedPoint.X, (int)clickedPoint.Y);
        foreach (var scr in screens)
            if (scr.Bounds.Contains(formsPoint))
                currentScreen = scr;
    }

    private void RegionClickCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isSelecting)
            return;

        System.Windows.Point movingPoint = e.GetPosition(this);

        if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            PanSelection(movingPoint);
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
        if (!isSelecting)
            return;

        isSelecting = false;
        currentScreen = null;
        TopButtonsStackPanel.Visibility = Visibility.Visible;
        CursorClipper.UnClipCursor();
        RegionClickCanvas.ReleaseMouseCapture();
        clippingGeometry.Rect = new Rect(
            new System.Windows.Point(0, 0),
            new System.Windows.Size(0, 0));

        System.Windows.Point movingPoint = e.GetPosition(this);
        Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
        movingPoint.X *= m.M11;
        movingPoint.Y *= m.M22;

        movingPoint.X = Math.Round(movingPoint.X);
        movingPoint.Y = Math.Round(movingPoint.Y);

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

        string grabbedText = string.Empty;

        if (NewGrabFrameMenuItem.IsChecked is true)
        {
            PlaceGrabFrameInSelectionRect();
            return;
        }

        try { RegionClickCanvas.Children.Remove(selectBorder); } catch { }

        Language? selectedOcrLang = LanguagesComboBox.SelectedItem as Language;

        if (selectedOcrLang is null)
            selectedOcrLang = LanguageUtilities.GetOCRLanguage();

        if (regionScaled.Width < 3 || regionScaled.Height < 3)
        {
            BackgroundBrush.Opacity = 0;
            grabbedText = await OcrExtensions.GetClickedWord(this, new System.Windows.Point(xDimScaled, yDimScaled), selectedOcrLang);
        }
        else
            grabbedText = await OcrExtensions.GetRegionsText(this, regionScaled, selectedOcrLang);

        if (!string.IsNullOrWhiteSpace(grabbedText))
        {
            HandleTextFromOcr(grabbedText);
            WindowUtilities.CloseAllFullscreenGrabs();
        }
        else
            BackgroundBrush.Opacity = .2;
    }

    private void PanSelection(System.Windows.Point movingPoint)
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
                    && isComboBoxReady)
                    LanguagesComboBox.SelectedIndex = numberPressed - 1;
                break;
            default:
                break;
        }
    }

    private void PlaceGrabFrameInSelectionRect()
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
    }

    private void HandleTextFromOcr(string grabbedText)
    {
        if (Settings.Default.CorrectErrors)
            grabbedText.TryFixEveryWordLetterNumberErrors();

        if (SingleLineMenuItem.IsChecked is true)
            grabbedText = grabbedText.MakeStringSingleLine();

        textFromOCR = grabbedText;

        if (!Settings.Default.NeverAutoUseClipboard
            && DestinationTextBox is null)
            try { Clipboard.SetDataObject(grabbedText, true); } catch { }

        if (Settings.Default.ShowToast
            && DestinationTextBox is null)
            NotificationUtilities.ShowToast(grabbedText);

        if (DestinationTextBox is not null)
        {
            // Do it this way instead of append text because it inserts the text at the cursor
            // Then puts the cursor at the end of the newly added text
            // AppendText() just adds the text to the end no matter what.
            DestinationTextBox.SelectedText = grabbedText;
            DestinationTextBox.Select(DestinationTextBox.SelectionStart + grabbedText.Length, 0);
            DestinationTextBox.Focus();
        }
    }

    private void LanguagesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox languageCmbBox || !isComboBoxReady)
            return;

        Language? pickedLang = languageCmbBox.SelectedItem as Language;

        if (pickedLang != null)
        {
            Settings.Default.LastUsedLang = pickedLang.LanguageTag;
            Settings.Default.Save();
        }

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

    private void LanguagesComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            Settings.Default.LastUsedLang = String.Empty;
            Settings.Default.Save();
        }
    }
}
