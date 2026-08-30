using Dapplo.Windows.User32;
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
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Extensions;
using Text_Grab.Interfaces;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.UndoRedoOperations;
using Text_Grab.Utilities;
using Windows.Globalization;
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
    private readonly GrabFrameTableEditState tableEditState = new();
    private ResultTable? AnalyzedResultTable;
    private Point clickedPoint;
    private ILanguage? currentLanguage;
    private TextBox? destinationTextBox;
    private ImageSource? frameContentImageSource;
    private HistoryInfo? historyItem;
    private readonly GrabTemplate? _editingTemplate;
    private GrabTemplate? _activeGrabTemplate = null;
    private string? _currentImagePath;
    private PdfDocumentRenderer? _loadedPdfDocument;
    private PdfPageContent? _currentPdfPageContent;
    private int _currentPdfPageIndex = -1;
    private int _initialPdfPageIndex;
    private bool hasLoadedImageSource = false;
    private bool IsDragOver = false;
    private bool isDrawing = false;
    private bool isAutoOcrRedrawPass = false;
    private bool isLanguageBoxLoaded = false;
    private bool isMiddleDown = false;
    private bool IsOcrValid = false;
    private bool isSearchSelectionOverridden = false;
    private bool isSelecting;
    private bool isSpaceJoining = true;
    private bool isSpacePanModifierDown = false;
    private DispatcherTimer? _spacePanGraceTimer;
    private bool isStaticImageSource = false;
    private readonly Dictionary<WordBorder, Rect> movingWordBordersDictionary = [];
    private IOcrLinesWords? ocrResultOfWindow;
    private UiAutomationOverlaySnapshot? frozenUiAutomationSnapshot;
    private UiAutomationOverlaySnapshot? liveUiAutomationSnapshot;
    private readonly DispatcherTimer frameMessageTimer = new();
    private readonly DispatcherTimer reDrawTimer = new();
    private readonly DispatcherTimer reSearchTimer = new();
    private readonly DispatcherTimer contentChangeTimer = new();
    private readonly ImageChangeDetector contentChangeDetector = new();
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
    private GrabFrameBorderStyle borderStyle = GrabFrameBorderStyle.Theme;
    private Color borderCustomColor = Color.FromRgb(0x2A, 0x76, 0x7E);
    private GrabFrameWordGroupingMode wordGroupingMode = GrabFrameWordGroupingMode.Paragraph;
    private double overlayOpacity = 0.05;
    private bool isTranslationEnabled = false;
    private string translationTargetLanguage = "English";
    private readonly DispatcherTimer translationTimer = new();
    private readonly Dictionary<WordBorder, string> originalTexts = [];
    private int totalWordsToTranslate = 0;
    private int translatedWordsCount = 0;
    private bool isTranslating = false;
    private CancellationTokenSource? translationCancellationTokenSource;
    private readonly List<PdfTextLineOverlay> pdfTextLineOverlays = [];
    private CancellationTokenSource? _pdfPageNavCts;
    private bool isLoadedVisualDocument = false;
    private double frozenFrameContentScale = 1;
    private const string TargetLanguageMenuHeader = "Target Language";
    private string _lastSpokenFrameText = string.Empty;
    private bool _speakOnNextFrameTextUpdate = false;
    private bool isSpeakEnabled = false;
    private WindowResizer? windowResizer;
    private bool _isCleanedUp;
    private int _freezeTransitionVersion;
    private readonly HashSet<string> hiddenBottomBarTools = new(StringComparer.OrdinalIgnoreCase);
    private bool translateToolAvailable = false;
    private readonly List<GrabFrameSearchMatch> currentSearchMatches = [];

    #endregion Fields

    private sealed record GrabFrameSearchMatch(
        string Text,
        IReadOnlyList<WordBorder> WordBorders,
        PdfTextLineOverlay? PdfTextLine)
    {
        public bool IsSelected => PdfTextLine?.IsSelected
            ?? (WordBorders.Count > 0 && WordBorders.All(wordBorder => wordBorder.IsSelected));
    }

    private sealed record GrabFrameSearchUnit(
        string Text,
        IReadOnlyList<(WordBorder WordBorder, int Start, int Length)> WordSegments,
        PdfTextLineOverlay? PdfTextLine,
        double Top,
        double Left);

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
    /// Creates a GrabFrame and loads the specified image or PDF file.
    /// </summary>
    /// <param name="imagePath">The path to the file to load.</param>
    public GrabFrame(string imagePath)
    {
        StandardInitialize();

        ShouldSaveOnClose = true;

        // Validate the path before loading
        if (string.IsNullOrEmpty(imagePath))
        {
            Debug.WriteLine("GrabFrame: Empty file path provided");
            Loaded += async (s, e) => await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Text Grab",
                Content = "No file path was provided.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return;
        }

        // Convert to absolute path to handle relative paths correctly
        string absolutePath = Path.GetFullPath(imagePath);

        if (!File.Exists(absolutePath))
        {
            Debug.WriteLine($"GrabFrame: File not found: {absolutePath}");
            Loaded += async (s, e) => await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Text Grab",
                Content = $"File not found:\n{absolutePath}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return;
        }

        Loaded += async (s, e) => await TryLoadDocumentFromPath(absolutePath);
    }

    public GrabFrame(HistoryInfo historyInfo, string sourcePath)
        : this(sourcePath)
    {
        historyItem = historyInfo;
        _initialPdfPageIndex = Math.Max(0, historyInfo.SourcePageIndex);
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

        TemplateSavePanel.Visibility = Visibility.Visible;

        if (!string.IsNullOrEmpty(template.SourceImagePath) && File.Exists(template.SourceImagePath))
        {
            isStaticImageSource = true;
            await TryLoadDocumentFromPath(template.SourceImagePath);
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
            Rect abs = region.ToAbsoluteRect(cw, ch).AsRect();

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
                    items.Add(new InlinePickerItem(displayLabel, value, PatternItem.SavedGroup));
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
        CancelTablePlacement(clearManualSeparators: true);
        FrameText = history.TextContent;
        currentLanguage = history.OcrLanguage;
        SyncLanguageComboBoxSelection(currentLanguage);
        isStaticImageSource = true;
        frozenUiAutomationSnapshot = null;
        liveUiAutomationSnapshot = null;

        string imageName = Path.GetFileName(history.ImagePath);

        // A Grab Frame file (.tggf) hands us the image already decoded in memory; the
        // History feature stores it on disk, so only read from the history folder when
        // the image has not already been loaded.
        System.Drawing.Bitmap? bgBitmap = history.ImageContent
            ?? await FileUtilities
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
            tableEditState.SetManualSeparators(history.ManualTableRowSeparators, history.ManualTableColumnSeparators);

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
            tableEditState.SetManualSeparators(history.ManualTableRowSeparators, history.ManualTableColumnSeparators);
            reDrawTimer.Start();
            ShouldSaveOnClose = true;
        }

        TableToggleButton.IsChecked = history.IsTable;

        if (ShouldRefreshOcrBordersForTableModeActivation())
            await DrawRectanglesAroundWords(SearchBar.SearchText);

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
        if ((wbInfoList.Count == 0
                && (history.ManualTableRowSeparators?.Count ?? 0) == 0
                && (history.ManualTableColumnSeparators?.Count ?? 0) == 0)
            || RectanglesCanvas.Width <= 0
            || RectanglesCanvas.Height <= 0)
        {
            return;
        }

        Size savedContentSize = GetSavedHistoryContentSize(history);
        if (savedContentSize.Width <= 0 || savedContentSize.Height <= 0)
            return;

        double scaleX = RectanglesCanvas.Width / savedContentSize.Width;
        double scaleY = RectanglesCanvas.Height / savedContentSize.Height;
        if (!double.IsFinite(scaleX) || !double.IsFinite(scaleY) || (scaleX <= 1.05 && scaleY <= 1.05))
            return;

        double maxRight = wbInfoList.Count > 0 ? wbInfoList.Max(info => info.BorderRect.Right) : 0;
        double maxBottom = wbInfoList.Count > 0 ? wbInfoList.Max(info => info.BorderRect.Bottom) : 0;

        // Scale only when saved word borders look like they were captured in
        // the old window-content coordinate space rather than image-space.
        if (wbInfoList.Count > 0
            && (maxRight > savedContentSize.Width * 1.1 || maxBottom > savedContentSize.Height * 1.1))
        {
            return;
        }

        foreach (WordBorderInfo info in wbInfoList)
        {
            Rect borderRect = info.BorderRect.AsRect();
            info.BorderRect = new Rect(
                borderRect.Left * scaleX,
                borderRect.Top * scaleY,
                borderRect.Width * scaleX,
                borderRect.Height * scaleY).AsRectangleF();

            if (info.DisplayLineHeight > 0)
                info.DisplayLineHeight *= scaleY;
        }

        if (history.ManualTableRowSeparators is not null)
            history.ManualTableRowSeparators = [.. history.ManualTableRowSeparators.Select(position => position * scaleY)];

        if (history.ManualTableColumnSeparators is not null)
            history.ManualTableColumnSeparators = [.. history.ManualTableColumnSeparators.Select(position => position * scaleX)];
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
        if (PresentationSource.FromVisual(this) is null)
            return System.Drawing.Rectangle.Empty;

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
        MainZoomBorder.ResetRequested += MainZoomBorder_ResetRequested;

        _ = LoadOcrLanguagesAsync();

        SetRestoreState();

        windowResizer = new WindowResizer(this);
        reDrawTimer.Interval = new(0, 0, 0, 0, 500);
        reDrawTimer.Tick += ReDrawTimer_Tick;

        reSearchTimer.Interval = new(0, 0, 0, 0, 300);
        reSearchTimer.Tick += ReSearchTimer_Tick;

        translationTimer.Interval = new(0, 0, 0, 0, 1000);
        translationTimer.Tick += TranslationTimer_Tick;

        frameMessageTimer.Interval = TimeSpan.FromSeconds(4);
        frameMessageTimer.Tick += FrameMessageTimer_Tick;

        contentChangeTimer.Interval = TimeSpan.FromSeconds(1);
        contentChangeTimer.Tick += ContentChangeTimer_Tick;
        contentChangeTimer.Start();

        _ = UndoRedo.HasUndoOperations();
        _ = UndoRedo.HasRedoOperations();

        GetGrabFrameUserSettings();
        SetRefreshOrOcrFrameBtnVis();

        DataContext = this;
        UpdateTableEditingUiState();
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

    private void AddTableColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        BeginTablePlacement(GrabFrameTablePlacementMode.AddColumn);
    }

    private void AddTableRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        BeginTablePlacement(GrabFrameTablePlacementMode.AddRow);
    }

    private void BeginTablePlacement(GrabFrameTablePlacementMode placementMode)
    {
        if (TableToggleButton.IsChecked is not true || wordBorders.Count == 0)
        {
            ShowFrameMessage("Turn on table analysis after OCR to place table dividers.");
            UpdateTableEditingUiState();
            return;
        }

        _ = TryToPlaceTable();
        tableEditState.BeginPlacement(placementMode);
        ClearTablePlacementPreview();
        UpdateTableEditingUiState();
    }

    private void CancelTablePlacement(bool clearManualSeparators = false)
    {
        if (clearManualSeparators)
            tableEditState.ClearAll();
        else
            tableEditState.CancelPlacement();

        ClearTablePlacementPreview();
        UpdateTableEditingUiState();
    }

    private void CancelTablePlacement_Click(object sender, RoutedEventArgs e)
    {
        CancelTablePlacement();
    }

    private void ClearTablePlacementPreview()
    {
        TablePlacementOverlayCanvas.Children.Clear();
    }

    private void DrawTablePlacementPreview(Rect tableBounds)
    {
        ClearTablePlacementPreview();

        if (!tableEditState.IsPlacementActive || tableEditState.PreviewPosition is not double previewPosition)
            return;

        SolidColorBrush previewBrush = tableEditState.IsPreviewValid
            ? new SolidColorBrush(Color.FromArgb(255, 40, 118, 126))
            : new SolidColorBrush(Color.FromArgb(255, 196, 43, 28));

        Border previewLine = new()
        {
            Background = previewBrush,
            IsHitTestVisible = false
        };

        if (tableEditState.PlacementMode == GrabFrameTablePlacementMode.AddRow)
        {
            previewLine.Height = 2;
            previewLine.Width = tableBounds.Width;
            Canvas.SetLeft(previewLine, tableBounds.Left);
            Canvas.SetTop(previewLine, previewPosition - 1);
        }
        else
        {
            previewLine.Width = 2;
            previewLine.Height = tableBounds.Height;
            Canvas.SetLeft(previewLine, previewPosition - 1);
            Canvas.SetTop(previewLine, tableBounds.Top);
        }

        TablePlacementOverlayCanvas.Children.Add(previewLine);
    }

    private bool TryCommitTablePlacement(Point pointerPosition)
    {
        UpdateTablePlacementPreview(pointerPosition);

        if (!tableEditState.TryCommitPreview())
        {
            ShowFrameMessage("Move farther from the table edge or another divider.");
            return true;
        }

        string placementLabel = tableEditState.PlacementMode == GrabFrameTablePlacementMode.AddRow
            ? "row"
            : "column";

        UpdateFrameText();
        UpdateTablePlacementPreview(pointerPosition);
        ShowFrameMessage($"Added {placementLabel} divider.");
        return true;
    }

    private bool TryGetTablePlacementBounds(out Rect tableBounds)
    {
        tableBounds = Rect.Empty;

        if (TableToggleButton.IsChecked is not true || wordBorders.Count == 0)
            return false;

        if (AnalyzedResultTable is null)
            _ = TryToPlaceTable();

        tableBounds = AnalyzedResultTable?.BoundingRect.AsRect() ?? Rect.Empty;
        return tableBounds != Rect.Empty
            && tableBounds.Width > 0
            && tableBounds.Height > 0;
    }

    private void UpdateTableEditingUiState()
    {
        bool canEditTable = TableToggleButton.IsChecked is true && wordBorders.Count > 0;

        EditTableMenuItem.IsEnabled = canEditTable;
        AddTableRowMenuItem.IsEnabled = canEditTable;
        AddTableColumnMenuItem.IsEnabled = canEditTable;
        CancelTablePlacementMenuItem.IsEnabled = tableEditState.IsPlacementActive;
        TableToggleAddRowMenuItem.IsEnabled = canEditTable;
        TableToggleAddColumnMenuItem.IsEnabled = canEditTable;
        TableToggleCancelPlacementMenuItem.IsEnabled = tableEditState.IsPlacementActive;

        TablePlacementBanner.Visibility = tableEditState.IsPlacementActive ? Visibility.Visible : Visibility.Collapsed;

        if (!tableEditState.IsPlacementActive)
            return;

        string placementTarget = tableEditState.PlacementMode == GrabFrameTablePlacementMode.AddRow
            ? "row"
            : "column";
        TablePlacementInstructionsTextBlock.Text = $"Click inside the table to place a {placementTarget} divider. Press Esc to cancel.";
    }

    private void UpdateTablePlacementPreview(Point pointerPosition)
    {
        if (!tableEditState.IsPlacementActive || !TryGetTablePlacementBounds(out Rect tableBounds))
        {
            ClearTablePlacementPreview();
            return;
        }

        double minimumPosition;
        double maximumPosition;
        double requestedPosition;
        IEnumerable<double> existingSeparators;

        if (tableEditState.PlacementMode == GrabFrameTablePlacementMode.AddRow)
        {
            minimumPosition = tableBounds.Top + GrabFrameTableEditState.MinimumSeparatorGap;
            maximumPosition = tableBounds.Bottom - GrabFrameTableEditState.MinimumSeparatorGap;
            requestedPosition = pointerPosition.Y;
            existingSeparators = AnalyzedResultTable?.RowLines ?? [];
        }
        else
        {
            minimumPosition = tableBounds.Left + GrabFrameTableEditState.MinimumSeparatorGap;
            maximumPosition = tableBounds.Right - GrabFrameTableEditState.MinimumSeparatorGap;
            requestedPosition = pointerPosition.X;
            existingSeparators = AnalyzedResultTable?.ColumnLines ?? [];
        }

        if (!tableEditState.TryUpdatePreview(
            requestedPosition,
            minimumPosition,
            maximumPosition,
            existingSeparators))
        {
            DrawTablePlacementPreview(tableBounds);
            return;
        }

        DrawTablePlacementPreview(tableBounds);
    }

    private void ClearLoadedPdfDocument()
    {
        _pdfPageNavCts?.Cancel();
        _pdfPageNavCts?.Dispose();
        _pdfPageNavCts = null;
        _loadedPdfDocument?.Dispose();
        _loadedPdfDocument = null;
        _currentPdfPageContent = null;
        _currentPdfPageIndex = -1;
        SetSpacePanModifierState(false);
        UpdateZoomPanMode();
        SetScrollBehaviorMenuItems();
        UpdatePdfPageNavigation();
    }

    private async Task ChangePdfPageAsync(int delta)
    {
        if (_loadedPdfDocument is null)
            return;

        int targetPageIndex = _currentPdfPageIndex + delta;
        if (targetPageIndex < 0 || targetPageIndex >= _loadedPdfDocument.PageCount)
            return;

        await ShowPdfPageAsync(targetPageIndex);
    }

    private async Task ShowPdfPageAsync(int pageIndex)
    {
        if (_loadedPdfDocument is null)
            return;

        CancellationTokenSource? previousCts = _pdfPageNavCts;
        _pdfPageNavCts = new CancellationTokenSource();
        CancellationToken ct = _pdfPageNavCts.Token;
        previousCts?.Cancel();
        previousCts?.Dispose();

        try
        {
            reDrawTimer.Stop();
            CancelTablePlacement(clearManualSeparators: true);
            ResetGrabFrame();
            await Task.Delay(300, ct);

            if (_loadedPdfDocument is null || ct.IsCancellationRequested)
                return;

            _currentPdfPageContent = await _loadedPdfDocument.GetPageContentAsync(pageIndex);
            frameContentImageSource = _currentPdfPageContent.RenderedPage;
            hasLoadedImageSource = true;
            isStaticImageSource = true;
            frozenUiAutomationSnapshot = null;
            liveUiAutomationSnapshot = null;
            _currentImagePath = _loadedPdfDocument.FilePath;
            _currentPdfPageIndex = pageIndex;
            FreezeToggleButton.IsChecked = true;
            FreezeGrabFrame();
            EnsureMinimumLoadedDocumentWindowSize();
            MainZoomBorder.CanZoom = true;
            FreezeToggleButton.Visibility = Visibility.Collapsed;
            UpdatePdfPageNavigation();
            SwitchToOcrFallbackIfUiAutomation();

            reDrawTimer.Start();
        }
        catch (OperationCanceledException)
        {
            // Navigation superseded by a newer request — no-op
        }
    }

    private void UpdatePdfPageNavigation()
    {
        bool isPdfLoaded = _loadedPdfDocument is not null;
        PdfPagePanel.Visibility = isPdfLoaded ? Visibility.Visible : Visibility.Collapsed;

        if (!isPdfLoaded || _currentPdfPageIndex < 0)
        {
            PdfPageTextBlock.Text = string.Empty;
            PreviousPdfPageButton.IsEnabled = false;
            NextPdfPageButton.IsEnabled = false;
            return;
        }

        PdfPageTextBlock.Text = $"Page {_currentPdfPageIndex + 1} / {_loadedPdfDocument!.PageCount}";
        PreviousPdfPageButton.IsEnabled = _currentPdfPageIndex > 0;
        NextPdfPageButton.IsEnabled = _currentPdfPageIndex < _loadedPdfDocument.PageCount - 1;
    }

    /// <summary>
    /// When a static image is loaded and the active language is UI Automation (Direct Text),
    /// silently switch to the OCR fallback language so no warning is shown.
    /// </summary>
    private void SwitchToOcrFallbackIfUiAutomation()
    {
        if (CurrentLanguage is not UiAutomationLang)
            return;

        ILanguage fallback = CaptureLanguageUtilities.GetUiAutomationFallbackLanguage();
        currentLanguage = fallback;
        SyncLanguageComboBoxSelection(fallback);
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
    private bool IsPdfDocumentLoaded => _loadedPdfDocument is not null;
    public bool IsWordEditMode { get; set; } = true;

    public bool ShouldSaveOnClose { get; set; } = true;

    #endregion Properties

    #region Methods

    public static bool CheckKey(VirtualKeyCodes code)
    {
        return (GetKeyState(code) & 0xFF00) == 0xFF00;
    }

    private static FrameworkElement? GetInteractionSurface(object? sender) => sender as FrameworkElement;

    private bool IsPdfTextInteraction(object? sender) => ReferenceEquals(sender, PdfTextCanvas);

    private bool IsZoomPanGestureActive =>
        MainZoomBorder.CanPan
        && !KeyboardExtensions.IsShiftDown()
        && !KeyboardExtensions.IsCtrlDown()
        && (!MainZoomBorder.RequireSpaceToPan || isSpacePanModifierDown || Keyboard.IsKeyDown(Key.Space));

    private bool CanUseSpacePanModifier =>
        MainZoomBorder.RequireSpaceToPan
        && MainZoomBorder.CanPan
        && !IsEditingAnyWordBorders
        && Keyboard.FocusedElement is not TextBox and not RichTextBox;

    private void SetSpacePanModifierState(bool isDown)
    {
        isSpacePanModifierDown = isDown;
        MainZoomBorder.IsSpacePanModifierPressed = isDown;
    }

    private void MoveKeyboardFocusFromButtonBase()
    {
        if (MainZoomBorder.CanPan && Keyboard.FocusedElement is ButtonBase)
            RectanglesCanvas.Focus();
    }

    private void UpdateZoomPanMode()
    {
        MainZoomBorder.RequireSpaceToPan = true;
    }

    private void MainZoomBorder_ResetRequested(object? sender, EventArgs e)
    {
        frozenFrameContentScale = 1;
    }

    private void ScaleFrozenOverlayElements(double widthScale, double heightScale)
    {
        if ((!double.IsFinite(widthScale) || widthScale <= 0)
            || (!double.IsFinite(heightScale) || heightScale <= 0))
        {
            return;
        }

        if (Math.Abs(widthScale - 1) < 0.001 && Math.Abs(heightScale - 1) < 0.001)
        {
            return;
        }

        foreach (WordBorder wordBorder in wordBorders)
        {
            wordBorder.Left *= widthScale;
            wordBorder.Top *= heightScale;
            wordBorder.Width *= widthScale;
            wordBorder.Height *= heightScale;

            if (wordBorder.DisplayLineHeight > 0)
                wordBorder.DisplayLineHeight *= heightScale;
        }

        foreach (PdfTextLineOverlay pdfTextLine in pdfTextLineOverlays)
        {
            pdfTextLine.Width *= widthScale;
            pdfTextLine.Height *= heightScale;
            Canvas.SetLeft(pdfTextLine, Canvas.GetLeft(pdfTextLine) * widthScale);
            Canvas.SetTop(pdfTextLine, Canvas.GetTop(pdfTextLine) * heightScale);
        }

        if (RectanglesCanvas.Children.Contains(selectBorder))
        {
            selectBorder.Width *= widthScale;
            selectBorder.Height *= heightScale;
            Canvas.SetLeft(selectBorder, Canvas.GetLeft(selectBorder) * widthScale);
            Canvas.SetTop(selectBorder, Canvas.GetTop(selectBorder) * heightScale);
        }

        tableEditState.ScaleSeparators(heightScale, widthScale);
        ClearTablePlacementPreview();
    }

    private void ApplyFrozenFrameContentScale()
    {
        MainZoomBorder.Reset();

        if (!IsFreezeMode || frameContentImageSource is null)
            return;

        double currentCanvasWidth = RectanglesCanvas.Width > 0 ? RectanglesCanvas.Width : RectanglesCanvas.ActualWidth;
        double currentCanvasHeight = RectanglesCanvas.Height > 0 ? RectanglesCanvas.Height : RectanglesCanvas.ActualHeight;

        SyncRectanglesCanvasSizeToImage();

        double newCanvasWidth = RectanglesCanvas.Width > 0 ? RectanglesCanvas.Width : RectanglesCanvas.ActualWidth;
        double newCanvasHeight = RectanglesCanvas.Height > 0 ? RectanglesCanvas.Height : RectanglesCanvas.ActualHeight;

        if (currentCanvasWidth > 0 && currentCanvasHeight > 0 && newCanvasWidth > 0 && newCanvasHeight > 0)
            ScaleFrozenOverlayElements(newCanvasWidth / currentCanvasWidth, newCanvasHeight / currentCanvasHeight);

        if (TableToggleButton.IsChecked is true && wordBorders.Count > 0)
            UpdateFrameText();
        else
            UpdateTemplateRegionOverlay();
    }

    private Rect GetCurrentWorkAreaBounds()
    {
        Rect currentWindowRect = new(
            Left,
            Top,
            ActualWidth > 1 ? ActualWidth : Width,
            ActualHeight > 1 ? ActualHeight : Height);

        Point windowCenter = new(
            currentWindowRect.Left + (currentWindowRect.Width / 2.0),
            currentWindowRect.Top + (currentWindowRect.Height / 2.0));

        Rect? fallbackBounds = null;
        double bestIntersectionArea = -1;

        foreach (DisplayInfo display in DisplayInfo.AllDisplayInfos)
        {
            Rect scaledBounds = display.ScaledBounds();

            fallbackBounds ??= scaledBounds;

            if (scaledBounds.Contains(windowCenter))
                return scaledBounds;

            Rect intersection = Rect.Intersect(scaledBounds, currentWindowRect);
            double intersectionArea = intersection.IsEmpty ? -1 : intersection.Width * intersection.Height;

            if (intersectionArea > bestIntersectionArea)
            {
                bestIntersectionArea = intersectionArea;
                fallbackBounds = scaledBounds;
            }
        }

        return fallbackBounds ?? SystemParameters.WorkArea;
    }

    private void EnsureMinimumLoadedDocumentWindowSize()
    {
        if (!isLoadedVisualDocument)
            return;

        Rect currentWindowRect = new(
            Left,
            Top,
            ActualWidth > 1 ? ActualWidth : Width,
            ActualHeight > 1 ? ActualHeight : Height);

        Rect targetWindowRect = GrabFrameViewScaleUtilities.GetMinimumWindowRect(
            currentWindowRect,
            new Size(
                GrabFrameViewScaleUtilities.MinimumLoadedDocumentWindowWidth,
                GrabFrameViewScaleUtilities.MinimumLoadedDocumentWindowHeight),
            GetCurrentWorkAreaBounds());

        if (Math.Abs(targetWindowRect.Width - currentWindowRect.Width) < 0.1
            && Math.Abs(targetWindowRect.Height - currentWindowRect.Height) < 0.1
            && Math.Abs(targetWindowRect.Left - currentWindowRect.Left) < 0.1
            && Math.Abs(targetWindowRect.Top - currentWindowRect.Top) < 0.1)
        {
            return;
        }

        Left = targetWindowRect.Left;
        Top = targetWindowRect.Top;
        Width = targetWindowRect.Width;
        Height = targetWindowRect.Height;
    }

    private void ClearLoadedVisualDocumentState()
    {
        isLoadedVisualDocument = false;
        frozenFrameContentScale = 1;
    }

    private void MarkLoadedVisualDocumentOpened()
    {
        isLoadedVisualDocument = true;
        frozenFrameContentScale = 1;
    }

    private void ResetView()
    {
        frozenFrameContentScale = 1;
        ApplyFrozenFrameContentScale();
    }

    private bool HasRenderedOcrOverlay() => wordBorders.Count > 0 || pdfTextLineOverlays.Count > 0;

    private void ChangeFrozenFrameScale(int direction)
    {
        bool preserveExistingOcr = HasRenderedOcrOverlay();

        if (preserveExistingOcr)
            reDrawTimer.Stop();

        if (!IsFreezeMode)
            FreezeGrabFrame();

        if (frameContentImageSource is null)
            return;

        frozenFrameContentScale = GrabFrameViewScaleUtilities.StepScale(
            frozenFrameContentScale,
            direction);

        ApplyFrozenFrameContentScale();
        ShowFrameMessage($"Image scale {frozenFrameContentScale:P0}");
    }

    public HistoryInfo AsHistoryItem()
    {
        System.Drawing.Bitmap? bitmap = ImageMethods.ImageSourceToBitmap(frameContentImageSource);

        List<WordBorderInfo> wbInfoList = [];

        foreach (WordBorder wb in wordBorders)
            wbInfoList.Add(WordBorderInfoFactory.Create(wb));

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

        (string languageTag, LanguageKind languageKind, bool usedUiAutomation) =
            LanguageUtilities.GetPersistedLanguageIdentity(currentLanguage ?? CurrentLanguage);
        bool isPdfHistory = IsPdfDocumentLoaded || historyItem?.IsPdfDocument is true;

        HistoryInfo historyInfo = new()
        {
            ID = id,
            LanguageTag = languageTag,
            LanguageKind = languageKind,
            UsedUiAutomation = usedUiAutomation,
            CaptureDateTime = DateTimeOffset.UtcNow,
            TextContent = FrameText,
            WordBorderInfoJson = wbInfoJson,
            WordBorderInfoFileName = wbInfoJson is null ? null : historyItem?.WordBorderInfoFileName,
            ImageContent = bitmap,
            PositionRect = sizePosRect,
            IsTable = TableToggleButton.IsChecked!.Value,
            ManualTableColumnSeparators = tableEditState.ManualColumnSeparators.Count > 0 ? [.. tableEditState.ManualColumnSeparators] : null,
            ManualTableRowSeparators = tableEditState.ManualRowSeparators.Count > 0 ? [.. tableEditState.ManualRowSeparators] : null,
            SourceMode = TextGrabMode.GrabFrame,
            SourceContentKind = isPdfHistory ? OpenContentKind.PdfDocument : OpenContentKind.Image,
            SourcePath = IsPdfDocumentLoaded
                ? _currentImagePath ?? string.Empty
                : historyItem?.SourcePath ?? string.Empty,
            SourcePageIndex = IsPdfDocumentLoaded
                ? _currentPdfPageIndex
                : historyItem?.SourcePageIndex ?? 0,
        };

        return historyInfo;
    }

    public void BreakWordBorderIntoWords(WordBorder wordBorder)
    {
        ICollection<string> wordLines =
            (string.IsNullOrWhiteSpace(wordBorder.DisplayText) ? wordBorder.Word : wordBorder.DisplayText)
                .Replace("\r\n", "\n")
                .Split('\n');

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

        RoutedCommand saveGrabFrameFileCommand = new();
        _ = saveGrabFrameFileCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(saveGrabFrameFileCommand, (s, args) => SaveGrabFrameFileMenuItem_Click()));

        _ = GrabCommand.InputGestures.Add(new KeyGesture(Key.G, ModifierKeys.Control));
        // _ = CommandBindings.Add(new CommandBinding(GrabCommand, GrabExecuted));

        _ = GrabTrimCommand.InputGestures.Add(new KeyGesture(Key.G, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Control));

        CheckBottomRowButtonsVis();

        if (historyItem is not null)
            await LoadContentFromHistory(historyItem);
    }

    public void GrabFrame_Unloaded(object sender, RoutedEventArgs e)
    {
        CleanupGrabFrame();
    }

    private void CleanupGrabFrame()
    {
        if (_isCleanedUp)
            return;
        _isCleanedUp = true;
        _freezeTransitionVersion++;

        MainZoomBorder.ResetRequested -= MainZoomBorder_ResetRequested;
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

        reSearchTimer.Stop();
        reSearchTimer.Tick -= ReSearchTimer_Tick;

        frameMessageTimer.Stop();
        frameMessageTimer.Tick -= FrameMessageTimer_Tick;

        contentChangeTimer.Stop();
        contentChangeTimer.Tick -= ContentChangeTimer_Tick;
        contentChangeDetector.Dispose();

        Singleton<TtsService>.Instance.BusyChanged -= OnTtsBusyChanged;

        translationTimer.Stop();
        translationTimer.Tick -= TranslationTimer_Tick;
        translationCancellationTokenSource?.Cancel();
        translationCancellationTokenSource?.Dispose();

        // Dispose the shared translation model during cleanup to prevent resource leaks
        WinAiTranslator.ReleaseModel();

        MinimizeButton.Click -= OnMinimizeButtonClick;
        RestoreButton.Click -= OnRestoreButtonClick;
        CloseButton.Click -= OnCloseButtonClick;

        RectanglesCanvas.MouseDown -= RectanglesCanvas_MouseDown;
        RectanglesCanvas.MouseMove -= RectanglesCanvas_MouseMove;
        RectanglesCanvas.MouseUp -= RectanglesCanvas_MouseUp;

        AspectRationMI.Checked -= AspectRationMI_Checked;
        AspectRationMI.Unchecked -= AspectRationMI_Checked;
        FreezeMI.Click -= FreezeMI_Click;

        SearchBar.SearchChanged -= SearchBar_SearchChanged;

        RefreshBTN.Click -= RefreshBTN_Click;
        FreezeToggleButton.Click -= FreezeToggleButton_Click;
        TableToggleButton.Click -= TableToggleButton_Click;
        EditToggleButton.Click -= EditToggleButton_Click;
        SettingsBTN.Click -= SettingsBTN_Click;
        EditTextToggleButton.Click -= EditTextBTN_Click;

        windowResizer?.Dispose();
        windowResizer = null;

        // Release the undo/redo history; its operations hold WordBorder
        // controls which in turn reference this window via OwnerGrabFrame.
        UndoRedo.Reset();

        foreach (WordBorder wb in wordBorders)
            wb.OwnerGrabFrame = null;
        wordBorders.Clear();

        _loadedPdfDocument?.Dispose();
        _loadedPdfDocument = null;
        _currentPdfPageContent = null;

        GrabFrameImage.Source = null;
        GrabFrameImage.UpdateLayout();
        frameContentImageSource = null;
        ocrResultOfWindow = null;
        frozenUiAutomationSnapshot = null;
        liveUiAutomationSnapshot = null;
        AnalyzedResultTable = null;
        destinationTextBox = null;
        historyItem = null;
        movingWordBordersDictionary.Clear();
        originalTexts.Clear();
        pdfTextLineOverlays.Clear();
        RectanglesCanvas.Children.Clear();

        // Drop any stale automation peers so a connected UIA client cannot
        // keep this closed window's visual tree alive.
        ResetAutomationPeerChildrenCache(RectanglesCanvas);
        ResetAutomationPeerChildrenCache(this);
    }

    private void DisposePreviousFrameContent()
    {
        if (GrabFrameImage.Source is null)
            return;

        GrabFrameImage.Source = null;
        GrabFrameImage.UpdateLayout();
    }

    public void MergeSelectedWordBorders()
    {
        ShouldSaveOnClose = true;
        RectanglesCanvas.ContextMenu.IsOpen = false;
        if (!IsFreezeMode)
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
        List<WordBorderInfo> selInfos = [.. selectedWordBorders.Select(wb => WordBorderInfoFactory.Create(wb))];
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
        SearchBar.UseRegex = true;
        SearchBar.SearchText = wordPattern;
        SearchBar.FocusInput();
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
        if (!IsFreezeMode)
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
        string ocrText = await OcrSourceUtilities.GetTextFromAbsoluteRectAsync(
            rect.GetScaleSizeByFraction(viewBoxZoomFactor),
            language,
            GetUiAutomationExcludedHandles());

        if (language is not UiAutomationLang && DefaultSettings.CorrectErrors)
            ocrText = ocrText.TryFixEveryWordLetterNumberErrors();

        if (language is not UiAutomationLang && DefaultSettings.CorrectToLatin && language.IsLatinBased())
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
        e.CanExecute = ShouldAllowWordBorderMerging(SelectedWordBorders().Count);
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
            SearchBar.Visibility = Visibility.Collapsed;
            MatchesMenu.Visibility = Visibility.Collapsed;
        }
        else
        {
            SearchBar.Visibility = Visibility.Visible;
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

        foreach (PdfTextLineOverlay pdfTextLine in pdfTextLineOverlays)
        {
            if (rectSelect.IntersectsWith(new Rect(pdfTextLine.Left, pdfTextLine.Top, pdfTextLine.Width, pdfTextLine.Height)))
            {
                clickedEmptySpace = false;

                if (!smallSelection)
                {
                    pdfTextLine.Select();
                    pdfTextLine.WasRegionSelected = true;
                }
                else if (!finalCheck)
                {
                    if (pdfTextLine.IsSelected)
                        pdfTextLine.Deselect();
                    else
                        pdfTextLine.Select();
                    pdfTextLine.WasRegionSelected = false;
                }
            }
            else if (pdfTextLine.WasRegionSelected && !smallSelection)
            {
                pdfTextLine.Deselect();
            }

            if (finalCheck)
                pdfTextLine.WasRegionSelected = false;
        }

        if (clickedEmptySpace
            && smallSelection
            && finalCheck)
        {
            foreach (WordBorder wb in wordBorders)
                wb.Deselect();

            foreach (PdfTextLineOverlay pdfTextLine in pdfTextLineOverlays)
                pdfTextLine.Deselect();
        }

        if (finalCheck)
            UpdateFrameText();
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
        if (!IsFreezeMode)
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
        currentSearchMatches.Clear();
        RectanglesCanvas.Children.Clear();
        wordBorders.Clear();
        ClearRenderedPdfTextLines();

        // When a UIA client (Narrator, touch keyboard, etc.) is connected,
        // WPF caches automation peers per element; without a reset the stale
        // peers keep every discarded WordBorder — and through OwnerGrabFrame,
        // this window — reachable until the client re-walks the tree.
        ResetAutomationPeerChildrenCache(RectanglesCanvas);
    }

    private static void ResetAutomationPeerChildrenCache(UIElement element)
    {
        if (UIElementAutomationPeer.FromElement(element) is AutomationPeer peer)
            peer.ResetChildrenCache();
    }

    private void ClearRenderedPdfTextLines()
    {
        PdfTextCanvas.Children.Clear();
        pdfTextLineOverlays.Clear();
        ResetAutomationPeerChildrenCache(PdfTextCanvas);
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

    private sealed class OcrBorderRenderInfo
    {
        public double DisplayLineHeight { get; init; }

        public string DisplayText { get; init; } = string.Empty;

        public bool KeepSingleLineOutput { get; init; }

        public int LineNumber { get; init; }

        public Windows.Foundation.Rect SourceRect { get; init; }

        public string Text { get; init; } = string.Empty;
    }

    private IReadOnlyList<OcrBorderRenderInfo> CreateOcrBorderRenderInfos(DpiScale dpi, double viewBoxZoomFactor)
    {
        if (ocrResultOfWindow is null)
            return [];

        List<OcrUtilities.PositionedOcrLine> positionedLines = [];

        for (int i = 0; i < ocrResultOfWindow.Lines.Length; i++)
        {
            IOcrLine ocrLine = ocrResultOfWindow.Lines[i];
            positionedLines.Add(new OcrUtilities.PositionedOcrLine(i, GetNormalizedOcrLineText(ocrLine), ocrLine.BoundingBox));
        }

        return wordGroupingMode switch
        {
            GrabFrameWordGroupingMode.Word => CreateWordLevelBorderInfos(),
            GrabFrameWordGroupingMode.Paragraph => CreateParagraphBorderInfos(positionedLines, dpi, viewBoxZoomFactor),
            GrabFrameWordGroupingMode.Window => CreateWindowBorderInfo(positionedLines),
            _ => CreateLineLevelBorderInfos(positionedLines), // Line (default)
        };
    }

    private IReadOnlyList<OcrBorderRenderInfo> CreateLineLevelBorderInfos(
        List<OcrUtilities.PositionedOcrLine> positionedLines)
    {
        return
        [
            .. positionedLines.Select(line => new OcrBorderRenderInfo
            {
                DisplayText = line.Text,
                LineNumber = line.LineNumber,
                SourceRect = line.BoundingBox,
                Text = line.Text,
            })
        ];
    }

    private IReadOnlyList<OcrBorderRenderInfo> CreateWordLevelBorderInfos()
    {
        if (ocrResultOfWindow is null)
            return [];

        List<OcrBorderRenderInfo> result = [];

        for (int lineIdx = 0; lineIdx < ocrResultOfWindow.Lines.Length; lineIdx++)
        {
            IOcrLine ocrLine = ocrResultOfWindow.Lines[lineIdx];
            foreach (IOcrWord word in ocrLine.Words)
            {
                string wordText = GetNormalizedOcrWordText(word);
                result.Add(new OcrBorderRenderInfo
                {
                    DisplayText = wordText,
                    LineNumber = lineIdx,
                    SourceRect = word.BoundingBox,
                    Text = wordText,
                });
            }
        }

        return result;
    }

    private IReadOnlyList<OcrBorderRenderInfo> CreateParagraphBorderInfos(
        List<OcrUtilities.PositionedOcrLine> positionedLines,
        DpiScale dpi,
        double viewBoxZoomFactor)
    {
        return
        [
            .. OcrUtilities.GroupWrappedParagraphLines(positionedLines)
                .Select(group => new OcrBorderRenderInfo
                {
                    DisplayLineHeight = group.Lines.Count > 1
                        ? group.Lines.Average(line => GetDisplayHeightFromSourceHeight(line.BoundingBox.Height, dpi, windowFrameImageScale, viewBoxZoomFactor))
                        : 0,
                    DisplayText = group.DisplayText,
                    KeepSingleLineOutput = group.Lines.Count > 1,
                    LineNumber = group.StartingLineNumber,
                    SourceRect = group.BoundingBox,
                    Text = group.SingleLineText,
                })
        ];
    }

    private IReadOnlyList<OcrBorderRenderInfo> CreateWindowBorderInfo(
        List<OcrUtilities.PositionedOcrLine> positionedLines)
    {
        if (positionedLines.Count == 0)
            return [];

        Windows.Foundation.Rect unionRect = positionedLines[0].BoundingBox;
        StringBuilder textBuilder = new();
        textBuilder.Append(positionedLines[0].Text);

        for (int i = 1; i < positionedLines.Count; i++)
        {
            Windows.Foundation.Rect r = positionedLines[i].BoundingBox;
            double left = Math.Min(unionRect.X, r.X);
            double top = Math.Min(unionRect.Y, r.Y);
            double right = Math.Max(unionRect.X + unionRect.Width, r.X + r.Width);
            double bottom = Math.Max(unionRect.Y + unionRect.Height, r.Y + r.Height);
            unionRect = new Windows.Foundation.Rect(left, top, right - left, bottom - top);

            textBuilder.AppendLine();
            textBuilder.Append(positionedLines[i].Text);
        }

        string fullText = textBuilder.ToString();
        return
        [
            new OcrBorderRenderInfo
            {
                DisplayText = fullText,
                LineNumber = 0,
                SourceRect = unionRect,
                Text = fullText,
            }
        ];
    }

    private string GetNormalizedOcrWordText(IOcrWord word)
    {
        string wordText = word.Text;

        if (DefaultSettings.CorrectErrors)
            wordText = wordText.TryFixNumberLetterErrors();

        if (DefaultSettings.CorrectToLatin && CurrentLanguage?.IsLatinBased() == true)
            wordText = wordText.ReplaceGreekOrCyrillicWithLatin();

        return wordText;
    }

    private double GetDisplayHeightFromSourceHeight(double sourceHeight, DpiScale dpi, double sourceScale, double viewBoxZoomFactor)
    {
        return ((sourceHeight / (dpi.DpiScaleY * sourceScale)) + 2) / viewBoxZoomFactor;
    }

    private string GetNormalizedOcrLineText(IOcrLine ocrLine)
    {
        StringBuilder lineText = new();
        ocrLine.GetTextFromOcrLine(isSpaceJoining, lineText, CurrentLanguage?.IsLatinBased() == true);
        lineText.RemoveTrailingNewlines();

        string ocrText = lineText.ToString();

        if (DefaultSettings.CorrectErrors)
            ocrText = ocrText.TryFixEveryWordLetterNumberErrors();

        if (DefaultSettings.CorrectToLatin && CurrentLanguage?.IsLatinBased() == true)
            ocrText = ocrText.ReplaceGreekOrCyrillicWithLatin();

        if (CurrentLanguage!.IsRightToLeft())
        {
            StringBuilder rtlText = new(ocrText);
            rtlText.ReverseWordsForRightToLeft();
            rtlText.RemoveTrailingNewlines();
            return rtlText.ToString().MakeStringSingleLine();
        }

        return ocrText.MakeStringSingleLine();
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
        double borderToCanvasY,
        string? displayText = null,
        bool keepSingleLineOutput = false,
        double displayLineHeight = 0)
    {
        double contentScale = IsFreezeMode ? frozenFrameContentScale : 1;

        WordBorder wordBorder = new()
        {
            DisplayLineHeight = displayLineHeight * contentScale,
            Width = (((sourceRect.Width / (dpi.DpiScaleX * sourceScale)) + 2) / viewBoxZoomFactor) * contentScale,
            Height = (((sourceRect.Height / (dpi.DpiScaleY * sourceScale)) + 2) / viewBoxZoomFactor) * contentScale,
            KeepSingleLineOutput = keepSingleLineOutput,
            Top = (((sourceRect.Y / (dpi.DpiScaleY * sourceScale) - 1) + borderToCanvasY) / viewBoxZoomFactor) * contentScale,
            Left = (((sourceRect.X / (dpi.DpiScaleX * sourceScale) - 1) + borderToCanvasX) / viewBoxZoomFactor) * contentScale,
            OwnerGrabFrame = this,
            LineNumber = lineNumber,
            IsFromEditWindow = IsFromEditWindow,
            MatchingBackground = backgroundBrush,
        };

        if (keepSingleLineOutput && !string.IsNullOrWhiteSpace(displayText))
            wordBorder.DisplayText = displayText;
        else
            wordBorder.Word = text;

        return wordBorder;
    }

    private void AddRenderedWordBorder(WordBorder wordBorderBox)
    {
        if (!IsOcrValid)
            return;

        wordBorders.Add(wordBorderBox);
        _ = RectanglesCanvas.Children.Add(wordBorderBox);

        if (isAutoOcrRedrawPass)
            return;

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.AddWordBorder,
            new GrabFrameOperationArgs()
            {
                WordBorder = wordBorderBox,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });
    }

    private PdfTextLineOverlay CreatePdfTextLineOverlay(Windows.Foundation.Rect sourceRect, double sourceScale, string text, DpiScale dpi)
    {
        double contentScale = IsFreezeMode ? frozenFrameContentScale : 1;
        Rect displayRect = new(
            (sourceRect.X / (dpi.DpiScaleX * sourceScale)) * contentScale,
            (sourceRect.Y / (dpi.DpiScaleY * sourceScale)) * contentScale,
            (sourceRect.Width / (dpi.DpiScaleX * sourceScale)) * contentScale,
            (sourceRect.Height / (dpi.DpiScaleY * sourceScale)) * contentScale);

        PdfTextLineOverlay overlay = new(text);
        overlay.ApplyLayout(displayRect);
        return overlay;
    }

    private void AddRenderedPdfTextLine(PdfTextLineOverlay overlay)
    {
        if (!IsOcrValid)
            return;

        pdfTextLineOverlays.Add(overlay);
        _ = PdfTextCanvas.Children.Add(overlay);
    }

    private async Task DrawRectanglesAroundWords(string searchWord = "")
    {
        if (CurrentLanguage is UiAutomationLang)
            await DrawUiAutomationRectanglesAsync(searchWord);
        else
            await DrawOcrRectanglesAsync(searchWord);

        // The overlay just changed; rebase the change detector so the newly
        // drawn word borders become part of the baseline instead of being
        // judged as screen-content changes that re-trigger a refresh.
        contentChangeDetector.Reset();

        // Only a fresh grab (or re-OCR) should trigger auto-speak. Selection,
        // edits, moves and other overlay mutations also rebuild FrameText, so
        // arm the speak-on-next-update flag here rather than speaking on every
        // UpdateFrameText call.
        _speakOnNextFrameTextUpdate = true;
    }

    private async Task DrawOcrRectanglesAsync(string searchWord = "")
    {
        if (isDrawing || IsDragOver)
            return;

        if (_currentPdfPageContent?.HasNativeText is true)
        {
            await DrawPdfRectanglesAsync(searchWord);
            return;
        }

        isDrawing = true;
        IsOcrValid = true;

        if (string.IsNullOrWhiteSpace(searchWord))
            searchWord = SearchBar.SearchText;

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
            if (frameContentImageSource is BitmapSource frozenBmp)
            {
                using System.Drawing.Bitmap bmpForOcr = ImageMethods.BitmapSourceToBitmap(frozenBmp);
                (ocrResultOfWindow, windowFrameImageScale) = await OcrSourceUtilities.GetOcrResultFromBitmapAsync(bmpForOcr, CurrentLanguage);
            }
            else
            {
                (ocrResultOfWindow, windowFrameImageScale) = await OcrSourceUtilities.GetOcrResultFromRegionAsync(rectCanvasSize, CurrentLanguage);
            }
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

        if (frameContentImageSource is BitmapSource bmpImg)
        {
            bmp = ImageMethods.BitmapSourceToBitmap(bmpImg);
            shouldDisposeBmp = true;
        }
        else
        {
            bmp = ImageMethods.GetRegionOfScreenAsBitmap(rectCanvasSize, cacheResult: false);
            shouldDisposeBmp = true;
        }

        bool useImageCoords = frameContentImageSource is not null;
        (double viewBoxZoomFactor, double borderToCanvasX, double borderToCanvasY) =
            useImageCoords ? (1.0, 0.0, 0.0) : GetOverlayRenderMetrics();

        if (useImageCoords)
            SyncRectanglesCanvasSizeToImage();

        IReadOnlyList<OcrBorderRenderInfo> renderInfos = CreateOcrBorderRenderInfos(dpi, viewBoxZoomFactor);

        foreach (OcrBorderRenderInfo renderInfo in renderInfos)
        {
            Windows.Foundation.Rect lineRect = renderInfo.SourceRect;

            SolidColorBrush backgroundBrush = new(Colors.Black);

            if (bmp is not null)
                backgroundBrush = GetBackgroundBrushFromOcrBitmap(windowFrameImageScale, bmp, ref lineRect);

            WordBorder wordBorderBox = CreateWordBorderFromSourceRect(
                lineRect,
                windowFrameImageScale,
                renderInfo.Text,
                renderInfo.LineNumber,
                backgroundBrush,
                dpi,
                viewBoxZoomFactor,
                borderToCanvasX,
                borderToCanvasY,
                renderInfo.DisplayText,
                renderInfo.KeepSingleLineOutput,
                renderInfo.DisplayLineHeight);

            AddRenderedWordBorder(wordBorderBox);
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
        if (isTranslationEnabled && WinAiTranslator.IsAvailable())
        {
            translationTimer.Stop();
            translationTimer.Start();
        }
    }

    private async Task DrawPdfRectanglesAsync(string searchWord = "")
    {
        if (isDrawing || IsDragOver || _loadedPdfDocument is null || _currentPdfPageContent is null || _currentPdfPageIndex < 0)
            return;

        isDrawing = true;
        IsOcrValid = true;
        windowFrameImageScale = 1;
        ocrResultOfWindow = null;

        if (string.IsNullOrWhiteSpace(searchWord))
            searchWord = SearchBar.SearchText;

        ClearRenderedWordBorders();

        if (frameContentImageSource is not BitmapSource)
        {
            isDrawing = false;
            reDrawTimer.Start();
            return;
        }

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        SyncRectanglesCanvasSizeToImage();
        isSpaceJoining = CurrentLanguage!.IsSpaceJoining();

        IReadOnlyList<PdfPageTextLine> pageLines = await _loadedPdfDocument.GetSelectableLinesAsync(_currentPdfPageIndex, CurrentLanguage);

        foreach (PdfPageTextLine pageLine in pageLines)
        {
            string lineText = pageLine.Text;
            if (!pageLine.IsNativeText)
            {
                if (DefaultSettings.CorrectErrors)
                    lineText = lineText.TryFixEveryWordLetterNumberErrors();

                if (DefaultSettings.CorrectToLatin && CurrentLanguage!.IsLatinBased())
                    lineText = lineText.ReplaceGreekOrCyrillicWithLatin();
            }

            if (CurrentLanguage!.IsRightToLeft() && !pageLine.IsNativeText)
            {
                StringBuilder sb = new(lineText);
                sb.ReverseWordsForRightToLeft();
                sb.RemoveTrailingNewlines();
                lineText = sb.ToString();
            }

            PdfTextLineOverlay overlay = CreatePdfTextLineOverlay(pageLine.SourceRect, 1, lineText, dpi);
            AddRenderedPdfTextLine(overlay);
        }

        if (DefaultSettings.TryToReadBarcodes)
            TryToReadBarcodes(dpi);

        isDrawing = false;
        reSearchTimer.Start();

        if (isTranslationEnabled && WinAiTranslator.IsAvailable())
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
            searchWord = SearchBar.SearchText;

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
        if ((isStaticImageSource || IsFreezeMode) && frozenUiAutomationSnapshot is not null)
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

        bool useImageCoords = frameContentImageSource is not null;
        (double viewBoxZoomFactor, double borderToCanvasX, double borderToCanvasY) =
            useImageCoords ? (1.0, 0.0, 0.0) : GetOverlayRenderMetrics();

        if (useImageCoords)
            SyncRectanglesCanvasSizeToImage();

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

        if (isTranslationEnabled && WinAiTranslator.IsAvailable())
        {
            translationTimer.Stop();
            translationTimer.Start();
        }
    }

    private void EditMatchesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        List<string> selectedMatches =
        [
            .. currentSearchMatches
                .Where(match => match.IsSelected && !string.IsNullOrEmpty(match.Text))
                .Select(match => match.Text)
        ];

        if (selectedMatches.Count == 0)
            return;

        EditTextWindow editWindow = new();
        editWindow.AddThisText(string.Join(Environment.NewLine, selectedMatches));
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
            EditTextWindow etw = WindowUtilities.OpenOrActivateEditTextWindow(TableToggleButton.IsChecked is true);
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
        if (tableEditState.IsPlacementActive)
        {
            CancelTablePlacement();
            return;
        }

        if (wordBorders.Any(x => x.IsEditing))
        {
            GrabBTN.Focus();
            return;
        }

        if (TextSearchUtilities.HasSearchText(SearchBar.SearchText) && SearchBar.SearchText != "Search For Text...")
            SearchBar.SearchText = "";
        else if (RectanglesCanvas.Children.Count > 0)
        {
            CancelTablePlacement(clearManualSeparators: true);
            ResetGrabFrame();
        }
        else if (PdfTextCanvas.Children.Count > 0)
        {
            CancelTablePlacement(clearManualSeparators: true);
            ResetGrabFrame();
        }
        else
            Close();
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

    /// <summary>
    /// Freezes the frame when a user starts editing a word border. Editing a word is a strong
    /// signal the user is correcting recognized text, so the frame should stop updating/resetting
    /// underneath them. No-op when the frame is already frozen (including loaded images/PDFs).
    /// This is the public entry point for that intent; the actual freeze routine
    /// (<see cref="FreezeGrabFrame"/>) stays private to the frame.
    /// </summary>
    public void FreezeFrameForWordEditing()
    {
        if (IsFreezeMode)
            return;

        FreezeGrabFrame();
    }

    private void FreezeGrabFrame()
    {
        _freezeTransitionVersion++;
        Opacity = 1;
        DisposePreviousFrameContent();
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
        UpdateZoomPanMode();

        if (scrollBehavior == ScrollBehavior.ZoomWhenFrozen)
            MainZoomBorder.CanZoom = true;

        ApplyFrozenFrameContentScale();
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
        double contentScale = IsFreezeMode ? frozenFrameContentScale : 1;
        double sourceWidth = (source.PixelWidth > 0 ? source.PixelWidth / dpi.DpiScaleX : source.Width) * contentScale;
        double sourceHeight = (source.PixelHeight > 0 ? source.PixelHeight / dpi.DpiScaleY : source.Height) * contentScale;

        if (double.IsFinite(sourceWidth) && sourceWidth > 0)
        {
            GrabFrameImage.Width = sourceWidth;
            PdfTextCanvas.Width = sourceWidth;
            RectanglesCanvas.Width = sourceWidth;
            TablePlacementOverlayCanvas.Width = sourceWidth;
            TemplateRegionOverlayCanvas.Width = sourceWidth;
        }

        if (double.IsFinite(sourceHeight) && sourceHeight > 0)
        {
            GrabFrameImage.Height = sourceHeight;
            PdfTextCanvas.Height = sourceHeight;
            RectanglesCanvas.Height = sourceHeight;
            TablePlacementOverlayCanvas.Height = sourceHeight;
            TemplateRegionOverlayCanvas.Height = sourceHeight;
        }
    }

    private async void FreezeMI_Click(object sender, RoutedEventArgs e)
    {
        if (IsFreezeMode)
        {
            if (IsPdfDocumentLoaded)
            {
                FreezeToggleButton.IsChecked = true;
                return;
            }

            FreezeToggleButton.IsChecked = false;
            // Diff the frozen snapshot against the live screen before clearing so
            // unchanged content keeps its (possibly edited) word borders.
            UnfreezeGrabFrameWithDiff();
            return;
        }

        RectanglesCanvas.ContextMenu.IsOpen = false;
        await Task.Delay(150);
        FreezeToggleButton.IsChecked = true;
        ResetGrabFrame();
        FreezeGrabFrame();

        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    private void FreezeToggleButton_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        if (FreezeToggleButton.IsChecked is bool freezeMode && freezeMode)
            FreezeGrabFrame();
        else if (IsPdfDocumentLoaded)
            FreezeToggleButton.IsChecked = true;
        else
            UnfreezeGrabFrameWithDiff();
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

        if (string.IsNullOrWhiteSpace(DefaultSettings.GrabFrameWordGrouping)
            || !Enum.TryParse(DefaultSettings.GrabFrameWordGrouping, out wordGroupingMode))
        {
            wordGroupingMode = DefaultSettings.ParagraphDetection
                ? GrabFrameWordGroupingMode.Paragraph
                : GrabFrameWordGroupingMode.Line;
        }

        SetWordGroupingMenuItems();
        LoadHiddenBottomBarTools();
        LoadBorderStyle();
        GetGrabFrameTranslationSettings();
        GetGrabFrameSpeakSettings();
        _ = Enum.TryParse(DefaultSettings.GrabFrameScrollBehavior, out scrollBehavior);
        SetScrollBehaviorMenuItems();
    }

    private void LoadHiddenBottomBarTools()
    {
        hiddenBottomBarTools.Clear();

        string saved = DefaultSettings.GrabFrameHiddenBottomBarTools ?? string.Empty;
        foreach (string key in saved.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            hiddenBottomBarTools.Add(key);

        ShowRefreshToolMenuItem.IsChecked = !hiddenBottomBarTools.Contains("Refresh");
        ShowFreezeToolMenuItem.IsChecked = !hiddenBottomBarTools.Contains("Freeze");
        ShowTableToolMenuItem.IsChecked = !hiddenBottomBarTools.Contains("Table");
        ShowTranslateToolMenuItem.IsChecked = !hiddenBottomBarTools.Contains("Translate");
        ShowSpeakToolMenuItem.IsChecked = !hiddenBottomBarTools.Contains("Speak");
        ShowEditTextToolMenuItem.IsChecked = !hiddenBottomBarTools.Contains("EditText");
        ShowTemplateToolMenuItem.IsChecked = !hiddenBottomBarTools.Contains("Template");

        ApplyBottomBarToolVisibility();
    }

    private bool IsBottomBarToolHidden(string key) => hiddenBottomBarTools.Contains(key);

    private void SetToolButtonVisibility(UIElement button, string key, bool appAvailable)
    {
        button.Visibility = appAvailable && !hiddenBottomBarTools.Contains(key)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyBottomBarToolVisibility()
    {
        // Refresh / OCR Frame buttons swap based on Auto OCR state; that helper honors the hide preference.
        SetRefreshOrOcrFrameBtnVis();

        // Freeze doubles as Unfreeze while frozen, so it must stay available in both
        // states — only a static (already-frozen) image source makes it irrelevant.
        SetToolButtonVisibility(FreezeToggleButton, "Freeze", !isStaticImageSource);

        SetToolButtonVisibility(TranslateToggleButton, "Translate", translateToolAvailable);

        // These tools are always available, so their visibility is driven purely by the hide preference.
        SetToolButtonVisibility(TableToggleButton, "Table", true);
        SetToolButtonVisibility(SpeakToggleButton, "Speak", true);
        SetToolButtonVisibility(EditTextToggleButton, "EditText", true);
        SetToolButtonVisibility(TemplateMenuButton, "Template", true);
    }

    private void ToggleBottomBarToolMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string key)
            return;

        if (menuItem.IsChecked)
            hiddenBottomBarTools.Remove(key);
        else
            hiddenBottomBarTools.Add(key);

        DefaultSettings.GrabFrameHiddenBottomBarTools = string.Join(",", hiddenBottomBarTools);
        DefaultSettings.Save();

        ApplyBottomBarToolVisibility();
    }

    private void LoadBorderStyle()
    {
        if (string.IsNullOrWhiteSpace(DefaultSettings.GrabFrameBorderStyle)
            || !Enum.TryParse(DefaultSettings.GrabFrameBorderStyle, out borderStyle))
            borderStyle = GrabFrameBorderStyle.Theme;

        borderCustomColor = ParseColorOrDefault(DefaultSettings.GrabFrameBorderColor, borderCustomColor);

        SetBorderStyleMenuItems();
        ApplyBorderStyle();
    }

    private static Color ParseColorOrDefault(string? hex, Color fallback)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                if (ColorConverter.ConvertFromString(hex) is Color parsed)
                    return parsed;
            }
            catch { /* fall through to the default color */ }
        }
        return fallback;
    }

    private void SetBorderStyleMenuItems()
    {
        BorderStyleThemeMenuItem.IsChecked = borderStyle == GrabFrameBorderStyle.Theme;
        BorderStyleHighContrastMenuItem.IsChecked = borderStyle == GrabFrameBorderStyle.HighContrast;

        string currentHex = $"#{borderCustomColor.R:X2}{borderCustomColor.G:X2}{borderCustomColor.B:X2}";
        foreach (object item in BorderColorMenuItem.Items)
        {
            if (item is MenuItem colorItem && colorItem.Tag is string tag)
                colorItem.IsChecked = borderStyle == GrabFrameBorderStyle.Color
                    && string.Equals(tag, currentHex, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Applies the current border style to both frame edges: the 1px window
    /// border (outer ring) and the 2px FrameBorder (inner ring). High contrast
    /// uses the two as opposing black/white tones so one always contrasts with
    /// whatever is behind the frame.
    /// </summary>
    private void ApplyBorderStyle()
    {
        switch (borderStyle)
        {
            case GrabFrameBorderStyle.HighContrast:
                BorderBrush = Brushes.White;
                FrameBorder.BorderBrush = Brushes.Black;
                break;
            case GrabFrameBorderStyle.Color:
                SolidColorBrush colorBrush = new(borderCustomColor);
                BorderBrush = colorBrush;
                FrameBorder.BorderBrush = colorBrush;
                break;
            case GrabFrameBorderStyle.Theme:
            default:
                // Use dynamic resource references so the border keeps following
                // live app light/dark theme changes, matching the XAML default.
                SetResourceReference(BorderBrushProperty, "ApplicationBackgroundBrush");
                FrameBorder.SetResourceReference(Border.BorderBrushProperty, "ApplicationBackgroundBrush");
                break;
        }
    }

    private void BorderStyleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || !Enum.TryParse(menuItem.Tag?.ToString(), out borderStyle))
            return;

        DefaultSettings.GrabFrameBorderStyle = borderStyle.ToString();
        DefaultSettings.Save();
        SetBorderStyleMenuItems();
        ApplyBorderStyle();
    }

    private void BorderColorMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not string hex)
            return;

        borderCustomColor = ParseColorOrDefault(hex, borderCustomColor);
        borderStyle = GrabFrameBorderStyle.Color;
        DefaultSettings.GrabFrameBorderStyle = borderStyle.ToString();
        DefaultSettings.GrabFrameBorderColor = hex;
        DefaultSettings.Save();
        SetBorderStyleMenuItems();
        ApplyBorderStyle();
    }

    private void GrabFrameWindow_Activated(object? sender, EventArgs e)
    {
        RectanglesCanvas.Opacity = 1;
        if (!IsWordEditMode && !IsFreezeMode)
            reDrawTimer.Start();
        else
            reSearchTimer.Start();

        // Reflect any border change made on the Settings page while away.
        if (IsLoaded)
            LoadBorderStyle();
    }

    private void GrabFrameWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (ShouldSaveOnClose)
            Singleton<HistoryService>.Instance.SaveToHistory(this);

        historyItem?.ClearTransientImage();
        ClearLoadedPdfDocument();

        FrameText = "";
        wordBorders.Clear();
        pdfTextLineOverlays.Clear();
        UpdateFrameText(preserveLinkedSpreadsheetSelection: true);
    }

    private void GrabFrameWindow_Deactivated(object? sender, EventArgs e)
    {
        _spacePanGraceTimer?.Stop();
        _spacePanGraceTimer = null;
        SetSpacePanModifierState(false);

        if (!IsWordEditMode && !IsFreezeMode)
        {
            ResetGrabFrame();
            return;
        }

        RectanglesCanvas.Opacity = 1;
        if (Keyboard.Modifiers != ModifierKeys.Alt)
            wasAltHeld = false;

        if (AutoOcrCheckBox.IsChecked is true && !IsFreezeMode)
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

        await TryLoadDocumentFromPath(fileName);

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

        if (scrollBehavior == ScrollBehavior.ZoomWhenFrozen && IsFreezeMode)
            return; // ZoomBorder handles scroll when frozen

        if (IsPdfDocumentLoaded)
        {
            // ZoomBorder handles the scroll and sets CanPan=true synchronously after we return.
            // Defer a focus check so ButtonBase never holds focus while panning is possible.
            Dispatcher.InvokeAsync(MoveKeyboardFocusFromButtonBase, DispatcherPriority.Input);
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

        foreach (PdfTextLineOverlay pdfTextLine in pdfTextLineOverlays)
        {
            if (pdfTextLine.IsSelected)
                pdfTextLine.Deselect();
            else
                pdfTextLine.Select();
        }

        UpdateFrameText();
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

    private async void NotifyIfUiAutomationNeedsLiveSource(ILanguage language)
    {
        if (!CaptureLanguageUtilities.RequiresLiveUiAutomationSource(
            language,
            isStaticImageSource,
            frozenUiAutomationSnapshot is not null))
            return;

        string message = DefaultSettings.UiAutomationFallbackToOcr
            ? "UI Automation reads live application controls. This Grab Frame currently contains a static image, so Text Grab will fall back to OCR for image-only operations."
            : "UI Automation reads live application controls. This Grab Frame currently contains a static image, so image-only operations will not return UI Automation text.";

        await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Text Grab",
            Content = message,
            CloseButtonText = "OK"
        }.ShowDialogAsync();
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
        await Singleton<HistoryService>.Instance.PopulateMenuItemWithRecentPdfs(OpenRecentPdfsMenuItem);
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
            Filter = FileUtilities.GetVisualDocumentFilter()
        };

        bool? result = dlg.ShowDialog();

        if (result is false || !File.Exists(dlg.FileName))
            return;

        await TryLoadDocumentFromPath(dlg.FileName);

        reDrawTimer.Start();
    }

    private async void CaptureFromCameraMenuItem_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        string? capturedImagePath = await CameraCaptureUtilities.CaptureImageFromCameraAsync(this);

        if (capturedImagePath is null)
            return;

        await TryLoadDocumentFromPath(capturedImagePath);

        reDrawTimer.Start();
    }

    private async void SaveGrabFrameFileMenuItem_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        Microsoft.Win32.SaveFileDialog dlg = new()
        {
            Filter = GrabFrameFileUtilities.GetGrabFrameFileFilter(),
            DefaultExt = GrabFrameFileUtilities.GrabFrameFileExtension,
            AddExtension = true,
            Title = "Save Grab Frame File",
            FileName = "Grab Frame",
        };

        if (dlg.ShowDialog() is not true)
            return;

        HistoryInfo historyInfo = AsHistoryItem();

        try
        {
            bool saved = await GrabFrameFileUtilities.SaveGrabFrameFileAsync(historyInfo, dlg.FileName);

            if (!saved)
            {
                await new Wpf.Ui.Controls.MessageBox
                {
                    Title = "Text Grab",
                    Content = $"Failed to save Grab Frame file:\n{dlg.FileName}",
                    CloseButtonText = "OK"
                }.ShowDialogAsync();
            }
        }
        finally
        {
            // AsHistoryItem() renders a fresh Bitmap from the frame content; dispose it once
            // the save completes so the GDI handle is released.
            historyInfo.ImageContent?.Dispose();
            historyInfo.ClearTransientImage();
        }
    }

    private async void OpenGrabFrameFileMenuItem_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        Microsoft.Win32.OpenFileDialog dlg = new()
        {
            Filter = GrabFrameFileUtilities.GetGrabFrameFileFilter(),
            Title = "Open Grab Frame File",
            CheckFileExists = true,
        };

        if (dlg.ShowDialog() is not true || !File.Exists(dlg.FileName))
            return;

        HistoryInfo? historyInfo = await GrabFrameFileUtilities.LoadGrabFrameFileAsync(dlg.FileName);

        if (historyInfo is null)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Text Grab",
                Content = $"Failed to open Grab Frame file:\n{dlg.FileName}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return;
        }

        // Open the loaded frame in its own window so the current frame is left untouched.
        GrabFrame grabFrame = new(historyInfo);
        grabFrame.Show();
        grabFrame.Activate();
    }

    private async void PasteExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        (bool success, ImageSource? clipboardImage) = ClipboardUtilities.TryGetImageFromClipboard();

        if (!success || clipboardImage is null)
            return;

        reDrawTimer.Stop();

        ClearLoadedVisualDocumentState();
        CancelTablePlacement(clearManualSeparators: true);
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

        ClearLoadedPdfDocument();
        hasLoadedImageSource = true;
        isStaticImageSource = true;
        MarkLoadedVisualDocumentOpened();
        frozenUiAutomationSnapshot = null;
        liveUiAutomationSnapshot = null;
        FreezeToggleButton.IsChecked = true;
        FreezeGrabFrame();
        EnsureMinimumLoadedDocumentWindowSize();
        FreezeToggleButton.Visibility = Visibility.Collapsed;
        SwitchToOcrFallbackIfUiAutomation();

        reDrawTimer.Start();
    }

    private async void RateAndReview_Click(object sender, RoutedEventArgs e)
    {
        _ = await Launcher.LaunchUriAsync(new Uri(string.Format("ms-windows-store:REVIEW?PFN={0}", "40087JoeFinApps.TextGrab_kdbpvth5scec4")));
    }

    private void RectanglesCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        bool isPdfTextInteraction = IsPdfTextInteraction(sender);
        FrameworkElement interactionSurface = isPdfTextInteraction
            ? (e.OriginalSource as FrameworkElement ?? PdfTextCanvas)
            : (GetInteractionSurface(sender) ?? RectanglesCanvas);
        bool shouldPanInsteadOfSelect = MainZoomBorder.CanPan
            && (IsPdfDocumentLoaded
                ? IsZoomPanGestureActive
                : (IsZoomPanGestureActive && !isPdfTextInteraction));

        reDrawTimer.Stop();
        if (!MainZoomBorder.CanPan)
            GrabBTN.Focus();

        if (tableEditState.IsPlacementActive)
        {
            if (e.RightButton == MouseButtonState.Pressed
                || e.MiddleButton == MouseButtonState.Pressed
                || IsCtrlDown
                || shouldPanInsteadOfSelect)
            {
                e.Handled = true;
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = TryCommitTablePlacement(e.GetPosition(RectanglesCanvas));
                return;
            }
        }

        if (e.RightButton == MouseButtonState.Pressed)
        {
            e.Handled = false;
            return;
        }

        if (MainZoomBorder.CanPan)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                ResetView();
                return;
            }

            if (shouldPanInsteadOfSelect)
                return;
        }

        isSelecting = true;
        clickedPoint = e.GetPosition(RectanglesCanvas);
        interactionSurface.CaptureMouse();
        selectBorder.Height = 1;
        selectBorder.Width = 1;

        isSearchSelectionOverridden = true;

        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            e.Handled = true;

            isMiddleDown = true;
            if (!IsPdfDocumentLoaded)
            {
                ResetGrabFrame();
                UnfreezeGrabFrame();
            }
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
        FrameworkElement interactionSurface = GetInteractionSurface(sender) ?? RectanglesCanvas;
        bool isPdfTextInteraction = IsPdfTextInteraction(sender);
        bool shouldPanInsteadOfSelect = MainZoomBorder.CanPan
            && ((IsPdfDocumentLoaded || !isPdfTextInteraction) && IsZoomPanGestureActive);

        if (tableEditState.IsPlacementActive)
        {
            interactionSurface.Cursor = (!IsCtrlDown && !shouldPanInsteadOfSelect)
                ? Cursors.Cross
                : Cursors.Arrow;

            if (IsCtrlDown || shouldPanInsteadOfSelect)
            {
                ClearTablePlacementPreview();
                return;
            }

            UpdateTablePlacementPreview(e.GetPosition(RectanglesCanvas));
            return;
        }

        if (IsCtrlDown)
            interactionSurface.Cursor = Cursors.Cross;
        else if (MainZoomBorder.CanPan)
            interactionSurface.Cursor = shouldPanInsteadOfSelect
                ? Cursors.SizeAll
                : Cursors.Arrow;
        else
            interactionSurface.Cursor = null;

        if (!isSelecting && !isMiddleDown && movingWordBordersDictionary.Count == 0)
            return;

        isMiddleDown = e.MiddleButton == MouseButtonState.Pressed;

        if (shouldPanInsteadOfSelect)
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
            if (!IsFreezeMode)
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
        Mouse.Captured?.ReleaseMouseCapture();

        if (tableEditState.IsPlacementActive)
            return;

        if (e.ChangedButton == MouseButton.Middle && scrollBehavior != ScrollBehavior.Zoom)
        {
            isMiddleDown = false;
            if (!IsPdfDocumentLoaded)
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

        if (SearchBar.SearchText is string searchText)
        {
            // Timer-driven redraws are not user actions, so the word borders
            // they render must not be recorded in the undo stack; recording
            // them pinned every rendered border for the life of the frame.
            isAutoOcrRedrawPass = true;
            try
            {
                await DrawRectanglesAroundWords(searchText);
            }
            finally
            {
                isAutoOcrRedrawPass = false;
            }
        }
    }

    private void ContentChangeTimer_Tick(object? sender, EventArgs e)
    {
        // Only an unfrozen frame shows live screen content worth watching.
        if (!IsLoaded
            || IsFreezeMode
            || hasLoadedImageSource
            || isStaticImageSource
            || IsPdfDocumentLoaded
            || WindowState == WindowState.Minimized)
        {
            contentChangeDetector.Reset();
            return;
        }

        // Skip while the user or the OCR pipeline is mid-operation; a capture
        // taken now would make an unstable baseline. Open context menus,
        // dropdowns, and tooltips render over the captured region, so they
        // would register as a content change and wrongly trigger a refresh.
        if (AutoOcrCheckBox.IsChecked is not true
            || isDrawing
            || reDrawTimer.IsEnabled
            || isSelecting
            || IsEditingAnyWordBorders
            || movingWordBordersDictionary.Count > 0
            || Mouse.Captured is not null
            || IsAnyPopupOpen()
            || CheckKey(VirtualKeyCodes.LeftButton)
            || CheckKey(VirtualKeyCodes.MiddleButton))
        {
            return;
        }

        System.Drawing.Rectangle contentRect = GetContentAreaScreenRect();
        if (contentRect.Width <= 1 || contentRect.Height <= 1)
            return;

        using System.Drawing.Bitmap capture = ImageMethods.GetRegionOfScreenAsBitmap(contentRect, cacheResult: false);

        if (!contentChangeDetector.CheckForChangeAndUpdate(capture))
            return;

        // The screen behind the frame changed; clear stale results and let the
        // redraw timer re-capture and re-OCR (same path as moving the window).
        // Reset the detector so the freshly drawn word borders become the next
        // baseline instead of immediately re-triggering another refresh.
        contentChangeDetector.Reset();
        ResetGrabFrame();
        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    /// <summary>
    /// Returns true when any WPF popup is showing on this thread — context
    /// menus, combo box dropdowns, submenus, and tooltips are all hosted in
    /// a visible PopupRoot presentation source.
    /// </summary>
    private static bool IsAnyPopupOpen()
    {
        foreach (PresentationSource source in PresentationSource.CurrentSources)
        {
            // PopupRoot is internal to WPF, so it can only be matched by type
            // name; if a future framework version renames it this check silently
            // stops detecting popups (the change detector would then run while a
            // popup is open, at worst causing one spurious refresh).
            if (source.RootVisual is UIElement { IsVisible: true } rootElement
                && rootElement.GetType().Name == "PopupRoot")
            {
                return true;
            }
        }

        return false;
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

        if (hasLoadedImageSource || IsFreezeMode)
        {
            // For loaded or frozen images, clear OCR results and re-run OCR on the same stored image.
            // Zoom must be reset because the OCR pipeline
            // calculates word border positions assuming no zoom transform.
            MainZoomBorder.Reset();
            RectanglesCanvas.RenderTransform = Transform.Identity;
            IsOcrValid = false;
            ocrResultOfWindow = null;
            ClearRenderedWordBorders();
            MatchesTXTBLK.Text = "- Matches";
            UpdateFrameText();
        }
        else
        {
            ResetGrabFrame();

            await Task.Delay(200);

            DisposePreviousFrameContent();
            frameContentImageSource = ImageMethods.GetWindowBoundsImage(this);
            GrabFrameImage.Source = frameContentImageSource;
        }

        if (AutoOcrCheckBox.IsChecked is false)
            FreezeGrabFrame();

        if (SearchBar.SearchText is string searchText)
            await DrawRectanglesAroundWords(searchText);

        UndoRedo.EndTransaction();
    }

    private void RemoveTableLines()
    {
        List<Canvas> tableLines =
            [.. RectanglesCanvas.Children
                .OfType<Canvas>()
                .Where(element => element.Tag is "TableLines")];

        foreach (Canvas tableLineCanvas in tableLines)
            RectanglesCanvas.Children.Remove(tableLineCanvas);
    }

    private void ReSearchTimer_Tick(object? sender, EventArgs e)
    {
        reSearchTimer.Stop();
        string searchText = SearchBar.SearchText;

        // A smart pattern (recognizer) chip is active; typed text narrows its matches.
        if (SearchBar.SelectedPattern is { } selectedPattern)
        {
            RunPatternSearch(selectedPattern, searchText);
            return;
        }

        if (!TextSearchUtilities.HasSearchText(searchText) && !isSearchSelectionOverridden)
        {
            ClearSearchMatchesAndSelection();
            MatchesTXTBLK.Text = $"0 Matches";
            UpdateFrameText();
            return;
        }

        if (!SearchBar.UseRegex)
            searchText = searchText.EscapeSpecialRegexChars(SearchBar.ExactMatch);

        Regex regex;

        try
        {
            regex = TextSearchUtilities.CreateGrabFrameSearchRegex(
                searchText,
                SearchBar.ExactMatch);
        }
        catch (Exception)
        {
            ClearSearchMatchesAndSelection();
            UpdateFrameText();
            MatchesTXTBLK.Text = $"Search Error";
            return;
        }

        if (!isSearchSelectionOverridden)
        {
            ClearSearchMatchesAndSelection();
            foreach (GrabFrameSearchUnit searchUnit in GetSearchUnits())
            {
                foreach (Match match in regex.Matches(searchUnit.Text).Cast<Match>().Where(match => match.Success))
                    AddSearchMatch(searchUnit, match.Index, match.Length, match.Value);
            }
        }

        UpdateFrameText();

        if (string.IsNullOrEmpty(searchText))
        {
            MatchesMenu.Visibility = Visibility.Collapsed;
            return;
        }

        int numberOfMatches = currentSearchMatches.Count;
        MatchesTXTBLK.Text = numberOfMatches == 1 ? "1 Match" : $"{numberOfMatches} Matches";
        MatchesMenu.Visibility = Visibility.Visible;
        LanguagesComboBox.Visibility = Visibility.Collapsed;

        if (TemplateSavePanel.Visibility == Visibility.Visible)
            UpdateTemplateBadges();
    }

    /// <summary>
    /// Selects every word border / PDF line that contains at least one entity recognized
    /// by <paramref name="recognizer"/>, and reports the total recognized-entity count.
    /// </summary>
    private static bool MatchesNarrowText(string text, string narrowText)
        => string.IsNullOrEmpty(narrowText) || text.Contains(narrowText, StringComparison.CurrentCultureIgnoreCase);

    private void RunPatternSearch(PatternItem pattern, string narrowText = "")
    {
        if (!isSearchSelectionOverridden)
        {
            ClearSearchMatchesAndSelection();
            foreach (GrabFrameSearchUnit searchUnit in GetSearchUnits())
            {
                foreach (RecognizerMatch match in PatternExecutor.GetMatches(pattern, searchUnit.Text)
                    .Where(match => MatchesNarrowText(match.Text, narrowText)))
                    AddSearchMatch(searchUnit, match.Start, match.Length, match.Text);
            }
        }

        UpdateFrameText();

        int numberOfMatches = currentSearchMatches.Count;
        MatchesTXTBLK.Text = numberOfMatches == 1 ? "1 Match" : $"{numberOfMatches} Matches";
        MatchesMenu.Visibility = Visibility.Visible;
        LanguagesComboBox.Visibility = Visibility.Collapsed;

        if (TemplateSavePanel.Visibility == Visibility.Visible)
            UpdateTemplateBadges();
    }

    private void AddSearchMatch(GrabFrameSearchUnit searchUnit, int start, int length, string text)
    {
        if (length <= 0 || string.IsNullOrEmpty(text))
            return;

        List<WordBorder> matchedWordBorders =
        [
            .. searchUnit.WordSegments
                .Where(segment => SpansOverlap(segment.Start, segment.Length, start, length))
                .Select(segment => segment.WordBorder)
        ];

        if (searchUnit.PdfTextLine is null && matchedWordBorders.Count == 0)
            return;

        foreach (WordBorder wordBorder in matchedWordBorders)
            wordBorder.Select();

        searchUnit.PdfTextLine?.Select();
        currentSearchMatches.Add(new GrabFrameSearchMatch(text, matchedWordBorders, searchUnit.PdfTextLine));
    }

    private void ClearSearchMatchesAndSelection()
    {
        currentSearchMatches.Clear();

        foreach (WordBorder wordBorder in wordBorders)
            wordBorder.Deselect();

        foreach (PdfTextLineOverlay pdfTextLine in pdfTextLineOverlays)
            pdfTextLine.Deselect();
    }

    private List<GrabFrameSearchUnit> GetSearchUnits()
    {
        List<GrabFrameSearchUnit> searchUnits = [];
        bool isRightToLeft = CurrentLanguage.IsRightToLeft();

        foreach (List<WordBorder> lineWords in GroupWordBordersIntoSearchLines())
        {
            (string lineText, IReadOnlyList<(int SourceIndex, int Start, int Length)> textSegments) =
                BuildSearchText(
                    [.. lineWords.Select(wordBorder => (wordBorder.Word, wordBorder.Left))],
                    isSpaceJoining,
                    isRightToLeft);

            List<(WordBorder WordBorder, int Start, int Length)> segments =
            [
                .. textSegments.Select(segment => (
                    lineWords[segment.SourceIndex],
                    segment.Start,
                    segment.Length))
            ];

            if (lineWords.Count > 0)
            {
                searchUnits.Add(new GrabFrameSearchUnit(
                    lineText,
                    segments,
                    null,
                    lineWords.Min(wordBorder => wordBorder.Top),
                    lineWords.Min(wordBorder => wordBorder.Left)));
            }
        }

        searchUnits.AddRange(pdfTextLineOverlays.Select(pdfTextLine => new GrabFrameSearchUnit(
            pdfTextLine.Text,
            [],
            pdfTextLine,
            pdfTextLine.Top,
            pdfTextLine.Left)));

        return [.. searchUnits.OrderBy(unit => unit.Top).ThenBy(unit => unit.Left)];
    }

    private List<List<WordBorder>> GroupWordBordersIntoSearchLines()
    {
        List<List<WordBorder>> lines = [];

        foreach (WordBorder wordBorder in wordBorders.OrderBy(wordBorder => wordBorder.Top).ThenBy(wordBorder => wordBorder.Left))
        {
            List<WordBorder>? matchingLine = lines.LastOrDefault(line =>
                AreOnSameSearchLine(
                    line[0].LineNumber,
                    line.Average(item => item.Top),
                    line.Max(item => item.Height),
                    wordBorder.LineNumber,
                    wordBorder.Top,
                    wordBorder.Height));

            if (matchingLine is null)
                lines.Add([wordBorder]);
            else
                matchingLine.Add(wordBorder);
        }

        return lines;
    }

    internal static (string Text, IReadOnlyList<(int SourceIndex, int Start, int Length)> Segments) BuildSearchText(
        IReadOnlyList<(string Text, double Left)> sourceItems,
        bool isSpaceJoining,
        bool isRightToLeft)
    {
        IEnumerable<(string Text, double Left, int SourceIndex)> orderedItems = sourceItems
            .Select((item, index) => (item.Text, item.Left, SourceIndex: index));
        orderedItems = isRightToLeft
            ? orderedItems.OrderByDescending(item => item.Left)
            : orderedItems.OrderBy(item => item.Left);

        string separator = isSpaceJoining ? " " : string.Empty;
        StringBuilder text = new();
        List<(int SourceIndex, int Start, int Length)> segments = [];

        foreach ((string itemText, double _, int sourceIndex) in orderedItems)
        {
            if (text.Length > 0)
                text.Append(separator);

            int start = text.Length;
            text.Append(itemText);
            segments.Add((sourceIndex, start, itemText.Length));
        }

        return (text.ToString(), segments);
    }

    internal static bool AreOnSameSearchLine(
        int firstLineNumber,
        double firstTop,
        double firstHeight,
        int secondLineNumber,
        double secondTop,
        double secondHeight)
    {
        if (firstLineNumber != secondLineNumber)
            return false;

        double firstCenter = firstTop + (firstHeight / 2);
        double secondCenter = secondTop + (secondHeight / 2);
        return Math.Abs(firstCenter - secondCenter) <= Math.Max(firstHeight, secondHeight) * 0.6;
    }

    internal static bool SpansOverlap(int firstStart, int firstLength, int secondStart, int secondLength)
    {
        return firstLength > 0
            && secondLength > 0
            && firstStart < secondStart + secondLength
            && secondStart < firstStart + firstLength;
    }

    private void ResetGrabFrame()
    {
        CancelTablePlacement();
        RemoveTableLines();
        AnalyzedResultTable = null;
        SetRefreshOrOcrFrameBtnVis();

        MainZoomBorder.Reset();
        RectanglesCanvas.RenderTransform = Transform.Identity;
        RectanglesCanvas.ClearValue(WidthProperty);
        RectanglesCanvas.ClearValue(HeightProperty);
        TablePlacementOverlayCanvas.ClearValue(WidthProperty);
        TablePlacementOverlayCanvas.ClearValue(HeightProperty);
        TemplateRegionOverlayCanvas.ClearValue(WidthProperty);
        TemplateRegionOverlayCanvas.ClearValue(HeightProperty);
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

    private void SearchBar_SearchChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded)
            return;

        isSearchSelectionOverridden = false;
        reSearchTimer.Stop();
        reSearchTimer.Start();
    }

    private void SelectAllWordBorders(object? sender = null, RoutedEventArgs? e = null)
    {
        foreach (WordBorder wordBorder in wordBorders)
            wordBorder.Select();

        foreach (PdfTextLineOverlay pdfTextLine in pdfTextLineOverlays)
            pdfTextLine.Select();

        UpdateFrameText();
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
        bool showRefreshTool = !IsBottomBarToolHidden("Refresh");

        if (AutoOcrCheckBox.IsChecked is false)
        {
            OcrFrameBTN.Visibility = showRefreshTool ? Visibility.Visible : Visibility.Collapsed;
            if (showRefreshTool)
                OcrFrameBTN.Focus();
            RefreshBTN.Visibility = Visibility.Collapsed;
        }
        else
        {
            OcrFrameBTN.Visibility = Visibility.Collapsed;
            RefreshBTN.Visibility = showRefreshTool ? Visibility.Visible : Visibility.Collapsed;
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
        bool show = TemplateSavePanel.Visibility != Visibility.Visible;
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

    private async void SaveTemplateSave_Click(object sender, RoutedEventArgs e)
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

        // Parse recognizer references from the output template
        List<TemplateRecognizerMatch> recognizerMatches = GrabTemplateExecutor.ParseRecognizerMatchesFromOutputTemplate(outputTemplateText);

        if (wordBorders.Count == 0 && patternMatches.Count == 0 && recognizerMatches.Count == 0)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "No Regions or Patterns",
                Content = "Use Ctrl+drag to draw at least one region, or add a pattern placeholder, before saving.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
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
            RecognizerMatches = recognizerMatches,
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

        await new Wpf.Ui.Controls.MessageBox
        {
            Title = "Template Saved",
            Content = $"Template \"{name}\" saved with {itemsDesc}.\n\nEnable it in Post-Grab Actions Settings to use it during a Fullscreen Grab.",
            CloseButtonText = "OK"
        }.ShowDialogAsync();
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
        TemplateSavePanel.Visibility = Visibility.Collapsed;
        UpdateTemplateBadges();
    }

    private void TemplateContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        menu.Items.Clear();

        MenuItem createItem = new() { Header = "Create new Grab Template..." };
        createItem.Click += (_, _) =>
        {
            bool show = TemplateSavePanel.Visibility != Visibility.Visible;
            TemplateSavePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                if (!IsFreezeMode) { FreezeToggleButton.IsChecked = true; FreezeGrabFrame(); }
                TemplateNameBox.Focus();
            }
            UpdateTemplateBadges();
        };
        menu.Items.Add(createItem);

        List<GrabTemplate> templates = GrabTemplateManager.GetAllTemplates();
        if (templates.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (GrabTemplate template in templates)
            {
                MenuItem item = new()
                {
                    Header = template.Name,
                    IsCheckable = true,
                    IsChecked = _activeGrabTemplate?.Id == template.Id,
                    StaysOpenOnClick = false,
                    Tag = template.Id,
                };
                item.Click += TemplateMenuItem_Click;
                menu.Items.Add(item);
            }
        }
    }

    private void TemplateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string templateId) return;

        if (_activeGrabTemplate?.Id == templateId)
        {
            _activeGrabTemplate = null;
        }
        else
        {
            _activeGrabTemplate = GrabTemplateManager.GetTemplateById(templateId);
        }

        UpdateTemplateButtonHighlight();
        UpdateTemplateRegionOverlay();
    }

    private void UpdateTemplateButtonHighlight()
    {
        TemplateMenuButton.Background = _activeGrabTemplate is not null
            ? (System.Windows.Media.Brush)FindResource("DarkTeal")
            : System.Windows.Media.Brushes.Transparent;
    }

    private void UpdateTemplateRegionOverlay()
    {
        TemplateRegionOverlayCanvas.Children.Clear();

        if (_activeGrabTemplate is null || _activeGrabTemplate.Regions.Count == 0)
            return;

        double canvasWidth = RectanglesCanvas.ActualWidth;
        double canvasHeight = RectanglesCanvas.ActualHeight;
        if (canvasWidth < 4 || canvasHeight < 4)
            return;

        HashSet<int> referencedRegions = [.. _activeGrabTemplate.GetReferencedRegionNumbers()];
        if (referencedRegions.Count == 0 && _activeGrabTemplate.PatternMatches.Count > 0)
            return;

        System.Windows.Media.Color borderColor = System.Windows.Media.Color.FromArgb(220, 255, 180, 0);
        System.Windows.Media.Color dimBorderColor = System.Windows.Media.Color.FromArgb(80, 255, 180, 0);

        foreach (TemplateRegion region in _activeGrabTemplate.Regions)
        {
            double regionLeft = region.RatioLeft * canvasWidth;
            double regionTop = region.RatioTop * canvasHeight;
            double regionWidth = region.RatioWidth * canvasWidth;
            double regionHeight = region.RatioHeight * canvasHeight;

            if (regionWidth < 1 || regionHeight < 1) continue;

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
            TemplateRegionOverlayCanvas.Children.Add(regionBorder);
        }
    }

    private void UpdateTemplateBadges()
    {
        bool isTemplateMode = TemplateSavePanel.Visibility == Visibility.Visible;

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
        if (TemplateSavePanel.Visibility != Visibility.Visible)
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

        // Pattern items — saved regexes and built-in recognizers as one "Patterns" concept,
        // split into "Saved Patterns" / "Smart Patterns" subsections.
        items.AddRange(PatternItemCatalog.GetAll().Select(TextOnlyTemplateDialog.InlinePickerItemFor));

        TemplateOutputBox.ItemsSource = items;

        // Wire up the pattern / recognizer selection callbacks
        TemplateOutputBox.PatternItemSelected ??= OnPatternItemSelected;
        TemplateOutputBox.RecognizerItemSelected ??= OnRecognizerItemSelected;
    }

    private TemplateRecognizerMatch? OnRecognizerItemSelected(InlinePickerItem item)
    {
        BuiltInRecognizer? recognizer = BuiltInRecognizer.GetByName(item.DisplayName);

        PatternMatchModeDialog dialog = new(recognizer?.Id ?? string.Empty, item.DisplayName, isRecognizer: true)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() is not true || dialog.Result is null)
            return null;

        return new TemplateRecognizerMatch(
            recognizerId: recognizer?.Id ?? string.Empty,
            recognizerName: item.DisplayName,
            matchMode: dialog.Result.MatchMode,
            separator: dialog.Result.Separator,
            outputKind: dialog.SelectedOutputKind);
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

    private async void TableToggleButton_Click(object? sender = null, RoutedEventArgs? e = null)
    {
        CancelTablePlacement();
        RemoveTableLines();

        if (ShouldRefreshOcrBordersForTableModeActivation())
        {
            await DrawRectanglesAroundWords(SearchBar.SearchText);
            UpdateFrameText();
            return;
        }

        UpdateFrameText();
    }

    private async Task TryLoadDocumentFromPath(string path)
    {
        if (IoUtilities.IsPdfFileExtension(Path.GetExtension(path)))
        {
            await TryLoadPdfFromPath(path);
            return;
        }

        await TryLoadImageFromPath(path);
    }

    private async Task TryLoadImageFromPath(string path)
    {
        Uri fileURI = new(path);
        try
        {
            ClearLoadedPdfDocument();
            ClearLoadedVisualDocumentState();
            CancelTablePlacement(clearManualSeparators: true);
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
            MarkLoadedVisualDocumentOpened();
            frozenUiAutomationSnapshot = null;
            liveUiAutomationSnapshot = null;
            _currentImagePath = path;
            FreezeToggleButton.IsChecked = true;
            FreezeGrabFrame();
            EnsureMinimumLoadedDocumentWindowSize();
            FreezeToggleButton.Visibility = Visibility.Collapsed;
            SwitchToOcrFallbackIfUiAutomation();

            reDrawTimer.Start();
        }
        catch (Exception)
        {
            ClearLoadedVisualDocumentState();
            hasLoadedImageSource = false;
            UnfreezeGrabFrame();
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Text Grab",
                Content = "Not an image",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
    }

    private async Task TryLoadPdfFromPath(string path)
    {
        try
        {
            ClearLoadedPdfDocument();
            ClearLoadedVisualDocumentState();
            _loadedPdfDocument = await PdfDocumentRenderer.LoadAsync(path);
            MarkLoadedVisualDocumentOpened();
            _currentImagePath = Path.GetFullPath(path);
            int pageIndex = Math.Min(_initialPdfPageIndex, Math.Max(0, _loadedPdfDocument.PageCount - 1));
            _initialPdfPageIndex = 0;
            await ShowPdfPageAsync(pageIndex);
        }
        catch (Exception ex)
        {
            ClearLoadedPdfDocument();
            ClearLoadedVisualDocumentState();
            hasLoadedImageSource = false;
            UnfreezeGrabFrame();
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Text Grab",
                Content = $"Failed to open PDF.{Environment.NewLine}{ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
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

    private List<WordBorderInfo> TryToPlaceTable()
    {
        RemoveTableLines();

        List<WordBorderInfo> wbInfos = [.. wordBorders.Select(wb => WordBorderInfoFactory.Create(wb))];
        if (wbInfos.Count == 0)
        {
            AnalyzedResultTable = null;
            tableEditState.SetManualSeparators(tableEditState.ManualRowSeparators, tableEditState.ManualColumnSeparators);
            return wbInfos;
        }

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
            AnalyzedResultTable.AnalyzeAsTable(
                wbInfos,
                rectCanvasSize,
                tableEditState.ManualRowSeparators,
                tableEditState.ManualColumnSeparators);
            tableEditState.SetManualSeparators(
                AnalyzedResultTable.ManualRowSeparators,
                AnalyzedResultTable.ManualColumnSeparators);
            RectanglesCanvas.Children.Add(ResultTableRenderer.BuildTableLines(AnalyzedResultTable));
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }

        return wbInfos;
    }

    private void TryToReadBarcodes(DpiScale dpi)
    {
        if (DefaultSettings.GrabFrameReadBarcodes is false)
        {
            Debug.WriteLine("TryToReadBarcodes: GrabFrameReadBarcodes is disabled, returning early");
            return;
        }

        System.Drawing.Bitmap? bitmapOfGrabFrame = null;

        if (frameContentImageSource is BitmapSource frameBitmapSource)
        {
            Debug.WriteLine("reuse frameBitmapSource");
            bitmapOfGrabFrame = ImageMethods.BitmapSourceToBitmap(frameBitmapSource);
        }
        else
        {
            Debug.WriteLine("Could not reuse frameBitmapSource");
            bitmapOfGrabFrame = ImageMethods.GetWindowsBoundsBitmap(this);
        }

        Debug.WriteLine($"TryToReadBarcodes: bitmap size = {bitmapOfGrabFrame.Width}x{bitmapOfGrabFrame.Height}, dpi = {dpi.DpiScaleX}x{dpi.DpiScaleY}");

        BarcodeReader barcodeReader = new()
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions { TryHarder = true }
        };

        Result[]? results = barcodeReader.DecodeMultiple(bitmapOfGrabFrame);

        if (results is null)
        {
            Debug.WriteLine("TryToReadBarcodes: DecodeMultiple returned null (no barcodes found)");
            return;
        }

        Debug.WriteLine($"TryToReadBarcodes: DecodeMultiple found {results.Length} result(s)");

        foreach (Result result in results)
        {
            if (result?.Text is null)
            {
                Debug.WriteLine("TryToReadBarcodes: skipping result with null Text");
                continue;
            }

            Debug.WriteLine($"TryToReadBarcodes: result format={result.BarcodeFormat}, text=\"{result.Text}\"");

            ResultPoint[] rawPoints = result.ResultPoints;

            if (rawPoints is null || rawPoints.Length == 0)
            {
                Debug.WriteLine("TryToReadBarcodes: rawPoints is null or empty, skipping");
                continue;
            }

            Debug.WriteLine($"TryToReadBarcodes: rawPoints count={rawPoints.Length}, null count={rawPoints.Count(p => p is null)}");
            for (int i = 0; i < rawPoints.Length; i++)
                Debug.WriteLine($"  rawPoints[{i}] = {(rawPoints[i] is null ? "null" : $"({rawPoints[i].X:F1}, {rawPoints[i].Y:F1})")}");

            ResultPoint[] validPoints = [.. rawPoints.Where(p => p is not null).Reverse().Take(4)];
            float[] xs = [.. validPoints.Select(x => x.X)];
            float[] ys = [.. validPoints.Select(x => x.Y)];

            if (xs.Length == 0)
            {
                Debug.WriteLine("TryToReadBarcodes: no valid points after filtering, skipping");
                continue;
            }

            Point minPoint = new(xs.Min(), ys.Min());
            Point maxPoint = new(xs.Max(), ys.Max());
            Point diffs = new(maxPoint.X - minPoint.X, maxPoint.Y - minPoint.Y);

            Debug.WriteLine($"TryToReadBarcodes: minPoint=({minPoint.X:F1},{minPoint.Y:F1}), maxPoint=({maxPoint.X:F1},{maxPoint.Y:F1}), diffs=({diffs.X:F1},{diffs.Y:F1})");

            if (diffs.Y < 5)
            {
                Debug.WriteLine($"TryToReadBarcodes: diffs.Y < 5, adjusting diffs.Y from {diffs.Y:F1} to {diffs.X / 10:F1}");
                diffs.Y = diffs.X / 10;
            }

            WordBorder wb = new()
            {
                Word = result.Text,
                Width = diffs.X / dpi.DpiScaleX + 12,
                Height = diffs.Y / dpi.DpiScaleY + 12,
                Left = minPoint.X / (dpi.DpiScaleX) - 6,
                Top = minPoint.Y / (dpi.DpiScaleY) - 6,
                OwnerGrabFrame = this
            };
            Debug.WriteLine($"TryToReadBarcodes: WordBorder Left={wb.Left:F1}, Top={wb.Top:F1}, Width={wb.Width:F1}, Height={wb.Height:F1}");
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
    }

    private void UndoExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        UndoRedo.Undo();
    }

    private void UnfreezeGrabFrame()
    {
        if (IsPdfDocumentLoaded)
            return;

        _freezeTransitionVersion++;
        Opacity = 1;
        reDrawTimer.Stop();
        ClearLoadedPdfDocument();
        ClearLoadedVisualDocumentState();
        hasLoadedImageSource = false;
        isStaticImageSource = false;
        frozenUiAutomationSnapshot = null;
        liveUiAutomationSnapshot = null;
        ResetGrabFrame();
        Topmost = true;
        GrabFrameImage.Opacity = 0;
        DisposePreviousFrameContent();
        frameContentImageSource = null;
        historyItem = null;
        RectanglesBorder.Background.Opacity = overlayOpacity;
        FreezeToggleButton.IsChecked = false;
        SetToolButtonVisibility(FreezeToggleButton, "Freeze", true);
        Background = new SolidColorBrush(Colors.Transparent);
        IsFreezeMode = false;
        UpdateZoomPanMode();

        if (scrollBehavior == ScrollBehavior.ZoomWhenFrozen)
            MainZoomBorder.CanZoom = false;

        reDrawTimer.Start();
    }

    /// <summary>
    /// Unfreezes a live screen freeze after diffing the frozen snapshot against the
    /// current screen. If the content changed the frame clears and re-OCRs as usual;
    /// if it is unchanged the existing word borders (including any manual edits) are
    /// kept instead of being cleared instantly. Loaded images/PDFs and any state
    /// without a frozen screen snapshot fall back to the immediate unfreeze.
    /// </summary>
    private void UnfreezeGrabFrameWithDiff()
    {
        if (IsPdfDocumentLoaded || hasLoadedImageSource || isStaticImageSource
            || frameContentImageSource is not BitmapSource frozenSource)
        {
            UnfreezeGrabFrame();
            return;
        }

        System.Drawing.Bitmap frozenBitmap = ImageMethods.BitmapSourceToBitmap(frozenSource);
        System.Drawing.Rectangle contentRect = GetContentAreaScreenRect();
        int transitionVersion = ++_freezeTransitionVersion;

        // Hide the entire layered window so neither its tint, borders, nor controls
        // contaminate the live screen sample used for the comparison.
        reDrawTimer.Stop();
        Topmost = true;
        GrabFrameImage.Opacity = 0;
        RectanglesBorder.Background.Opacity = overlayOpacity;
        FreezeToggleButton.IsChecked = false;
        SetToolButtonVisibility(FreezeToggleButton, "Freeze", true);
        Background = new SolidColorBrush(Colors.Transparent);
        IsFreezeMode = false;
        UpdateZoomPanMode();
        Opacity = 0;

        if (scrollBehavior == ScrollBehavior.ZoomWhenFrozen)
            MainZoomBorder.CanZoom = false;

        _ = FinishUnfreezeWithDiffAsync(frozenBitmap, contentRect, transitionVersion);
    }

    private async Task FinishUnfreezeWithDiffAsync(
        System.Drawing.Bitmap frozenBitmap,
        System.Drawing.Rectangle contentRect,
        int transitionVersion)
    {
        bool contentChanged = true;

        try
        {
            // Let the compositor present the fully hidden window before sampling.
            await Task.Delay(120);

            if (!ShouldApplyUnfreezeResult(
                transitionVersion,
                _freezeTransitionVersion,
                IsFreezeMode,
                _isCleanedUp))
            {
                return;
            }

            if (contentRect.Width > 1 && contentRect.Height > 1)
            {
                using System.Drawing.Bitmap liveCapture =
                    ImageMethods.GetRegionOfScreenAsBitmap(contentRect, cacheResult: false);
                contentChanged = ImageChangeDetector.ImagesDifferBeyondThreshold(frozenBitmap, liveCapture);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unfreeze diff failed: {ex.Message}");
            contentChanged = true;
        }
        finally
        {
            frozenBitmap.Dispose();
        }

        if (!ShouldApplyUnfreezeResult(
            transitionVersion,
            _freezeTransitionVersion,
            IsFreezeMode,
            _isCleanedUp))
        {
            return;
        }

        Opacity = 1;

        if (contentChanged)
        {
            // Screen moved on: drop the stale borders and re-OCR the live content.
            frozenUiAutomationSnapshot = null;
            liveUiAutomationSnapshot = null;
            DisposePreviousFrameContent();
            frameContentImageSource = null;
            historyItem = null;
            ResetGrabFrame();
            reDrawTimer.Stop();
            reDrawTimer.Start();
        }
        else
        {
            // Live content still matches the frozen snapshot, so the existing word
            // borders (including manual corrections) remain valid. Keep them and let
            // the content-change watcher re-OCR only once the screen actually changes.
            contentChangeDetector.Reset();
            ShowFrameMessage("Frame unchanged — keeping recognized words.");
        }
    }

    internal static bool ShouldApplyUnfreezeResult(
        int transitionVersion,
        int currentTransitionVersion,
        bool isFreezeMode,
        bool isCleanedUp)
        => transitionVersion == currentTransitionVersion && !isFreezeMode && !isCleanedUp;

    private async void PreviousPdfPageButton_Click(object sender, RoutedEventArgs e)
    {
        await ChangePdfPageAsync(-1);
    }

    private async void NextPdfPageButton_Click(object sender, RoutedEventArgs e)
    {
        await ChangePdfPageAsync(1);
    }

    private void AppendPositionedTextLines(
        StringBuilder stringBuilder,
        IEnumerable<(double Top, double Left, double Height, string Text, bool AllowParagraphJoin)> lines)
    {
        List<(double Top, double Left, double Height, string Text, bool AllowParagraphJoin)> orderedLines =
            [.. lines
                .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                .OrderBy(line => line.Top)
                .ThenBy(line => line.Left)];

        if (orderedLines.Count == 0)
            return;

        stringBuilder.Append(orderedLines[0].Text);
        for (int i = 1; i < orderedLines.Count; i++)
        {
            (double Top, double Left, double Height, string Text, bool AllowParagraphJoin) previousLine = orderedLines[i - 1];
            (double Top, double Left, double Height, string Text, bool AllowParagraphJoin) currentLine = orderedLines[i];

            bool shouldJoinParagraph =
                IsParagraphDetectionActive()
                && previousLine.AllowParagraphJoin
                && currentLine.AllowParagraphJoin
                && OcrUtilities.IsWrappedParagraph(previousLine.Top, previousLine.Height, currentLine.Top, currentLine.Height);

            if (shouldJoinParagraph)
                stringBuilder.Append(' ');
            else
                stringBuilder.AppendLine();

            stringBuilder.Append(currentLine.Text);
        }
    }

    internal static bool ShouldUpdateLinkedDestinationText(
        bool isFromEditWindow,
        bool hasDestinationTextBox,
        bool shouldAlwaysUpdateEtw,
        bool isEditTextToggleEnabled,
        bool hasActiveGrabTemplate,
        bool preserveLinkedSpreadsheetSelection,
        bool isDestinationSpreadsheetMode)
    {
        return isFromEditWindow
            && hasDestinationTextBox
            && shouldAlwaysUpdateEtw
            && isEditTextToggleEnabled
            && !hasActiveGrabTemplate
            && !(preserveLinkedSpreadsheetSelection && isDestinationSpreadsheetMode);
    }

    private bool IsLinkedEditTextWindowInSpreadsheetMode()
    {
        return destinationTextBox is not null
            && Window.GetWindow(destinationTextBox) is EditTextWindow { IsSpreadsheetMode: true };
    }

    private void UpdateFrameText(bool preserveLinkedSpreadsheetSelection = false)
    {
        // Nearly every overlay mutation (selection, edits, merges, moves,
        // deletes, table changes) funnels through here, and each repaints the
        // word borders; rebase the change detector so those repaints are not
        // judged as screen-content changes.
        contentChangeDetector.Reset();

        StringBuilder stringBuilder = new();
        List<(double Top, double Left, double Height, string Text, bool AllowParagraphJoin)> selectedLines =
            [.. wordBorders
                .Where(w => w.IsSelected)
                .Select(w => (w.Top, w.Left, w.Height, w.Word, AllowParagraphJoin: false))
                .Concat(pdfTextLineOverlays
                    .Where(line => line.IsSelected)
                    .Select(line => (line.Top, line.Left, line.Height, line.Text, AllowParagraphJoin: true)))];

        if (TableToggleButton.IsChecked is true && wordBorders.Count > 0)
        {
            List<WordBorderInfo> infos = TryToPlaceTable();
            ResultTable.GetTextFromTabledWordBorders(stringBuilder, infos, isSpaceJoining);
        }
        else
        {
            if (selectedLines.Count > 0)
                AppendPositionedTextLines(stringBuilder, selectedLines);
            else if (pdfTextLineOverlays.Count > 0)
                AppendPositionedTextLines(
                    stringBuilder,
                    wordBorders
                        .Select(w => (w.Top, w.Left, w.Height, w.Word, AllowParagraphJoin: false))
                        .Concat(pdfTextLineOverlays.Select(line => (line.Top, line.Left, line.Height, line.Text, AllowParagraphJoin: true))));
            else
                AppendWordBordersForMode(stringBuilder);
        }

        FrameText = stringBuilder.ToString();

        // Speak only when this update follows a fresh grab/re-OCR, speaking is
        // enabled via the toolbar toggle, and the text actually changed.
        if (_speakOnNextFrameTextUpdate)
        {
            _speakOnNextFrameTextUpdate = false;

            if (isSpeakEnabled
                && FrameText != _lastSpokenFrameText
                && !string.IsNullOrWhiteSpace(FrameText))
            {
                _lastSpokenFrameText = FrameText;
                Singleton<TtsService>.Instance.Speak(FrameText);
            }
        }

        if (destinationTextBox is not null
            && ShouldUpdateLinkedDestinationText(
                IsFromEditWindow,
                hasDestinationTextBox: true,
                AlwaysUpdateEtwCheckBox.IsChecked is true,
                EditTextToggleButton.IsChecked is true,
                _activeGrabTemplate is not null,
                preserveLinkedSpreadsheetSelection,
                IsLinkedEditTextWindowInSpreadsheetMode()))
        {
            destinationTextBox.SelectedText = FrameText;
        }

        UpdateTableEditingUiState();
        UpdateTemplateRegionOverlay();
    }

    private void AppendWordBordersForMode(StringBuilder sb)
    {
        List<WordBorder> sorted = [.. wordBorders.OrderBy(w => w.Top).ThenBy(w => w.Left)];
        if (sorted.Count == 0)
            return;

        switch (wordGroupingMode)
        {
            case GrabFrameWordGroupingMode.Word:
                // Group by LineNumber; join words on the same line with a space,
                // and separate lines with a newline.
                IOrderedEnumerable<IGrouping<int, WordBorder>> lineGroups = sorted
                    .GroupBy(w => w.LineNumber)
                    .OrderBy(g => g.Min(w => w.Top));
                bool firstLine = true;
                foreach (IGrouping<int, WordBorder>? lineGroup in lineGroups)
                {
                    if (!firstLine)
                        sb.AppendLine();
                    firstLine = false;
                    sb.Append(string.Join(" ", lineGroup.OrderBy(w => w.Left).Select(w => w.Word)));
                }
                break;

            case GrabFrameWordGroupingMode.Window:
                // Single WordBorder — its Word already contains the full text.
                sb.Append(sorted[0].Word);
                break;

            case GrabFrameWordGroupingMode.Paragraph:
                sb.Append(sorted[0].Word);
                for (int i = 1; i < sorted.Count; i++)
                {
                    WordBorder prev = sorted[i - 1];
                    WordBorder curr = sorted[i];
                    if (IsParagraphDetectionActive()
                        && OcrUtilities.IsWrappedParagraph(prev.Top, prev.Height, curr.Top, curr.Height))
                        sb.Append(' ');
                    else
                        sb.AppendLine();
                    sb.Append(curr.Word);
                }
                break;

            default: // Line
                sb.Append(sorted[0].Word);
                for (int i = 1; i < sorted.Count; i++)
                {
                    sb.AppendLine();
                    sb.Append(sorted[i].Word);
                }
                break;
        }
    }

    private bool IsParagraphDetectionActive()
    {
        return wordGroupingMode == GrabFrameWordGroupingMode.Paragraph
            && OcrUtilities.ShouldUseParagraphDetection(isSpaceJoining, TableToggleButton.IsChecked is true);
    }

    internal static bool ShouldAllowWordBorderMerging(int selectedWordBorderCount)
    {
        return selectedWordBorderCount > 1;
    }

    internal static bool ShouldRefreshOcrBordersForTableModeActivation(
        bool isTableModeSelected,
        ILanguage? language,
        bool paragraphDetectionEnabled,
        bool hasNativePdfText,
        bool hasMergedParagraphBorders)
    {
        return isTableModeSelected
            && language is not null
            && language is not UiAutomationLang
            && language.IsSpaceJoining()
            && paragraphDetectionEnabled
            && !hasNativePdfText
            && hasMergedParagraphBorders;
    }

    private bool ShouldRefreshOcrBordersForTableModeActivation()
    {
        return ShouldRefreshOcrBordersForTableModeActivation(
            TableToggleButton.IsChecked is true,
            CurrentLanguage,
            wordGroupingMode == GrabFrameWordGroupingMode.Paragraph,
            _currentPdfPageContent?.HasNativeText is true,
            wordBorders.Any(wb => wb.KeepSingleLineOutput));
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        SetGrabFrameUserSettings();
        CleanupGrabFrame();
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
        if (e.Key == Key.Space)
        {
            // Cancel any pending grace-period clear when Space is pressed
            _spacePanGraceTimer?.Stop();
            _spacePanGraceTimer = null;
            if (CanUseSpacePanModifier)
            {
                SetSpacePanModifierState(true);
                e.Handled = true;
                return;
            }
        }

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
        if (e.Key == Key.Space)
        {
            // Keep the pan modifier active for a short grace period after Space is released.
            // Users commonly release Space a split-second before clicking to start a pan,
            // so clearing immediately makes the gesture feel broken.
            _spacePanGraceTimer?.Stop();
            _spacePanGraceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _spacePanGraceTimer.Tick += (_, _) =>
            {
                _spacePanGraceTimer?.Stop();
                _spacePanGraceTimer = null;
                if (!Keyboard.IsKeyDown(Key.Space))
                    SetSpacePanModifierState(false);
            };
            _spacePanGraceTimer.Start();

            if (CanUseSpacePanModifier)
            {
                e.Handled = true;
                return;
            }
        }

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

    private void ResetViewMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ResetView();
    }

    private void ScaleUpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ChangeFrozenFrameScale(1);
    }

    private void ScaleDownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ChangeFrozenFrameScale(-1);
    }

    private void ShowWordBordersMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Visibility overlayVisibility = ShowWordBordersMenuItem.IsChecked is true
            ? Visibility.Visible
            : Visibility.Hidden;

        RectanglesCanvas.Visibility = overlayVisibility;
        PdfTextCanvas.Visibility = overlayVisibility;
    }

    private void OverlayOpacityMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem clicked
            || clicked.Tag is not string tagStr
            || !double.TryParse(tagStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double opacity))
            return;

        overlayOpacity = opacity;

        OverlayOpacityOffMenuItem.IsChecked = false;
        OverlayOpacityLowMenuItem.IsChecked = false;
        clicked.IsChecked = true;

        if (!IsFreezeMode)
            RectanglesBorder.Background.Opacity = overlayOpacity;
    }

    private void CanExecuteGrab(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(FrameText))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private async void GrabExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string outputText = FrameText;

        if (_activeGrabTemplate is not null)
        {
            if (isStaticImageSource && frameContentImageSource is BitmapSource bmpSrc)
            {
                using System.Drawing.Bitmap bmp = ImageMethods.BitmapSourceToBitmap(bmpSrc);
                outputText = await GrabTemplateExecutor.ExecuteTemplateOnBitmapAsync(
                    _activeGrabTemplate, bmp, CurrentLanguage);
            }
            else
            {
                System.Drawing.Rectangle screenRect = GetContentAreaScreenRect();
                Rect captureRect = new(screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height);
                outputText = await GrabTemplateExecutor.ExecuteTemplateAsync(
                    _activeGrabTemplate, captureRect, CurrentLanguage);
            }

            if (!string.IsNullOrWhiteSpace(outputText))
                GrabTemplateManager.RecordUsage(_activeGrabTemplate.Id);
        }

        if (string.IsNullOrWhiteSpace(outputText))
            return;

        if (destinationTextBox is not null)
        {
            if (_activeGrabTemplate is not null || AlwaysUpdateEtwCheckBox.IsChecked is false)
                destinationTextBox.SelectedText = outputText;

            destinationTextBox.Select(destinationTextBox.SelectionStart + destinationTextBox.SelectionLength, 0);
            destinationTextBox.AppendText(Environment.NewLine);
            UpdateFrameText();

            if (CloseOnGrabMenuItem.IsChecked)
                Close();
            return;
        }

        if (!DefaultSettings.NeverAutoUseClipboard)
            try { Clipboard.SetDataObject(outputText, true); } catch { }

        if (DefaultSettings.ShowToast)
            NotificationUtilities.ShowToast(outputText);

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

    private void SetWordGroupingMenuItems()
    {
        WordGroupingWordMenuItem.IsChecked = wordGroupingMode == GrabFrameWordGroupingMode.Word;
        WordGroupingLineMenuItem.IsChecked = wordGroupingMode == GrabFrameWordGroupingMode.Line;
        WordGroupingParagraphMenuItem.IsChecked = wordGroupingMode == GrabFrameWordGroupingMode.Paragraph;
        WordGroupingWindowMenuItem.IsChecked = wordGroupingMode == GrabFrameWordGroupingMode.Window;
    }

    private void SetScrollBehaviorMenuItems()
    {
        switch (scrollBehavior)
        {
            case ScrollBehavior.None:
                NoScrollBehaviorMenuItem.IsChecked = true;
                ResizeScrollMenuItem.IsChecked = false;
                ZoomScrollMenuItem.IsChecked = false;
                ZoomWhenFrozenScrollMenuItem.IsChecked = false;
                MainZoomBorder.CanZoom = false;
                break;
            case ScrollBehavior.Resize:
                NoScrollBehaviorMenuItem.IsChecked = false;
                ResizeScrollMenuItem.IsChecked = true;
                ZoomScrollMenuItem.IsChecked = false;
                ZoomWhenFrozenScrollMenuItem.IsChecked = false;
                MainZoomBorder.CanZoom = false;
                break;
            case ScrollBehavior.Zoom:
                NoScrollBehaviorMenuItem.IsChecked = false;
                ResizeScrollMenuItem.IsChecked = false;
                ZoomScrollMenuItem.IsChecked = true;
                ZoomWhenFrozenScrollMenuItem.IsChecked = false;
                MainZoomBorder.CanZoom = true;
                break;
            case ScrollBehavior.ZoomWhenFrozen:
                NoScrollBehaviorMenuItem.IsChecked = false;
                ResizeScrollMenuItem.IsChecked = false;
                ZoomScrollMenuItem.IsChecked = false;
                ZoomWhenFrozenScrollMenuItem.IsChecked = true;
                MainZoomBorder.CanZoom = IsFreezeMode;
                break;
            default:
                break;
        }

        if (IsPdfDocumentLoaded)
            MainZoomBorder.CanZoom = true;
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

        ImageSource? invertedSource = MagickHelpers.Invert(frameContentImageSource);
        DisposePreviousFrameContent();
        frameContentImageSource = invertedSource;
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
        ClearRenderedPdfTextLines();

        if (!IsFreezeMode)
            FreezeGrabFrame();

        if (frameContentImageSource is null)
        {
            reDrawTimer.Start();
            UndoRedo.EndTransaction();
            return;
        }

        ImageSource? contrastedSource = MagickHelpers.Contrast(frameContentImageSource);
        DisposePreviousFrameContent();
        frameContentImageSource = contrastedSource;
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
        ClearRenderedPdfTextLines();

        if (!IsFreezeMode)
            FreezeGrabFrame();

        if (frameContentImageSource is null)
        {
            reDrawTimer.Start();
            UndoRedo.EndTransaction();
            return;
        }

        ImageSource? brightenedSource = MagickHelpers.Brighten(frameContentImageSource);
        DisposePreviousFrameContent();
        frameContentImageSource = brightenedSource;
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
        ClearRenderedPdfTextLines();

        if (!IsFreezeMode)
            FreezeGrabFrame();

        if (frameContentImageSource is null)
        {
            reDrawTimer.Start();
            UndoRedo.EndTransaction();
            return;
        }

        ImageSource? darkenedSource = MagickHelpers.Darken(frameContentImageSource);
        DisposePreviousFrameContent();
        frameContentImageSource = darkenedSource;
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
        ClearRenderedPdfTextLines();

        if (!IsFreezeMode)
            FreezeGrabFrame();

        if (frameContentImageSource is null)
        {
            reDrawTimer.Start();
            UndoRedo.EndTransaction();
            return;
        }

        ImageSource? grayscaledSource = MagickHelpers.Grayscale(frameContentImageSource as BitmapSource);
        DisposePreviousFrameContent();
        frameContentImageSource = grayscaledSource;
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

    private async void WordGroupingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem
            || !Enum.TryParse(menuItem.Tag?.ToString(), out GrabFrameWordGroupingMode newMode))
            return;

        wordGroupingMode = newMode;
        DefaultSettings.GrabFrameWordGrouping = wordGroupingMode.ToString();
        DefaultSettings.Save();
        SetWordGroupingMenuItems();

        await DrawRectanglesAroundWords(SearchBar.SearchText);
        UpdateFrameText();
    }

    private async void TranslateToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (TranslateToggleButton.IsChecked is bool isChecked)
        {
            isTranslationEnabled = isChecked;
            EnableTranslationMenuItem.IsChecked = isChecked;
            DefaultSettings.GrabFrameTranslationEnabled = isChecked;
            DefaultSettings.Save();

            if (isChecked)
            {
                (bool available, string? reason) = WinAiTranslator.CheckAvailability();
                if (!available)
                {
                    await new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "Translation Not Available",
                        Content = reason ?? "Windows AI is not available on this device.",
                        CloseButtonText = "OK"
                    }.ShowDialogAsync();
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
                WinAiTranslator.ReleaseModel();
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

        if (!isTranslationEnabled || !WinAiTranslator.IsAvailable())
            return;

        await PerformTranslationAsync();
    }

    private async Task PerformTranslationAsync()
    {
        if (translationCancellationTokenSource == null || translationCancellationTokenSource.IsCancellationRequested)
            return;

        // The timer restarts on every draw / resize / OCR refresh, so a second pass can be kicked off
        // while this one is still awaiting the model. Two passes share the progress counters and the
        // streamed callbacks index into their own bordersToTranslate list, so the first run's results
        // would land on the second run's word borders. One at a time.
        if (isTranslating)
            return;

        ShowTranslationProgress();

        totalWordsToTranslate = wordBorders.Count;
        translatedWordsCount = 0;

        CancellationToken cancellationToken = translationCancellationTokenSource.Token;

        // Every word box goes through the model together. Translating them one at a time meant one
        // full on-device inference per word, which is why this used to take minutes on a busy frame
        // and produced worse wording (each word was translated with no surrounding context).
        List<WordBorder> bordersToTranslate = [];
        List<string> textsToTranslate = [];

        foreach (WordBorder wb in wordBorders)
        {
            // Store original text if not already stored
            if (!originalTexts.ContainsKey(wb))
                originalTexts[wb] = wb.Word;

            string originalText = originalTexts[wb];
            if (string.IsNullOrWhiteSpace(originalText))
                continue;

            bordersToTranslate.Add(wb);
            textsToTranslate.Add(originalText);
        }

        totalWordsToTranslate = bordersToTranslate.Count;
        UpdateTranslationProgress();

        string? failureMessage = null;

        // Set as late as possible — nothing above awaits, so no second pass can slip in before here,
        // and a throw in the setup above can't leave the flag stuck on.
        isTranslating = true;

        try
        {
            // Results stream back as the model generates them, so boxes fill in progressively.
            BatchTranslationResult result = await WinAiTranslator.TranslateBatchAsync(
                textsToTranslate,
                translationTargetLanguage,
                (index, translated) => Dispatcher.InvokeAsync(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    bordersToTranslate[index].Word = translated;
                    translatedWordsCount++;
                    UpdateTranslationProgress();
                }),
                cancellationToken);

            if (!result.Succeeded)
                failureMessage = result.Message;

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
            failureMessage = $"Translation failed: {ex.Message}";
        }
        finally
        {
            isTranslating = false;
            HideTranslationProgress();
        }

        // Turn translation back off on failure so the frame does not silently retry on every
        // redraw, and tell the user what went wrong.
        if (failureMessage is not null && !cancellationToken.IsCancellationRequested)
        {
            isTranslationEnabled = false;
            TranslateToggleButton.IsChecked = false;
            EnableTranslationMenuItem.IsChecked = false;

            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Translation Failed",
                Content = failureMessage,
                CloseButtonText = "OK"
            }.ShowDialogAsync();
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

        int completed = Math.Min(translatedWordsCount, totalWordsToTranslate);
        TranslationProgressBar.Value = (double)completed / totalWordsToTranslate * 100;
        TranslationCountText.Text = $"{completed}/{totalWordsToTranslate}";
    }

    private void GetGrabFrameTranslationSettings()
    {
        isTranslationEnabled = DefaultSettings.GrabFrameTranslationEnabled;
        translationTargetLanguage = DefaultSettings.GrabFrameTranslationLanguage;

        // Hide translation button if Windows AI is not available
        bool canUseWinAI = WinAiTranslator.IsAvailable();
        translateToolAvailable = canUseWinAI;
        SetToolButtonVisibility(TranslateToggleButton, "Translate", canUseWinAI);
        TranslationMenuItem.Visibility = canUseWinAI ? Visibility.Visible : Visibility.Collapsed;
        ShowTranslateToolMenuItem.Visibility = canUseWinAI ? Visibility.Visible : Visibility.Collapsed;

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

    private void GetGrabFrameSpeakSettings()
    {
        isSpeakEnabled = DefaultSettings.GrabFrameSpeakEnabled;
        SpeakToggleButton.IsChecked = isSpeakEnabled;

        TtsService tts = Singleton<TtsService>.Instance;
        tts.BusyChanged += OnTtsBusyChanged;
        SetSpeakingProgressVisible(tts.IsBusy);
    }

    private void SpeakToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (SpeakToggleButton.IsChecked is not bool isChecked)
            return;

        bool wasSpeakEnabled = isSpeakEnabled;
        isSpeakEnabled = isChecked;
        DefaultSettings.GrabFrameSpeakEnabled = isChecked;
        DefaultSettings.Save();

        // Turning speaking off should silence anything already queued/playing.
        if (!isChecked)
        {
            Singleton<TtsService>.Instance.Stop();
            return;
        }

        if (ShouldSpeakCurrentFrameWhenEnabled(wasSpeakEnabled, isChecked, FrameText))
        {
            _lastSpokenFrameText = FrameText;
            Singleton<TtsService>.Instance.Speak(FrameText);
        }
    }

    internal static bool ShouldSpeakCurrentFrameWhenEnabled(
        bool wasSpeakEnabled,
        bool isSpeakEnabled,
        string frameText)
        => !wasSpeakEnabled && isSpeakEnabled && !string.IsNullOrWhiteSpace(frameText);

    private void StopSpeakingButton_Click(object sender, RoutedEventArgs e)
    {
        Singleton<TtsService>.Instance.Stop();
    }

    private void OnTtsBusyChanged(bool isBusy)
    {
        // BusyChanged can fire on a background thread; marshal to the UI thread.
        Dispatcher.BeginInvoke(() => SetSpeakingProgressVisible(isBusy));
    }

    private void SetSpeakingProgressVisible(bool visible)
    {
        SpeakingProgressBorder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    [GeneratedRegex(@"\{p:([^:}]+):([^:}]+)(?::([^}]*))?\}")]
    private static partial Regex TemplatePattern();
    [GeneratedRegex(@"\{(\d+)(?::[a-z]+)?\}")]
    private static partial Regex OutputTemplateReferenced();

    #endregion Methods
}
