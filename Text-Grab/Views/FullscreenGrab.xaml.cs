using Dapplo.Windows.User32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Extensions;
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

    private System.Windows.Point clickedPoint = new();
    private TextBox? destinationTextBox;
    private DpiScale? dpiScale;
    private bool isComboBoxReady = false;
    private bool isSelecting = false;
    private bool isShiftDown = false;
    private readonly Border selectBorder = new();
    private double selectLeft;
    private double selectTop;
    private System.Windows.Point shiftPoint = new();
    private double xShiftDelta;
    private double yShiftDelta;
    private HistoryInfo? historyInfo;
    private readonly bool usingTesseract;
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    private const double MaxZoomScale = 16.0;
    private const double EdgePanThresholdPercent = 0.10;
    private const double EdgePanSpeed = 8.0;
    private readonly DispatcherTimer edgePanTimer;

    #endregion Fields

    #region Constructors

    public FullscreenGrab()
    {
        InitializeComponent();
        App.SetTheme();
        usingTesseract = DefaultSettings.UseTesseract && TesseractHelper.CanLocateTesseractExe();

        edgePanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        edgePanTimer.Tick += EdgePanTimer_Tick;
    }

    #endregion Constructors

    #region Properties

    public TextBox? DestinationTextBox
    {
        get => destinationTextBox;
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
    public string? TextFromOCR { get; set; }
    private DisplayInfo? CurrentScreen { get; set; }

    #endregion Properties

    #region Methods

    public void SetImageToBackground()
    {
        // Dispose old image source if it exists
        DisposeBitmapSource(BackgroundImage);

        BackgroundImage.Source = ImageMethods.GetWindowBoundsImage(this);
        // Honor user preference for shaded overlay while selecting
        BackgroundBrush.Opacity = DefaultSettings.FsgShadeOverlay ? 0.2 : 0.0;
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
                    NewGrabFrameToggleButton.IsChecked = !NewGrabFrameToggleButton.IsChecked;
                else
                    NewGrabFrameToggleButton.IsChecked = isActive.Value;

                if (NewGrabFrameToggleButton.IsChecked is true)
                    SelectSingleToggleButton(NewGrabFrameToggleButton);
                else
                    SelectSingleToggleButton();
                break;
            case Key.S:
                if (isActive is null)
                    SingleLineToggleButton.IsChecked = !SingleLineToggleButton.IsChecked;
                else
                    SingleLineToggleButton.IsChecked = isActive.Value;

                if (SingleLineToggleButton.IsChecked is true)
                    SelectSingleToggleButton(SingleLineToggleButton);
                else
                    SelectSingleToggleButton();

                bool isSingleLineChecked = false;
                if (SingleLineToggleButton.IsChecked is true)
                    isSingleLineChecked = true;
                DefaultSettings.FSGMakeSingleLineToggle = isSingleLineChecked;
                DefaultSettings.Save();
                break;
            case Key.E:
                if (isActive is null)
                    SendToEditTextToggleButton.IsChecked = !SendToEditTextToggleButton.IsChecked;
                else
                    SendToEditTextToggleButton.IsChecked = isActive.Value;

                bool isSendToEditChecked = false;
                if (SendToEditTextToggleButton.IsChecked is true)
                    isSendToEditChecked = true;
                DefaultSettings.FsgSendEtwToggle = isSendToEditChecked;
                DefaultSettings.Save();
                break;
            case Key.F:
                if (isActive is null)
                    FreezeMenuItem.IsChecked = !FreezeMenuItem.IsChecked;
                else
                    FreezeMenuItem.IsChecked = isActive.Value;

                FreezeUnfreeze(FreezeMenuItem.IsChecked);
                break;
            case Key.N:
                if (isActive is null)
                    StandardModeToggleButton.IsChecked = !StandardModeToggleButton.IsChecked;
                else
                    StandardModeToggleButton.IsChecked = isActive.Value;

                if (StandardModeToggleButton.IsChecked is true)
                    SelectSingleToggleButton(StandardModeToggleButton);
                else
                    SelectSingleToggleButton();

                bool isNormalChecked = false;
                if (StandardModeToggleButton.IsChecked is true)
                    isNormalChecked = true;
                DefaultSettings.FSGMakeSingleLineToggle = !isNormalChecked;
                DefaultSettings.Save();
                break;
            case Key.T:
                if (TableToggleButton.Visibility == Visibility.Collapsed)
                    return;

                if (isActive is null)
                    TableToggleButton.IsChecked = !TableToggleButton.IsChecked;
                else
                    TableToggleButton.IsChecked = isActive.Value;

                if (TableToggleButton.IsChecked is true)
                    SelectSingleToggleButton(TableToggleButton);
                else
                    SelectSingleToggleButton();
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

                if (KeyboardExtensions.IsCtrlDown())
                {
                    if (NextStepDropDownButton.Flyout is not ContextMenu flyoutMenu
                        || !flyoutMenu.HasItems
                        || numberPressed - 1 >= flyoutMenu.Items.Count
                        || flyoutMenu.Items[numberPressed - 1] is not MenuItem selectedItem)
                    {
                        return;
                    }

                    selectedItem.IsChecked = !selectedItem.IsChecked;
                    CheckIfAnyPostActionsSelected();
                    return;
                }

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

    private void CheckIfAnyPostActionsSelected()
    {
        if (NextStepDropDownButton.Flyout is not ContextMenu flyoutMenu || !flyoutMenu.HasItems)
            return;

        foreach (object anyItem in flyoutMenu.Items)
        {
            if (anyItem is MenuItem item && item.IsChecked is true)
            {
                if (FindResource("DarkTeal") is SolidColorBrush tealButtonStyle)
                    NextStepDropDownButton.Background = tealButtonStyle;
                return;
            }
        }

        if (FindResource("ControlFillColorDefaultBrush") is SolidColorBrush SymbolButtonStyle)
            NextStepDropDownButton.Background = SymbolButtonStyle;
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

    private void LoadDynamicPostGrabActions()
    {
        if (NextStepDropDownButton.Flyout is not ContextMenu contextMenu)
            return;

        // Clear existing items
        contextMenu.Items.Clear();

        // Get enabled post-grab actions from settings
        List<ButtonInfo> enabledActions = PostGrabActionManager.GetEnabledPostGrabActions();

        // Get the PostGrabStayOpen setting
        bool stayOpen = DefaultSettings.PostGrabStayOpen;

        // Remove any existing keyboard handler to avoid duplicates
        contextMenu.PreviewKeyDown -= FullscreenGrab_KeyDown;
        
        // Add keyboard handling once for the entire context menu
        contextMenu.PreviewKeyDown += FullscreenGrab_KeyDown;

        int index = 1;
        foreach (ButtonInfo action in enabledActions)
        {
            MenuItem menuItem = new()
            {
                Header = action.ButtonText,
                IsCheckable = true,
                Tag = action,
                IsChecked = PostGrabActionManager.GetCheckState(action),
                StaysOpenOnClick = stayOpen,
                InputGestureText = $"Ctrl+{index}"
            };

            // Wire up click handler
            menuItem.Click += PostActionMenuItem_Click;

            contextMenu.Items.Add(menuItem);
            index++;
        }

        contextMenu.Items.Add(new Separator());

        // Add "Edit this list..." menu item
        MenuItem editPostGrabMenuItem = new()
        {
            Header = "Edit this list..."
        };
        editPostGrabMenuItem.Click += EditPostGrabActions_Click;
        contextMenu.Items.Add(editPostGrabMenuItem);

        // Add "Close this menu" menu item
        MenuItem hidePostGrabMenuItem = new()
        {
            Header = "Close this menu"
        };
        hidePostGrabMenuItem.Click += HidePostGrabActions_Click;
        contextMenu.Items.Add(hidePostGrabMenuItem);

        // Update the dropdown button appearance
        CheckIfAnyPostActionsSelected();
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

            if (this.IsMouseInWindow())
                TopButtonsStackPanel.Visibility = Visibility.Visible;
        }
        else
        {
            DisposeBitmapSource(BackgroundImage);
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

        posLeft = Canvas.GetLeft(selectBorder) + (absPosPoint.X / dpi.PixelsPerDip);
        posTop = Canvas.GetTop(selectBorder) + (absPosPoint.Y / dpi.PixelsPerDip);
    }

    private void LanguagesComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            DefaultSettings.LastUsedLang = String.Empty;
            DefaultSettings.Save();
        }
    }

    private void LanguagesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox languageCmbBox || !isComboBoxReady)
            return;

        if (languageCmbBox.SelectedItem is TessLang tessLang)
        {
            DefaultSettings.LastUsedLang = tessLang.CultureDisplayName;
            DefaultSettings.Save();

            TableMenuItem.Visibility = Visibility.Collapsed;
            TableToggleButton.Visibility = Visibility.Collapsed;
        }
        else if (languageCmbBox.SelectedItem is Language pickedLang)
        {
            DefaultSettings.LastUsedLang = pickedLang.LanguageTag;
            DefaultSettings.Save();

            TableMenuItem.Visibility = Visibility.Visible;
            TableToggleButton.Visibility = Visibility.Visible;
        }
        else if (languageCmbBox.SelectedItem is WindowsAiLang winAiLang)
        {
            DefaultSettings.LastUsedLang = winAiLang.LanguageTag;
            DefaultSettings.Save();
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

    private static async Task LoadOcrLanguages(ComboBox languagesComboBox, bool usingTesseract, List<FrameworkElement>? tesseractIncompatibleElements = null)
    {
        if (languagesComboBox.Items.Count > 0)
            return;

        int count = 0;
        // TODO Find a way to combine with the ETW language drop down
        // or just put this logic into Language Utilities

        bool haveSetLastLang = false;
        string lastTextLang = DefaultSettings.LastUsedLang;

        if (WindowsAiUtilities.CanDeviceUseWinAI())
        {
            WindowsAiLang winAiLang = new();
            languagesComboBox.Items.Add(winAiLang);

            if (lastTextLang == winAiLang.LanguageTag)
            {
                languagesComboBox.SelectedIndex = 0;
            }
        }

        if (usingTesseract)
        {
            List<ILanguage> tesseractLanguages = await TesseractHelper.TesseractLanguages();

            foreach (ILanguage language in tesseractLanguages)
            {
                languagesComboBox.Items.Add(language);

                if (!haveSetLastLang && language.CultureDisplayName == lastTextLang)
                {
                    languagesComboBox.SelectedIndex = count;
                    haveSetLastLang = true;

                    if (tesseractIncompatibleElements is not null)
                        foreach (FrameworkElement element in tesseractIncompatibleElements)
                            element.Visibility = Visibility.Collapsed;
                }

                count++;
            }
        }

        IReadOnlyList<Language> possibleOCRLanguages = OcrEngine.AvailableRecognizerLanguages;

        ILanguage firstLang = LanguageUtilities.GetOCRLanguage();

        foreach (Language language in possibleOCRLanguages)
        {
            languagesComboBox.Items.Add(language);

            if (!haveSetLastLang &&
                (language.AbbreviatedName.Equals(firstLang?.AbbreviatedName.ToLower(), StringComparison.CurrentCultureIgnoreCase)
                || language.LanguageTag.Equals(firstLang?.LanguageTag.ToLower(), StringComparison.CurrentCultureIgnoreCase)))
            {
                languagesComboBox.SelectedIndex = count;
                haveSetLastLang = true;
            }

            count++;
        }

        // if no lang is set, select the first one
        if (languagesComboBox.SelectedIndex == -1)
            languagesComboBox.SelectedIndex = 0;
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
        SelectSingleToggleButton(sender);

        // null out any zoom/scaling because it does not translate into GF Size
        // TODO: when placing the Grab Frame consider zoom
        BackgroundImage.RenderTransform = null;
        edgePanTimer.Stop();
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

        if (CurrentScreen is not null && dpiScale is not null)
        {
            double currentScreenLeft = 0;
            double currentScreenTop = 0;
            double currentScreenRight = CurrentScreen.Bounds.Width / dpiScale.Value.DpiScaleX;
            double currentScreenBottom = CurrentScreen.Bounds.Height / dpiScale.Value.DpiScaleY;

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
        GetDpiAdjustedRegionOfSelectBorder(out DpiScale dpi, out double posLeft, out double posTop);

        // TODO: The Grab Frame does not get the background image and position it.
        // BackgroundImage should be passed to the GF and used as the image
        // it would need to be positioned/cropped to the bounds of the GF

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

        // Clean up background image before closing to free memory immediately
        DisposeBitmapSource(BackgroundImage);

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
        selectBorder.Height = 2;
        selectBorder.Width = 2;

        dpiScale = VisualTreeHelper.GetDpi(this);

        try { RegionClickCanvas.Children.Remove(selectBorder); } catch (Exception) { }

        selectBorder.BorderThickness = new Thickness(2);
        System.Windows.Media.Color borderColor = System.Windows.Media.Color.FromArgb(255, 40, 118, 126);
        selectBorder.BorderBrush = new SolidColorBrush(borderColor);
        _ = RegionClickCanvas.Children.Add(selectBorder);
        Canvas.SetLeft(selectBorder, clickedPoint.X);
        Canvas.SetTop(selectBorder, clickedPoint.Y);

        WindowUtilities.GetMousePosition(out System.Windows.Point mousePoint);
        foreach (DisplayInfo? screen in DisplayInfo.AllDisplayInfos)
        {
            Rect bound = screen.ScaledBounds();
            if (bound.Contains(mousePoint))
                CurrentScreen = screen;
        }
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

        double left = Math.Min(clickedPoint.X, movingPoint.X);
        double top = Math.Min(clickedPoint.Y, movingPoint.Y);

        selectBorder.Height = Math.Max(clickedPoint.Y, movingPoint.Y) - top;
        selectBorder.Width = Math.Max(clickedPoint.X, movingPoint.X) - left;
        selectBorder.Height += 2;
        selectBorder.Width += 2;

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
        CurrentScreen = null;
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

        double xDimScaled = Canvas.GetLeft(selectBorder) * m.M11;
        double yDimScaled = Canvas.GetTop(selectBorder) * m.M22;

        Rectangle regionScaled = new(
            (int)xDimScaled,
            (int)yDimScaled,
            (int)(selectBorder.Width * m.M11),
            (int)(selectBorder.Height * m.M22));

        TextFromOCR = string.Empty;

        if (NewGrabFrameMenuItem.IsChecked is true)
        {
            PlaceGrabFrameInSelectionRect();
            return;
        }

        try { RegionClickCanvas.Children.Remove(selectBorder); } catch { }

        if (LanguagesComboBox.SelectedItem is not ILanguage selectedOcrLang)
            selectedOcrLang = LanguageUtilities.GetOCRLanguage();

        bool isSmallClick = (selectBorder.Width < 3 || selectBorder.Height < 3);

        bool isSingleLine = SingleLineMenuItem is not null && SingleLineMenuItem.IsChecked;
        bool isTable = TableMenuItem is not null && TableMenuItem.IsChecked;

        if (isSmallClick)
        {
            BackgroundBrush.Opacity = 0;
            TextFromOCR = await OcrUtilities.GetClickedWordAsync(this, new System.Windows.Point(xDimScaled, yDimScaled), selectedOcrLang);
        }
        else if (isTable)
            TextFromOCR = await OcrUtilities.GetRegionsTextAsTableAsync(this, regionScaled, selectedOcrLang);
        else
            TextFromOCR = await OcrUtilities.GetRegionsTextAsync(this, regionScaled, selectedOcrLang);

        if (DefaultSettings.UseHistory && !isSmallClick)
        {
            GetDpiAdjustedRegionOfSelectBorder(out _, out double posLeft, out double posTop);

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
                LanguageTag = LanguageUtilities.GetLanguageTag(selectedOcrLang),
                LanguageKind = LanguageUtilities.GetLanguageKind(selectedOcrLang),
                CaptureDateTime = DateTimeOffset.Now,
                PositionRect = historyRect,
                IsTable = TableToggleButton.IsChecked!.Value,
                TextContent = TextFromOCR,
                ImageContent = Singleton<HistoryService>.Instance.CachedBitmap,
                SourceMode = TextGrabMode.Fullscreen,
            };
        }

        if (string.IsNullOrWhiteSpace(TextFromOCR))
        {
            BackgroundBrush.Opacity = DefaultSettings.FsgShadeOverlay ? .2 : 0.0;
            TopButtonsStackPanel.Visibility = Visibility.Visible;
            return;
        }

        // Execute enabled post-grab actions dynamically
        if (NextStepDropDownButton.Flyout is ContextMenu contextMenu)
        {
            bool shouldInsert = false;

            foreach (object item in contextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.IsChecked && menuItem.Tag is ButtonInfo action)
                {
                    // Special handling for Insert action - defer until after window closes
                    if (action.ClickEvent == "Insert_Click")
                    {
                        shouldInsert = true;
                        continue;
                    }

                    // Execute the action
                    TextFromOCR = await PostGrabActionManager.ExecutePostGrabAction(action, TextFromOCR);
                }
            }

            // Handle insert after all other actions
            if (shouldInsert && !DefaultSettings.TryInsert)
            {
                // Store for later execution after window closes
                string textToInsert = TextFromOCR;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // Small delay to ensure window is closed
                    await WindowUtilities.TryInsertString(textToInsert);
                });
            }
        }

        if (SendToEditTextToggleButton.IsChecked is true
            && destinationTextBox is null)
        {
            // Only open ETW if we're not doing a web search
            bool isWebSearch = false;
            if (NextStepDropDownButton.Flyout is ContextMenu cm)
            {
                foreach (object item in cm.Items)
                {
                    if (item is MenuItem mi && mi.IsChecked && mi.Tag is ButtonInfo act && act.ClickEvent == "WebSearch_Click")
                    {
                        isWebSearch = true;
                        break;
                    }
                }
            }

            if (!isWebSearch)
            {
                EditTextWindow etw = WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
                destinationTextBox = etw.PassedTextControl;
            }
        }

        OutputUtilities.HandleTextFromOcr(
            TextFromOCR,
            isSingleLine,
            isTable,
            destinationTextBox);
        WindowUtilities.CloseAllFullscreenGrabs();
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
        SelectSingleToggleButton(sender);

        if (isActive)
        {
            bool isSingleLineChecked = false;
            if (SingleLineToggleButton.IsChecked is true)
                isSingleLineChecked = true;
            DefaultSettings.FSGMakeSingleLineToggle = isSingleLineChecked;
            DefaultSettings.Save();
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (historyInfo is not null)
            Singleton<HistoryService>.Instance.SaveToHistory(historyInfo);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Maximized;
        FullWindow.Rect = new Rect(0, 0, Width, Height);
        KeyDown += FullscreenGrab_KeyDown;
        KeyUp += FullscreenGrab_KeyUp;

        SetImageToBackground();

        // Remove legacy pre-load toggle selection; we'll apply defaults after languages are loaded
        // to account for Table mode availability based on OCR engine.

        if (DefaultSettings.FsgSendEtwToggle)
            SendToEditTextToggleButton.IsChecked = true;

#if DEBUG
        Topmost = false;
#endif

        List<FrameworkElement> tesseractIncompatibleFrameworkElements =
        [
            TableMenuItem, TableToggleButton
        ];
        await LoadOcrLanguages(LanguagesComboBox, usingTesseract, tesseractIncompatibleFrameworkElements);
        isComboBoxReady = true;

        // Load dynamic post-grab actions
        LoadDynamicPostGrabActions();

        // TODO Find a more graceful async way to do this. Translation takes too long
        // Show translation option only if Windows AI is available
        // if (WindowsAiUtilities.CanDeviceUseWinAI())
        //     TranslatePostCapture.Visibility = Visibility.Visible;

        // Apply default mode based on new FsgDefaultMode setting, with fallback to legacy SingleLine flag
        try
        {
            FsgDefaultMode mode = FsgDefaultMode.Default;
            string? modeSetting = DefaultSettings.FsgDefaultMode;
            if (!string.IsNullOrWhiteSpace(modeSetting))
                Enum.TryParse(modeSetting, true, out mode);

            switch (mode)
            {
                case FsgDefaultMode.SingleLine:
                    SingleLineToggleButton.IsChecked = true;
                    SelectSingleToggleButton(SingleLineToggleButton);
                    break;
                case FsgDefaultMode.Table:
                    if (TableToggleButton.Visibility == Visibility.Visible)
                    {
                        TableToggleButton.IsChecked = true;
                        SelectSingleToggleButton(TableToggleButton);
                    }
                    else
                    {
                        // Fallback when Table mode isn't available for selected OCR engine
                        if (DefaultSettings.FSGMakeSingleLineToggle)
                        {
                            SingleLineToggleButton.IsChecked = true;
                            SelectSingleToggleButton(SingleLineToggleButton);
                        }
                        else
                        {
                            StandardModeToggleButton.IsChecked = true;
                            SelectSingleToggleButton(StandardModeToggleButton);
                        }
                    }
                    break;
                case FsgDefaultMode.Default:
                default:
                    if (DefaultSettings.FSGMakeSingleLineToggle)
                    {
                        SingleLineToggleButton.IsChecked = true;
                        SelectSingleToggleButton(SingleLineToggleButton);
                    }
                    else
                    {
                        StandardModeToggleButton.IsChecked = true;
                        SelectSingleToggleButton(StandardModeToggleButton);
                    }
                    break;
            }
        }
        catch
        {
            // Fallback to legacy behavior if parsing fails
            if (DefaultSettings.FSGMakeSingleLineToggle)
            {
                SingleLineToggleButton.IsChecked = true;
                SelectSingleToggleButton(SingleLineToggleButton);
            }
            else
            {
                StandardModeToggleButton.IsChecked = true;
                SelectSingleToggleButton(StandardModeToggleButton);
            }
        }

        if (IsMouseOver)
            TopButtonsStackPanel.Visibility = Visibility.Visible;
    }

    private void DisposeBitmapSource(System.Windows.Controls.Image image)
    {
        if (image.Source is not BitmapSource oldSource)
            return;

        image.Source = null;
        image.UpdateLayout();
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e)
    {
        edgePanTimer.Stop();
        edgePanTimer.Tick -= EdgePanTimer_Tick;

        DisposeBitmapSource(BackgroundImage);

        // Clear transform to release any scaled/transformed images
        BackgroundImage.RenderTransform = null;

        // Remove select border from canvas
        if (RegionClickCanvas.Children.Contains(selectBorder))
            RegionClickCanvas.Children.Remove(selectBorder);

        // Clean up dynamically created post-grab action menu items
        if (NextStepDropDownButton.Flyout is ContextMenu contextMenu)
        {
            contextMenu.PreviewKeyDown -= FullscreenGrab_KeyDown;
            
            foreach (object item in contextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Tag is ButtonInfo)
                {
                    menuItem.Click -= PostActionMenuItem_Click;
                }
                else if (item is MenuItem editMenuItem && editMenuItem.Header?.ToString() == "Edit this list...")
                {
                    editMenuItem.Click -= EditPostGrabActions_Click;
                }
                else if (item is MenuItem hideMenuItem && hideMenuItem.Header?.ToString() == "Close this menu")
                {
                    hideMenuItem.Click -= HidePostGrabActions_Click;
                }
            }
            
            contextMenu.Items.Clear();
        }

        CurrentScreen = null;
        dpiScale = null;
        TextFromOCR = null;
        destinationTextBox = null;
        historyInfo = null;

        Loaded -= Window_Loaded;
        Unloaded -= Window_Unloaded;

        RegionClickCanvas.MouseDown -= RegionClickCanvas_MouseDown;
        RegionClickCanvas.MouseMove -= RegionClickCanvas_MouseMove;
        RegionClickCanvas.MouseUp -= RegionClickCanvas_MouseUp;
        RegionClickCanvas.MouseEnter -= RegionClickCanvas_MouseEnter;
        RegionClickCanvas.MouseLeave -= RegionClickCanvas_MouseLeave;
        RegionClickCanvas.PreviewMouseWheel -= RegionClickCanvas_PreviewMouseWheel;
        RegionClickCanvas.ContextMenuOpening -= RegionClickCanvas_ContextMenuOpening;

        SingleLineMenuItem.Click -= SingleLineMenuItem_Click;
        FreezeMenuItem.Click -= FreezeMenuItem_Click;
        NewGrabFrameMenuItem.Click -= NewGrabFrameMenuItem_Click;
        SendToEtwMenuItem.Click -= NewEditTextMenuItem_Click;
        SettingsMenuItem.Click -= SettingsMenuItem_Click;
        CancelMenuItem.Click -= CancelMenuItem_Click;
        EditLastGrabMenuItem.Click -= EditLastGrab_Click;

        LanguagesComboBox.SelectionChanged -= LanguagesComboBox_SelectionChanged;
        LanguagesComboBox.PreviewMouseDown -= LanguagesComboBox_PreviewMouseDown;

        SingleLineToggleButton.Click -= SingleLineMenuItem_Click;
        FreezeToggleButton.Click -= FreezeMenuItem_Click;
        NewGrabFrameToggleButton.Click -= NewGrabFrameMenuItem_Click;
        SendToEditTextToggleButton.Click -= SendToEditTextToggleButton_Click;
        TableToggleButton.Click -= TableToggleButton_Click;
        StandardModeToggleButton.Click -= StandardModeToggleButton_Click;
        SettingsButton.Click -= SettingsMenuItem_Click;
        CancelButton.Click -= CancelMenuItem_Click;

        KeyDown -= FullscreenGrab_KeyDown;
        KeyUp -= FullscreenGrab_KeyUp;
        Closing -= Window_Closing;
    }

    private void StandardModeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);
        WindowUtilities.FullscreenKeyDown(Key.N, isActive);
        SelectSingleToggleButton(sender);

        if (isActive)
        {
            bool isStandardChecked = false;
            if (StandardModeToggleButton.IsChecked is true)
                isStandardChecked = true;

            DefaultSettings.FSGMakeSingleLineToggle = !isStandardChecked;
            DefaultSettings.Save();
        }
    }

    private void SelectSingleToggleButton(object? sender = null)
    {
        if (sender is not ToggleButton clickedToggleButton)
        {
            if (StandardModeToggleButton.IsChecked is false
                && SingleLineToggleButton.IsChecked is false
                && TableToggleButton.IsChecked is false
                && NewGrabFrameToggleButton.IsChecked is false)
                StandardModeToggleButton.IsChecked = true;

            return;
        }

        StandardModeToggleButton.IsChecked = false;
        SingleLineToggleButton.IsChecked = false;
        TableToggleButton.IsChecked = false;
        NewGrabFrameToggleButton.IsChecked = false;

        clickedToggleButton.IsChecked = true;
    }

    private void TableToggleButton_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        bool isActive = CheckIfCheckingOrUnchecking(sender);
        WindowUtilities.FullscreenKeyDown(Key.T, isActive);
        SelectSingleToggleButton(sender);
    }

    private void PostActionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Save check state for LastUsed tracking
        if (sender is MenuItem menuItem 
            && menuItem.Tag is ButtonInfo action
            && action.DefaultCheckState == DefaultCheckState.LastUsed)
        {
            PostGrabActionManager.SaveCheckState(action, menuItem.IsChecked);
        }

        CheckIfAnyPostActionsSelected();
    }
    #endregion Methods

    private void EdgePanTimer_Tick(object? sender, EventArgs e)
    {
        if (BackgroundImage.RenderTransform is not TransformGroup transformGroup)
        {
            edgePanTimer.Stop();
            return;
        }

        ScaleTransform? scaleTransform = null;
        foreach (Transform? transform in transformGroup.Children)
        {
            if (transform is ScaleTransform st)
            {
                scaleTransform = st;
                break;
            }
        }

        if (scaleTransform == null || scaleTransform.ScaleX <= 1.0)
        {
            edgePanTimer.Stop();
            return;
        }

        if (!WindowUtilities.GetMousePosition(out System.Windows.Point mousePos))
            return;

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        System.Windows.Point absPosPoint = this.GetAbsolutePosition();

        Rect windowRect = new(
            absPosPoint.X,
            absPosPoint.Y,
            ActualWidth * dpi.DpiScaleX,
            ActualHeight * dpi.DpiScaleY);

        if (!windowRect.Contains(mousePos))
            return;

        double relativeX = mousePos.X - windowRect.Left;
        double relativeY = mousePos.Y - windowRect.Top;

        double edgeThresholdX = windowRect.Width * EdgePanThresholdPercent;
        double edgeThresholdY = windowRect.Height * EdgePanThresholdPercent;

        double panX = 0;
        double panY = 0;

        if (relativeX < edgeThresholdX)
            panX = EdgePanSpeed * (1.0 - (relativeX / edgeThresholdX));
        else if (relativeX > windowRect.Width - edgeThresholdX)
            panX = -EdgePanSpeed * (1.0 - ((windowRect.Width - relativeX) / edgeThresholdX));

        if (relativeY < edgeThresholdY)
            panY = EdgePanSpeed * (1.0 - (relativeY / edgeThresholdY));
        else if (relativeY > windowRect.Height - edgeThresholdY)
            panY = -EdgePanSpeed * (1.0 - ((windowRect.Height - relativeY) / edgeThresholdY));

        const double Epsilon = 1e-6;
        if (Math.Abs(panX) > Epsilon || Math.Abs(panY) > Epsilon)
            PanBackgroundImage(panX, panY, transformGroup);
    }

    private void PanBackgroundImage(double deltaX, double deltaY, TransformGroup transformGroup)
    {
        ScaleTransform? scaleTransform = null;
        TranslateTransform? translateTransform = null;

        foreach (Transform? transform in transformGroup.Children)
        {
            if (transform is ScaleTransform st)
                scaleTransform = st;
            else if (transform is TranslateTransform tt)
                translateTransform = tt;
        }

        if (scaleTransform == null)
            return;

        if (translateTransform == null)
        {
            translateTransform = new TranslateTransform();
            transformGroup.Children.Add(translateTransform);
        }

        double imageWidth = BackgroundImage.ActualWidth;
        double imageHeight = BackgroundImage.ActualHeight;
        double scale = scaleTransform.ScaleX;

        double centerX = scaleTransform.CenterX;
        double centerY = scaleTransform.CenterY;

        // Calculate new translation values
        double newX = translateTransform.X + deltaX;
        double newY = translateTransform.Y + deltaY;

        // The image is scaled around centerX, centerY
        // Calculate where the image edges would be after applying the translation

        // Left edge position = -centerX * (scale - 1) + newX
        // Right edge position = imageWidth + (imageWidth - centerX) * (scale - 1) + newX
        // Top edge position = -centerY * (scale - 1) + newY
        // Bottom edge position = imageHeight + (imageHeight - centerY) * (scale - 1) + newY

        double leftEdge = -centerX * (scale - 1) + newX;
        double rightEdge = imageWidth + (imageWidth - centerX) * (scale - 1) + newX;
        double topEdge = -centerY * (scale - 1) + newY;
        double bottomEdge = imageHeight + (imageHeight - centerY) * (scale - 1) + newY;

        // Clamp so edges never go past window bounds (0 to imageWidth/imageHeight)
        // Left edge must be <= 0 (can't see past left side)
        // Right edge must be >= imageWidth (can't see past right side)
        // Top edge must be <= 0 (can't see past top side)
        // Bottom edge must be >= imageHeight (can't see past bottom side)

        if (leftEdge > 0)
            newX -= leftEdge;
        if (rightEdge < imageWidth)
            newX += (imageWidth - rightEdge);
        if (topEdge > 0)
            newY -= topEdge;
        if (bottomEdge < imageHeight)
            newY += (imageHeight - bottomEdge);

        translateTransform.X = newX;
        translateTransform.Y = newY;
    }

    private void RegionClickCanvas_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (NewGrabFrameMenuItem.IsChecked)
        {
            BackgroundImage.RenderTransform = null;
            edgePanTimer.Stop();
            return;
        }

        System.Windows.Point point = Mouse.GetPosition(this);

        if (BackgroundImage.RenderTransform is TransformGroup transformGroup)
        {
            ScaleTransform? scaleTransform = null;
            foreach (Transform? transform in transformGroup.Children)
            {
                if (transform is ScaleTransform st)
                {
                    scaleTransform = st;
                    break;
                }
            }

            if (scaleTransform != null)
            {
                double changingScale = scaleTransform.ScaleX;
                if (e.Delta > 0)
                    changingScale *= 1.1;
                else
                    changingScale *= 0.9;

                if (changingScale < 1.2)
                {
                    BackgroundImage.RenderTransform = null;
                    edgePanTimer.Stop();
                    e.Handled = true;
                    return;
                }

                if (changingScale > MaxZoomScale)
                    changingScale = MaxZoomScale;

                scaleTransform.ScaleX = changingScale;
                scaleTransform.ScaleY = changingScale;

                if (!edgePanTimer.IsEnabled)
                    edgePanTimer.Start();

                e.Handled = true;
                return;
            }
        }

        // Only create a new transform when zooming in (e.Delta > 0)
        // Skip when zooming out at base scale since there's nothing to zoom out from
        if (e.Delta <= 0)
        {
            e.Handled = true;
            return;
        }

        double scale = 1.1;

        TransformGroup newGroup = new();
        ScaleTransform newScaleTransform = new()
        {
            ScaleX = scale,
            ScaleY = scale,
            CenterX = point.X,
            CenterY = point.Y
        };
        newGroup.Children.Add(newScaleTransform);
        newGroup.Children.Add(new TranslateTransform());

        BackgroundImage.RenderTransform = newGroup;
        edgePanTimer.Start();

        e.Handled = true;
    }

    private void HidePostGrabActions_Click(object sender, RoutedEventArgs e)
    {
        if (NextStepDropDownButton.Flyout is ContextMenu menu)
            menu.IsOpen = false;
    }

    private void EditPostGrabActions_Click(object sender, RoutedEventArgs e)
    {
        PostGrabActionEditor postGrabActionEditor = new();
        postGrabActionEditor.Show();

        WindowUtilities.CloseAllFullscreenGrabs();
    }
}
