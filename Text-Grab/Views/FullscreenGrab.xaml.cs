using Dapplo.Windows.User32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Text_Grab.Controls;
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
    private readonly Canvas templateOverlayCanvas = new() { ClipToBounds = true, IsHitTestVisible = false };

    private const double MaxZoomScale = 16.0;
    private const double EdgePanThresholdPercent = 0.10;
    private const double EdgePanSpeed = 8.0;
    private const string EditPostGrabActionsTag = "EditPostGrabActions";
    private const string ClosePostGrabMenuTag = "ClosePostGrabMenu";
    private readonly DispatcherTimer edgePanTimer;

    #endregion Fields

    #region Constructors

    public FullscreenGrab()
    {
        InitializeComponent();
        App.SetTheme();
        usingTesseract = DefaultSettings.UseTesseract && TesseractHelper.CanLocateTesseractExe();
        InitializeSelectionStyles();

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
    public string? PreselectedTemplateId { get; set; }
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
            case Key.R:
                ApplySelectionStyle(FsgSelectionStyle.Region);
                break;
            case Key.W:
                ApplySelectionStyle(FsgSelectionStyle.Window);
                break;
            case Key.D:
                ApplySelectionStyle(FsgSelectionStyle.Freeform);
                break;
            case Key.A:
                ApplySelectionStyle(FsgSelectionStyle.AdjustAfter);
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
                    return;

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

    internal static string GetPostGrabActionKey(ButtonInfo action)
    {
        if (!string.IsNullOrWhiteSpace(action.TemplateId))
            return $"template:{action.TemplateId}";

        if (!string.IsNullOrWhiteSpace(action.ClickEvent))
            return $"click:{action.ClickEvent}";

        return $"text:{action.ButtonText}";
    }

    internal static List<MenuItem> GetActionablePostGrabMenuItems(ContextMenu contextMenu)
    {
        return [.. contextMenu.Items
            .OfType<MenuItem>()
            .Where(static item => item.Tag is ButtonInfo)];
    }

    internal static Dictionary<string, bool> BuildPostGrabActionSnapshot(
        IEnumerable<MenuItem> actionableItems,
        string? changedActionKey = null,
        bool? changedIsChecked = null)
    {
        List<(MenuItem MenuItem, ButtonInfo Action, string ActionKey)> postGrabItems = [];

        foreach (MenuItem menuItem in actionableItems)
        {
            if (menuItem.Tag is not ButtonInfo action)
                continue;

            postGrabItems.Add((menuItem, action, GetPostGrabActionKey(action)));
        }

        Dictionary<string, bool> actionStates = [];
        foreach ((MenuItem menuItem, _, string actionKey) in postGrabItems)
        {
            bool isChecked = changedActionKey == actionKey && changedIsChecked.HasValue
                ? changedIsChecked.Value
                : menuItem.IsChecked;
            actionStates[actionKey] = isChecked;
        }

        List<string> checkedTemplateKeys = [.. postGrabItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Action.TemplateId) && actionStates[item.ActionKey])
            .Select(item => item.ActionKey)];

        if (checkedTemplateKeys.Count > 1)
        {
            string templateToKeep = !string.IsNullOrWhiteSpace(changedActionKey)
                && changedIsChecked == true
                && checkedTemplateKeys.Contains(changedActionKey)
                ? changedActionKey
                : checkedTemplateKeys[0];

            foreach (string templateKey in checkedTemplateKeys.Where(key => key != templateToKeep))
                actionStates[templateKey] = false;
        }

        return actionStates;
    }

    private void CheckIfAnyPostActionsSelected()
    {
        if (NextStepDropDownButton.Flyout is not ContextMenu flyoutMenu || !flyoutMenu.HasItems)
            return;

        foreach (MenuItem item in GetActionablePostGrabMenuItems(flyoutMenu))
        {
            if (item.IsChecked)
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

    private void RefreshPostGrabActionVisuals()
    {
        CheckIfAnyPostActionsSelected();

        if (CurrentSelectionStyle == FsgSelectionStyle.Freeform)
        {
            TemplateOverlayHost.Children.Clear();
            templateOverlayCanvas.Children.Clear();
            return;
        }

        if (RegionClickCanvas.Children.Contains(selectBorder)
            && selectBorder.Width > 2
            && selectBorder.Height > 2)
        {
            double selLeft = Canvas.GetLeft(selectBorder);
            double selTop = Canvas.GetTop(selectBorder);

            if (!double.IsNaN(selLeft) && !double.IsNaN(selTop))
            {
                UpdateTemplateRegionOverlays(selLeft, selTop, selectBorder.Width, selectBorder.Height);
                return;
            }
        }

        TemplateOverlayHost.Children.Clear();
        templateOverlayCanvas.Children.Clear();
    }

    private void SynchronizePostGrabActionShortcut(int actionIndex)
    {
        if (NextStepDropDownButton.Flyout is not ContextMenu contextMenu || !contextMenu.HasItems)
            return;

        List<MenuItem> actionableItems = GetActionablePostGrabMenuItems(contextMenu);
        if (actionIndex < 0 || actionIndex >= actionableItems.Count)
            return;

        MenuItem selectedItem = actionableItems[actionIndex];
        SynchronizePostGrabActionSelection(selectedItem, !selectedItem.IsChecked);
    }

    private void SynchronizePostGrabActionSelection(MenuItem menuItem, bool isChecked)
    {
        if (menuItem.Tag is not ButtonInfo action
            || menuItem.Parent is not ContextMenu contextMenu)
        {
            RefreshPostGrabActionVisuals();
            return;
        }

        Dictionary<string, bool> actionStates = BuildPostGrabActionSnapshot(
            GetActionablePostGrabMenuItems(contextMenu),
            GetPostGrabActionKey(action),
            isChecked);

        ApplyPostGrabActionSnapshot(
            actionStates,
            persistLastUsed: true,
            forcePersistActionKey: GetPostGrabActionKey(action));
        WindowUtilities.SyncFullscreenPostGrabActionStates(actionStates, this);
    }

    internal static bool ShouldPersistLastUsedState(ButtonInfo action, bool previousChecked, bool isChecked, string? forcePersistActionKey = null)
    {
        if (action.DefaultCheckState != DefaultCheckState.LastUsed)
            return false;

        return previousChecked != isChecked || GetPostGrabActionKey(action) == forcePersistActionKey;
    }

    internal void ApplyPostGrabActionSnapshot(
        IReadOnlyDictionary<string, bool> actionStates,
        bool persistLastUsed = false,
        string? forcePersistActionKey = null)
    {
        if (NextStepDropDownButton.Flyout is not ContextMenu contextMenu || !contextMenu.HasItems)
            return;

        foreach (MenuItem menuItem in GetActionablePostGrabMenuItems(contextMenu))
        {
            if (menuItem.Tag is not ButtonInfo action)
                continue;

            bool previousChecked = menuItem.IsChecked;
            bool isChecked = actionStates.TryGetValue(GetPostGrabActionKey(action), out bool syncedState) && syncedState;
            menuItem.IsChecked = isChecked;

            if (persistLastUsed
                && ShouldPersistLastUsedState(action, previousChecked, isChecked, forcePersistActionKey))
            {
                PostGrabActionManager.SaveCheckState(action, isChecked);
            }
        }

        RefreshPostGrabActionVisuals();
    }

    private void AddPostGrabActionMenuItem(ContextMenu contextMenu, ButtonInfo action, bool isChecked, bool stayOpen, int shortcutIndex)
    {
        MenuItem menuItem = new()
        {
            Header = action.ButtonText,
            IsCheckable = true,
            Tag = action,
            IsChecked = isChecked,
            StaysOpenOnClick = stayOpen,
            InputGestureText = shortcutIndex <= 9 ? $"Ctrl+{shortcutIndex}" : string.Empty
        };

        menuItem.Click += PostActionMenuItem_Click;
        contextMenu.Items.Add(menuItem);
    }

    private List<ButtonInfo> GetEnabledPostGrabActionsForMenu()
    {
        List<ButtonInfo> enabledActions = PostGrabActionManager.GetEnabledPostGrabActions();

        if (string.IsNullOrWhiteSpace(PreselectedTemplateId)
            || enabledActions.Any(action => action.TemplateId == PreselectedTemplateId))
        {
            return enabledActions;
        }

        GrabTemplate? template = GrabTemplateManager.GetTemplateById(PreselectedTemplateId);
        if (template is null)
            return enabledActions;

        enabledActions.Add(GrabTemplateManager.CreateButtonInfoForTemplate(template));
        return enabledActions;
    }

    private void LoadDynamicPostGrabActions()
    {
        if (NextStepDropDownButton.Flyout is not ContextMenu contextMenu)
            return;

        // Clear existing items
        contextMenu.Items.Clear();

        List<ButtonInfo> enabledActions = GetEnabledPostGrabActionsForMenu();

        bool stayOpen = DefaultSettings.PostGrabStayOpen;

        contextMenu.PreviewKeyDown -= FullscreenGrab_KeyDown;
        contextMenu.PreviewKeyDown += FullscreenGrab_KeyDown;

        List<ButtonInfo> regularActions = [.. enabledActions.Where(a => string.IsNullOrEmpty(a.TemplateId))];
        List<ButtonInfo> templateActions = [.. enabledActions.Where(a => !string.IsNullOrEmpty(a.TemplateId))];
        bool templatePreselected = !string.IsNullOrEmpty(PreselectedTemplateId);

        int index = 1;
        foreach (ButtonInfo action in regularActions)
        {
            AddPostGrabActionMenuItem(contextMenu, action, PostGrabActionManager.GetCheckState(action), stayOpen, index);
            index++;
        }

        if (regularActions.Count > 0 && templateActions.Count > 0)
            contextMenu.Items.Add(new Separator());

        foreach (ButtonInfo action in templateActions)
        {
            bool isChecked = templatePreselected
                ? action.TemplateId == PreselectedTemplateId
                : PostGrabActionManager.GetCheckState(action);

            AddPostGrabActionMenuItem(contextMenu, action, isChecked, stayOpen, index);
            index++;
        }

        contextMenu.Items.Add(new Separator());

        MenuItem editPostGrabMenuItem = new()
        {
            Header = "✨ Customize Actions \u0026 Templates...",
            Tag = EditPostGrabActionsTag
        };
        editPostGrabMenuItem.Click += EditPostGrabActions_Click;
        contextMenu.Items.Add(editPostGrabMenuItem);

        // Add "Close this menu" menu item
        MenuItem hidePostGrabMenuItem = new()
        {
            Header = "Close this menu",
            Tag = ClosePostGrabMenuTag
        };
        hidePostGrabMenuItem.Click += HidePostGrabActions_Click;
        contextMenu.Items.Add(hidePostGrabMenuItem);

        IReadOnlyDictionary<string, bool>? synchronizedActionStates = WindowUtilities.GetFullscreenPostGrabActionStates();
        if (synchronizedActionStates is not null)
        {
            ApplyPostGrabActionSnapshot(synchronizedActionStates);
            return;
        }

        RefreshPostGrabActionVisuals();
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
        if (e.Key == Key.Enter && isAwaitingAdjustAfterCommit)
        {
            AcceptSelectionButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        int keyValue = (int)e.Key;
        if (KeyboardExtensions.IsCtrlDown()
            && keyValue >= (int)Key.D1
            && keyValue <= (int)Key.D9)
        {
            SynchronizePostGrabActionShortcut(keyValue - (int)Key.D1);
            e.Handled = true;
            return;
        }

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

    private GrabTemplate? GetActiveTemplate()
    {
        if (NextStepDropDownButton.Flyout is not ContextMenu contextMenu)
            return null;

        foreach (MenuItem menuItem in GetActionablePostGrabMenuItems(contextMenu))
        {
            if (menuItem.IsChecked
                && menuItem.Tag is ButtonInfo action
                && action.ClickEvent == "ApplyTemplate_Click"
                && !string.IsNullOrEmpty(action.TemplateId))
            {
                return GrabTemplateManager.GetTemplateById(action.TemplateId);
            }
        }

        return null;
    }

    /// <summary>
    /// Draws scaled template region overlays inside the current selection border.
    /// Each region's stored ratio coordinates are applied directly to the current
    /// selection dimensions (stretch-to-fill), so both axes scale independently.
    /// </summary>
    private void UpdateTemplateRegionOverlays(double selLeft, double selTop, double selWidth, double selHeight)
    {
        TemplateOverlayHost.Children.Clear();
        templateOverlayCanvas.Children.Clear();

        if (CurrentSelectionStyle == FsgSelectionStyle.Freeform)
            return;

        GrabTemplate? template = GetActiveTemplate();
        if (template is null || template.Regions.Count == 0)
            return;

        // If the output template references no regions (pattern-only), skip overlays
        HashSet<int> referencedRegions = [.. template.GetReferencedRegionNumbers()];
        if (referencedRegions.Count == 0 && template.PatternMatches.Count > 0)
            return;

        if (selWidth < 4 || selHeight < 4)
            return;

        templateOverlayCanvas.Width = selWidth;
        templateOverlayCanvas.Height = selHeight;
        Canvas.SetLeft(templateOverlayCanvas, selLeft);
        Canvas.SetTop(templateOverlayCanvas, selTop);

        System.Windows.Media.Color borderColor = System.Windows.Media.Color.FromArgb(220, 255, 180, 0);
        System.Windows.Media.Color dimBorderColor = System.Windows.Media.Color.FromArgb(80, 255, 180, 0);

        foreach (TemplateRegion region in template.Regions)
        {
            double regionLeft = region.RatioLeft * selWidth;
            double regionTop = region.RatioTop * selHeight;
            double regionWidth = region.RatioWidth * selWidth;
            double regionHeight = region.RatioHeight * selHeight;

            if (regionWidth < 1 || regionHeight < 1)
                continue;

            bool isReferenced = referencedRegions.Count == 0 || referencedRegions.Contains(region.RegionNumber);
            Border regionBorder = new()
            {
                Width = regionWidth,
                Height = regionHeight,
                BorderBrush = new SolidColorBrush(isReferenced ? borderColor : dimBorderColor),
                BorderThickness = new Thickness(1.5),
            };

            Canvas.SetLeft(regionBorder, regionLeft);
            Canvas.SetTop(regionBorder, regionTop);
            templateOverlayCanvas.Children.Add(regionBorder);
        }

        TemplateOverlayHost.Children.Add(templateOverlayCanvas);
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
            LanguageUtilities.InvalidateOcrLanguageCache();
        }
    }

    private void ApplySelectedLanguageState(ILanguage selectedLanguage)
    {
        bool supportsTableOutput = CaptureLanguageUtilities.SupportsTableOutput(selectedLanguage);
        TableMenuItem.Visibility = supportsTableOutput ? Visibility.Visible : Visibility.Collapsed;
        TableToggleButton.Visibility = supportsTableOutput ? Visibility.Visible : Visibility.Collapsed;

        if (!supportsTableOutput)
        {
            TableMenuItem.IsChecked = false;
            TableToggleButton.IsChecked = false;
            SelectSingleToggleButton(StandardModeToggleButton);
        }
    }

    private void LanguagesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox languageCmbBox
            || languageCmbBox.SelectedItem is not ILanguage selectedLanguage
            || !isComboBoxReady)
            return;

        CaptureLanguageUtilities.PersistSelectedLanguage(selectedLanguage);
        ApplySelectedLanguageState(selectedLanguage);

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

    private static async Task LoadOcrLanguages(ComboBox languagesComboBox, bool usingTesseract)
    {
        if (languagesComboBox.Items.Count > 0)
            return;

        List<ILanguage> availableLanguages = await CaptureLanguageUtilities.GetCaptureLanguagesAsync(usingTesseract);
        foreach (ILanguage language in availableLanguages)
            languagesComboBox.Items.Add(language);

        int selectedIndex = CaptureLanguageUtilities.FindPreferredLanguageIndex(
            availableLanguages,
            DefaultSettings.LastUsedLang,
            LanguageUtilities.GetOCRLanguage());

        if (selectedIndex >= 0)
            languagesComboBox.SelectedIndex = selectedIndex;
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
            new System.Windows.Size(selectBorder.Width, selectBorder.Height));
        Canvas.SetLeft(selectBorder, leftValue);
        Canvas.SetTop(selectBorder, topValue);

        UpdateTemplateRegionOverlays(leftValue, topValue, selectBorder.Width, selectBorder.Height);
    }

    private void PlaceGrabFrameInSelectionRect()
    {
        // Make a new GrabFrame and show it on screen
        // Then place it where the user just drew the region
        // Add space around the window to account for Titlebar
        // bottom bar and width of GrabFrame
        GetDpiAdjustedRegionOfSelectBorder(out DpiScale dpi, out double posLeft, out double posTop);

        // Crop the frozen background image to the selected region so the GrabFrame
        // shows exactly what the user saw in the Fullscreen Grab (freeze continuity).
        GrabFrame grabFrame;
        if (BackgroundImage.Source is BitmapSource backgroundBitmap)
        {
            Matrix m = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
            Rect selectionRect = GetCurrentSelectionRect();

            if (TryGetBitmapCropRectForSelection(
                selectionRect,
                m,
                BackgroundImage.RenderTransform,
                backgroundBitmap.PixelWidth,
                backgroundBitmap.PixelHeight,
                out Int32Rect cropRect))
            {
                CroppedBitmap croppedBitmap = new(backgroundBitmap, cropRect);
                croppedBitmap.Freeze();
                grabFrame = new GrabFrame(croppedBitmap);
            }
            else
            {
                grabFrame = new GrabFrame();
            }
        }
        else
        {
            grabFrame = new GrabFrame();
        }

        grabFrame.Left = posLeft;
        grabFrame.Top = posTop;

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
        UpdateTopToolbarVisibility(isPointerOverSelectionSurface: false);
    }

    private void RegionClickCanvas_MouseEnter(object sender, MouseEventArgs e)
    {
        UpdateTopToolbarVisibility(isPointerOverSelectionSurface: true);
    }

    private void RegionClickCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.RightButton == MouseButtonState.Pressed)
            return;
        HandleRegionCanvasMouseDown(e);
    }

    private void RegionClickCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        HandleRegionCanvasMouseMove(e);
    }

    private async void RegionClickCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        await HandleRegionCanvasMouseUpAsync(e);
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

        await LoadOcrLanguages(LanguagesComboBox, usingTesseract);
        isComboBoxReady = true;
        if (LanguagesComboBox.SelectedItem is ILanguage selectedLanguage)
            ApplySelectedLanguageState(selectedLanguage);

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

        FsgSelectionStyle selectionStyle = FsgSelectionStyle.Region;
        if (!string.IsNullOrWhiteSpace(DefaultSettings.FsgSelectionStyle))
            Enum.TryParse(DefaultSettings.FsgSelectionStyle, true, out selectionStyle);

        ApplySelectionStyle(selectionStyle, persistToSettings: false);
        windowSelectionTimer.Start();
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
        windowSelectionTimer.Stop();
        windowSelectionTimer.Tick -= WindowSelectionTimer_Tick;

        DisposeBitmapSource(BackgroundImage);

        // Clear transform to release any scaled/transformed images
        BackgroundImage.RenderTransform = null;

        // Remove select border from canvas
        if (RegionClickCanvas.Children.Contains(selectBorder))
            RegionClickCanvas.Children.Remove(selectBorder);

        if (SelectionOutlineHost.Children.Contains(selectionOutlineBorder))
            SelectionOutlineHost.Children.Remove(selectionOutlineBorder);

        // Clean up dynamically created post-grab action menu items
        if (NextStepDropDownButton.Flyout is ContextMenu contextMenu)
        {
            contextMenu.PreviewKeyDown -= FullscreenGrab_KeyDown;

            foreach (object item in contextMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    if (menuItem.Tag is ButtonInfo)
                    {
                        menuItem.Click -= PostActionMenuItem_Click;
                    }
                    else if (menuItem.Tag is string tag)
                    {
                        if (tag == EditPostGrabActionsTag)
                        {
                            menuItem.Click -= EditPostGrabActions_Click;
                        }
                        else if (tag == ClosePostGrabMenuTag)
                        {
                            menuItem.Click -= HidePostGrabActions_Click;
                        }
                    }
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
        RegionSelectionMenuItem.Click -= SelectionStyleMenuItem_Click;
        WindowSelectionMenuItem.Click -= SelectionStyleMenuItem_Click;
        FreeformSelectionMenuItem.Click -= SelectionStyleMenuItem_Click;
        AdjustAfterSelectionMenuItem.Click -= SelectionStyleMenuItem_Click;
        NewGrabFrameMenuItem.Click -= NewGrabFrameMenuItem_Click;
        SendToEtwMenuItem.Click -= NewEditTextMenuItem_Click;
        SettingsMenuItem.Click -= SettingsMenuItem_Click;
        CancelMenuItem.Click -= CancelMenuItem_Click;
        EditLastGrabMenuItem.Click -= EditLastGrab_Click;

        LanguagesComboBox.SelectionChanged -= LanguagesComboBox_SelectionChanged;
        LanguagesComboBox.PreviewMouseDown -= LanguagesComboBox_PreviewMouseDown;
        SelectionStyleComboBox.SelectionChanged -= SelectionStyleComboBox_SelectionChanged;

        SingleLineToggleButton.Click -= SingleLineMenuItem_Click;
        FreezeToggleButton.Click -= FreezeMenuItem_Click;
        NewGrabFrameToggleButton.Click -= NewGrabFrameMenuItem_Click;
        AcceptSelectionButton.Click -= AcceptSelectionButton_Click;
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
        if (sender is not MenuItem menuItem)
        {
            RefreshPostGrabActionVisuals();
            return;
        }

        SynchronizePostGrabActionSelection(menuItem, menuItem.IsChecked);
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

    internal static bool TryGetBitmapCropRectForSelection(
        Rect selectionRect,
        Matrix transformToDevice,
        Transform? backgroundRenderTransform,
        int bitmapPixelWidth,
        int bitmapPixelHeight,
        out Int32Rect cropRect)
    {
        cropRect = default;

        if (selectionRect.IsEmpty
            || selectionRect.Width <= 0
            || selectionRect.Height <= 0
            || bitmapPixelWidth <= 0
            || bitmapPixelHeight <= 0)
        {
            return false;
        }

        Matrix selectionToBackground = backgroundRenderTransform?.Value ?? Matrix.Identity;
        if (selectionToBackground.HasInverse)
            selectionToBackground.Invert();
        else
            selectionToBackground = Matrix.Identity;

        Point[] backgroundPoints =
        [
            selectionToBackground.Transform(selectionRect.TopLeft),
            selectionToBackground.Transform(new Point(selectionRect.Right, selectionRect.Top)),
            selectionToBackground.Transform(new Point(selectionRect.Left, selectionRect.Bottom)),
            selectionToBackground.Transform(selectionRect.BottomRight)
        ];

        Point[] bitmapPoints =
        [
            transformToDevice.Transform(backgroundPoints[0]),
            transformToDevice.Transform(backgroundPoints[1]),
            transformToDevice.Transform(backgroundPoints[2]),
            transformToDevice.Transform(backgroundPoints[3])
        ];

        double left = bitmapPoints.Min(static point => point.X);
        double top = bitmapPoints.Min(static point => point.Y);
        double right = bitmapPoints.Max(static point => point.X);
        double bottom = bitmapPoints.Max(static point => point.Y);

        int cropLeft = Math.Max(0, (int)Math.Floor(left));
        int cropTop = Math.Max(0, (int)Math.Floor(top));
        int cropRight = Math.Min(bitmapPixelWidth, (int)Math.Ceiling(right));
        int cropBottom = Math.Min(bitmapPixelHeight, (int)Math.Ceiling(bottom));

        if (cropRight <= cropLeft || cropBottom <= cropTop)
            return false;

        cropRect = new Int32Rect(cropLeft, cropTop, cropRight - cropLeft, cropBottom - cropTop);
        return true;
    }

    private void EditPostGrabActions_Click(object sender, RoutedEventArgs e)
    {
        PostGrabActionEditor postGrabActionEditor = new();
        postGrabActionEditor.Show();

        WindowUtilities.CloseAllFullscreenGrabs();
    }
}
