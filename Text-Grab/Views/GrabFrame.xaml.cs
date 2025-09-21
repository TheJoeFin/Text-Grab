﻿using Fasetto.Word;
using Microsoft.Windows.AI.Imaging;
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
    private bool IsDragOver = false;
    private bool isDrawing = false;
    private bool isLanguageBoxLoaded = false;
    private bool isMiddleDown = false;
    private bool IsOcrValid = false;
    private bool isSearchSelectionOverridden = false;
    private bool isSelecting;
    private bool isSpaceJoining = true;
    private readonly Dictionary<WordBorder, Rect> movingWordBordersDictionary = [];
    private IOcrLinesWords? ocrResultOfWindow;
    private readonly DispatcherTimer reDrawTimer = new();
    private readonly DispatcherTimer reSearchTimer = new();
    private Side resizingSide = Side.None;
    private readonly Border selectBorder = new();
    private Point startingMovingPoint;
    private readonly UndoRedo UndoRedo = new();
    private bool wasAltHeld = false;
    private double windowFrameImageScale = 1;
    private readonly ObservableCollection<WordBorder> wordBorders = [];
    private static readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private ScrollBehavior scrollBehavior = ScrollBehavior.Resize;

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

    private async Task LoadContentFromHistory(HistoryInfo history)
    {
        FrameText = history.TextContent;
        currentLanguage = history.OcrLanguage;

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

        frameContentImageSource = ImageMethods.BitmapToImageSource(bgBitmap);
        GrabFrameImage.Source = frameContentImageSource;
        FreezeGrabFrame();

        List<WordBorderInfo>? wbInfoList = null;

        if (!string.IsNullOrWhiteSpace(history.WordBorderInfoJson))
            wbInfoList = JsonSerializer.Deserialize<List<WordBorderInfo>>(history.WordBorderInfoJson);

        if (wbInfoList is not null && wbInfoList.Count > 0)
        {
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

        if (history.PositionRect != Rect.Empty)
        {
            Left = history.PositionRect.Left;
            Top = history.PositionRect.Top;
            Height = history.PositionRect.Height;
            Width = history.PositionRect.Width;

            if (history.SourceMode == TextGrabMode.Fullscreen)
            {
                int borderThickness = 2;
                int titleBarHeight = 32;
                int bottomBarHeight = 42;
                Height += (titleBarHeight + bottomBarHeight);
                Width += (2 * borderThickness);
            }
        }

        TableToggleButton.IsChecked = history.IsTable;

        UpdateFrameText();
    }

    public Rect GetImageContentRect()
    {
        // This is a WIP to try to remove the gray letterboxes on either
        // side of the image when zooming it.

        Rect imageRect = Rect.Empty;

        if (frameContentImageSource is null)
            return imageRect;

        imageRect = RectanglesCanvas.GetAbsolutePlacement(true);
        Size rectCanvasSize = RectanglesCanvas.RenderSize;
        imageRect.Width = rectCanvasSize.Width;
        imageRect.Height = rectCanvasSize.Height;

        return imageRect;
    }

    private void StandardInitialize()
    {
        InitializeComponent();
        App.SetTheme();

        LoadOcrLanguages();

        SetRestoreState();

        WindowResizer resizer = new(this);
        reDrawTimer.Interval = new(0, 0, 0, 0, 500);
        reDrawTimer.Tick += ReDrawTimer_Tick;

        reSearchTimer.Interval = new(0, 0, 0, 0, 300);
        reSearchTimer.Tick += ReSearchTimer_Tick;

        _ = UndoRedo.HasUndoOperations();
        _ = UndoRedo.HasRedoOperations();

        GetGrabFrameUserSettings();
        SetRefreshOrOcrFrameBtnVis();

        DataContext = this;
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
        get { return destinationTextBox; }
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

    public static bool CheckKey(VirtualKeyCodes code) => (GetKeyState(code) & 0xFF00) == 0xFF00;

    public HistoryInfo AsHistoryItem()
    {
        System.Drawing.Bitmap? bitmap = null;

        if (frameContentImageSource is BitmapImage image)
            bitmap = ImageMethods.BitmapImageToBitmap(image);

        List<WordBorderInfo> wbInfoList = [];

        foreach (WordBorder wb in wordBorders)
            wbInfoList.Add(new WordBorderInfo(wb));

        string wbInfoJson;
        try
        {
            wbInfoJson = JsonSerializer.Serialize(wbInfoList);
        }
        catch
        {
            wbInfoJson = string.Empty;
#if DEBUG
            throw;
#endif
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
        string ocrText = await OcrUtilities.GetTextFromAbsoluteRectAsync(rect.GetScaleSizeByFraction(viewBoxZoomFactor), language);

        if (DefaultSettings.CorrectErrors)
            ocrText = ocrText.TryFixEveryWordLetterNumberErrors();

        if (DefaultSettings.CorrectToLatin)
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

    public async Task DrawRectanglesAroundWords(string searchWord = "")
    {
        if (isDrawing || IsDragOver)
            return;

        isDrawing = true;
        IsOcrValid = true;

        if (string.IsNullOrWhiteSpace(searchWord))
            searchWord = SearchBox.Text;

        RectanglesCanvas.Children.Clear();
        wordBorders.Clear();

        Point windowPosition = this.GetAbsolutePosition();
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double canvasScale = CanvasViewBox.GetHorizontalScaleFactor();
        Point rectanglesPosition = RectanglesCanvas.TransformToAncestor(this)
                                                   .Transform(new Point(0, 0));

        if (double.IsNaN(canvasScale))
            canvasScale = 1;

        double ContentWidth = RectanglesCanvas.RenderSize.Width;
        double ContentHeight = RectanglesCanvas.RenderSize.Height;

        if (ContentWidth == 4 || ContentHeight == 2)
        {
            ContentWidth = RectanglesBorder.RenderSize.Width;
            ContentHeight = RectanglesBorder.RenderSize.Height;
            rectanglesPosition = new(-2, 32);
        }

        System.Drawing.Rectangle rectCanvasSize = new()
        {
            Width = (int)(ContentWidth * dpi.DpiScaleX * canvasScale),
            Height = (int)(ContentHeight * dpi.DpiScaleY * canvasScale),
            X = (int)((windowPosition.X + rectanglesPosition.X) * dpi.DpiScaleX),
            Y = (int)((windowPosition.Y + rectanglesPosition.Y) * dpi.DpiScaleY)
        };

        if (ocrResultOfWindow is null || ocrResultOfWindow.Lines.Length == 0)
        {
            ILanguage lang = CurrentLanguage ?? LanguageUtilities.GetCurrentInputLanguage();
            (ocrResultOfWindow, windowFrameImageScale) = await OcrUtilities.GetOcrResultFromRegionAsync(rectCanvasSize, CurrentLanguage);
        }

        if (ocrResultOfWindow is null)
            return;

        isSpaceJoining = CurrentLanguage!.IsSpaceJoining();

        System.Drawing.Bitmap? bmp = null;

        if (frameContentImageSource is BitmapSource bmpImg)
            bmp = ImageMethods.BitmapSourceToBitmap(bmpImg);

        int lineNumber = 0;
        double viewBoxZoomFactor = CanvasViewBox.GetHorizontalScaleFactor();

        foreach (IOcrLine ocrLine in ocrResultOfWindow.Lines)
        {
            StringBuilder lineText = new();
            ocrLine.GetTextFromOcrLine(isSpaceJoining, lineText);
            lineText.RemoveTrailingNewlines();

            Windows.Foundation.Rect lineRect = ocrLine.BoundingBox;

            SolidColorBrush backgroundBrush = new(Colors.Black);

            if (bmp is not null)
                backgroundBrush = GetBackgroundBrushFromBitmap(ref dpi, windowFrameImageScale, bmp, ref lineRect);

            string ocrText = lineText.ToString();

            if (DefaultSettings.CorrectErrors)
                ocrText = ocrText.TryFixEveryWordLetterNumberErrors();

            if (DefaultSettings.CorrectToLatin)
                ocrText = ocrText.ReplaceGreekOrCyrillicWithLatin();

            WordBorder wordBorderBox = new()
            {
                Width = ((lineRect.Width / (dpi.DpiScaleX * windowFrameImageScale)) + 2) / viewBoxZoomFactor,
                Height = ((lineRect.Height / (dpi.DpiScaleY * windowFrameImageScale)) + 2) / viewBoxZoomFactor,
                Top = (lineRect.Y / (dpi.DpiScaleY * windowFrameImageScale) - 1) / viewBoxZoomFactor,
                Left = (lineRect.X / (dpi.DpiScaleX * windowFrameImageScale) - 1) / viewBoxZoomFactor,
                Word = ocrText,
                OwnerGrabFrame = this,
                LineNumber = lineNumber,
                IsFromEditWindow = IsFromEditWindow,
                MatchingBackground = backgroundBrush,
            };

            if (CurrentLanguage!.IsRightToLeft())
            {
                StringBuilder sb = new(ocrText);
                sb.ReverseWordsForRightToLeft();
                sb.RemoveTrailingNewlines();
                wordBorderBox.Word = sb.ToString();
            }

            if (IsOcrValid)
            {
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

            lineNumber++;
        }

        SetRotationBasedOnOcrResult();

        if (DefaultSettings.TryToReadBarcodes)
            TryToReadBarcodes(dpi);

        if (IsWordEditMode)
            EnterEditMode();

        isDrawing = false;

        bmp?.Dispose();
        reSearchTimer.Start();
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
            frameContentImageSource = ImageMethods.GetWindowBoundsImage(this);
            GrabFrameImage.Source = frameContentImageSource;
        }

        FreezeToggleButton.IsChecked = true;
        Topmost = false;
        Background = new SolidColorBrush(Colors.DimGray);
        RectanglesBorder.Background.Opacity = 0;
        IsFreezeMode = true;
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

        int pxLeft = Math.Clamp((int)(leftFraction * bmp.Width) - 1, 0, bmp.Width - 1);
        int pxTop = Math.Clamp((int)(topFraction * bmp.Height) - 2, 0, bmp.Height - 1);
        int pxRight = Math.Clamp((int)(rightFraction * bmp.Width) + 1, 0, bmp.Width - 1);
        int pxBottom = Math.Clamp((int)(bottomFraction * bmp.Height) + 1, 0, bmp.Height - 1);
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
        if (SearchBox.IsFocused)
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
        }
    }

    private void LanguagesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isLanguageBoxLoaded || sender is not ComboBox langComboBox)
            return;

        ILanguage? pickedLang = langComboBox.SelectedItem as ILanguage;

        if (langComboBox.SelectedItem is WindowsAiLang winAiLang)
            pickedLang = winAiLang;

        if (pickedLang != null)
        {
            currentLanguage = pickedLang;
            DefaultSettings.LastUsedLang = pickedLang.LanguageTag;
            DefaultSettings.Save();
        }

        ResetGrabFrame();

        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    private void LoadOcrLanguages()
    {
        if (LanguagesComboBox.Items.Count > 0)
            return;

        IReadOnlyList<Language> possibleOCRLangs = OcrEngine.AvailableRecognizerLanguages;
        ILanguage firstLang = LanguageUtilities.GetOCRLanguage();

        foreach (Language language in possibleOCRLangs)
        {
            GlobalLang globalLang = new(language);
            LanguagesComboBox.Items.Add(globalLang);
        }

        if (WindowsAiUtilities.CanDeviceUseWinAI())
        {
            WindowsAiLang winAiLang = new();
            LanguagesComboBox.Items.Insert(0, winAiLang);
        }

        for (int i = 0; i < LanguagesComboBox.Items.Count; i++)
        {
            if (LanguagesComboBox.Items[i] is not ILanguage item)
                continue;

            if (item.LanguageTag == firstLang.LanguageTag)
            {
                LanguagesComboBox.SelectedIndex = i;
                break;
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

        FreezeToggleButton.IsChecked = true;
        FreezeGrabFrame();
        FreezeToggleButton.Visibility = Visibility.Collapsed;

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
        reDrawTimer.Stop();

        UndoRedo.StartTransaction();

        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
new GrabFrameOperationArgs()
{
    RemovingWordBorders = [.. wordBorders],
    WordBorders = wordBorders,
    GrabFrameCanvas = RectanglesCanvas
});

        ResetGrabFrame();

        await Task.Delay(200);

        frameContentImageSource = ImageMethods.GetWindowBoundsImage(this);
        GrabFrameImage.Source = frameContentImageSource;

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
                int numberOfMatchesInWord = regex.Matches(wb.Word).Count;
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
    }

    private void ResetGrabFrame()
    {
        SetRefreshOrOcrFrameBtnVis();

        MainZoomBorder.Reset();
        IsOcrValid = false;
        ocrResultOfWindow = null;
        frameContentImageSource = null;
        RectanglesCanvas.Children.Clear();
        wordBorders.Clear();
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
            System.Drawing.RotateFlipType rotateFlipType = ImageMethods.GetRotateFlipType(path);
            ImageMethods.RotateImage(droppedImage, rotateFlipType);
            droppedImage.EndInit();
            frameContentImageSource = droppedImage;
            FreezeToggleButton.IsChecked = true;
            FreezeGrabFrame();
            FreezeToggleButton.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
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

        if (IsEditingAnyWordBorders || SearchBox.IsFocused)
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

    #endregion Methods
}
