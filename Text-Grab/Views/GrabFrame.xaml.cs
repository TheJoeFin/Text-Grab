using Fasetto.Word;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.UndoRedoOperations;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;
using Windows.System;
using ZXing;
using ZXing.Windows.Compatibility;

namespace Text_Grab.Views;

/// <summary>
/// Interaction logic for PersistentWindow.xaml
/// </summary>
public partial class GrabFrame : Window
{
    #region Fields

    public static RoutedCommand DeleteWordsCommand = new();
    public static RoutedCommand MergeWordsCommand = new();
    public static RoutedCommand PasteCommand = new();
    public static RoutedCommand RedoCommand = new();
    public static RoutedCommand UndoCommand = new();
    public static RoutedCommand GrabCommand = new();
    public static RoutedCommand GrabTrimCommand = new();
    private ResultTable? AnalyzedResultTable;
    private Point clickedPoint;
    private ILanguage? currentLanguage;
    private TextBox? destinationTextBox;
    private ImageSource? frameContentImageSource;
    private HistoryInfo? historyItem;
    private readonly GrabTemplate? _editingTemplate;
    private string? _currentImagePath;
    private bool hasLoadedImageSource = false;
    private bool IsDragOver = false;
    private bool isDrawing = false;
    private bool isLanguageBoxLoaded = false;
    private bool isMiddleDown = false;
    private bool IsOcrValid = false;
    private bool isSearchSelectionOverridden = false;
    private bool isSelecting;
    private bool isSpaceJoining = true;
    private bool isStaticImageSource = false;
    private readonly Dictionary<WordBorder, Rect> movingWordBordersDictionary = [];
    private IOcrLinesWords? ocrResultOfWindow;
    private UiAutomationOverlaySnapshot? frozenUiAutomationSnapshot;
    private UiAutomationOverlaySnapshot? liveUiAutomationSnapshot;
    private readonly DispatcherTimer frameMessageTimer = new();
    private readonly DispatcherTimer reDrawTimer = new();
    private readonly DispatcherTimer reSearchTimer = new();
    private Side resizingSide = Side.None;
    private readonly Border selectBorder = new();
    private Point startingMovingPoint;
    private readonly UndoRedo UndoRedo = new();
    private bool wasAltHeld = false;
    private bool isSyncingLanguageSelection = false;
    private double windowFrameImageScale = 1;
    private readonly ObservableCollection<WordBorder> wordBorders = [];
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private ScrollBehavior scrollBehavior = ScrollBehavior.Resize;
    private bool isTranslationEnabled = false;
    private string translationTargetLanguage = "English";
    private readonly DispatcherTimer translationTimer = new();
    private readonly Dictionary<WordBorder, string> originalTexts = [];
    private readonly SemaphoreSlim translationSemaphore = new(3); // Limit to 3 concurrent translations
    private int totalWordsToTranslate = 0;
    private int translatedWordsCount = 0;
    private CancellationTokenSource? translationCancellationTokenSource;
    private const string TargetLanguageMenuHeader = "Target Language";

    #endregion Fields

    #region Constructors

    public GrabFrame()
    {
        StandardInitialize();

        reDrawTimer.Start();
    }

    public GrabFrame(HistoryInfo historyInfo)
    {
        StandardInitialize();

        ShouldSaveOnClose = false;
        historyItem = historyInfo;
    }

    /// <summary>
    /// Creates a GrabFrame and loads the specified image file.
    /// </summary>
    /// <param name="imagePath">The path to the image file to load.</param>
    public GrabFrame(string imagePath)
    {
        StandardInitialize();

        ShouldSaveOnClose = true;

        // Validate the path before loading
        if (string.IsNullOrEmpty(imagePath))
        {
            Debug.WriteLine("GrabFrame: Empty image path provided");
            Loaded += (s, e) => MessageBox.Show("No image file path was provided.", "Text Grab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Convert to absolute path to handle relative paths correctly
        string absolutePath = Path.GetFullPath(imagePath);

        if (!File.Exists(absolutePath))
        {
            Debug.WriteLine($"GrabFrame: Image file not found: {absolutePath}");
            Loaded += (s, e) => MessageBox.Show($"Image file not found:\n{absolutePath}", "Text Grab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Loaded += async (s, e) => await TryLoadImageFromPath(absolutePath);
    }

    /// <summary>
    /// Creates a GrabFrame pre-loaded with a frozen image cropped from a Fullscreen Grab selection.
    /// The frame opens in freeze mode showing the provided bitmap and can render either OCR results
    /// or a pre-captured UI Automation snapshot, depending on the selected language.
    /// </summary>
    /// <param name="frozenImage">The cropped bitmap to display as the initial frozen background.</param>
    public GrabFrame(BitmapSource frozenImage, UiAutomationOverlaySnapshot? uiAutomationSnapshot = null)
    {
        StandardInitialize();

        ShouldSaveOnClose = true;
        frameContentImageSource = frozenImage;
        hasLoadedImageSource = true;
        isStaticImageSource = true;
        frozenUiAutomationSnapshot = uiAutomationSnapshot;

        Loaded += (s, e) =>
        {
            FreezeToggleButton.IsChecked = true;
            FreezeGrabFrame();
            reDrawTimer.Start();
        };
    }

    /// <summary>
    /// Opens GrabFrame in template editing mode with existing regions pre-loaded.
    /// </summary>
    /// <param name="template">The template to edit.</param>
    public GrabFrame(GrabTemplate template)
    {
        StandardInitialize();

        ShouldSaveOnClose = false;
        _editingTemplate = template;
        Title = $"Edit Template: {template.Name}";

        Loaded += async (s, e) => await LoadTemplateForEditing(template);
    }

    private async Task LoadTemplateForEditing(GrabTemplate template)
    {
        TemplateNameBox.Text = template.Name;

        SaveAsTemplateBTN.IsChecked = true;
        TemplateSavePanel.Visibility = Visibility.Visible;

        if (!string.IsNullOrEmpty(template.SourceImagePath) && File.Exists(template.SourceImagePath))
        {
            isStaticImageSource = true;
            await TryLoadImageFromPath(template.SourceImagePath);
            reDrawTimer.Stop();
        }
        else
        {
            // No reference image — freeze into a clean empty canvas without capturing the screen
            GrabFrameImage.Opacity = 0;
            FreezeToggleButton.IsChecked = true;
            FreezeToggleButton.Visibility = Visibility.Collapsed;
            Topmost = false;
            Background = new SolidColorBrush(Colors.DimGray);
            RectanglesBorder.Background.Opacity = 0;
            IsFreezeMode = true;
        }

        // Allow WPF to measure the canvas after the image loads
        await Task.Delay(150);

        double cw = RectanglesCanvas.ActualWidth;
        double ch = RectanglesCanvas.ActualHeight;

        if (cw <= 0) cw = template.ReferenceImageWidth;
        if (ch <= 0) ch = template.ReferenceImageHeight;

        foreach (TemplateRegion region in template.Regions.OrderBy(r => r.RegionNumber))
        {
            Rect abs = region.ToAbsoluteRect(cw, ch);

            WordBorder wb = new()
            {
                Width = Math.Max(abs.Width, 10),
                Height = Math.Max(abs.Height, 10),
                Left = abs.X,
                Top = abs.Y,
                Word = region.Label,
                OwnerGrabFrame = this,
                MatchingBackground = new SolidColorBrush(Colors.Black),
            };

            wordBorders.Add(wb);
            _ = RectanglesCanvas.Children.Add(wb);
        }

        EnterEditMode();
        UpdateTemplateBadges();
        UpdateTemplatePickerItems();

        // For editing, also add picker items for the template's specific pattern configurations
        // so SetSerializedText can match the exact placeholder values and recreate chips
        if (template.PatternMatches.Count > 0)
        {
            List<InlinePickerItem> items = [.. TemplateOutputBox.ItemsSource ?? []];
            foreach (TemplatePatternMatch pm in template.PatternMatches)
            {
                string displayLabel = $"{pm.PatternName} ({pm.MatchMode})";
                string value = BuildPatternPlaceholderValue(pm);
                // Only add if not already in the list (avoid duplicates with the default "first" items)
                if (!items.Any(i => i.Value == value))
                    items.Add(new InlinePickerItem(displayLabel, value, "Patterns"));
            }
            TemplateOutputBox.ItemsSource = items;
        }

        // Repopulate the output box AFTER ItemsSource is set so chips are recreated correctly
        TemplateOutputBox.SetSerializedText(template.OutputTemplate);
        reSearchTimer.Start();
    }

    private static string BuildPatternPlaceholderValue(TemplatePatternMatch config)
    {
        bool needsSeparator = config.MatchMode == "all"
            || (config.MatchMode.Contains(',') && config.MatchMode.Split(',').Length > 1);

        if (needsSeparator && config.Separator != ", ")
            return $"{{p:{config.PatternName}:{config.MatchMode}:{config.Separator}}}";

        return $"{{p:{config.PatternName}:{config.MatchMode}}}";
    }

    private async Task LoadContentFromHistory(HistoryInfo history)
    {
        FrameText = history.TextContent;
        currentLanguage = history.OcrLanguage;
        SyncLanguageComboBoxSelection(currentLanguage);
        isStaticImageSource = true;
        frozenUiAutomationSnapshot = null;
        liveUiAutomationSnapshot = null;

        string imageName = Path.GetFileName(history.ImagePath);

        System.Drawing.Bitmap? bgBitmap = await FileUtilities
            .GetImageFileAsync(
                imageName,
                FileStorageKind.WithHistory);

        if (bgBitmap is null)
        {
            Close();
            return;
        }

        history.ImageContent = bgBitmap;
        frameContentImageSource = ImageMethods.BitmapToImageSource(bgBitmap);
        hasLoadedImageSource = true;
        GrabFrameImage.Source = frameContentImageSource;
        FreezeGrabFrame();

        List<WordBorderInfo> wbInfoList = await Singleton<HistoryService>.Instance.GetWordBorderInfosAsync(history);

        if (wbInfoList.Count < 1)
            NotifyIfUiAutomationNeedsLiveSource(currentLanguage);

        if (history.PositionRect != Rect.Empty)
        {
            Left = history.PositionRect.Left;
            Top = history.PositionRect.Top;

            if (history.SourceMode == TextGrabMode.Fullscreen)
            {
                Size nonContentSize = GetGrabFrameNonContentSize();
                Width = history.PositionRect.Width + nonContentSize.Width;
                Height = history.PositionRect.Height + nonContentSize.Height;
            }
            else
            {
                Width = history.PositionRect.Width;
                Height = history.PositionRect.Height;
            }
        }

        if (wbInfoList.Count > 0)
        {
            ScaleHistoryWordBordersToCanvas(history, wbInfoList);

            foreach (WordBorderInfo info in wbInfoList)
            {
                WordBorder wb = new(info)
                {
                    OwnerGrabFrame = this
                };

                if (wb.IsBarcode)
                    wb.SetAsBarcode();

                wordBorders.Add(wb);
                _ = RectanglesCanvas.Children.Add(wb);
            }
        }
        else
        {
            reDrawTimer.Start();
            ShouldSaveOnClose = true;
        }

        TableToggleButton.IsChecked = history.IsTable;

        UpdateFrameText();
        history.ClearTransientImage();
    }

    private Size GetGrabFrameNonContentSize()
    {
        const double defaultNonContentWidth = 4;
        const double defaultNonContentHeight = 74;

        UpdateLayout();

        if (ActualWidth <= 1 || ActualHeight <= 1
            || RectanglesBorder.ActualWidth <= 1 || RectanglesBorder.ActualHeight <= 1)
        {
            return new Size(defaultNonContentWidth, defaultNonContentHeight);
        }

        double nonContentWidth = ActualWidth - RectanglesBorder.ActualWidth;
        double nonContentHeight = ActualHeight - RectanglesBorder.ActualHeight;

        if (!double.IsFinite(nonContentWidth) || nonContentWidth < 0 || nonContentWidth > 100)
            nonContentWidth = defaultNonContentWidth;

        if (!double.IsFinite(nonContentHeight) || nonContentHeight < 0 || nonContentHeight > 200)
            nonContentHeight = defaultNonContentHeight;

        return new Size(nonContentWidth, nonContentHeight);
    }

    private void ScaleHistoryWordBordersToCanvas(HistoryInfo history, List<WordBorderInfo> wbInfoList)
    {
        if (wbInfoList.Count == 0 || RectanglesCanvas.Width <= 0 || RectanglesCanvas.Height <= 0)
            return;

        Size savedContentSize = GetSavedHistoryContentSize(history);
        if (savedContentSize.Width <= 0 || savedContentSize.Height <= 0)
            return;

        double scaleX = RectanglesCanvas.Width / savedContentSize.Width;
        double scaleY = RectanglesCanvas.Height / savedContentSize.Height;
        if (!double.IsFinite(scaleX) || !double.IsFinite(scaleY) || (scaleX <= 1.05 && scaleY <= 1.05))
            return;

        double maxRight = wbInfoList.Max(info => info.BorderRect.Right);
        double maxBottom = wbInfoList.Max(info => info.BorderRect.Bottom);

        // Scale only when saved word borders look like they were captured in
        // the old window-content coordinate space rather than image-space.
        if (maxRight > savedContentSize.Width * 1.1 || maxBottom > savedContentSize.Height * 1.1)
            return;

        foreach (WordBorderInfo info in wbInfoList)
        {
            Rect borderRect = info.BorderRect;
            info.BorderRect = new Rect(
                borderRect.Left * scaleX,
                borderRect.Top * scaleY,
                borderRect.Width * scaleX,
                borderRect.Height * scaleY);
        }
    }

    private Size GetSavedHistoryContentSize(HistoryInfo history)
    {
        if (history.ImageContent is System.Drawing.Bitmap imageContentBitmap
            && imageContentBitmap.Width > 0 && imageContentBitmap.Height > 0)
        {
            return new Size(imageContentBitmap.Width, imageContentBitmap.Height);
        }

        Rect positionRect = history.PositionRect;
        if (positionRect == Rect.Empty || positionRect.Width <= 0 || positionRect.Height <= 0)
            return new Size(0, 0);

        if (history.SourceMode == TextGrabMode.Fullscreen)
            return new Size(positionRect.Width, positionRect.Height);

        Size nonContentSize = GetGrabFrameNonContentSize();
        double contentWidth = positionRect.Width - nonContentSize.Width;
        double contentHeight = positionRect.Height - nonContentSize.Height;

        if (!double.IsFinite(contentWidth) || contentWidth <= 0)
            contentWidth = positionRect.Width;

        if (!double.IsFinite(contentHeight) || contentHeight <= 0)
            contentHeight = positionRect.Height;

        return new Size(contentWidth, contentHeight);
    }

    /// <summary>
    /// Returns the physical-pixel screen rectangle that exactly covers the
    /// transparent content area (RectanglesBorder, Row 1 of the grid).
    /// Uses PointToScreen so it is always accurate regardless of border
    /// thickness, DPI, or future layout changes.
    /// </summary>
    internal System.Drawing.Rectangle GetContentAreaScreenRect()
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        Point topLeft = RectanglesBorder.PointToScreen(new Point(0, 0));
        return new System.Drawing.Rectangle(
            (int)topLeft.X,
            (int)topLeft.Y,
            (int)(RectanglesBorder.ActualWidth * dpi.DpiScaleX),
            (int)(RectanglesBorder.ActualHeight * dpi.DpiScaleY));
    }

    public Rect GetImageContentRect()
    {
        // This is a WIP to try to remove the gray letterboxes on either
        // side of the image when zooming it.

        if (frameContentImageSource is null || !IsLoaded || !RectanglesCanvas.IsLoaded)
            return Rect.Empty;

        Rect canvasPlacement = RectanglesCanvas.GetAbsolutePlacement(true);
        if (canvasPlacement == Rect.Empty)
            return Rect.Empty;

        Size rectCanvasSize = RectanglesCanvas.RenderSize;
        if (!double.IsFinite(rectCanvasSize.Width) || !double.IsFinite(rectCanvasSize.Height)
            || rectCanvasSize.Width <= 0 || rectCanvasSize.Height <= 0)
        {
            return canvasPlacement;
        }

        return new Rect(canvasPlacement.X, canvasPlacement.Y, rectCanvasSize.Width, rectCanvasSize.Height);
    }

    private void StandardInitialize()
    {
        InitializeComponent();
        App.SetTheme();

        _ = LoadOcrLanguagesAsync();

        SetRestoreState();

        WindowResizer resizer = new(this);
        reDrawTimer.Interval = new(0, 0, 0, 0, 500);
        reDrawTimer.Tick += ReDrawTimer_Tick;

        reSearchTimer.Interval = new(0, 0, 0, 0, 300);
        reSearchTimer.Tick += ReSearchTimer_Tick;

        translationTimer.Interval = new(0, 0, 0, 0, 1000);
        translationTimer.Tick += TranslationTimer_Tick;

        frameMessageTimer.Interval = TimeSpan.FromSeconds(4);
        frameMessageTimer.Tick += FrameMessageTimer_Tick;

        _ = UndoRedo.HasUndoOperations();
        _ = UndoRedo.HasRedoOperations();

        GetGrabFrameUserSettings();
        SetRefreshOrOcrFrameBtnVis();

        DataContext = this;
    }

    private void FrameMessageTimer_Tick(object? sender, EventArgs e)
    {
        frameMessageTimer.Stop();
        HideFrameMessage();
    }

    private void HideFrameMessage()
    {
        FrameMessageBorder.Visibility = Visibility.Collapsed;
        FrameMessageTextBlock.Text = string.Empty;
    }

    private void ShowFrameMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        FrameMessageTextBlock.Text = message;
        FrameMessageBorder.Visibility = Visibility.Visible;
        frameMessageTimer.Stop();
        frameMessageTimer.Start();
    }

    private void SyncLanguageComboBoxSelection(ILanguage language)
    {
        if (LanguagesComboBox.Items.Count == 0)
            return;

        List<ILanguage> availableLanguages = [.. LanguagesComboBox.Items.OfType<ILanguage>()];
        int selectedIndex = CaptureLanguageUtilities.FindPreferredLanguageIndex(
            availableLanguages,
            language.LanguageTag,
            language);

        if (selectedIndex < 0 || LanguagesComboBox.SelectedIndex == selectedIndex)
            return;

        isSyncingLanguageSelection = true;
        try
        {
            LanguagesComboBox.SelectedIndex = selectedIndex;
            currentLanguage = availableLanguages[selectedIndex];
        }
        finally
        {
            isSyncingLanguageSelection = false;
        }
    }

    #endregion Constructors

    #region Properties

    public ILanguage CurrentLanguage
    {
        get
        {
            if (currentLanguage is not null)
                return currentLanguage;

            if (LanguagesComboBox.SelectedItem is ILanguage selectedILang)
                currentLanguage = selectedILang;
            else if (LanguagesComboBox.SelectedItem is Language selectedLang) // Should not happen if ComboBox is populated with ILanguage
                currentLanguage = new GlobalLang(selectedLang);

            currentLanguage ??= LanguageUtilities.GetOCRLanguage();

            return currentLanguage;
        }
    }

    public TextBox? DestinationTextBox
    {
        get => destinationTextBox;
        set
        {
            destinationTextBox = value;
            if (destinationTextBox is not null)
                EditTextToggleButton.IsChecked = true;
            else
                EditTextToggleButton.IsChecked = false;
        }
    }

    public string FrameText { get; private set; } = string.Empty;
    public bool IsCtrlDown => KeyboardExtensions.IsCtrlDown() || AddEditOcrMenuItem.IsChecked is true;
    public bool IsEditingAnyWordBorders => wordBorders.Any(x => x.IsEditing);
    public bool IsFreezeMode { get; set; } = false;
    public bool IsFromEditWindow => destinationTextBox is not null;
    public bool IsWordEditMode { get; set; } = true;

    public bool ShouldSaveOnClose { get; set; } = true;

    #endregion Properties

    #region Methods

    public static bool CheckKey(VirtualKeyCodes code)
    {
        return (GetKeyState(code) & 0xFF00) == 0xFF00;
    }

    public HistoryInfo AsHistoryItem()
    {
        System.Drawing.Bitmap? bitmap = ImageMethods.ImageSourceToBitmap(frameContentImageSource);

        List<WordBorderInfo> wbInfoList = [];

        foreach (WordBorder wb in wordBorders)
            wbInfoList.Add(new WordBorderInfo(wb));

        string? wbInfoJson = null;
        if (wbInfoList.Count > 0)
        {
            try
            {
                wbInfoJson = JsonSerializer.Serialize(wbInfoList);
            }
            catch
            {
                wbInfoJson = null;
#if DEBUG
                throw;
#endif
            }
        }

        Rect sizePosRect = new()
        {
            Width = Width,
            Height = Height,
            X = Left,
            Y = Top
        };

        string id = string.Empty;
        if (historyItem is not null)
            id = historyItem.ID;

        HistoryInfo historyInfo = new()
        {
            ID = id,
            LanguageTag = CurrentLanguage.LanguageTag,
            LanguageKind = LanguageUtilities.GetLanguageKind(currentLanguage ?? CurrentLanguage),
            CaptureDateTime = DateTimeOffset.UtcNow,
            TextContent = FrameText,
            WordBorderInfoJson = wbInfoJson,
            WordBorderInfoFileName = wbInfoJson is null ? null : historyItem?.WordBorderInfoFileName,
            ImageContent = bitmap,
            PositionRect = sizePosRect,
            IsTable = TableToggleButton.IsChecked!.Value,
            SourceMode = TextGrabMode.GrabFrame,
        };

        return historyInfo;
    }

    public void BreakWordBorderIntoWords(WordBorder wordBorder)
    {
        ICollection<string> wordLines = wordBorder.Word.Split(Environment.NewLine);

        const double widthScaleAdjustFactor = 1.5;
        ShouldSaveOnClose = true;

        double top = wordBorder.Top;
        double left = wordBorder.Left;
        int numberOfLines = wordLines.Count;
        double wordHeight = wordBorder.Height / numberOfLines;

        DeleteThisWordBorder(wordBorder, false);
        UndoRedo.StartTransaction();

        int lineIterator = 0;
        foreach (string line in wordLines)
        {
            double lineWidth = GetWidthOfString(line, (int)wordBorder.Width, (int)wordHeight);
            ICollection<string> lineWords = line.Split();

            double wordFractionWidth = lineWidth / lineWords.Count;
            // double diffBetweenWordAndBorder = (wordBorder.Width - (lineWidth / widthScaleAdjustFactor)) / lineWords.Count;

            foreach (string word in lineWords)
            {
                double wordWidth = (double)GetWidthOfString(word, (int)wordFractionWidth, (int)wordHeight) / widthScaleAdjustFactor;
                WordBorder wordBorderBox = new()
                {
                    Width = wordWidth,
                    Height = wordHeight,
                    Word = word,
                    OwnerGrabFrame = this,
                    Top = top + (lineIterator * wordHeight),
                    Left = left,
                    MatchingBackground = wordBorder.MatchingBackground,
                };

                wordBorders.Add(wordBorderBox);
                _ = RectanglesCanvas.Children.Add(wordBorderBox);

                UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.AddWordBorder,
                    new GrabFrameOperationArgs()
                    {
                        WordBorder = wordBorderBox,
                        WordBorders = wordBorders,
                        GrabFrameCanvas = RectanglesCanvas
                    });

                left += wordWidth; // + diffBetweenWordAndBorder;
            }
            lineIterator++;
            left = wordBorder.Left;
        }
        UndoRedo.EndTransaction();
    }

    public void DeleteThisWordBorder(WordBorder wordBorder, bool startEndTransaction = true)
    {
        ShouldSaveOnClose = true;
        wordBorders.Remove(wordBorder);
        RectanglesCanvas.Children.Remove(wordBorder);

        if (startEndTransaction)
            UndoRedo.StartTransaction();

        List<WordBorder> deletedWordBorder = [wordBorder];
        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = deletedWordBorder,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });

