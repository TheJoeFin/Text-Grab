using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace Text_Grab.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class FullscreenGrab : Window
{
    #region Fields

    private System.Windows.Point clickedPoint = new System.Windows.Point();
    private TextBox? destinationTextBox;
    private DpiScale? dpiScale;
    private bool isComboBoxReady = false;
    private bool isSelecting = false;
    private bool isShiftDown = false;
    private Border selectBorder = new Border();
    private double selectLeft;
    private double selectTop;
    private System.Windows.Point shiftPoint = new System.Windows.Point();
    private double xShiftDelta;
    private double yShiftDelta;
    private HistoryInfo? historyInfo;
    bool usingTesseract;

    #endregion Fields

    #region Constructors

    public FullscreenGrab()
    {
        InitializeComponent();
        App.SetTheme();
        usingTesseract = Settings.Default.UseTesseract && TesseractHelper.CanLocateTesseractExe();
    }

    #endregion Constructors

    #region Properties

    public TextBox? DestinationTextBox
    {
        get { return destinationTextBox; }
        set
        {
            destinationTextBox = value;
            if (destinationTextBox != null)
                SendToEditTextToggleButton.IsChecked = true;
            else
                SendToEditTextToggleButton.IsChecked = false;
        }
    }

    public bool IsFreeze { get; set; } = false;
    public string? textFromOCR { get; set; }
    private System.Windows.Forms.Screen? currentScreen { get; set; }

    #endregion Properties

    #region Methods

    public void SetImageToBackground()
    {
        BackgroundImage.Source = null;
        BackgroundImage.Source = ImageMethods.GetWindowBoundsImage(this);
        BackgroundBrush.Opacity = 0.2;
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
                if (isActive is null)
                    NewGrabFrameMenuItem.IsChecked = !NewGrabFrameMenuItem.IsChecked;
                else
                    NewGrabFrameMenuItem.IsChecked = isActive.Value;
                break;
            case Key.S:
                if (isActive is null)
                    SingleLineMenuItem.IsChecked = !SingleLineMenuItem.IsChecked;
                else
                    SingleLineMenuItem.IsChecked = isActive.Value;

                Settings.Default.FSGMakeSingleLineToggle = SingleLineMenuItem.IsChecked;
                Settings.Default.Save();
                break;
            case Key.E:
                if (isActive is null)
                    SendToEtwMenuItem.IsChecked = !SendToEtwMenuItem.IsChecked;
                else
                    SendToEtwMenuItem.IsChecked = isActive.Value;

                Settings.Default.FsgSendEtwToggle = SendToEtwMenuItem.IsChecked;
                Settings.Default.Save();
                break;
            case Key.F:
                if (isActive is null)
                    FreezeMenuItem.IsChecked = !FreezeMenuItem.IsChecked;
                else
                    FreezeMenuItem.IsChecked = isActive.Value;

                FreezeUnfreeze(FreezeMenuItem.IsChecked);
                break;
            case Key.T:
                if (usingTesseract)
                    return;

                if (isActive is null)
                    TableToggleButton.IsChecked = !TableToggleButton.IsChecked;
                else
                    TableToggleButton.IsChecked = isActive.Value;
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

    private static bool CheckIfCheckingOrUnchecking(object? sender)
    {
        bool isActive = false;
        if (sender is ToggleButton tb && tb.IsChecked is not null)
            isActive = tb.IsChecked.Value;
        else if (sender is MenuItem mi)
            isActive = mi.IsChecked;
        return isActive;
    }

    private void CancelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.CloseAllFullscreenGrabs();
    }

    private void EditLastGrab_Click(object sender, RoutedEventArgs e)
    {
        Close();
        Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame();
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

            if (IsMouseOver)
                TopButtonsStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            BackgroundImage.Source = null;
        }
    }

    private void FullscreenGrab_KeyDown(object sender, KeyEventArgs e)
    {
        WindowUtilities.FullscreenKeyDown(e.Key);
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

    private void GetDpiAdjustedRegionOfSelectBorder(out DpiScale dpi, out double posLeft, out double posTop)
    {
        System.Windows.Point absPosPoint = this.GetAbsolutePosition();
        dpi = VisualTreeHelper.GetDpi(this);
        int firstScreenBPP = System.Windows.Forms.Screen.AllScreens[0].BitsPerPixel;

        posLeft = Canvas.GetLeft(selectBorder) + (absPosPoint.X / dpi.PixelsPerDip);
        posTop = Canvas.GetTop(selectBorder) + (absPosPoint.Y / dpi.PixelsPerDip);
    }

    private void LanguagesComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            Settings.Default.LastUsedLang = String.Empty;
            Settings.Default.Save();
        }
    }

    private void LanguagesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox languageCmbBox || !isComboBoxReady)
            return;

        if (languageCmbBox.SelectedItem is TessLang tessLang)
        {
            Settings.Default.LastUsedLang = tessLang.DisplayName;
            Settings.Default.Save();

            TableMenuItem.Visibility = Visibility.Collapsed;
            TableToggleButton.Visibility = Visibility.Collapsed;
        }

        if (languageCmbBox.SelectedItem is Language pickedLang)
        {
            Settings.Default.LastUsedLang = pickedLang.LanguageTag;
            Settings.Default.Save();

            TableMenuItem.Visibility = Visibility.Visible;
            TableToggleButton.Visibility = Visibility.Visible;
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

    private async void LoadOcrLanguages()
    {
        if (LanguagesComboBox.Items.Count > 0)
            return;

        int count = 0;

        bool haveSetLastLang = false;
        string lastTextLang = Settings.Default.LastUsedLang;
        if (usingTesseract)
        {
            List<ILanguage> tesseractLanguages = await TesseractHelper.TesseractLanguages();

            foreach (ILanguage language in tesseractLanguages)
            {
                LanguagesComboBox.Items.Add(language);

                if (!haveSetLastLang && language.DisplayName == lastTextLang)
                {
                    LanguagesComboBox.SelectedIndex = count;
                    haveSetLastLang = true;

                    TableMenuItem.Visibility = Visibility.Collapsed;
                    TableToggleButton.Visibility = Visibility.Collapsed;
                }

                count++;
            }
            if (LanguagesComboBox.SelectedIndex == -1)
                LanguagesComboBox.SelectedIndex = 0;
        }

        IReadOnlyList<Language> possibleOCRLanguages = OcrEngine.AvailableRecognizerLanguages;

        Language firstLang = LanguageUtilities.GetOCRLanguage();

        foreach (Language language in possibleOCRLanguages)
        {
            LanguagesComboBox.Items.Add(language);

            if (!haveSetLastLang &&
                (language.AbbreviatedName.ToLower() == firstLang?.AbbreviatedName.ToLower()
                || language.LanguageTag.ToLower() == firstLang?.LanguageTag.ToLower()))
            {
                LanguagesComboBox.SelectedIndex = count;
                haveSetLastLang = true;
            }

            count++;
        }

        isComboBoxReady = true;
    }

    private void NewEditTextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
        WindowUtilities.CloseAllFullscreenGrabs();
    }

    private void NewGrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);
        WindowUtilities.FullscreenKeyDown(Key.G, isActive);
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

    private void PlaceGrabFrameInSelectionRect()
    {
        // Make a new GrabFrame and show it on screen
        // Then place it where the user just drew the region
        // Add space around the window to account for Titlebar
        // bottom bar and width of GrabFrame
        DpiScale dpi;
        double posLeft, posTop;
        GetDpiAdjustedRegionOfSelectBorder(out dpi, out posLeft, out posTop);

        GrabFrame grabFrame = new()
        {
            Left = posLeft,
            Top = posTop,
        };

        grabFrame.Left -= (2 / dpi.PixelsPerDip);
        grabFrame.Top -= (48 / dpi.PixelsPerDip);

        if (destinationTextBox is not null)
            grabFrame.DestinationTextBox = destinationTextBox;

        grabFrame.TableToggleButton.IsChecked = TableToggleButton.IsChecked;
        if (selectBorder.Width > 20 && selectBorder.Height > 20)
        {
            grabFrame.Width = selectBorder.Width + 4;
            grabFrame.Height = selectBorder.Height + 74;
        }
        grabFrame.Show();
        grabFrame.Activate();
        WindowUtilities.CloseAllFullscreenGrabs();
    }

    private void RegionClickCanvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        EditLastGrabMenuItem.IsEnabled = Singleton<HistoryService>.Instance.HasAnyHistoryWithImages();
    }

    private void RegionClickCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        TopButtonsStackPanel.Visibility = Visibility.Collapsed;
    }

    private void RegionClickCanvas_MouseEnter(object sender, MouseEventArgs e)
    {
        TopButtonsStackPanel.Visibility = Visibility.Visible;
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

        string tessTag = string.Empty;

        if (LanguagesComboBox.SelectedItem is TessLang tessLang)
            tessTag = tessLang.LanguageTag;

        bool isSmallClick = (regionScaled.Width < 3 || regionScaled.Height < 3);

        bool isSingleLine = SingleLineMenuItem is null ? false : SingleLineMenuItem.IsChecked;
        bool isTable = TableMenuItem is null ? false : TableMenuItem.IsChecked;

        if (isSmallClick)
        {
            BackgroundBrush.Opacity = 0;
            grabbedText = await OcrUtilities.GetClickedWordAsync(this, new System.Windows.Point(xDimScaled, yDimScaled), selectedOcrLang);
        }
        else if (isTable)
            grabbedText = await OcrUtilities.GetRegionsTextAsTableAsync(this, regionScaled, selectedOcrLang);
        else
            grabbedText = await OcrUtilities.GetRegionsTextAsync(this, regionScaled, selectedOcrLang, tessTag);

        if (Settings.Default.UseHistory && !isSmallClick)
        {
            GetDpiAdjustedRegionOfSelectBorder(out DpiScale dpi, out double posLeft, out double posTop);

            Rect historyRect = new()
            {
                X = posLeft,
                Y = posTop,
                Width = selectBorder.Width,
                Height = selectBorder.Height,
            };

            historyInfo = new()
            {
                ID = Guid.NewGuid().ToString(),
                DpiScaleFactor = m.M11,
                LanguageTag = selectedOcrLang.LanguageTag,
                CaptureDateTime = DateTimeOffset.Now,
                PositionRect = historyRect,
                IsTable = TableToggleButton.IsChecked!.Value,
                TextContent = grabbedText,
                ImageContent = Singleton<HistoryService>.Instance.CachedBitmap,
                SourceMode = TextGrabMode.Fullscreen,
            };
        }

        if (!string.IsNullOrWhiteSpace(grabbedText))
        {
            if (SendToEditTextToggleButton.IsChecked is true && destinationTextBox is null)
            {
                EditTextWindow etw = WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
                destinationTextBox = etw.PassedTextControl;
            }

            OutputUtilities.HandleTextFromOcr(
                grabbedText,
                isSingleLine,
                isTable,
                destinationTextBox);
            WindowUtilities.CloseAllFullscreenGrabs();
        }
        else
        {
            BackgroundBrush.Opacity = .2;

            if (IsMouseOver)
                TopButtonsStackPanel.Visibility = Visibility.Visible;
        }
    }

    private void SendToEditTextToggleButton_Click(object sender, RoutedEventArgs e)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);
        WindowUtilities.FullscreenKeyDown(Key.E, isActive);
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

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (historyInfo is not null)
            Singleton<HistoryService>.Instance.SaveToHistory(historyInfo);
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

        if (Settings.Default.FsgSendEtwToggle)
            SendToEditTextToggleButton.IsChecked = true;

        if (IsMouseOver)
            TopButtonsStackPanel.Visibility = Visibility.Visible;