        if (startEndTransaction)
            UndoRedo.EndTransaction();

        reSearchTimer.Start();
    }

    public async void GrabFrame_Loaded(object sender, RoutedEventArgs e)
    {
        PreviewMouseWheel += HandlePreviewMouseWheel;
        PreviewKeyDown += Window_PreviewKeyDown;
        PreviewKeyUp += Window_PreviewKeyUp;

        RoutedCommand escapeCmd = new();
        _ = escapeCmd.InputGestures.Add(new KeyGesture(Key.Escape));
        _ = CommandBindings.Add(new CommandBinding(escapeCmd, Escape_Keyed));

        RoutedCommand pasteCommand = new();
        _ = pasteCommand.InputGestures.Add(new KeyGesture(Key.V, ModifierKeys.Control | ModifierKeys.Shift));
        _ = CommandBindings.Add(new CommandBinding(pasteCommand, PasteExecuted));

        _ = GrabCommand.InputGestures.Add(new KeyGesture(Key.G, ModifierKeys.Control));
        // _ = CommandBindings.Add(new CommandBinding(GrabCommand, GrabExecuted));

        _ = GrabTrimCommand.InputGestures.Add(new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Control));

        CheckBottomRowButtonsVis();

        if (historyItem is not null)
            await LoadContentFromHistory(historyItem);
    }

    public void GrabFrame_Unloaded(object sender, RoutedEventArgs e)
    {
        Activated -= GrabFrameWindow_Activated;
        Closed -= Window_Closed;
        Deactivated -= GrabFrameWindow_Deactivated;
        DragLeave -= GrabFrameWindow_DragLeave;
        DragOver -= GrabFrameWindow_DragOver;
        Loaded -= GrabFrame_Loaded;
        LocationChanged -= Window_LocationChanged;
        SizeChanged -= Window_SizeChanged;
        Unloaded -= GrabFrame_Unloaded;
        PreviewMouseWheel -= HandlePreviewMouseWheel;
        PreviewKeyDown -= Window_PreviewKeyDown;
        PreviewKeyUp -= Window_PreviewKeyUp;

        reDrawTimer.Stop();
        reDrawTimer.Tick -= ReDrawTimer_Tick;

        frameMessageTimer.Stop();
        frameMessageTimer.Tick -= FrameMessageTimer_Tick;

        translationTimer.Stop();
        translationTimer.Tick -= TranslationTimer_Tick;
        translationSemaphore.Dispose();
        translationCancellationTokenSource?.Cancel();
        translationCancellationTokenSource?.Dispose();

        // Dispose the shared translation model during cleanup to prevent resource leaks
        WindowsAiUtilities.DisposeTranslationModel();

        MinimizeButton.Click -= OnMinimizeButtonClick;
        RestoreButton.Click -= OnRestoreButtonClick;
        CloseButton.Click -= OnCloseButtonClick;

        RectanglesCanvas.MouseDown -= RectanglesCanvas_MouseDown;
        RectanglesCanvas.MouseMove -= RectanglesCanvas_MouseMove;
        RectanglesCanvas.MouseUp -= RectanglesCanvas_MouseUp;

        AspectRationMI.Checked -= AspectRationMI_Checked;
        AspectRationMI.Unchecked -= AspectRationMI_Checked;
        FreezeMI.Click -= FreezeMI_Click;

        SearchBox.TextChanged -= SearchBox_TextChanged;

        ClearBTN.Click -= ClearBTN_Click;
        ExactMatchChkBx.Click -= ExactMatchChkBx_Click;

        RefreshBTN.Click -= RefreshBTN_Click;
        FreezeToggleButton.Click -= FreezeToggleButton_Click;
        TableToggleButton.Click -= TableToggleButton_Click;
        EditToggleButton.Click -= EditToggleButton_Click;
        SettingsBTN.Click -= SettingsBTN_Click;
        EditTextToggleButton.Click -= EditTextBTN_Click;
    }

    public void MergeSelectedWordBorders()
    {
        ShouldSaveOnClose = true;
        RectanglesCanvas.ContextMenu.IsOpen = false;
        FreezeGrabFrame();

        List<WordBorder> selectedWordBorders = [.. wordBorders.Where(w => w.IsSelected).OrderBy(o => o.Left)];

        if (selectedWordBorders.Count < 2)
            return;

        Windows.Foundation.Rect bounds = new()
        {
            X = selectedWordBorders.Select(w => w.Left).Min(),
            Y = selectedWordBorders.Select(w => w.Top).Min(),
            Width = selectedWordBorders.Select(w => w.Right).Max() - selectedWordBorders.Select(w => w.Left).Min(),
            Height = selectedWordBorders.Select(w => w.Bottom).Max() - selectedWordBorders.Select(w => w.Top).Min()
        };

        UndoRedo.StartTransaction();

        List<WordBorder> deletedWordBorders = DeleteSelectedWordBorders();
        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = deletedWordBorders,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });


        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        // Build merged content via model-only ResultTable
        List<WordBorderInfo> selInfos = [.. selectedWordBorders.Select(wb => new WordBorderInfo(wb))];
        ResultTable tmp = new();
        tmp.AnalyzeAsTable(selInfos, new System.Drawing.Rectangle(0, 0, (int)ActualWidth, (int)ActualHeight));
        StringBuilder sb = new();
        ResultTable.GetTextFromTabledWordBorders(sb, selInfos, CurrentLanguage.IsSpaceJoining());
        string mergedContent = sb.ToString().Replace('\t', ' ');

        SolidColorBrush backgroundBrush = new(Colors.Black);
        System.Drawing.Bitmap? bmp = null;

        if (frameContentImageSource is BitmapImage bmpImg)
            bmp = ImageMethods.BitmapSourceToBitmap(bmpImg);

        Windows.Foundation.Rect lineRect = new()
        {
            X = bounds.X * windowFrameImageScale,
            Y = bounds.Y * windowFrameImageScale,
            Width = bounds.Width * windowFrameImageScale,
            Height = bounds.Height * windowFrameImageScale,
        };

        if (bmp is not null)
            backgroundBrush = GetBackgroundBrushFromBitmap(ref dpi, windowFrameImageScale, bmp, ref lineRect);

        WordBorder wordBorderBox = new()
        {
            Width = bounds.Width,
            Height = bounds.Height,
            Word = mergedContent,
            OwnerGrabFrame = this,
            Top = bounds.Top,
            Left = bounds.Left,
            MatchingBackground = backgroundBrush,
        };

        wordBorders.Add(wordBorderBox);
        _ = RectanglesCanvas.Children.Add(wordBorderBox);

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.AddWordBorder,
            new GrabFrameOperationArgs()
            {
                WordBorder = wordBorderBox,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });
        UndoRedo.EndTransaction();
        // Get a Result Table of the selected borders

        // Go from 0,0 from the top down, left to right adding to word Border

        reSearchTimer.Start();
    }

    public void OnRedo()
    {
        UndoRedo.Redo();
        reSearchTimer.Start();
    }

    public void OnUndo()
    {
        UndoRedo.Undo();
        reSearchTimer.Start();
    }

    public List<WordBorder> SelectedWordBorders()
    {
        return [.. wordBorders.Where(w => w.IsSelected)];
    }

    public void StartWordBorderMoveResize(WordBorder wordBorder, Side sideEnum)
    {
        startingMovingPoint = new(wordBorder.Left, wordBorder.Top);
        resizingSide = sideEnum;

        ICollection<WordBorder> bordersMoving = [wordBorder];

        if (sideEnum == Side.None)
            bordersMoving = SelectedWordBorders();

        foreach (WordBorder b in bordersMoving)
        {
            Rect originalSize = new(b.Left, b.Top, b.Width, b.Height);
            movingWordBordersDictionary.Add(b, originalSize);
        }
    }

    public void UndoableWordChange(WordBorder wordBorder, string oldWord, bool isSingleTransaction)
    {
        ShouldSaveOnClose = true;
        if (isSingleTransaction)
            UndoRedo.StartTransaction();

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.ChangeWord, new GrabFrameOperationArgs()
        {
            WordBorder = wordBorder,
            OldWord = oldWord,
            NewWord = wordBorder.Word
        });

        if (isSingleTransaction)
            UndoRedo.EndTransaction();
    }

    public void WordChanged()
    {
        reSearchTimer.Stop();
        reSearchTimer.Start();
    }
    internal void SearchForSimilar(WordBorder wordBorder)
    {
        TextBox wordTextBox = wordBorder.EditWordTextBox;
        string wordPattern = wordBorder.Word.ExtractSimplePattern();
        if (wordTextBox.SelectionLength != 0)
            wordPattern = wordTextBox.SelectedText;
        SearchWithRegexCheckBox.IsChecked = true;
        Keyboard.Focus(SearchBox);
        SearchBox.Text = wordPattern;
    }

    [LibraryImport("user32.dll")]
    private static partial short GetKeyState(VirtualKeyCodes code);

    private static float GetWidthOfString(string str, int width, int height)
    {
        using System.Drawing.Bitmap objBitmap = new(width, height);
        using System.Drawing.Graphics objGraphics = System.Drawing.Graphics.FromImage(objBitmap);

        System.Drawing.SizeF stringSize = objGraphics.MeasureString(str, new System.Drawing.Font("Segoe UI", (int)(height * 0.8)));
        return stringSize.Width;
    }

    // If the data object in args is a single file, this method will return the filename.
    // Otherwise, it returns null.
    private static string? IsSingleFile(DragEventArgs args)
    {
        // Check for files in the hovering data object.
        if (args.Data.GetDataPresent(DataFormats.FileDrop, true))
        {
            string[]? fileNames = args.Data.GetData(DataFormats.FileDrop, true) as string[];
            // Check for a single file or folder.
            if (fileNames?.Length is 1)
            {
                // Check for a file (a directory will return false).
                if (File.Exists(fileNames[0]))
                {
                    // At this point we know there is a single file.
                    return fileNames[0];
                }
            }
        }
        return null;
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<FirstRunWindow>();
    }

    private async void AddNewWordBorder(Border selectBorder)
    {
        FreezeGrabFrame();

        ShouldSaveOnClose = true;
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        SolidColorBrush backgroundBrush = new(Colors.Black);
        System.Drawing.Bitmap? bmp = null;

        double viewBoxZoomFactor = CanvasViewBox.GetHorizontalScaleFactor();
        Rect rect = selectBorder.GetAbsolutePlacement(true);
        rect = new(rect.X + 4, rect.Y, (rect.Width * dpi.DpiScaleX) + 10, rect.Height * dpi.DpiScaleY);
        // Language language = CurrentLanguage.AsLanguage() ?? LanguageUtilities.GetCurrentInputLanguage().AsLanguage() ?? new Language("en-US");
        ILanguage language = CurrentLanguage ?? LanguageUtilities.GetCurrentInputLanguage();
        string ocrText = await OcrUtilities.GetTextFromAbsoluteRectAsync(
            rect.GetScaleSizeByFraction(viewBoxZoomFactor),
            language,
            GetUiAutomationExcludedHandles());

        if (language is not UiAutomationLang && DefaultSettings.CorrectErrors)
            ocrText = ocrText.TryFixEveryWordLetterNumberErrors();

        if (language is not UiAutomationLang && DefaultSettings.CorrectToLatin)
            ocrText = ocrText.ReplaceGreekOrCyrillicWithLatin();

        if (frameContentImageSource is BitmapImage bmpImg)
            bmp = ImageMethods.BitmapSourceToBitmap(bmpImg);

        Windows.Foundation.Rect lineRect = new()
        {
            X = ((Canvas.GetLeft(selectBorder) * windowFrameImageScale) - 10) * dpi.DpiScaleX,
            Y = (Canvas.GetTop(selectBorder) * windowFrameImageScale) * dpi.DpiScaleY,
            Width = (selectBorder.Width * windowFrameImageScale) * dpi.DpiScaleX,
            Height = (selectBorder.Height * windowFrameImageScale) * dpi.DpiScaleY,
        };

        if (bmp is not null)
            backgroundBrush = GetBackgroundBrushFromBitmap(ref dpi, windowFrameImageScale, bmp, ref lineRect);

        UndoRedo.StartTransaction();

        WordBorder wordBorderBox = new()
        {
            Width = selectBorder.Width,
            Height = selectBorder.Height - 3,
            Word = ocrText.Trim(),
            OwnerGrabFrame = this,
            Top = Canvas.GetTop(selectBorder) + 3,
            Left = Canvas.GetLeft(selectBorder),
            MatchingBackground = backgroundBrush,
        };

        wordBorders.Add(wordBorderBox);
        _ = RectanglesCanvas.Children.Add(wordBorderBox);
        wordBorderBox.EnterEdit();
        await Task.Delay(50);
        wordBorderBox.Deselect();
        wordBorderBox.FocusTextbox();

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.AddWordBorder,
            new GrabFrameOperationArgs()
            {
                WordBorder = wordBorderBox,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });
        UndoRedo.EndTransaction();
        reSearchTimer.Start();
    }

    private void AspectRationMI_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem aspectMI)
            return;

        if (aspectMI.IsChecked is false)
            CanvasViewBox.Stretch = Stretch.Fill;
        else
            CanvasViewBox.Stretch = Stretch.Uniform;
    }

    private void AutoOcrCheckBox_Click(object sender, RoutedEventArgs e)
    {
        reDrawTimer.Start();
    }

    private void CanChangeWordBorderExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (wordBorders.Any(x => x.IsSelected))
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void CanExecuteMergeWordBorders(object sender, CanExecuteRoutedEventArgs e)
    {
        if (SelectedWordBorders().Count > 1)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void CanPasteExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (System.Windows.Clipboard.ContainsImage())
        {
            e.CanExecute = true;
            return;
        }

        e.CanExecute = false;
    }

    private void CanRedoExecuted(object sender, CanExecuteRoutedEventArgs e)
    {
        if (UndoRedo.HasRedoOperations())
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void CanUndoCommand(object sender, CanExecuteRoutedEventArgs e)
    {
        if (UndoRedo.HasUndoOperations())
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void CheckBottomRowButtonsVis()
    {
        if (Width < 270)
            ButtonsStackPanel.Visibility = Visibility.Collapsed;
        else
            ButtonsStackPanel.Visibility = Visibility.Visible;

        if (Width < 390)
        {
            SearchBox.Visibility = Visibility.Collapsed;
            ClearBTN.Visibility = Visibility.Collapsed;
            MatchesMenu.Visibility = Visibility.Collapsed;
        }
        else
        {
            SearchBox.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(SearchBox.Text))
                ClearBTN.Visibility = Visibility.Visible;
            else
                ClearBTN.Visibility = Visibility.Collapsed;
        }

        if (Width < 480)
            LanguagesComboBox.Visibility = Visibility.Collapsed;
        else
            LanguagesComboBox.Visibility = Visibility.Visible;
    }

    private void CheckSelectBorderIntersections(bool finalCheck = false)
    {
        Rect rectSelect = new(Canvas.GetLeft(selectBorder), Canvas.GetTop(selectBorder), selectBorder.Width, selectBorder.Height);

        bool clickedEmptySpace = true;
        bool smallSelection = false;
        if (rectSelect.Width < 10 && rectSelect.Height < 10)
            smallSelection = true;

        foreach (WordBorder wordBorder in wordBorders)
        {
            Rect wbRect = new(Canvas.GetLeft(wordBorder), Canvas.GetTop(wordBorder), wordBorder.Width, wordBorder.Height);

            if (rectSelect.IntersectsWith(wbRect))
            {
                clickedEmptySpace = false;

                if (!smallSelection)
                {
                    wordBorder.Select();
                    wordBorder.WasRegionSelected = true;
                }
                else if (!finalCheck)
                {
                    if (wordBorder.IsSelected)
                        wordBorder.Deselect();
                    else
                        wordBorder.Select();
                    wordBorder.WasRegionSelected = false;
                }

            }
            else
            {
                if (wordBorder.WasRegionSelected
                    && !smallSelection)
                    wordBorder.Deselect();
            }

            if (finalCheck)
                wordBorder.WasRegionSelected = false;
        }

        if (clickedEmptySpace
            && smallSelection
            && finalCheck)
        {
            foreach (WordBorder wb in wordBorders)
                wb.Deselect();
        }

        if (finalCheck)
            UpdateFrameText();
    }

    private void ClearBTN_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        MatchesMenu.Visibility = Visibility.Collapsed;
    }

    private async void ContactMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = await Launcher.LaunchUriAsync(new Uri(string.Format("mailto:support@textgrab.net")));
    }

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetDataObject(FrameText, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy text to clipboard: {ex.Message}");
        }
    }

    private List<WordBorder> DeleteSelectedWordBorders()
    {
        FreezeGrabFrame();

        List<WordBorder> selectedWordBorders = [.. wordBorders.Where(x => x.IsSelected)];

        if (selectedWordBorders.Count == 0)
            return selectedWordBorders;


        foreach (WordBorder wordBorder in selectedWordBorders)
        {
            RectanglesCanvas.Children.Remove(wordBorder);
            wordBorders.Remove(wordBorder);
        }

        return selectedWordBorders;
    }

    private void DeleteWordBordersExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        ShouldSaveOnClose = true;
        UndoRedo.StartTransaction();
        List<WordBorder> deletedWordBorders = DeleteSelectedWordBorders();
        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = deletedWordBorders,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });

        UndoRedo.EndTransaction();
        reSearchTimer.Start();
    }

    private void ClearRenderedWordBorders()
    {
        RectanglesCanvas.Children.Clear();
        wordBorders.Clear();
    }

    private IReadOnlyCollection<IntPtr>? GetUiAutomationExcludedHandles()
    {
        IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        return handle == IntPtr.Zero ? null : [handle];
    }

    private (double ViewBoxZoomFactor, double BorderToCanvasX, double BorderToCanvasY) GetOverlayRenderMetrics()
    {
        double viewBoxZoomFactor = CanvasViewBox.GetHorizontalScaleFactor();
        if (!double.IsFinite(viewBoxZoomFactor) || viewBoxZoomFactor <= 0 || viewBoxZoomFactor > 4)
            viewBoxZoomFactor = 1;

        Point canvasOriginInBorder = RectanglesCanvas.TranslatePoint(new Point(0, 0), RectanglesBorder);
        return (viewBoxZoomFactor, -canvasOriginInBorder.X, -canvasOriginInBorder.Y);
    }

    private WordBorder CreateWordBorderFromSourceRect(
        Windows.Foundation.Rect sourceRect,
        double sourceScale,
        string text,
        int lineNumber,
        SolidColorBrush backgroundBrush,
        DpiScale dpi,
        double viewBoxZoomFactor,
        double borderToCanvasX,
        double borderToCanvasY)
    {
        return new()
        {
            Width = ((sourceRect.Width / (dpi.DpiScaleX * sourceScale)) + 2) / viewBoxZoomFactor,
            Height = ((sourceRect.Height / (dpi.DpiScaleY * sourceScale)) + 2) / viewBoxZoomFactor,
            Top = ((sourceRect.Y / (dpi.DpiScaleY * sourceScale) - 1) + borderToCanvasY) / viewBoxZoomFactor,
            Left = ((sourceRect.X / (dpi.DpiScaleX * sourceScale) - 1) + borderToCanvasX) / viewBoxZoomFactor,
            Word = text,
            OwnerGrabFrame = this,
            LineNumber = lineNumber,
            IsFromEditWindow = IsFromEditWindow,
            MatchingBackground = backgroundBrush,
        };
    }

    private void AddRenderedWordBorder(WordBorder wordBorderBox)
    {
        if (!IsOcrValid)
            return;

        wordBorders.Add(wordBorderBox);
        _ = RectanglesCanvas.Children.Add(wordBorderBox);

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.AddWordBorder,
            new GrabFrameOperationArgs()
            {
                WordBorder = wordBorderBox,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });
    }

    private Task DrawRectanglesAroundWords(string searchWord = "")
    {
        return CurrentLanguage is UiAutomationLang
            ? DrawUiAutomationRectanglesAsync(searchWord)
            : DrawOcrRectanglesAsync(searchWord);
    }

    private async Task DrawOcrRectanglesAsync(string searchWord = "")
    {
        if (isDrawing || IsDragOver)
            return;

        isDrawing = true;
        IsOcrValid = true;

        if (string.IsNullOrWhiteSpace(searchWord))
            searchWord = SearchBox.Text;

        ClearRenderedWordBorders();

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        System.Drawing.Rectangle rectCanvasSize = GetContentAreaScreenRect();
        if (rectCanvasSize.Width <= 0 || rectCanvasSize.Height <= 0)
        {
            isDrawing = false;
            reDrawTimer.Start();
            return;
        }

        if (ocrResultOfWindow is null || ocrResultOfWindow.Lines.Length == 0)
        {
            ILanguage lang = CurrentLanguage ?? LanguageUtilities.GetCurrentInputLanguage();
            (ocrResultOfWindow, windowFrameImageScale) = await OcrUtilities.GetOcrResultFromRegionAsync(rectCanvasSize, CurrentLanguage);
        }

        if (ocrResultOfWindow is null)
        {
            isDrawing = false;
            reDrawTimer.Start();
            return;
        }

        isSpaceJoining = CurrentLanguage!.IsSpaceJoining();

        System.Drawing.Bitmap? bmp = null;
        bool shouldDisposeBmp = false;

        if (isStaticImageSource && frameContentImageSource is BitmapSource bmpImg)
        {
            bmp = ImageMethods.BitmapSourceToBitmap(bmpImg);
            shouldDisposeBmp = true;
        }
        else
        {
            bmp = ImageMethods.GetRegionOfScreenAsBitmap(rectCanvasSize, cacheResult: false);
            shouldDisposeBmp = true;
        }

        int lineNumber = 0;
        (double viewBoxZoomFactor, double borderToCanvasX, double borderToCanvasY) = GetOverlayRenderMetrics();

        foreach (IOcrLine ocrLine in ocrResultOfWindow.Lines)
        {
            StringBuilder lineText = new();
            ocrLine.GetTextFromOcrLine(isSpaceJoining, lineText);
            lineText.RemoveTrailingNewlines();

            Windows.Foundation.Rect lineRect = ocrLine.BoundingBox;

            SolidColorBrush backgroundBrush = new(Colors.Black);

            if (bmp is not null)
                backgroundBrush = GetBackgroundBrushFromOcrBitmap(windowFrameImageScale, bmp, ref lineRect);

            string ocrText = lineText.ToString();

            if (DefaultSettings.CorrectErrors)
                ocrText = ocrText.TryFixEveryWordLetterNumberErrors();

            if (DefaultSettings.CorrectToLatin)
                ocrText = ocrText.ReplaceGreekOrCyrillicWithLatin();

            WordBorder wordBorderBox = CreateWordBorderFromSourceRect(
                lineRect,
                windowFrameImageScale,
                ocrText,
                lineNumber,
                backgroundBrush,
                dpi,
                viewBoxZoomFactor,
                borderToCanvasX,
                borderToCanvasY);

            if (CurrentLanguage!.IsRightToLeft())
            {
                StringBuilder sb = new(ocrText);
                sb.ReverseWordsForRightToLeft();
                sb.RemoveTrailingNewlines();
                wordBorderBox.Word = sb.ToString();
            }

            AddRenderedWordBorder(wordBorderBox);

            lineNumber++;
        }

        SetRotationBasedOnOcrResult();

        if (DefaultSettings.TryToReadBarcodes)
            TryToReadBarcodes(dpi);

        if (IsWordEditMode)
            EnterEditMode();

        isDrawing = false;

        if (shouldDisposeBmp)
            bmp?.Dispose();
        reSearchTimer.Start();

        // Trigger translation if enabled
        if (isTranslationEnabled && WindowsAiUtilities.CanDeviceUseWinAI())
        {
            translationTimer.Stop();
            translationTimer.Start();
        }
    }

    private async Task DrawUiAutomationRectanglesAsync(string searchWord = "")
    {
        if (isDrawing || IsDragOver)
            return;

        isDrawing = true;
        IsOcrValid = true;

        if (string.IsNullOrWhiteSpace(searchWord))
            searchWord = SearchBox.Text;

        ClearRenderedWordBorders();

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        System.Drawing.Rectangle rectCanvasSize = GetContentAreaScreenRect();
        if (rectCanvasSize.Width <= 0 || rectCanvasSize.Height <= 0)
        {
            isDrawing = false;
            reDrawTimer.Start();
            return;
        }

        UiAutomationOverlaySnapshot? overlaySnapshot = null;
        if (isStaticImageSource && frozenUiAutomationSnapshot is not null)
        {
            overlaySnapshot = frozenUiAutomationSnapshot;
        }
        else
        {
            liveUiAutomationSnapshot = await UIAutomationUtilities.GetOverlaySnapshotFromRegionAsync(
                new Rect(rectCanvasSize.X, rectCanvasSize.Y, rectCanvasSize.Width, rectCanvasSize.Height),
                GetUiAutomationExcludedHandles());
            overlaySnapshot = liveUiAutomationSnapshot;
        }

        if (overlaySnapshot is null || overlaySnapshot.Items.Count == 0)
        {
            isDrawing = false;

            if (DefaultSettings.UiAutomationFallbackToOcr)
            {
                await DrawOcrRectanglesAsync(searchWord);
                return;
            }

            reSearchTimer.Start();
            return;
        }

        System.Drawing.Bitmap? bmp = Singleton<HistoryService>.Instance.CachedBitmap;
        bool shouldDisposeBmp = false;

        if (bmp is null && frameContentImageSource is BitmapSource bmpImg)
        {
            bmp = ImageMethods.BitmapSourceToBitmap(bmpImg);
            shouldDisposeBmp = true;
        }

        (double viewBoxZoomFactor, double borderToCanvasX, double borderToCanvasY) = GetOverlayRenderMetrics();
        Rect sourceBounds = overlaySnapshot.CaptureBounds;
        int lineNumber = 0;

        foreach (UiAutomationOverlayItem overlayItem in overlaySnapshot.Items)
        {
            Rect relativeBounds = new(
                overlayItem.ScreenBounds.X - sourceBounds.X,
                overlayItem.ScreenBounds.Y - sourceBounds.Y,
                overlayItem.ScreenBounds.Width,
                overlayItem.ScreenBounds.Height);

            if (relativeBounds == Rect.Empty || relativeBounds.Width < 1 || relativeBounds.Height < 1)
                continue;

            Windows.Foundation.Rect sourceRect = new(relativeBounds.X, relativeBounds.Y, relativeBounds.Width, relativeBounds.Height);
            SolidColorBrush backgroundBrush = new(Colors.Black);

            if (bmp is not null)
                backgroundBrush = GetBackgroundBrushFromBitmap(ref dpi, 1, bmp, ref sourceRect);

            WordBorder wordBorderBox = CreateWordBorderFromSourceRect(
                sourceRect,
                1,
                overlayItem.Text,
                lineNumber,
                backgroundBrush,
                dpi,
                viewBoxZoomFactor,
                borderToCanvasX,
                borderToCanvasY);

            AddRenderedWordBorder(wordBorderBox);
            lineNumber++;
        }

        isDrawing = false;

        if (shouldDisposeBmp)
            bmp?.Dispose();

        reSearchTimer.Start();

        if (isTranslationEnabled && WindowsAiUtilities.CanDeviceUseWinAI())
        {
            translationTimer.Stop();
            translationTimer.Start();
        }
    }

    private void EditMatchesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        List<WordBorder> selectedWords = [.. wordBorders.Where(m => m.IsSelected)];
        if (selectedWords.Count == 0)
            return;

        EditTextWindow editWindow = new();
        bool isSpaceJoiningLang = CurrentLanguage.IsSpaceJoining();
        DpiScale dpiScale = VisualTreeHelper.GetDpi(this);

        // Convert to model-only infos and generate text
        List<WordBorderInfo> infos = [.. selectedWords.Select(wb => new WordBorderInfo(wb))];
        ResultTable tmp = new();
        tmp.AnalyzeAsTable(infos, new System.Drawing.Rectangle(0, 0, (int)ActualWidth, (int)ActualHeight));
        StringBuilder sb = new();
        ResultTable.GetTextFromTabledWordBorders(sb, infos, isSpaceJoiningLang);
        string stringForETW = sb.ToString();

        editWindow.AddThisText(stringForETW);
        editWindow.Show();
    }

    private void EditTextBTN_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        if (sender is ToggleButton toggleButton
            && toggleButton.IsChecked is false
            && destinationTextBox is not null)
        {
            destinationTextBox.SelectedText = "";
            destinationTextBox = null;
            return;
        }

        if (destinationTextBox is null)
        {
            EditTextWindow etw = WindowUtilities.OpenOrActivateWindow<EditTextWindow>();
            destinationTextBox = etw.GetMainTextBox();
        }

        UpdateFrameText();
    }

    private void EditToggleButton_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        if (EditToggleButton.IsChecked is bool isEditMode && isEditMode)
        {
            if (!IsFreezeMode)
            {
                FreezeToggleButton.IsChecked = true;
                FreezeGrabFrame();
            }

            EnterEditMode();
        }
        else
            ExitEditMode();
    }

    private void EnterEditMode()
    {
        IsWordEditMode = true;

        foreach (UIElement uIElement in RectanglesCanvas.Children)
        {
            if (uIElement is WordBorder wb)
                wb.EnterEdit();
        }
    }

    private void Escape_Keyed(object sender, ExecutedRoutedEventArgs e)
    {
        if (wordBorders.Any(x => x.IsEditing))
        {
            GrabBTN.Focus();
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchBox.Text) && SearchBox.Text != "Search For Text...")
            SearchBox.Text = "";
        else if (RectanglesCanvas.Children.Count > 0)
            ResetGrabFrame();
        else
            Close();
    }

    private void ExactMatchChkBx_Click(object sender, RoutedEventArgs e)
    {
        reSearchTimer.Stop();
        reSearchTimer.Start();
    }

    private void ExitEditMode()
    {
        IsWordEditMode = false;

        foreach (UIElement uIElement in RectanglesCanvas.Children)
        {
            if (uIElement is WordBorder wb)
                wb.ExitEdit();
        }
    }

    private void FeedbackMenuItem_Click(object sender, RoutedEventArgs ev)
    {
        Uri source = new("https://github.com/TheJoeFin/Text-Grab/issues", UriKind.Absolute);
        RequestNavigateEventArgs e = new(source, "https://github.com/TheJoeFin/Text-Grab/issues");
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void FreezeGrabFrame()
    {
        GrabFrameImage.Opacity = 1;
        if (frameContentImageSource is not null)
            GrabFrameImage.Source = frameContentImageSource;
        else
        {
            isStaticImageSource = false;
            frozenUiAutomationSnapshot = null;
            frameContentImageSource = ImageMethods.GetWindowBoundsImage(this);
            GrabFrameImage.Source = frameContentImageSource;
        }

        SyncRectanglesCanvasSizeToImage();

        FreezeToggleButton.IsChecked = true;
        Topmost = false;
        Background = new SolidColorBrush(Colors.DimGray);
        RectanglesBorder.Background.Opacity = 0;
        IsFreezeMode = true;
    }

    private void SyncRectanglesCanvasSizeToImage()
    {
        if (GrabFrameImage.Source is not BitmapSource source)
            return;

        // Convert physical pixels to WPF device-independent pixels so the canvas
        // coordinate space stays consistent with DrawRectanglesAroundWords, which
        // divides OCR pixel coordinates by dpi.DpiScaleX/Y to produce DIP positions.
        // Using raw PixelWidth would cause the Viewbox to scale down at DPI > 100%,
        // shifting viewBoxZoomFactor and borderToCanvasX/Y, and misplacing word borders.
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double sourceWidth = source.PixelWidth > 0 ? source.PixelWidth / dpi.DpiScaleX : source.Width;
        double sourceHeight = source.PixelHeight > 0 ? source.PixelHeight / dpi.DpiScaleY : source.Height;

        if (double.IsFinite(sourceWidth) && sourceWidth > 0)
        {
            GrabFrameImage.Width = sourceWidth;
            RectanglesCanvas.Width = sourceWidth;
        }

        if (double.IsFinite(sourceHeight) && sourceHeight > 0)
        {
            GrabFrameImage.Height = sourceHeight;
            RectanglesCanvas.Height = sourceHeight;
        }
    }

    private async void FreezeMI_Click(object sender, RoutedEventArgs e)
    {
        if (IsFreezeMode)
        {
            FreezeToggleButton.IsChecked = false;
            UnfreezeGrabFrame();
            ResetGrabFrame();
        }
        else
        {
            RectanglesCanvas.ContextMenu.IsOpen = false;
            await Task.Delay(150);
            FreezeToggleButton.IsChecked = true;
            ResetGrabFrame();
            FreezeGrabFrame();
        }

        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    private void FreezeToggleButton_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        if (FreezeToggleButton.IsChecked is bool freezeMode && freezeMode)
            FreezeGrabFrame();
        else
            UnfreezeGrabFrame();
    }

    private static SolidColorBrush GetBackgroundBrushFromOcrBitmap(double scale, System.Drawing.Bitmap bmp, ref Windows.Foundation.Rect lineRect)
    {
        if (!double.IsFinite(scale) || scale <= 0)
            scale = 1;

        double boxLeft = lineRect.Left / scale;
        double boxTop = lineRect.Top / scale;
        double boxRight = lineRect.Right / scale;
        double boxBottom = lineRect.Bottom / scale;
        double boxWidth = Math.Max(0, boxRight - boxLeft);
        double boxHeight = Math.Max(0, boxBottom - boxTop);
        double insetX = Math.Min(boxWidth / 2, Math.Max(1, boxWidth * 0.12));
        double insetY = Math.Min(boxHeight / 2, Math.Max(1, boxHeight * 0.12));

        int pxLeft = Math.Clamp((int)(boxLeft + insetX), 0, bmp.Width - 1);
        int pxTop = Math.Clamp((int)(boxTop + insetY), 0, bmp.Height - 1);
        int pxRight = Math.Clamp((int)(boxRight - insetX), 0, bmp.Width - 1);
        int pxBottom = Math.Clamp((int)(boxBottom - insetY), 0, bmp.Height - 1);

        if (pxRight < pxLeft)
            pxRight = pxLeft;

        if (pxBottom < pxTop)
            pxBottom = pxTop;

        System.Drawing.Color pxColorLeftTop = bmp.GetPixel(pxLeft, pxTop);
        System.Drawing.Color pxColorRightTop = bmp.GetPixel(pxRight, pxTop);
        System.Drawing.Color pxColorRightBottom = bmp.GetPixel(pxRight, pxBottom);
        System.Drawing.Color pxColorLeftBottom = bmp.GetPixel(pxLeft, pxBottom);

        List<Color> mediaColorList =
        [
            ColorHelper.MediaColorFromDrawingColor(pxColorLeftTop),
            ColorHelper.MediaColorFromDrawingColor(pxColorRightTop),
            ColorHelper.MediaColorFromDrawingColor(pxColorRightBottom),
            ColorHelper.MediaColorFromDrawingColor(pxColorLeftBottom),
        ];

        Color? mostCommonColor = mediaColorList.GroupBy(c => c)
                                               .OrderBy(g => g.Count())
                                               .LastOrDefault()?.Key;

        if (mostCommonColor is not null)
            return new SolidColorBrush(mostCommonColor.Value);

        return ColorHelper.SolidColorBrushFromDrawingColor(pxColorLeftTop);
    }

    private SolidColorBrush GetBackgroundBrushFromBitmap(ref DpiScale dpi, double scale, System.Drawing.Bitmap bmp, ref Windows.Foundation.Rect lineRect)
    {
        SolidColorBrush backgroundBrush = new(Colors.Black);
        double pxToRectanglesFactor = (RectanglesCanvas.ActualWidth / bmp.Width) * dpi.DpiScaleX;
        double boxLeft = lineRect.Left / (dpi.DpiScaleX * scale);
        double boxTop = lineRect.Top / (dpi.DpiScaleY * scale);
        double boxRight = lineRect.Right / (dpi.DpiScaleX * scale);
        double boxBottom = lineRect.Bottom / (dpi.DpiScaleY * scale);

        double leftFraction = boxLeft / RectanglesCanvas.ActualWidth;
        double topFraction = boxTop / RectanglesCanvas.ActualHeight;
        double rightFraction = boxRight / RectanglesCanvas.ActualWidth;
        double bottomFraction = boxBottom / RectanglesCanvas.ActualHeight;

        int rawLeft = Math.Clamp((int)(leftFraction * bmp.Width), 0, bmp.Width - 1);
        int rawTop = Math.Clamp((int)(topFraction * bmp.Height), 0, bmp.Height - 1);
        int rawRight = Math.Clamp((int)(rightFraction * bmp.Width), 0, bmp.Width - 1);
        int rawBottom = Math.Clamp((int)(bottomFraction * bmp.Height), 0, bmp.Height - 1);

        int spanX = Math.Max(0, rawRight - rawLeft);
        int spanY = Math.Max(0, rawBottom - rawTop);
        int insetX = Math.Min(spanX / 2, Math.Max(1, spanX / 8));
        int insetY = Math.Min(spanY / 2, Math.Max(1, spanY / 8));
        int pxLeft = Math.Clamp(rawLeft + insetX, 0, bmp.Width - 1);
        int pxTop = Math.Clamp(rawTop + insetY, 0, bmp.Height - 1);
        int pxRight = Math.Clamp(rawRight - insetX, 0, bmp.Width - 1);
        int pxBottom = Math.Clamp(rawBottom - insetY, 0, bmp.Height - 1);

        if (pxRight < pxLeft)
            pxRight = pxLeft;

        if (pxBottom < pxTop)
            pxBottom = pxTop;

        System.Drawing.Color pxColorLeftTop = bmp.GetPixel(pxLeft, pxTop);
        System.Drawing.Color pxColorRightTop = bmp.GetPixel(pxRight, pxTop);
        System.Drawing.Color pxColorRightBottom = bmp.GetPixel(pxRight, pxBottom);
        System.Drawing.Color pxColorLeftBottom = bmp.GetPixel(pxLeft, pxBottom);

        List<Color> MediaColorList =
        [
            ColorHelper.MediaColorFromDrawingColor(pxColorLeftTop),
            ColorHelper.MediaColorFromDrawingColor(pxColorRightTop),
            ColorHelper.MediaColorFromDrawingColor(pxColorRightBottom),
            ColorHelper.MediaColorFromDrawingColor(pxColorLeftBottom),
        ];

        Color? MostCommonColor = MediaColorList.GroupBy(c => c)
                                               .OrderBy(g => g.Count())
                                               .LastOrDefault()?.Key;

        backgroundBrush = ColorHelper.SolidColorBrushFromDrawingColor(pxColorLeftTop);

        if (MostCommonColor is not null)
            backgroundBrush = new SolidColorBrush(MostCommonColor.Value);

        return backgroundBrush;
    }

    private void GetGrabFrameUserSettings()
    {
        AutoOcrCheckBox.IsChecked = DefaultSettings.GrabFrameAutoOcr;
        AlwaysUpdateEtwCheckBox.IsChecked = DefaultSettings.GrabFrameUpdateEtw;
        CloseOnGrabMenuItem.IsChecked = DefaultSettings.CloseFrameOnGrab;
        ReadBarcodesMenuItem.IsChecked = DefaultSettings.GrabFrameReadBarcodes;
        GetGrabFrameTranslationSettings();
        _ = Enum.TryParse(DefaultSettings.GrabFrameScrollBehavior, out scrollBehavior);
        SetScrollBehaviorMenuItems();
    }

    private void GrabFrameWindow_Activated(object? sender, EventArgs e)
    {
        RectanglesCanvas.Opacity = 1;
        if (!IsWordEditMode && !IsFreezeMode)
            reDrawTimer.Start();
        else
            reSearchTimer.Start();
    }

    private void GrabFrameWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (ShouldSaveOnClose)
            Singleton<HistoryService>.Instance.SaveToHistory(this);

        historyItem?.ClearTransientImage();

        FrameText = "";
        wordBorders.Clear();
        UpdateFrameText();
    }

    private void GrabFrameWindow_Deactivated(object? sender, EventArgs e)
    {
        if (!IsWordEditMode && !IsFreezeMode)
        {
            ResetGrabFrame();
            return;
        }

        RectanglesCanvas.Opacity = 1;
        if (Keyboard.Modifiers != ModifierKeys.Alt)
            wasAltHeld = false;

        if (AutoOcrCheckBox.IsChecked is true)
            FreezeGrabFrame();
    }

    private void GrabFrameWindow_DragLeave(object sender, DragEventArgs e)
    {
        IsDragOver = false;
    }

    private void GrabFrameWindow_DragOver(object sender, DragEventArgs e)
    {
        IsDragOver = true;
        // As an arbitrary design decision, we only want to deal with a single file.
        e.Effects = IsSingleFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        // Mark the event as handled, so TextBox's native DragOver handler is not called.
        e.Handled = true;
    }

    private async void GrabFrameWindow_Drop(object sender, DragEventArgs e)
    {
        // Mark the event as handled, so TextBox's native Drop handler is not called.
        e.Handled = true;
        string? fileName = IsSingleFile(e);
        if (fileName is null) return;

        Activate();
        frameContentImageSource = null;
        isStaticImageSource = true;

        await TryLoadImageFromPath(fileName);

        IsDragOver = false;

        reDrawTimer.Start();
    }

    private void GrabFrameWindow_Initialized(object sender, EventArgs e)
    {
        WindowUtilities.SetWindowPosition(this);
        CheckBottomRowButtonsVis();
    }

    private bool HandleCtrlCombo(Key key)
    {
        switch (key)
        {
            case Key.A:
                SelectAllWordBorders();
                break;
            case Key.I:
                InvertSelection();
                break;
            case Key.M:
                MergeSelectedWordBorders();
                break;
            case Key.O:
                OpenImageMenuItem_Click();
                break;
            case Key.R:
                RefreshBTN_Click();
                break;
            case Key.Y:
                OnRedo();
                break;
            case Key.Z:
                OnUndo();
                break;
            default:
                return false;
        }
        return true;
    }

    private void HandleDelete(object? sender = null, RoutedEventArgs? e = null)
    {
        if (Keyboard.FocusedElement is TextBox)
            return;

        UndoRedo.StartTransaction();
        List<WordBorder> deletedWordBorders = DeleteSelectedWordBorders();
        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = deletedWordBorders,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });

        UndoRedo.EndTransaction();
        reSearchTimer.Start();
    }

    private bool HandleHotKey(Key key)
    {
        switch (key)
        {
            case Key.E:
                EditTextToggleButton.IsChecked = !EditTextToggleButton.IsChecked;
                EditTextBTN_Click();
                break;
            case Key.F:
                if (FreezeToggleButton.Visibility == Visibility.Collapsed)
                    return false;
                FreezeToggleButton.IsChecked = !FreezeToggleButton.IsChecked;
                FreezeToggleButton_Click();
                break;
            case Key.T:
                TableToggleButton.IsChecked = !TableToggleButton.IsChecked;
                TableToggleButton_Click();
                break;
            default:
                return false;
        }
        return true;
    }

    private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Source: StackOverflow, read on Sep. 10, 2021
        // https://stackoverflow.com/a/53698638/7438031

        if (WindowState == WindowState.Maximized
            || scrollBehavior == ScrollBehavior.None)
            return;

        if (scrollBehavior == ScrollBehavior.Zoom)
        {
            if (!IsFreezeMode)
                FreezeGrabFrame();

            return;
        }

        e.Handled = true;
        double aspectRatio = (Height - 66) / (Width - 4);

        float changeFraction = 0.2f;
        double widthDelta = Width * changeFraction;
        double offsetDelta = Width * (changeFraction / 2);

        if (e.Delta > 0)
        {
            Width += widthDelta;
            Left -= offsetDelta;

            if (!KeyboardExtensions.IsShiftDown())
            {
                Height += (widthDelta) * aspectRatio;
                Top -= (offsetDelta) * aspectRatio;
            }
        }
        else if (e.Delta < 0)
        {
            if (Width > 120 && Height > 120)
            {
                Width -= widthDelta;
                Left += offsetDelta;

                if (!KeyboardExtensions.IsShiftDown())
                {
                    Height -= (widthDelta) * aspectRatio;
                    Top += (offsetDelta) * aspectRatio;
                }
            }
        }
    }

    private void InvertSelection(object? sender = null, RoutedEventArgs? e = null)
    {
        foreach (WordBorder wordBorder in wordBorders)
        {
            if (wordBorder.IsSelected)
                wordBorder.Deselect();
            else
                wordBorder.Select();
        }
    }

    private void LanguagesComboBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            DefaultSettings.LastUsedLang = string.Empty;
            DefaultSettings.Save();
            LanguageUtilities.InvalidateOcrLanguageCache();
        }
    }

    private void NotifyIfUiAutomationNeedsLiveSource(ILanguage language)
    {
        if (!CaptureLanguageUtilities.RequiresLiveUiAutomationSource(
            language,
            isStaticImageSource,
            frozenUiAutomationSnapshot is not null))
            return;

        string message = DefaultSettings.UiAutomationFallbackToOcr
            ? "UI Automation reads live application controls. This Grab Frame currently contains a static image, so Text Grab will fall back to OCR for image-only operations."
            : "UI Automation reads live application controls. This Grab Frame currently contains a static image, so image-only operations will not return UI Automation text.";

        MessageBox.Show(message, "Text Grab", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LanguagesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox langComboBox
            || langComboBox.SelectedItem is not ILanguage pickedLang)
            return;

        if (isSyncingLanguageSelection)
        {
            currentLanguage = pickedLang;
            return;
        }

        if (!isLanguageBoxLoaded)
            return;

        HideFrameMessage();
        currentLanguage = pickedLang;
        CaptureLanguageUtilities.PersistSelectedLanguage(pickedLang);
        NotifyIfUiAutomationNeedsLiveSource(pickedLang);

        ResetGrabFrame();

        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    private async Task LoadOcrLanguagesAsync()
    {
        if (LanguagesComboBox.Items.Count > 0)
            return;

        List<ILanguage> availableLanguages = await CaptureLanguageUtilities.GetCaptureLanguagesAsync(includeTesseract: false);
        foreach (ILanguage language in availableLanguages)
            LanguagesComboBox.Items.Add(language);

        ILanguage preferredLanguage = currentLanguage ?? LanguageUtilities.GetOCRLanguage();
        int selectedIndex = CaptureLanguageUtilities.FindPreferredLanguageIndex(
            availableLanguages,
            currentLanguage?.LanguageTag ?? DefaultSettings.LastUsedLang,
            preferredLanguage);

        if (selectedIndex >= 0)
        {
            isSyncingLanguageSelection = true;
            try
            {
                LanguagesComboBox.SelectedIndex = selectedIndex;
                currentLanguage = availableLanguages[selectedIndex];
            }
            finally
            {
                isSyncingLanguageSelection = false;
            }
        }

        isLanguageBoxLoaded = true;
    }

    private async void MenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        await Singleton<HistoryService>.Instance.PopulateMenuItemWithRecentGrabs(OpenRecentGrabsMenuItem);
    }

    private void MergeWordBordersExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        MergeSelectedWordBorders();
    }

    private void MoveAllWordBorders(Point movingPoint)
    {
        if (movingWordBordersDictionary.Count == 0)
            return;

        foreach (WordBorder movingWb in movingWordBordersDictionary.Keys)
        {
            Rect previousSize = movingWordBordersDictionary[movingWb];
            MoveResizeWordBorder(movingPoint, movingWb, previousSize);
        }
    }

    private void MoveResizeWordBorder(Point movingPoint, WordBorder movingWordBorder, Rect prevSize)
    {
        double xShiftDelta = (movingPoint.X - clickedPoint.X);
        double yShiftDelta = (movingPoint.Y - clickedPoint.Y);
        Canvas.SetZIndex(movingWordBorder, wordBorders.Count + 1);

        switch (resizingSide)
        {
            case Side.Left:
                double newWidth = prevSize.Width - xShiftDelta;
                if (newWidth > 20)
                {
                    movingWordBorder.Width = newWidth;
                    Canvas.SetLeft(movingWordBorder, Canvas.GetLeft(movingWordBorder) + xShiftDelta);
                }
                movingWordBorder.Width = newWidth;
                movingWordBorder.Left = movingPoint.X;
                break;
            case Side.Right:
                double newRight = movingPoint.X - movingWordBorder.Left;
                if (newRight > 20)
                    movingWordBorder.Width = newRight;
                break;
            case Side.Bottom:
                double newBottom = movingPoint.Y - movingWordBorder.Top;
                if (newBottom > 12)
                    movingWordBorder.Height = newBottom;
                break;
            case Side.Top:
                double newHeight = prevSize.Height - yShiftDelta;
                if (newHeight > 12)
                {
                    movingWordBorder.Height = newHeight;
                    movingWordBorder.Top = movingPoint.Y;
                }
                break;
            default:
                movingWordBorder.Left = prevSize.X + xShiftDelta;
                movingWordBorder.Top = prevSize.Y + yShiftDelta;
                break;
        }
    }

    private void MoveWindowWithMiddleMouse(Point movingPoint)
    {
        double xShiftDelta = (movingPoint.X - clickedPoint.X);
        double yShiftDelta = (movingPoint.Y - clickedPoint.Y);

        Top += yShiftDelta;
        Left += xShiftDelta;
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnRestoreButtonClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else
            WindowState = WindowState.Maximized;

        SetRestoreState();
    }

    private async void OpenImageMenuItem_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        // Create OpenFileDialog 
        Microsoft.Win32.OpenFileDialog dlg = new()
        {
            // Set filter for file extension and default file extension
            Filter = FileUtilities.GetImageFilter()
        };

        bool? result = dlg.ShowDialog();

        if (result is false || !File.Exists(dlg.FileName))
            return;

        await TryLoadImageFromPath(dlg.FileName);

        reDrawTimer.Start();
    }

    private async void PasteExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        (bool success, ImageSource? clipboardImage) = ClipboardUtilities.TryGetImageFromClipboard();

        if (!success || clipboardImage is null)
            return;

        reDrawTimer.Stop();

        ResetGrabFrame();
        await Task.Delay(300);

        if (clipboardImage is System.Windows.Interop.InteropBitmap interopBitmap)
        {
            System.Drawing.Bitmap bmp = ImageMethods.InteropBitmapToBitmap(interopBitmap);
            frameContentImageSource = ImageMethods.BitmapToImageSource(bmp);
        }
        else
        {
            frameContentImageSource = clipboardImage;
        }

        hasLoadedImageSource = true;
        isStaticImageSource = true;
        frozenUiAutomationSnapshot = null;
        liveUiAutomationSnapshot = null;
        FreezeToggleButton.IsChecked = true;
        FreezeGrabFrame();
        FreezeToggleButton.Visibility = Visibility.Collapsed;
        NotifyIfUiAutomationNeedsLiveSource(CurrentLanguage);

        reDrawTimer.Start();
    }

    private async void RateAndReview_Click(object sender, RoutedEventArgs e)
    {
        _ = await Launcher.LaunchUriAsync(new Uri(string.Format("ms-windows-store:REVIEW?PFN={0}", "40087JoeFinApps.TextGrab_kdbpvth5scec4")));
    }

    private void RectanglesCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        reDrawTimer.Stop();
        GrabBTN.Focus();

        if (e.RightButton == MouseButtonState.Pressed)
        {
            e.Handled = false;
            return;
        }

        if (MainZoomBorder.CanPan)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                MainZoomBorder.Reset();
                return;
            }

            if (!KeyboardExtensions.IsShiftDown() && !KeyboardExtensions.IsCtrlDown())
                return;
        }

        isSelecting = true;
        clickedPoint = e.GetPosition(RectanglesCanvas);
        RectanglesCanvas.CaptureMouse();
        selectBorder.Height = 1;
        selectBorder.Width = 1;

        isSearchSelectionOverridden = true;

        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            e.Handled = true;

            isMiddleDown = true;
            ResetGrabFrame();
            UnfreezeGrabFrame();
            return;
        }

        CursorClipper.ClipCursor(RectanglesBorder);

        try { RectanglesCanvas.Children.Remove(selectBorder); } catch (Exception) { }

        selectBorder.BorderThickness = new Thickness(2);
        Color borderColor = Color.FromArgb(255, 40, 118, 126);
        selectBorder.BorderBrush = new SolidColorBrush(borderColor);
        Color backgroundColor = Color.FromArgb(15, 40, 118, 126);
        selectBorder.Background = new SolidColorBrush(backgroundColor);
        _ = RectanglesCanvas.Children.Add(selectBorder);
        Canvas.SetLeft(selectBorder, clickedPoint.X);
        Canvas.SetTop(selectBorder, clickedPoint.Y);
    }

    private void RectanglesCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (IsCtrlDown)
            RectanglesCanvas.Cursor = Cursors.Cross;
        else if (MainZoomBorder.CanPan)
            RectanglesCanvas.Cursor = Cursors.SizeAll;
        else
            RectanglesCanvas.Cursor = null;

        if (!isSelecting && !isMiddleDown && movingWordBordersDictionary.Count == 0)
            return;

        isMiddleDown = e.MiddleButton == MouseButtonState.Pressed;

        if (MainZoomBorder.CanPan
            && !KeyboardExtensions.IsShiftDown()
            && !KeyboardExtensions.IsCtrlDown())
        {
            isSelecting = false;
            return;
        }

        Point movingPoint = e.GetPosition(RectanglesCanvas);

        double left = Math.Min(clickedPoint.X, movingPoint.X);
        double top = Math.Min(clickedPoint.Y, movingPoint.Y);

        if (isMiddleDown)
        {
            MoveWindowWithMiddleMouse(movingPoint);
            return;
        }

        if (movingWordBordersDictionary.Count > 0)
        {
            FreezeGrabFrame();
            MoveAllWordBorders(movingPoint);
            return;
        }

        selectBorder.Width = Math.Max(clickedPoint.X, movingPoint.X) - left;
        Canvas.SetLeft(selectBorder, left);

        selectBorder.Height = Math.Max(clickedPoint.Y, movingPoint.Y) - top;
        Canvas.SetTop(selectBorder, top);

        if (IsCtrlDown)
        {
            double smallestHeight = 6;
            double largestHeight = Height;
            double gridSnapSize = 3.0;

            selectBorder.Height = Math.Clamp(selectBorder.Height, smallestHeight, largestHeight);
            selectBorder.Height = Math.Round(selectBorder.Height / gridSnapSize) * gridSnapSize;
            selectBorder.Width = Math.Round(selectBorder.Width / gridSnapSize) * gridSnapSize;
        }
        else
            CheckSelectBorderIntersections();
    }

    private void RectanglesCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        isSelecting = false;
        CursorClipper.UnClipCursor();
        RectanglesCanvas.ReleaseMouseCapture();

        if (e.ChangedButton == MouseButton.Middle && scrollBehavior != ScrollBehavior.Zoom)
        {
            isMiddleDown = false;
            FreezeGrabFrame();
            reDrawTimer.Start();
            return;
        }

        if (movingWordBordersDictionary.Count > 0)
        {
            UndoRedo.StartTransaction();

            foreach (WordBorder movedWb in movingWordBordersDictionary.Keys)
            {
                Rect previousSize = movingWordBordersDictionary[movedWb];
                UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.ResizeWordBorder,
                    new GrabFrameOperationArgs()
                    {
                        WordBorder = movedWb,
                        OldSize = previousSize,
                        NewSize = new(movedWb.Left, movedWb.Top, movedWb.Width, movedWb.Height)
                    });
            }
            UndoRedo.EndTransaction();
        }

        if (IsCtrlDown && movingWordBordersDictionary.Count == 0
            && selectBorder.Height > 6 && selectBorder.Width > 6)
            AddNewWordBorder(selectBorder);

        try { RectanglesCanvas.Children.Remove(selectBorder); } catch { }

        movingWordBordersDictionary.Clear();
        resizingSide = Side.None;
        CheckSelectBorderIntersections(true);
        UpdateFrameText();
    }

    private void RedoExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        UndoRedo.Redo();
    }

    private async void ReDrawTimer_Tick(object? sender, EventArgs? e)
    {
        reDrawTimer.Stop();
        SetRefreshOrOcrFrameBtnVis();

        if (!IsLoaded || RectanglesBorder.ActualWidth <= 1 || RectanglesBorder.ActualHeight <= 1)
        {
            reDrawTimer.Start();
            return;
        }

        if (CheckKey(VirtualKeyCodes.LeftButton) || CheckKey(VirtualKeyCodes.MiddleButton))
        {
            reDrawTimer.Start();
            return;
        }

        // does not re-OCR frame content at zoomed level
        // it just takes the original source image
        if (frameContentImageSource is null)
        {
            frameContentImageSource = ImageMethods.GetWindowBoundsImage(this);
            GrabFrameImage.Source = frameContentImageSource;
        }

        if (AutoOcrCheckBox.IsChecked is false)
            return;

        if (SearchBox.Text is string searchText)
            await DrawRectanglesAroundWords(searchText);
    }

    private async void RefreshBTN_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        if (CaptureLanguageUtilities.RequiresLiveUiAutomationSource(
            CurrentLanguage,
            isStaticImageSource,
            frozenUiAutomationSnapshot is not null))
        {
            ShowFrameMessage("Cannot use UI Automation on a saved image. Switch to an OCR language to refresh.");
            return;
        }

        HideFrameMessage();
        reDrawTimer.Stop();

        UndoRedo.StartTransaction();

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
new GrabFrameOperationArgs()
{
    RemovingWordBorders = [.. wordBorders],
    WordBorders = wordBorders,
    GrabFrameCanvas = RectanglesCanvas
});

        if (hasLoadedImageSource)
        {
            // For loaded images, clear OCR results and re-run OCR on the same image.
            // Zoom must be reset because the screen-capture-based OCR pipeline
            // calculates word border positions assuming no zoom transform.
            MainZoomBorder.Reset();
            RectanglesCanvas.RenderTransform = Transform.Identity;
            IsOcrValid = false;
            ocrResultOfWindow = null;
            ClearRenderedWordBorders();
            MatchesTXTBLK.Text = "- Matches";
            UpdateFrameText();

            // Allow WPF to repaint the unzoomed view before screen-capture OCR
            await Task.Delay(200);
        }
        else
        {
            ResetGrabFrame();

            await Task.Delay(200);

            frameContentImageSource = ImageMethods.GetWindowBoundsImage(this);
            GrabFrameImage.Source = frameContentImageSource;
        }

        if (AutoOcrCheckBox.IsChecked is false)
            FreezeGrabFrame();

        if (SearchBox.Text is string searchText)
            await DrawRectanglesAroundWords(searchText);

        UndoRedo.EndTransaction();
    }

    private void RemoveTableLines()
    {
        Canvas? tableLines = null;

        foreach (object? child in RectanglesCanvas.Children)
            if (child is Canvas element && element.Tag is "TableLines")
                tableLines = element;

        RectanglesCanvas.Children.Remove(tableLines);
    }

    private void ReSearchTimer_Tick(object? sender, EventArgs e)
    {
        reSearchTimer.Stop();
        if (SearchBox.Text is not string searchText)
            return;

        if (string.IsNullOrWhiteSpace(searchText) && !isSearchSelectionOverridden)
        {
            foreach (WordBorder wb in wordBorders)
                wb.Deselect();
            MatchesTXTBLK.Text = $"0 Matches";
            UpdateFrameText();
            return;
        }

        if (SearchWithRegexCheckBox.IsChecked is false && ExactMatchChkBx.IsChecked is bool matchExactly)
            searchText = searchText.EscapeSpecialRegexChars(matchExactly);

        Regex regex;

        try
        {
            regex = new(searchText, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

            if (ExactMatchChkBx.IsChecked is true)
                regex = new(searchText, RegexOptions.Multiline);
        }
        catch (Exception)
        {
            foreach (WordBorder wb in wordBorders)
                wb.Deselect();
            UpdateFrameText();
            MatchesTXTBLK.Text = $"Search Error";
            return;
        }

        int numberOfMatches = 0;

        if (!isSearchSelectionOverridden)
        {
            foreach (WordBorder wb in wordBorders)
            {
                int numberOfMatchesInWord = regex.Count(wb.Word);
                numberOfMatches += numberOfMatchesInWord;

                if (numberOfMatchesInWord > 0)
                    wb.Select();
                else
                    wb.Deselect();
            }
        }

        UpdateFrameText();

        if (string.IsNullOrEmpty(searchText))
        {
            MatchesMenu.Visibility = Visibility.Collapsed;
            return;
        }

        if (numberOfMatches == 1)
            MatchesTXTBLK.Text = $"{numberOfMatches} Match";
        else
            MatchesTXTBLK.Text = $"{numberOfMatches} Matches";
        MatchesMenu.Visibility = Visibility.Visible;
        LanguagesComboBox.Visibility = Visibility.Collapsed;

        if (SaveAsTemplateBTN.IsChecked == true)
            UpdateTemplateBadges();
    }

    private void ResetGrabFrame()
    {
        SetRefreshOrOcrFrameBtnVis();

        MainZoomBorder.Reset();
        RectanglesCanvas.RenderTransform = Transform.Identity;
        RectanglesCanvas.ClearValue(WidthProperty);
        RectanglesCanvas.ClearValue(HeightProperty);
        GrabFrameImage.ClearValue(WidthProperty);
        GrabFrameImage.ClearValue(HeightProperty);
        IsOcrValid = false;
        ocrResultOfWindow = null;
        liveUiAutomationSnapshot = null;

        if (!hasLoadedImageSource)
            frameContentImageSource = null;

        ClearRenderedWordBorders();
        MatchesTXTBLK.Text = "- Matches";
        UpdateFrameText();
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        isSearchSelectionOverridden = false;
        reSearchTimer.Stop();
        reSearchTimer.Start();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is not TextBox searchBox) return;

        if (string.IsNullOrEmpty(SearchBox.Text))
        {
            ClearBTN.Visibility = Visibility.Collapsed;
            SearchLabel.Visibility = Visibility.Visible;
        }
        else
        {
            ClearBTN.Visibility = Visibility.Visible;
            SearchLabel.Visibility = Visibility.Collapsed;
        }

        isSearchSelectionOverridden = false;

        reSearchTimer.Stop();
        reSearchTimer.Start();
    }

    private void SelectAllWordBorders(object? sender = null, RoutedEventArgs? e = null)
    {
        foreach (WordBorder wordBorder in wordBorders)
            wordBorder.Select();
    }

    private void SetGrabFrameUserSettings()
    {
        string windowSizeAndPosition = $"{Left},{Top},{Width},{Height}";
        DefaultSettings.GrabFrameWindowSizeAndPosition = windowSizeAndPosition;
        DefaultSettings.GrabFrameAutoOcr = AutoOcrCheckBox.IsChecked;
        DefaultSettings.GrabFrameUpdateEtw = AlwaysUpdateEtwCheckBox.IsChecked;
        DefaultSettings.Save();
    }

    private void SetRefreshOrOcrFrameBtnVis()
    {
        if (AutoOcrCheckBox.IsChecked is false)
        {
            OcrFrameBTN.Visibility = Visibility.Visible;
            OcrFrameBTN.Focus();
            RefreshBTN.Visibility = Visibility.Collapsed;
        }
        else
        {
            OcrFrameBTN.Visibility = Visibility.Collapsed;
            RefreshBTN.Visibility = Visibility.Visible;
        }
    }

    private void SetRestoreState()
    {
        if (WindowState == WindowState.Maximized)
            RestoreTextlock.Text = "";
        else
            RestoreTextlock.Text = "";
    }

    private void SetRotationBasedOnOcrResult()
    {
        if (ocrResultOfWindow is null)
            return;

        RotateTransform transform = new((double)ocrResultOfWindow.Angle)
        {
            CenterX = (Width - 4) / 2,
            CenterY = (Height - 60) / 2
        };
        RectanglesCanvas.RenderTransform = transform;
    }

    private void SettingsBTN_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<SettingsWindow>();
    }

    private void ManageGrabTemplates_Click(object sender, RoutedEventArgs e)
    {
        PostGrabActionEditor editor = new();
        editor.Show();
    }

    private void SaveAsTemplate_Click(object sender, RoutedEventArgs e)
    {
        bool show = SaveAsTemplateBTN.IsChecked == true;
        TemplateSavePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            if (!IsFreezeMode)
            {
                FreezeToggleButton.IsChecked = true;
                FreezeGrabFrame();
            }
            TemplateNameBox.Focus();
        }

        UpdateTemplateBadges();
    }

    private void SaveTemplateSave_Click(object sender, RoutedEventArgs e)
    {
        string name = TemplateNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TemplateNameBox.Focus();
            return;
        }

        string outputTemplateText = TemplateOutputBox.GetSerializedText();

        // Parse pattern references from the output template
        List<TemplatePatternMatch> patternMatches = ParsePatternMatchesFromTemplate(outputTemplateText);

        if (wordBorders.Count == 0 && patternMatches.Count == 0)
        {
            MessageBox.Show(
                "Use Ctrl+drag to draw at least one region, or add a pattern placeholder, before saving.",
                "No Regions or Patterns",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        double cw = RectanglesCanvas.ActualWidth;
        double ch = RectanglesCanvas.ActualHeight;

        // Sort regions in reading order: top-to-bottom, then left-to-right
        List<WordBorder> sorted = [.. wordBorders.OrderBy(w => w.Top).ThenBy(w => w.Left)];

        List<TemplateRegion> regions = [.. sorted.Select((wb, i) => new TemplateRegion
        {
            RegionNumber = i + 1,
            Label = string.IsNullOrWhiteSpace(wb.Word) ? $"Region {i + 1}" : wb.Word,
            RatioLeft = wb.Left / cw,
            RatioTop = wb.Top / ch,
            RatioWidth = wb.ActualWidth / cw,
            RatioHeight = wb.ActualHeight / ch,
        })];

        GrabTemplate template = new(name)
        {
            OutputTemplate = outputTemplateText,
            ReferenceImageWidth = cw,
            ReferenceImageHeight = ch,
            Regions = regions,
            PatternMatches = patternMatches,
        };

        if (_editingTemplate is not null)
        {
            template.Id = _editingTemplate.Id;
            template.CreatedDate = _editingTemplate.CreatedDate;
        }

        template.SourceImagePath = GrabTemplateManager.SaveTemplateReferenceImage(frameContentImageSource as BitmapSource, name, template.Id)
            ?? _currentImagePath
            ?? _editingTemplate?.SourceImagePath
            ?? string.Empty;

        GrabTemplateManager.AddOrUpdateTemplate(template);

        SaveAsTemplateBTN.IsChecked = false;
        TemplateSavePanel.Visibility = Visibility.Collapsed;
        TemplateNameBox.Text = string.Empty;
        TemplateOutputBox.SetSerializedText(string.Empty);
        UpdateTemplateBadges();

        int totalItems = regions.Count + patternMatches.Count;
        string itemsDesc = regions.Count > 0 && patternMatches.Count > 0
            ? $"{regions.Count} region(s) and {patternMatches.Count} pattern(s)"
            : regions.Count > 0
                ? $"{regions.Count} region(s)"
                : $"{patternMatches.Count} pattern(s)";

        MessageBox.Show(
            $"Template \"{name}\" saved with {itemsDesc}.\n\nEnable it in Post-Grab Actions Settings to use it during a Fullscreen Grab.",
            "Template Saved",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>
    /// Parses {p:Name:mode} and {p:Name:mode:separator} placeholders from the output template
    /// and builds TemplatePatternMatch objects by resolving against saved patterns.
    /// </summary>
    private static List<TemplatePatternMatch> ParsePatternMatchesFromTemplate(string outputTemplate)
    {
        if (string.IsNullOrEmpty(outputTemplate))
            return [];

        MatchCollection matches = TemplatePattern().Matches(outputTemplate);
        Dictionary<string, TemplatePatternMatch> uniquePatterns = new(StringComparer.OrdinalIgnoreCase);

        StoredRegex[] savedPatterns = LoadSavedPatterns();

        foreach (Match match in matches)
        {
            string patternName = match.Groups[1].Value;
            string mode = match.Groups[2].Value;
            string separator = match.Groups[3].Success ? match.Groups[3].Value : ", ";

            if (uniquePatterns.ContainsKey(patternName))
                continue;

            StoredRegex? stored = savedPatterns.FirstOrDefault(
                p => p.Name.Equals(patternName, StringComparison.OrdinalIgnoreCase));

            uniquePatterns[patternName] = new TemplatePatternMatch(
                patternId: stored?.Id ?? string.Empty,
                patternName: patternName,
                matchMode: mode,
                separator: separator);
        }

        return [.. uniquePatterns.Values];
    }

    private void SaveTemplateCancel_Click(object sender, RoutedEventArgs e)
    {
        SaveAsTemplateBTN.IsChecked = false;
        TemplateSavePanel.Visibility = Visibility.Collapsed;
        UpdateTemplateBadges();
    }

    private void UpdateTemplateBadges()
    {
        bool isTemplateMode = SaveAsTemplateBTN.IsChecked == true;

        if (!isTemplateMode)
        {
            foreach (WordBorder wb in wordBorders)
            {
                wb.TemplateIndex = 0;
                wb.Opacity = 1.0;
                wb.SetHighlightedForOutput(false);
            }
            return;
        }

        List<WordBorder> sorted = [.. wordBorders.OrderBy(w => w.Top).ThenBy(w => w.Left)];
        for (int i = 0; i < sorted.Count; i++)
            sorted[i].TemplateIndex = i + 1;

        UpdateTemplatePickerItems();
        UpdateTemplateRegionOpacities();
    }

    private void UpdateTemplateRegionOpacities()
    {
        if (SaveAsTemplateBTN.IsChecked != true)
            return;

        string outputTemplate = TemplateOutputBox.GetSerializedText();
        HashSet<int> referenced = [.. OutputTemplateReferenced().Matches(outputTemplate)
            .Select(m => int.TryParse(m.Groups[1].Value, out int n) ? n : 0)
            .Where(n => n > 0)];

        foreach (WordBorder wb in wordBorders)
        {
            bool isReferenced = referenced.Count == 0 || referenced.Contains(wb.TemplateIndex);
            wb.Opacity = 1.0;
            wb.SetHighlightedForOutput(isReferenced);
        }
    }

    private void TemplateOutputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateTemplateRegionOpacities();
    }

    private void UpdateTemplatePickerItems()
    {
        List<WordBorder> sorted = [.. wordBorders.OrderBy(w => w.Top).ThenBy(w => w.Left)];

        // Region items
        List<InlinePickerItem> items = [.. sorted
            .Select((wb, i) =>
            {
                string label = string.IsNullOrWhiteSpace(wb.Word) ? $"Region {i + 1}" : wb.Word;
                return new InlinePickerItem(label, $"{{{i + 1}}}", "Regions");
            })];

        // Pattern items from saved StoredRegex patterns
        List<InlinePickerItem> patternItems = LoadPatternPickerItems();
        items.AddRange(patternItems);

        TemplateOutputBox.ItemsSource = items;

        // Wire up the pattern selection callback
        TemplateOutputBox.PatternItemSelected ??= OnPatternItemSelected;
    }

    private static List<InlinePickerItem> LoadPatternPickerItems()
    {
        StoredRegex[] patterns = LoadSavedPatterns();

        return [.. patterns.Select(p =>
            new InlinePickerItem(p.Name, $"{{p:{p.Name}:first}}", "Patterns"))];
    }

    private TemplatePatternMatch? OnPatternItemSelected(InlinePickerItem item)
    {
        // Extract pattern ID by looking up the name
        StoredRegex[] patterns = LoadSavedPatterns();

        StoredRegex? storedRegex = patterns.FirstOrDefault(
            p => p.Name.Equals(item.DisplayName, StringComparison.OrdinalIgnoreCase));

        string patternId = storedRegex?.Id ?? string.Empty;
        string patternName = item.DisplayName;

        PatternMatchModeDialog dialog = new(patternId, patternName)
        {
            Owner = this,
        };

        bool? dialogResult = dialog.ShowDialog();
        return dialogResult == true ? dialog.Result : null;
    }

    private static StoredRegex[] LoadSavedPatterns()
    {
        StoredRegex[] patterns = AppUtilities.TextGrabSettingsService.LoadStoredRegexes();
        return patterns.Length == 0 ? StoredRegex.GetDefaultPatterns() : patterns;
    }

    private void TableToggleButton_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        RemoveTableLines();
        UpdateFrameText();
    }

    private async Task TryLoadImageFromPath(string path)
    {
        Uri fileURI = new(path);
        try
        {
            ResetGrabFrame();
            await Task.Delay(300);
            BitmapImage droppedImage = new();
            droppedImage.BeginInit();
            droppedImage.UriSource = fileURI;
            droppedImage.CacheOption = BitmapCacheOption.OnLoad; // decode fully into memory and release the file handle
            System.Drawing.RotateFlipType rotateFlipType = ImageMethods.GetRotateFlipType(path);
            ImageMethods.RotateImage(droppedImage, rotateFlipType);
            droppedImage.EndInit();
            frameContentImageSource = droppedImage;
            hasLoadedImageSource = true;
            isStaticImageSource = true;
            frozenUiAutomationSnapshot = null;
            liveUiAutomationSnapshot = null;
            _currentImagePath = path;
            FreezeToggleButton.IsChecked = true;
            FreezeGrabFrame();
            FreezeToggleButton.Visibility = Visibility.Collapsed;
            NotifyIfUiAutomationNeedsLiveSource(CurrentLanguage);

            reDrawTimer.Start();
        }
        catch (Exception)
        {
            hasLoadedImageSource = false;
            UnfreezeGrabFrame();
            MessageBox.Show("Not an image");
        }
    }

    private void TryToAlphaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        List<WordBorder> wbToEdit = SelectedWordBorders();

        if (wbToEdit.Count == 0)
            wbToEdit = [.. wordBorders];

        UndoRedo.StartTransaction();
        foreach (WordBorder wb in wbToEdit)
        {
            string oldWord = wb.Word;
            wb.Word = wb.Word.TryFixToLetters();
            UndoableWordChange(wb, oldWord, false);
        }
        UndoRedo.EndTransaction();
    }

    private void TryToNumberMenuItem_Click(object sender, RoutedEventArgs e)
    {
        List<WordBorder> wbToEdit = SelectedWordBorders();

        if (wbToEdit.Count == 0)
            wbToEdit = [.. wordBorders];

        UndoRedo.StartTransaction();
        foreach (WordBorder wb in wbToEdit)
        {
            string oldWord = wb.Word;
            wb.Word = wb.Word.TryFixToNumbers();
            UndoableWordChange(wb, oldWord, false);
        }
        UndoRedo.EndTransaction();
    }

    private void TryToPlaceTable()
    {
        RemoveTableLines();

        Point windowPosition = this.GetAbsolutePosition();
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        System.Drawing.Rectangle rectCanvasSize = new()
        {
            Width = (int)((ActualWidth + 2) * dpi.DpiScaleX),
            Height = (int)((ActualHeight - 64) * dpi.DpiScaleY),
            X = (int)((windowPosition.X - 2) * dpi.DpiScaleX),
            Y = (int)((windowPosition.Y + 24) * dpi.DpiScaleY)
        };

        try
        {
            AnalyzedResultTable = new();
            // Convert UI controls to model-only infos
            List<WordBorderInfo> wbInfos = [.. wordBorders.Select(wb => new WordBorderInfo(wb))];
            AnalyzedResultTable.AnalyzeAsTable(wbInfos, rectCanvasSize);
            if (AnalyzedResultTable.TableLines is not null)
                RectanglesCanvas.Children.Add(AnalyzedResultTable.TableLines);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    private void TryToReadBarcodes(DpiScale dpi)
    {
        if (DefaultSettings.GrabFrameReadBarcodes is false)
            return;

        System.Drawing.Bitmap bitmapOfGrabFrame = ImageMethods.GetWindowsBoundsBitmap(this);

        BarcodeReader barcodeReader = new()
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions { TryHarder = true }
        };

        Result result = barcodeReader.Decode(bitmapOfGrabFrame);

        if (result is null)
            return;

        ResultPoint[] rawPoints = result.ResultPoints;

        float[] xs = [.. rawPoints.Reverse().Take(4).Select(x => x.X)];
        float[] ys = [.. rawPoints.Reverse().Take(4).Select(x => x.Y)];

        Point minPoint = new(xs.Min(), ys.Min());
        Point maxPoint = new(xs.Max(), ys.Max());
        Point diffs = new(maxPoint.X - minPoint.X, maxPoint.Y - minPoint.Y);

        if (diffs.Y < 5)
            diffs.Y = diffs.X / 10;

        WordBorder wb = new()
        {
            Word = result.Text,
            Width = diffs.X / dpi.DpiScaleX + 12,
            Height = diffs.Y / dpi.DpiScaleY + 12,
            Left = minPoint.X / (dpi.DpiScaleX) - 6,
            Top = minPoint.Y / (dpi.DpiScaleY) - 6,
            OwnerGrabFrame = this
        };
        wb.SetAsBarcode();
        wordBorders.Add(wb);
        _ = RectanglesCanvas.Children.Add(wb);

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.AddWordBorder,
        new GrabFrameOperationArgs()
        {
            WordBorder = wb,
            WordBorders = wordBorders,
            GrabFrameCanvas = RectanglesCanvas
        });
    }

    private void UndoExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        UndoRedo.Undo();
    }

    private void UnfreezeGrabFrame()
    {
        reDrawTimer.Stop();
        hasLoadedImageSource = false;
        isStaticImageSource = false;
        frozenUiAutomationSnapshot = null;
        liveUiAutomationSnapshot = null;
        ResetGrabFrame();
        Topmost = true;
        GrabFrameImage.Opacity = 0;
        frameContentImageSource = null;
        historyItem = null;
        RectanglesBorder.Background.Opacity = 0.05;
        FreezeToggleButton.IsChecked = false;
        FreezeToggleButton.Visibility = Visibility.Visible;
        Background = new SolidColorBrush(Colors.Transparent);
        IsFreezeMode = false;
        reDrawTimer.Start();
    }

    private void UpdateFrameText()
    {
        string[] selectedWbs = [.. wordBorders
            .OrderBy(b => b.Top)
            .Where(w => w.IsSelected)
            .Select(t => t.Word)];

        StringBuilder stringBuilder = new();

        if (TableToggleButton.IsChecked is true)
        {
            TryToPlaceTable();
            // Build table text via model-only API
            List<WordBorderInfo> infos = [.. wordBorders.Select(wb => new WordBorderInfo(wb))];
            ResultTable.GetTextFromTabledWordBorders(stringBuilder, infos, isSpaceJoining);
        }
        else
        {
            if (selectedWbs.Length > 0)
                stringBuilder.AppendJoin(Environment.NewLine, selectedWbs);
            else
                stringBuilder.AppendJoin(Environment.NewLine, [.. wordBorders.Select(w => w.Word)]);
        }

        FrameText = stringBuilder.ToString();

        if (IsFromEditWindow
            && destinationTextBox is not null
            && AlwaysUpdateEtwCheckBox.IsChecked is true
            && EditTextToggleButton.IsChecked is true)
        {
            destinationTextBox.SelectedText = FrameText;
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        SetGrabFrameUserSettings();
        WindowUtilities.ShouldShutDown();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded || IsFreezeMode || isMiddleDown)
            return;

        ResetGrabFrame();
        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!wasAltHeld && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt))
        {
            RectanglesCanvas.Opacity = 0.1;
            wasAltHeld = true;
            if (IsEditingAnyWordBorders)
                e.Handled = true;
        }

        if (IsCtrlDown)
            RectanglesCanvas.Cursor = Cursors.Cross;

        if (IsEditingAnyWordBorders || Keyboard.FocusedElement is TextBox or RichTextBox)
            return;

        if (e.Key == Key.Delete)
            HandleDelete();

        if (KeyboardExtensions.IsCtrlDown())
            e.Handled = HandleCtrlCombo(e.Key);
        else
            e.Handled = HandleHotKey(e.Key);
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (wasAltHeld && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt))
        {
            RectanglesCanvas.Opacity = 1;
            wasAltHeld = false;

            if (IsEditingAnyWordBorders)
                e.Handled = true;
        }

        if (!IsCtrlDown)
            RectanglesCanvas.Cursor = null;
    }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        CheckBottomRowButtonsVis();
        SetRestoreState();

        if (IsFreezeMode)
            return;

        ResetGrabFrame();
        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    private void CloseOnGrabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DefaultSettings.CloseFrameOnGrab = CloseOnGrabMenuItem.IsChecked is true;
        DefaultSettings.Save();
    }

    private void CanExecuteGrab(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(FrameText))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void GrabExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FrameText))
            return;

        if (destinationTextBox is not null)
        {
            if (AlwaysUpdateEtwCheckBox.IsChecked is false)
                destinationTextBox.SelectedText = FrameText;

            destinationTextBox.Select(destinationTextBox.SelectionStart + destinationTextBox.SelectionLength, 0);
            destinationTextBox.AppendText(Environment.NewLine);
            UpdateFrameText();

            if (CloseOnGrabMenuItem.IsChecked)
                Close();
            return;
        }

        if (!DefaultSettings.NeverAutoUseClipboard)
            try { Clipboard.SetDataObject(FrameText, true); } catch { }

        if (DefaultSettings.ShowToast)
            NotificationUtilities.ShowToast(FrameText);

        if (CloseOnGrabMenuItem.IsChecked)
            Close();
    }

    private void GrabTrimExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FrameText))
            return;

        string trimmedSingleLineFrameText = FrameText.MakeStringSingleLine();

        if (destinationTextBox is not null)
        {
            if (AlwaysUpdateEtwCheckBox.IsChecked is false)
                destinationTextBox.SelectedText = trimmedSingleLineFrameText;

            destinationTextBox.Select(destinationTextBox.SelectionStart + destinationTextBox.SelectionLength, 0);
            destinationTextBox.AppendText(Environment.NewLine);
            UpdateFrameText();

            if (CloseOnGrabMenuItem.IsChecked)
                Close();
            return;
        }

        if (!DefaultSettings.NeverAutoUseClipboard)
            try { Clipboard.SetDataObject(trimmedSingleLineFrameText, true); } catch { }

        if (DefaultSettings.ShowToast)
            NotificationUtilities.ShowToast(trimmedSingleLineFrameText);

        if (CloseOnGrabMenuItem.IsChecked)
            Close();
    }

    private void ScrollBehaviorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || !Enum.TryParse(menuItem.Tag.ToString(), out scrollBehavior))
            return;

        DefaultSettings.GrabFrameScrollBehavior = scrollBehavior.ToString();
        DefaultSettings.Save();
        SetScrollBehaviorMenuItems();
    }

    private void SetScrollBehaviorMenuItems()
    {
        switch (scrollBehavior)
        {
            case ScrollBehavior.None:
                NoScrollBehaviorMenuItem.IsChecked = true;
                ResizeScrollMenuItem.IsChecked = false;
                ZoomScrollMenuItem.IsChecked = false;
                MainZoomBorder.CanZoom = false;
                break;
            case ScrollBehavior.Resize:
                NoScrollBehaviorMenuItem.IsChecked = false;
                ResizeScrollMenuItem.IsChecked = true;
                ZoomScrollMenuItem.IsChecked = false;
                MainZoomBorder.CanZoom = false;
                break;
            case ScrollBehavior.Zoom:
                NoScrollBehaviorMenuItem.IsChecked = false;
                ResizeScrollMenuItem.IsChecked = false;
                ZoomScrollMenuItem.IsChecked = true;
                MainZoomBorder.CanZoom = true;
                break;
            default:
                break;
        }
    }

    private void InvertColorsMI_Click(object sender, RoutedEventArgs e)
    {
        UndoRedo.EndTransaction();

        List<WordBorder> existingWordBorders = [.. wordBorders];

        GrabFrameOperationArgs args = new()
        {
            WordBorders = wordBorders,
            GrabFrameCanvas = RectanglesCanvas,
            DestinationImage = GrabFrameImage,
            RemovingWordBorders = existingWordBorders,
            OldImage = frameContentImageSource
        };

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = existingWordBorders,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });

        reDrawTimer.Stop();
        ClearRenderedWordBorders();

        if (!IsFreezeMode)
            FreezeGrabFrame();

        if (frameContentImageSource is null)
        {
            reDrawTimer.Start();
            UndoRedo.EndTransaction();
            return;
        }

        frameContentImageSource = MagickHelpers.Invert(frameContentImageSource);
        GrabFrameImage.Source = frameContentImageSource;

        args.NewImage = frameContentImageSource;

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.ChangedImage, args);
        UndoRedo.EndTransaction();
        reDrawTimer.Start();
    }

    private void AutoContrastMI_Click(object sender, RoutedEventArgs e)
    {
        UndoRedo.EndTransaction();

        List<WordBorder> existingWordBorders = [.. wordBorders];

        GrabFrameOperationArgs args = new()
        {
            WordBorders = wordBorders,
            GrabFrameCanvas = RectanglesCanvas,
            DestinationImage = GrabFrameImage,
            RemovingWordBorders = existingWordBorders,
            OldImage = frameContentImageSource
        };

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = existingWordBorders,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });

        reDrawTimer.Stop();
        RectanglesCanvas.Children.Clear();
        wordBorders.Clear();

        if (!IsFreezeMode)
            FreezeGrabFrame();

        if (frameContentImageSource is null)
        {
            reDrawTimer.Start();
            UndoRedo.EndTransaction();
            return;
        }

        frameContentImageSource = MagickHelpers.Contrast(frameContentImageSource);
        GrabFrameImage.Source = frameContentImageSource;

        args.NewImage = frameContentImageSource;

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.ChangedImage, args);
        UndoRedo.EndTransaction();
        reDrawTimer.Start();
    }

    private void BrightenMI_Click(object sender, RoutedEventArgs e)
    {
        UndoRedo.EndTransaction();

        List<WordBorder> existingWordBorders = [.. wordBorders];

        GrabFrameOperationArgs args = new()
        {
            WordBorders = wordBorders,
            GrabFrameCanvas = RectanglesCanvas,
            DestinationImage = GrabFrameImage,
            RemovingWordBorders = existingWordBorders,
            OldImage = frameContentImageSource
        };

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = existingWordBorders,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });

        reDrawTimer.Stop();
        RectanglesCanvas.Children.Clear();
        wordBorders.Clear();

        if (!IsFreezeMode)
            FreezeGrabFrame();

        if (frameContentImageSource is null)
        {
            reDrawTimer.Start();
            UndoRedo.EndTransaction();
            return;
        }

        frameContentImageSource = MagickHelpers.Brighten(frameContentImageSource);
        GrabFrameImage.Source = frameContentImageSource;

        args.NewImage = frameContentImageSource;

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.ChangedImage, args);
        UndoRedo.EndTransaction();
        reDrawTimer.Start();
    }

    private void DarkenMI_Click(object sender, RoutedEventArgs e)
    {
        UndoRedo.EndTransaction();

        List<WordBorder> existingWordBorders = [.. wordBorders];

        GrabFrameOperationArgs args = new()
        {
            WordBorders = wordBorders,
            GrabFrameCanvas = RectanglesCanvas,
            DestinationImage = GrabFrameImage,
            RemovingWordBorders = existingWordBorders,
            OldImage = frameContentImageSource
        };

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = existingWordBorders,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });

        reDrawTimer.Stop();
        RectanglesCanvas.Children.Clear();
        wordBorders.Clear();

        if (!IsFreezeMode)
            FreezeGrabFrame();

        if (frameContentImageSource is null)
        {
            reDrawTimer.Start();
            UndoRedo.EndTransaction();
            return;
        }

        frameContentImageSource = MagickHelpers.Darken(frameContentImageSource);
        GrabFrameImage.Source = frameContentImageSource;

        args.NewImage = frameContentImageSource;

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.ChangedImage, args);
        UndoRedo.EndTransaction();
        reDrawTimer.Start();
    }

    private void GrayscaleMI_Click(object sender, RoutedEventArgs e)
    {
        UndoRedo.EndTransaction();

        List<WordBorder> existingWordBorders = [.. wordBorders];

        GrabFrameOperationArgs args = new()
        {
            WordBorders = wordBorders,
            GrabFrameCanvas = RectanglesCanvas,
            DestinationImage = GrabFrameImage,
            RemovingWordBorders = existingWordBorders,
            OldImage = frameContentImageSource
        };

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = existingWordBorders,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });

        reDrawTimer.Stop();
        RectanglesCanvas.Children.Clear();
        wordBorders.Clear();

        if (!IsFreezeMode)
            FreezeGrabFrame();

        if (frameContentImageSource is null)
        {
            reDrawTimer.Start();
            UndoRedo.EndTransaction();
            return;
        }

        frameContentImageSource = MagickHelpers.Grayscale(frameContentImageSource as BitmapSource);
        GrabFrameImage.Source = frameContentImageSource;

        args.NewImage = frameContentImageSource;

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.ChangedImage, args);
        UndoRedo.EndTransaction();
        reDrawTimer.Start();
    }

    private void ReadBarcodesMenuItem_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem barcodeMenuItem)
            return;

        DefaultSettings.GrabFrameReadBarcodes = barcodeMenuItem.IsChecked is true;
        DefaultSettings.Save();
    }

    private void TranslateToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (TranslateToggleButton.IsChecked is bool isChecked)
        {
            isTranslationEnabled = isChecked;
            EnableTranslationMenuItem.IsChecked = isChecked;
            DefaultSettings.GrabFrameTranslationEnabled = isChecked;
            DefaultSettings.Save();

            if (isChecked)
            {
                if (!WindowsAiUtilities.CanDeviceUseWinAI())
                {
                    MessageBox.Show("Windows AI is not available on this device. Translation requires Windows AI support.",
                        "Translation Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    TranslateToggleButton.IsChecked = false;
                    isTranslationEnabled = false;
                    return;
                }

                // ALWAYS freeze the frame before translation to ensure static content
                if (!IsFreezeMode)
                {
                    FreezeToggleButton.IsChecked = true;
                    FreezeGrabFrame();
                }

                // Store original texts before translation
                foreach (WordBorder wb in wordBorders.Where(wb => !originalTexts.ContainsKey(wb)))
                {
                    originalTexts[wb] = wb.Word;
                }

                // Create new cancellation token source
                translationCancellationTokenSource?.Cancel();
                translationCancellationTokenSource?.Dispose();
                translationCancellationTokenSource = new CancellationTokenSource();

                translationTimer.Start();
            }
            else
            {
                translationTimer.Stop();

                // Cancel any ongoing translation
                translationCancellationTokenSource?.Cancel();

                // Restore original texts
                foreach (WordBorder wb in wordBorders.Where(wb => originalTexts.ContainsKey(wb)))
                {
                    if (originalTexts.TryGetValue(wb, out string? originalText))
                        wb.Word = originalText;
                }
                originalTexts.Clear();

                // Dispose the translation model to free resources when not in use
                WindowsAiUtilities.DisposeTranslationModel();
            }
        }
    }

    private void EnableTranslationMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            TranslateToggleButton.IsChecked = menuItem.IsChecked;
            TranslateToggleButton_Click(TranslateToggleButton, e);
        }
    }

    private void TranslationLanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string language)
            return;

        translationTargetLanguage = language;
        DefaultSettings.GrabFrameTranslationLanguage = language;
        DefaultSettings.Save();

        // Update the tooltip to show the current target language
        TranslateToggleButton.ToolTip = $"Enable real-time translation to {language}";

        // Uncheck all language menu items and check only the selected one
        if (menuItem.Parent is MenuItem parentMenu)
        {
            foreach (object? item in parentMenu.Items)
            {
                if (item is MenuItem langMenuItem && langMenuItem.Tag is string)
                    langMenuItem.IsChecked = langMenuItem.Tag.ToString() == language;
            }
        }

        // Re-translate if translation is currently enabled
        if (isTranslationEnabled)
        {
            translationTimer.Stop();
            translationTimer.Start();
        }
    }

    private async void TranslationTimer_Tick(object? sender, EventArgs e)
    {
        translationTimer.Stop();

        if (!isTranslationEnabled || !WindowsAiUtilities.CanDeviceUseWinAI())
            return;

        await PerformTranslationAsync();
    }

    private async Task PerformTranslationAsync()
    {
        if (translationCancellationTokenSource == null || translationCancellationTokenSource.IsCancellationRequested)
            return;

        ShowTranslationProgress();

        totalWordsToTranslate = wordBorders.Count;
        translatedWordsCount = 0;

        CancellationToken cancellationToken = translationCancellationTokenSource.Token;

        // Translate all word borders with controlled concurrency (max 3 at a time)
        List<Task> translationTasks = [];

        try
        {
            foreach (WordBorder wb in wordBorders)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Store original text if not already stored
                if (!originalTexts.ContainsKey(wb))
                    originalTexts[wb] = wb.Word;

                string originalText = originalTexts[wb];
                if (!string.IsNullOrWhiteSpace(originalText))
                {
                    translationTasks.Add(TranslateWordBorderAsync(wb, originalText, cancellationToken));
                }
                else
                {
                    translatedWordsCount++;
                    UpdateTranslationProgress();
                }
            }

            // Wait for all translations to complete or cancellation
            // Use WhenAll with exception handling to gracefully handle cancellations
            try
            {
                await Task.WhenAll(translationTasks);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                Debug.WriteLine("Translation tasks cancelled during WhenAll");
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                UpdateFrameText();
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Translation was cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Translation error: {ex.Message}");
        }
        finally
        {
            HideTranslationProgress();
        }
    }

    private void ShowTranslationProgress()
    {
        TranslationProgressBorder.Visibility = Visibility.Visible;
        TranslationProgressBar.Value = 0;
        TranslationProgressText.Text = "Translating...";
        TranslationCountText.Text = "0/0";
    }

    private void HideTranslationProgress()
    {
        TranslationProgressBorder.Visibility = Visibility.Collapsed;
    }

    private void UpdateTranslationProgress()
    {
        if (totalWordsToTranslate == 0)
            return;

        double progress = (double)translatedWordsCount / totalWordsToTranslate * 100;
        TranslationProgressBar.Value = progress;
        TranslationCountText.Text = $"{translatedWordsCount}/{totalWordsToTranslate}";
    }

    private async Task TranslateWordBorderAsync(WordBorder wordBorder, string originalText, CancellationToken cancellationToken)
    {
        try
        {
            await translationSemaphore.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Semaphore wait was cancelled - exit gracefully
            return;
        }

        try
        {
            // Ensure cancellation is honored immediately before starting translation
            cancellationToken.ThrowIfCancellationRequested();

            string translatedText = await WindowsAiUtilities.TranslateText(originalText, translationTargetLanguage);

            // If cancellation was requested during translation, abort before updating UI state
            cancellationToken.ThrowIfCancellationRequested();

            wordBorder.Word = translatedText;

            translatedWordsCount++;
            await Dispatcher.InvokeAsync(() => UpdateTranslationProgress());
        }
        catch (OperationCanceledException)
        {
            // Expected during cancellation - don't propagate
            Debug.WriteLine($"Translation cancelled for word: {originalText}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Translation failed for '{originalText}': {ex.Message}");
            // On error, keep original text (don't update word border)
        }
        finally
        {
            translationSemaphore.Release();
        }
    }

    private void GetGrabFrameTranslationSettings()
    {
        isTranslationEnabled = DefaultSettings.GrabFrameTranslationEnabled;
        translationTargetLanguage = DefaultSettings.GrabFrameTranslationLanguage;

        // Hide translation button if Windows AI is not available
        bool canUseWinAI = WindowsAiUtilities.CanDeviceUseWinAI();
        TranslateToggleButton.Visibility = canUseWinAI ? Visibility.Visible : Visibility.Collapsed;
        TranslationMenuItem.Visibility = canUseWinAI ? Visibility.Visible : Visibility.Collapsed;

        if (canUseWinAI)
        {
            TranslateToggleButton.IsChecked = isTranslationEnabled;
            EnableTranslationMenuItem.IsChecked = isTranslationEnabled;
            TranslateToggleButton.ToolTip = $"Enable real-time translation to {translationTargetLanguage}";
        }
        else
        {
            // Disable translation if Windows AI is not available
            isTranslationEnabled = false;
        }

        // Set the checked state for the translation language menu item
        // Find the "Target Language" submenu by searching through items
        if (canUseWinAI && TranslationMenuItem != null)
        {
            foreach (object? item in TranslationMenuItem.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header.ToString() == TargetLanguageMenuHeader)
                {
                    foreach (object? langItem in menuItem.Items)
                    {
                        if (langItem is MenuItem langMenuItem && langMenuItem.Tag is string tag)
                            langMenuItem.IsChecked = tag == translationTargetLanguage;
                    }
                    break;
                }
            }
        }
    }

    private void CancelTranslationButton_Click(object sender, RoutedEventArgs e)
    {
        translationCancellationTokenSource?.Cancel();
        HideTranslationProgress();

        // Restore original texts
        foreach (WordBorder wb in wordBorders.Where(wb => originalTexts.ContainsKey(wb)))
        {
            if (originalTexts.TryGetValue(wb, out string? originalText))
                wb.Word = originalText;
        }

        UpdateFrameText();
    }

    [GeneratedRegex(@"\{p:([^:}]+):([^:}]+)(?::([^}]*))?\}")]
    private static partial Regex TemplatePattern();
    [GeneratedRegex(@"\{(\d+)(?::[a-z]+)?\}")]
    private static partial Regex OutputTemplateReferenced();

    #endregion Methods
}