#if DEBUG
        Topmost = false;
#endif

        LoadOcrLanguages();
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        BackgroundImage.Source = null;
        BackgroundImage.UpdateLayout();
        currentScreen = null;
        dpiScale = null;
        textFromOCR = null;

        this.Loaded -= Window_Loaded;
        this.Unloaded -= Window_Unloaded;

        RegionClickCanvas.MouseDown -= RegionClickCanvas_MouseDown;
        RegionClickCanvas.MouseMove -= RegionClickCanvas_MouseMove;
        RegionClickCanvas.MouseUp -= RegionClickCanvas_MouseUp;

        SingleLineMenuItem.Click -= SingleLineMenuItem_Click;
        FreezeMenuItem.Click -= FreezeMenuItem_Click;
        NewGrabFrameMenuItem.Click -= NewGrabFrameMenuItem_Click;
        SendToEtwMenuItem.Click -= NewEditTextMenuItem_Click;
        SettingsMenuItem.Click -= SettingsMenuItem_Click;
        CancelMenuItem.Click -= CancelMenuItem_Click;

        LanguagesComboBox.SelectionChanged -= LanguagesComboBox_SelectionChanged;

        SingleLineToggleButton.Click -= SingleLineMenuItem_Click;
        FreezeToggleButton.Click -= FreezeMenuItem_Click;
        NewGrabFrameToggleButton.Click -= NewGrabFrameMenuItem_Click;
        SendToEditTextToggleButton.Click -= NewEditTextMenuItem_Click;
        SettingsButton.Click -= SettingsMenuItem_Click;
        CancelButton.Click -= CancelMenuItem_Click;

        this.KeyDown -= FullscreenGrab_KeyDown;
        this.KeyUp -= FullscreenGrab_KeyUp;
    }
    #endregion Methods
}
