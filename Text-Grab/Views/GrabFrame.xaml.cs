using Fasetto.Word;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.UndoRedoOperations;
using Text_Grab.Utilities;
using Windows.Globalization;
using Windows.Media.Ocr;
using ZXing;
using ZXing.Windows.Compatibility;

namespace Text_Grab.Views;

/// <summary>
/// Interaction logic for PersistentWindow.xaml
/// </summary>
public partial class GrabFrame : Window
{
    private bool isDrawing = false;
    private OcrResult? ocrResultOfWindow;
    private ObservableCollection<WordBorder> wordBorders = new();
    private DispatcherTimer reSearchTimer = new();
    private DispatcherTimer reDrawTimer = new();
    private bool isSelecting;
    private bool isMiddleDown = false;
    private bool isCtrlDown = false;
    private Point clickedPoint;
    private Side resizingSide = Side.None;
    private Rect? oldSize;
    private WordBorder? movingWordBorder;
    private Point startingMovingPoint;
    private Border selectBorder = new();

    private ImageSource? frameContentImageSource;

    private ImageSource? droppedImageSource;

    private bool isSpaceJoining = true;

    private ResultTable? AnalyedResultTable;

    private UndoRedo UndoRedo = new();

    private bool CanUndo => UndoRedo.HasUndoOperations();

    private bool CanRedo => UndoRedo.HasRedoOperations();

    public bool IsFromEditWindow
    {
        get
        {
            return destinationTextBox is not null;
        }
    }

    public bool IsWordEditMode { get; set; } = true;

    public bool IsFreezeMode { get; set; } = false;

    private bool IsDragOver = false;

    private bool wasAltHeld = false;

    private bool isLanguageBoxLoaded = false;

    public static RoutedCommand PasteCommand = new();

    public string FrameText { get; private set; } = string.Empty;

    private TextBox? destinationTextBox;

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


    public GrabFrame()
    {
        InitializeComponent();

        LoadOcrLanguages();

        SetRestoreState();

        WindowResizer resizer = new(this);
        reDrawTimer.Interval = new(0, 0, 0, 0, 500);
        reDrawTimer.Tick += ReDrawTimer_Tick;
        reDrawTimer.Start();

        reSearchTimer.Interval = new(0, 0, 0, 0, 300);
        reSearchTimer.Tick += ReSearchTimer_Tick;

        RoutedCommand newCmd = new();
        _ = newCmd.InputGestures.Add(new KeyGesture(Key.Escape));
        _ = CommandBindings.Add(new CommandBinding(newCmd, Escape_Keyed));

        _ = UndoRedo.HasUndoOperations();
        _ = UndoRedo.HasRedoOperations();
    }

    public void OnUndo()
    {
        UndoRedo.Undo();
    }

    public void OnRedo()
    {
        UndoRedo.Redo();
    }

    public void GrabFrame_Loaded(object sender, RoutedEventArgs e)
    {
        this.PreviewMouseWheel += HandlePreviewMouseWheel;
        this.PreviewKeyDown += Window_PreviewKeyDown;
        this.PreviewKeyUp += Window_PreviewKeyUp;

        RoutedCommand pasteCommand = new();
        _ = pasteCommand.InputGestures.Add(new KeyGesture(Key.V, ModifierKeys.Control | ModifierKeys.Shift));
        _ = CommandBindings.Add(new CommandBinding(pasteCommand, PasteExecuted));

        CheckBottomRowButtonsVis();
    }

    public void GrabFrame_Unloaded(object sender, RoutedEventArgs e)
    {
        this.Activated -= GrabFrameWindow_Activated;
        this.Closed -= Window_Closed;
        this.Deactivated -= GrabFrameWindow_Deactivated;
        this.DragLeave -= GrabFrameWindow_DragLeave;
        this.DragOver -= GrabFrameWindow_DragOver;
        this.Loaded -= GrabFrame_Loaded;
        this.LocationChanged -= Window_LocationChanged;
        this.SizeChanged -= Window_SizeChanged;
        this.Unloaded -= GrabFrame_Unloaded;
        this.PreviewMouseWheel -= HandlePreviewMouseWheel;
        this.PreviewKeyDown -= Window_PreviewKeyDown;
        this.PreviewKeyUp -= Window_PreviewKeyUp;

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

        SearchBox.GotFocus -= SearchBox_GotFocus;
        SearchBox.TextChanged -= SearchBox_TextChanged;

        ClearBTN.Click -= ClearBTN_Click;
        ExactMatchChkBx.Click -= ExactMatchChkBx_Click;

        RefreshBTN.Click -= RefreshBTN_Click;
        FreezeToggleButton.Click -= FreezeToggleButton_Click;
        TableToggleButton.Click -= TableToggleButton_Click;
        EditToggleButton.Click -= EditToggleButton_Click;
        SettingsBTN.Click -= SettingsBTN_Click;
        EditTextToggleButton.Click -= EditTextBTN_Click;
        GrabBTN.Click -= GrabBTN_Click;
    }

    public void MergeSelectedWordBorders()
    {
        FreezeGrabFrame();

        List<WordBorder> selectedWordBorders = wordBorders.Where(w => w.IsSelected).OrderBy(o => o.Left).ToList();

        Rect bounds = new()
        {
            X = selectedWordBorders.Select(w => w.Left).Min(),
            Y = selectedWordBorders.Select(w => w.Top).Min(),
            Width = selectedWordBorders.Select(w => w.Right).Max() - selectedWordBorders.Select(w => w.Left).Min(),
            Height = selectedWordBorders.Select(w => w.Bottom).Max() - selectedWordBorders.Select(w => w.Top).Min()
        };

        UndoRedo.StartTransaction();

        var deletedWordBorders = DeleteSelectedWordBorders();
        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = deletedWordBorders,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });

        StringBuilder sb = new();
        List<string> words = new();

        foreach (WordBorder wb in selectedWordBorders)
            words.Add(wb.Word);

        sb.AppendJoin(' ', words.ToArray());

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        SolidColorBrush backgroundBrush = new(Colors.Black);
        System.Drawing.Bitmap? bmp = null;

        if (frameContentImageSource is BitmapImage bmpImg)
            bmp = ImageMethods.BitmapSourceToBitmap(bmpImg);

        Rect lineRect = new()
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
            Word = sb.ToString(),
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
    }

    public void StartWordBorderMoveResize(WordBorder wordBorder, Side sideEnum)
    {
        movingWordBorder = wordBorder;
        startingMovingPoint = new(wordBorder.Left, wordBorder.Top);
        resizingSide = sideEnum;
        oldSize = new(wordBorder.Left, wordBorder.Top, wordBorder.Width, wordBorder.Height);
    }

    public void BreakWordBorderIntoWords(WordBorder wordBorder)
    {

    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (wasAltHeld && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt))
        {
            RectanglesCanvas.Opacity = 1;
            wasAltHeld = false;
        }

        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            RectanglesCanvas.Cursor = null;
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

    private async void PasteExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        (bool success, ImageSource? clipboardImage) = ClipboardUtilities.TryGetImageFromClipboard();

        if (!success || clipboardImage is null)
            return;

        reDrawTimer.Stop();
        droppedImageSource = null;

        ResetGrabFrame();
        await Task.Delay(300);

        droppedImageSource = clipboardImage;
        FreezeToggleButton.IsChecked = true;
        FreezeGrabFrame();

        reDrawTimer.Start();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!wasAltHeld && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt))
        {
            RectanglesCanvas.Opacity = 0.1;
            wasAltHeld = true;
        }

        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            RectanglesCanvas.Cursor = Cursors.Cross;

        if (e.Key == Key.Delete)
            HandleDelete();

        if (e.Key == Key.Z && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            OnUndo();

        if (e.Key == Key.Y && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            OnRedo();
    }

    private void HandleDelete()
    {
        bool editingAnyWordBorders = wordBorders.Any(x => x.IsEditing);
        if (editingAnyWordBorders)
            return;

        UndoRedo.StartTransaction();
        var deletedWordBorders = DeleteSelectedWordBorders();
        UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.RemoveWordBorder,
            new GrabFrameOperationArgs()
            {
                RemovingWordBorders = deletedWordBorders,
                WordBorders = wordBorders,
                GrabFrameCanvas = RectanglesCanvas
            });

        UndoRedo.EndTransaction();
        UpdateFrameText();
    }

    private List<WordBorder> DeleteSelectedWordBorders()
    {
        FreezeGrabFrame();

        List<WordBorder> selectedWordBorders = wordBorders.Where(x => x.IsSelected).ToList();

        if (selectedWordBorders.Count == 0)
            return selectedWordBorders;


        foreach (var wordBorder in selectedWordBorders)
        {
            RectanglesCanvas.Children.Remove(wordBorder);
            wordBorders.Remove(wordBorder);
        }

        return selectedWordBorders;
    }

    private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Source: StackOverflow, read on Sep. 10, 2021
        // https://stackoverflow.com/a/53698638/7438031

        if (this.WindowState == WindowState.Maximized)
            return;

        e.Handled = true;
        double aspectRatio = (this.Height - 66) / (this.Width - 4);

        bool isShiftDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        bool isCtrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

        if (e.Delta > 0)
        {
            this.Width += 100;
            this.Left -= 50;

            if (!isShiftDown)
            {
                this.Height += 100 * aspectRatio;
                this.Top -= 50 * aspectRatio;
            }
        }
        else if (e.Delta < 0)
        {
            if (this.Width > 120 && this.Height > 120)
            {
                this.Width -= 100;
                this.Left += 50;

                if (!isShiftDown)
                {
                    this.Height -= 100 * aspectRatio;
                    this.Top += 50 * aspectRatio;
                }
            }
        }
    }

    private void GrabFrameWindow_Initialized(object sender, EventArgs e)
    {
        WindowUtilities.SetWindowPosition(this);
        CheckBottomRowButtonsVis();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        string windowSizeAndPosition = $"{this.Left},{this.Top},{this.Width},{this.Height}";
        Properties.Settings.Default.GrabFrameWindowSizeAndPosition = windowSizeAndPosition;
        Properties.Settings.Default.Save();

        WindowUtilities.ShouldShutDown();
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

    private async void ReDrawTimer_Tick(object? sender, EventArgs? e)
    {
        reDrawTimer.Stop();
        ResetGrabFrame();

        if (CheckKey(VirtualKeyCodes.LeftButton) || CheckKey(VirtualKeyCodes.MiddleButton))
        {
            reDrawTimer.Start();
            return;
        }

        frameContentImageSource = ImageMethods.GetWindowBoundsImage(this);
        GrabFrameImage.Source = frameContentImageSource;
        if (SearchBox.Text is string searchText)
            await DrawRectanglesAroundWords(searchText);
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GrabBTN_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FrameText))
            return;

        if (destinationTextBox is not null)
        {
            destinationTextBox.Select(destinationTextBox.SelectionStart + destinationTextBox.SelectionLength, 0);
            destinationTextBox.AppendText(Environment.NewLine);
            UpdateFrameText();
            return;
        }

        if (!Settings.Default.NeverAutoUseClipboard)
            try { Clipboard.SetDataObject(FrameText, true); } catch { }

        if (Settings.Default.ShowToast)
            NotificationUtilities.ShowToast(FrameText);
    }

    private void ResetGrabFrame()
    {
        ocrResultOfWindow = null;
        frameContentImageSource = null;
        RectanglesCanvas.Children.Clear();
        wordBorders.Clear();
        MatchesTXTBLK.Text = "Matches: 0";
        UpdateFrameText();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded || IsFreezeMode || isMiddleDown)
            return;

        ResetGrabFrame();
        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded || IsFreezeMode)
            return;

        CheckBottomRowButtonsVis();
        SetRestoreState();

        if (IsFreezeMode)
            return;

        ResetGrabFrame();
        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    public enum VirtualKeyCodes : short
    {
        LeftButton = 0x01,
        RightButton = 0x02,
        MiddleButton = 0x04
    }

    [DllImport("user32.dll")]
    private static extern short GetKeyState(VirtualKeyCodes code);

    public static bool CheckKey(VirtualKeyCodes code) => (GetKeyState(code) & 0xFF00) == 0xFF00;

    private void CheckBottomRowButtonsVis()
    {
        if (this.Width < 340)
            ButtonsStackPanel.Visibility = Visibility.Collapsed;
        else
            ButtonsStackPanel.Visibility = Visibility.Visible;

        if (this.Width < 460)
        {
            SearchBox.Visibility = Visibility.Collapsed;
            MatchesTXTBLK.Visibility = Visibility.Collapsed;
            ClearBTN.Visibility = Visibility.Collapsed;
        }
        else
        {
            SearchBox.Visibility = Visibility.Visible;
            ClearBTN.Visibility = Visibility.Visible;
        }

        if (this.Width < 580)
            LanguagesComboBox.Visibility = Visibility.Collapsed;
        else
            LanguagesComboBox.Visibility = Visibility.Visible;
    }

    private void GrabFrameWindow_Deactivated(object? sender, EventArgs e)
    {
        if (!IsWordEditMode && !IsFreezeMode)
            ResetGrabFrame();
        else
        {
            RectanglesCanvas.Opacity = 1;
            if (Keyboard.Modifiers != ModifierKeys.Alt)
                wasAltHeld = false;

            if (!IsFreezeMode)
                FreezeGrabFrame();
        }

    }

    private double windowFrameImageScale = 1;

    private async Task DrawRectanglesAroundWords(string searchWord = "")
    {
        if (isDrawing || IsDragOver)
            return;

        isDrawing = true;

        if (string.IsNullOrWhiteSpace(searchWord))
            searchWord = SearchBox.Text;

        RectanglesCanvas.Children.Clear();
        wordBorders.Clear();

        Point windowPosition = this.GetAbsolutePosition();
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        System.Drawing.Rectangle rectCanvasSize = new System.Drawing.Rectangle
        {
            Width = (int)((ActualWidth + 2) * dpi.DpiScaleX),
            Height = (int)((ActualHeight - 64) * dpi.DpiScaleY),
            X = (int)((windowPosition.X - 2) * dpi.DpiScaleX),
            Y = (int)((windowPosition.Y + 24) * dpi.DpiScaleY)
        };

        Language? currentLang = LanguagesComboBox.SelectedItem as Language;
        if (currentLang is null)
            currentLang = LanguageUtilities.GetOCRLanguage();

        if (ocrResultOfWindow is null || ocrResultOfWindow.Lines.Count == 0)
            (ocrResultOfWindow, windowFrameImageScale) = await OcrExtensions.GetOcrResultFromRegion(rectCanvasSize, currentLang);

        if (ocrResultOfWindow is null)
            return;

        isSpaceJoining = LanguageUtilities.IsLanguageSpaceJoining(currentLang);

        System.Drawing.Bitmap? bmp = null;

        if (frameContentImageSource is BitmapImage bmpImg)
            bmp = ImageMethods.BitmapSourceToBitmap(bmpImg);

        int numberOfMatches = 0;
        int lineNumber = 0;

        foreach (OcrLine ocrLine in ocrResultOfWindow.Lines)
        {
            StringBuilder lineText = new();
            ocrLine.GetTextFromOcrLine(isSpaceJoining, lineText);

            Rect lineRect = ocrLine.GetBoundingRect();

            SolidColorBrush backgroundBrush = new(Colors.Black);

            if (bmp is not null)
                backgroundBrush = GetBackgroundBrushFromBitmap(ref dpi, windowFrameImageScale, bmp, ref lineRect);

            WordBorder wordBorderBox = new()
            {
                Width = lineRect.Width / (dpi.DpiScaleX * windowFrameImageScale),
                Height = lineRect.Height / (dpi.DpiScaleY * windowFrameImageScale),
                Top = lineRect.Y,
                Left = lineRect.X,
                Word = lineText.ToString().Trim(),
                OwnerGrabFrame = this,
                LineNumber = lineNumber,
                IsFromEditWindow = IsFromEditWindow,
                MatchingBackground = backgroundBrush,
            };

            if ((bool)ExactMatchChkBx.IsChecked!)
            {
                if (lineText.ToString().Equals(searchWord, StringComparison.CurrentCulture))
                {
                    wordBorderBox.Select();
                    numberOfMatches++;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(searchWord)
                    && lineText.ToString().Contains(searchWord, StringComparison.CurrentCultureIgnoreCase))
                {
                    wordBorderBox.Select();
                    numberOfMatches++;
                }
            }
            wordBorders.Add(wordBorderBox);
            _ = RectanglesCanvas.Children.Add(wordBorderBox);
            wordBorderBox.Left = lineRect.Left / (dpi.DpiScaleX * windowFrameImageScale);
            wordBorderBox.Top = lineRect.Top / (dpi.DpiScaleY * windowFrameImageScale);

            lineNumber++;
        }

        if (ocrResultOfWindow != null && ocrResultOfWindow.TextAngle != null)
        {
            RotateTransform transform = new((double)ocrResultOfWindow.TextAngle)
            {
                CenterX = (Width - 4) / 2,
                CenterY = (Height - 60) / 2
            };
            RectanglesCanvas.RenderTransform = transform;
        }
        else
        {
            RotateTransform transform = new(0)
            {
                CenterX = (Width - 4) / 2,
                CenterY = (Height - 60) / 2
            };
            RectanglesCanvas.RenderTransform = transform;
        }

        if (Settings.Default.TryToReadBarcodes)
            TryToReadBarcodes(dpi);

        List<WordBorder> wordBordersRePlace = new();
        foreach (UIElement child in RectanglesCanvas.Children)
        {
            if (child is WordBorder wordBorder)
                wordBordersRePlace.Add(wordBorder);
        }
        RectanglesCanvas.Children.Clear();
        foreach (WordBorder wordBorder in wordBordersRePlace)
        {
            // First the Word borders are placed smaller, then table analysis occurs.
            // After table can be analyzed with the position of the word borders they are adjusted 
            wordBorder.Width += 16;
            wordBorder.Height += 4;
            double leftWB = Canvas.GetLeft(wordBorder);
            double topWB = Canvas.GetTop(wordBorder);
            wordBorder.Left = leftWB - 10;
            wordBorder.Top = topWB - 2;
            RectanglesCanvas.Children.Add(wordBorder);
        }

        if (IsWordEditMode)
            EnterEditMode();

        MatchesTXTBLK.Text = $"Matches: {numberOfMatches}";
        isDrawing = false;

        UpdateFrameText();
        bmp?.Dispose();
    }

    private void RemoveTableLines()
    {
        Canvas? tableLines = null;

        foreach (var child in RectanglesCanvas.Children)
            if (child is Canvas element && element.Tag is "TableLines")
                tableLines = element;

        RectanglesCanvas.Children.Remove(tableLines);
    }

    private void TryToPlaceTable()
    {
        RemoveTableLines();

        Point windowPosition = this.GetAbsolutePosition();
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        System.Drawing.Rectangle rectCanvasSize = new System.Drawing.Rectangle
        {
            Width = (int)((ActualWidth + 2) * dpi.DpiScaleX),
            Height = (int)((ActualHeight - 64) * dpi.DpiScaleY),
            X = (int)((windowPosition.X - 2) * dpi.DpiScaleX),
            Y = (int)((windowPosition.Y + 24) * dpi.DpiScaleY)
        };

        try
        {
            AnalyedResultTable = new();
            AnalyedResultTable.AnalyzeAsTable(wordBorders.ToList(), rectCanvasSize);
            RectanglesCanvas.Children.Add(AnalyedResultTable.TableLines);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    private SolidColorBrush GetBackgroundBrushFromBitmap(ref DpiScale dpi, double scale, System.Drawing.Bitmap bmp, ref Rect lineRect)
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

        List<System.Windows.Media.Color> MediaColorList = new()
        {
            ColorHelper.MediaColorFromDrawingColor(pxColorLeftTop),
            ColorHelper.MediaColorFromDrawingColor(pxColorRightTop),
            ColorHelper.MediaColorFromDrawingColor(pxColorRightBottom),
            ColorHelper.MediaColorFromDrawingColor(pxColorLeftBottom),
        };

        System.Windows.Media.Color? MostCommonColor = MediaColorList.GroupBy(c => c)
                                                                    .OrderBy(g => g.Count())
                                                                    .LastOrDefault()?.Key;

        backgroundBrush = ColorHelper.SolidColorBrushFromDrawingColor(pxColorLeftTop);

        if (MostCommonColor is not null)
            backgroundBrush = new SolidColorBrush(MostCommonColor.Value);

        return backgroundBrush;
    }

    private void TryToReadBarcodes(DpiScale dpi)
    {
        System.Drawing.Bitmap bitmapOfGrabFrame = ImageMethods.GetWindowsBoundsBitmap(this);

        BarcodeReader barcodeReader = new()
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions { TryHarder = true }
        };

        ZXing.Result result = barcodeReader.Decode(bitmapOfGrabFrame);

        if (result is not null)
        {
            ResultPoint[] rawPoints = result.ResultPoints;

            float[] xs = rawPoints.Reverse().Take(4).Select(x => x.X).ToArray();
            float[] ys = rawPoints.Reverse().Take(4).Select(x => x.Y).ToArray();

            Point minPoint = new Point(xs.Min(), ys.Min());
            Point maxPoint = new Point(xs.Max(), ys.Max());
            Point diffs = new Point(maxPoint.X - minPoint.X, maxPoint.Y - minPoint.Y);

            if (diffs.Y < 5)
                diffs.Y = diffs.X / 10;


            WordBorder wb = new();
            wb.Word = result.Text;
            wb.Width = diffs.X / dpi.DpiScaleX + 12;
            wb.Height = diffs.Y / dpi.DpiScaleY + 12;
            wb.SetAsBarcode();
            wordBorders.Add(wb);
            _ = RectanglesCanvas.Children.Add(wb);
            double left = minPoint.X / (dpi.DpiScaleX) - 6;
            double top = minPoint.Y / (dpi.DpiScaleY) - 6;
            Canvas.SetLeft(wb, left);
            Canvas.SetTop(wb, top);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is not TextBox searchBox) return;

        reSearchTimer.Stop();
        reSearchTimer.Start();
    }

    private void ReSearchTimer_Tick(object? sender, EventArgs e)
    {
        reSearchTimer.Stop();
        if (SearchBox.Text is not string searchText)
            return;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            foreach (WordBorder wb in wordBorders)
                wb.Deselect();
        }
        else
        {
            foreach (WordBorder wb in wordBorders)
            {
                if (!string.IsNullOrWhiteSpace(searchText)
                    && wb.Word.ToLower().Contains(searchText.ToLower()))
                    wb.Select();
                else
                    wb.Deselect();
            }
        }

        UpdateFrameText();

        MatchesTXTBLK.Visibility = Visibility.Visible;
    }

    private void UpdateFrameText()
    {
        string[] selectedWbs = wordBorders
            .OrderBy(b => b.Top)
            .Where(w => w.IsSelected)
            .Select(t => t.Word).ToArray();

        StringBuilder stringBuilder = new();

        if (TableToggleButton.IsChecked is true)
        {
            ResultTable.GetTextFromTabledWordBorders(stringBuilder, wordBorders.ToList(), isSpaceJoining);
            TryToPlaceTable();
        }
        else
        {
            if (selectedWbs.Length > 0)
                stringBuilder.AppendJoin(Environment.NewLine, selectedWbs);
            else
                stringBuilder.AppendJoin(Environment.NewLine, wordBorders.Select(w => w.Word).ToArray());
        }

        FrameText = stringBuilder.ToString();

        if (string.IsNullOrEmpty(FrameText))
            GrabBTN.IsEnabled = false;
        else
            GrabBTN.IsEnabled = true;

        if (IsFromEditWindow
            && destinationTextBox is not null
            && EditTextToggleButton.IsChecked is true)
        {
            destinationTextBox.SelectedText = FrameText;
        }
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (SearchBox is TextBox searchBox)
            searchBox.Text = "";
    }

    private void ExactMatchChkBx_Click(object sender, RoutedEventArgs e)
    {
        reSearchTimer.Stop();
        reSearchTimer.Start();
    }

    private void ClearBTN_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
    }

    private void RefreshBTN_Click(object sender, RoutedEventArgs e)
    {
        TextBox searchBox = SearchBox;
        ResetGrabFrame();

        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    private void SettingsBTN_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<SettingsWindow>();
    }

    private void GrabFrameWindow_Activated(object? sender, EventArgs e)
    {
        RectanglesCanvas.Opacity = 1;
        if (!IsWordEditMode && !IsFreezeMode)
            reDrawTimer.Start();
        else
            UpdateFrameText();
    }

    private void RectanglesCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        GrabBTN.Focus();

        if (e.RightButton == MouseButtonState.Pressed)
        {
            e.Handled = false;
            return;
        }

        isSelecting = true;
        clickedPoint = e.GetPosition(RectanglesCanvas);
        RectanglesCanvas.CaptureMouse();
        selectBorder.Height = 1;
        selectBorder.Width = 1;

        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            e.Handled = true;

            isMiddleDown = true;
            ResetGrabFrame();
            UnfreezeGrabFrame();
            return;
        }

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            isCtrlDown = true;
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
        if (!isSelecting && !isMiddleDown && movingWordBorder is null)
            return;

        Point movingPoint = e.GetPosition(RectanglesCanvas);

        var left = Math.Min(clickedPoint.X, movingPoint.X);
        var top = Math.Min(clickedPoint.Y, movingPoint.Y);

        if (isMiddleDown)
        {
            MoveWindowWithMiddleMouse(movingPoint);
            return;
        }

        if (movingWordBorder is not null)
        {
            FreezeGrabFrame();

            MoveResizeWordBorder(movingPoint, oldSize ?? new Rect());
            return;
        }

        selectBorder.Width = Math.Max(clickedPoint.X, movingPoint.X) - left;
        Canvas.SetLeft(selectBorder, left);

        if (isCtrlDown)
        {
            UpdateSelectBorderForNewWordBorder(movingPoint);
            return;
        }

        selectBorder.Height = Math.Max(clickedPoint.Y, movingPoint.Y) - top;
        Canvas.SetTop(selectBorder, top);

        CheckSelectBorderIntersections();
    }

    private void RectanglesCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        isSelecting = false;
        CursorClipper.UnClipCursor();
        RectanglesCanvas.ReleaseMouseCapture();

        if (e.ChangedButton == MouseButton.Middle)
        {
            isMiddleDown = false;
            FreezeGrabFrame();

            reDrawTimer.Stop();
            reDrawTimer.Start();
            return;
        }

        if (movingWordBorder is not null && oldSize is not null)
        {
            UndoRedo.StartTransaction();
            UndoRedo.InsertUndoRedoOperation(UndoRedoOperation.ResizeWordBorder,
                new GrabFrameOperationArgs()
                {
                    WordBorder = movingWordBorder,
                    OldSize = oldSize.Value,
                    NewSize = new(movingWordBorder.Left, movingWordBorder.Top, movingWordBorder.Width, movingWordBorder.Height)
                });
            UndoRedo.EndTransaction();
        }

        if (isCtrlDown && movingWordBorder is null
            && selectBorder.Height > 6 && selectBorder.Width > 6)
            AddNewWordBorder(selectBorder);

        try { RectanglesCanvas.Children.Remove(selectBorder); } catch { }

        movingWordBorder = null;
        oldSize = null;
        resizingSide = Side.None;
        CheckSelectBorderIntersections(true);
        isCtrlDown = false;
    }

    private void UpdateSelectBorderForNewWordBorder(Point movingPoint)
    {
        double smallestHeight = 6;
        double largestHeight = 50;

        if (wordBorders.Count > 4)
        {
            smallestHeight = wordBorders.Select(x => x.Height).Min();
            largestHeight = wordBorders.Select(x => x.Height).Max();
        }

        selectBorder.Height = Math.Clamp(movingPoint.Y - clickedPoint.Y, smallestHeight, largestHeight + 10);
        selectBorder.Height = Math.Round(selectBorder.Height / 3.0) * 3;
    }

    private void MoveResizeWordBorder(Point movingPoint, Rect prevSize)
    {
        if (movingWordBorder is null)
            return;

        double xShiftDelta = (movingPoint.X - clickedPoint.X);
        double yShiftDelta = (movingPoint.Y - clickedPoint.Y);

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
                movingWordBorder.Left = startingMovingPoint.X + (movingPoint.X - clickedPoint.X);
                movingWordBorder.Top = startingMovingPoint.Y + (movingPoint.Y - clickedPoint.Y);
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

    private async void AddNewWordBorder(Border selectBorder)
    {
        FreezeGrabFrame();

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        SolidColorBrush backgroundBrush = new(Colors.Black);
        System.Drawing.Bitmap? bmp = null;

        Language? currentLang = LanguagesComboBox.SelectedItem as Language;
        if (currentLang is null)
            currentLang = LanguageUtilities.GetOCRLanguage();

        double zoomFactor = CanvasViewBox.GetHorizontalScaleFactor();
        Rect rect = selectBorder.GetAbsolutePlacement(true);
        rect = new(rect.X + 4, rect.Y, (rect.Width * dpi.DpiScaleX) + 10, rect.Height * dpi.DpiScaleY);
        string ocrText = await OcrExtensions.GetTextFromAbsoluteRect(rect.GetScaleSizeByFraction(zoomFactor), currentLang);

        if (frameContentImageSource is BitmapImage bmpImg)
            bmp = ImageMethods.BitmapSourceToBitmap(bmpImg);

        Rect lineRect = new()
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
            Width = selectBorder.Width + 8,
            Height = selectBorder.Height - 6,
            Word = ocrText.MakeStringSingleLine(),
            OwnerGrabFrame = this,
            Top = Canvas.GetTop(selectBorder) + 3,
            Left = Canvas.GetLeft(selectBorder) - 4,
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
        UpdateFrameText();
    }

    private void CheckSelectBorderIntersections(bool finalCheck = false)
    {
        Rect rectSelect = new Rect(Canvas.GetLeft(selectBorder), Canvas.GetTop(selectBorder), selectBorder.Width, selectBorder.Height);

        bool clickedEmptySpace = true;
        bool smallSelction = false;
        if (rectSelect.Width < 10 && rectSelect.Height < 10)
            smallSelction = true;

        foreach (WordBorder wordBorder in wordBorders)
        {
            Rect wbRect = new Rect(Canvas.GetLeft(wordBorder), Canvas.GetTop(wordBorder), wordBorder.Width, wordBorder.Height);

            if (rectSelect.IntersectsWith(wbRect))
            {
                clickedEmptySpace = false;

                if (!smallSelction)
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
                    && !smallSelction)
                    wordBorder.Deselect();
            }

            if (finalCheck)
                wordBorder.WasRegionSelected = false;
        }

        if (clickedEmptySpace
            && smallSelction
            && finalCheck)
        {
            foreach (WordBorder wb in wordBorders)
                wb.Deselect();
        }

        if (finalCheck)
            UpdateFrameText();
    }

    private void TableToggleButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveTableLines();
        UpdateFrameText();
    }

    private void EditToggleButton_Click(object sender, RoutedEventArgs e)
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

    private void ExitEditMode()
    {
        IsWordEditMode = false;

        foreach (UIElement uIElement in RectanglesCanvas.Children)
        {
            if (uIElement is WordBorder wb)
                wb.ExitEdit();
        }
    }

    private void FreezeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (FreezeToggleButton.IsChecked is bool freezeMode && freezeMode)
            FreezeGrabFrame();
        else
            UnfreezeGrabFrame();
    }

    private void FreezeGrabFrame()
    {
        GrabFrameImage.Opacity = 1;
        if (droppedImageSource is not null)
            GrabFrameImage.Source = droppedImageSource;
        else if (frameContentImageSource is not null)
            GrabFrameImage.Source = frameContentImageSource;
        else
        {
            frameContentImageSource = ImageMethods.GetWindowBoundsImage(this);
            GrabFrameImage.Source = frameContentImageSource;
        }

        FreezeToggleButton.IsChecked = true;
        Topmost = false;
        this.Background = new SolidColorBrush(Colors.DimGray);
        RectanglesBorder.Background.Opacity = 0;
        IsFreezeMode = true;
    }

    private void UnfreezeGrabFrame()
    {
        reDrawTimer.Stop();
        ResetGrabFrame();
        Topmost = true;
        GrabFrameImage.Opacity = 0;
        frameContentImageSource = null;
        droppedImageSource = null;
        RectanglesBorder.Background.Opacity = 0.05;
        FreezeToggleButton.IsChecked = false;
        this.Background = new SolidColorBrush(Colors.Transparent);
        IsFreezeMode = false;
        reDrawTimer.Start();
    }

    private void EditTextBTN_Click(object sender, RoutedEventArgs e)
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

    private async void GrabFrameWindow_Drop(object sender, DragEventArgs e)
    {
        // Mark the event as handled, so TextBox's native Drop handler is not called.
        e.Handled = true;
        var fileName = IsSingleFile(e);
        if (fileName is null) return;

        Activate();
        frameContentImageSource = null;

        await TryLoadImageFromPath(fileName);

        IsDragOver = false;

        reDrawTimer.Start();
    }

    private void GrabFrameWindow_DragOver(object sender, DragEventArgs e)
    {
        IsDragOver = true;
        // As an arbitrary design decision, we only want to deal with a single file.
        e.Effects = IsSingleFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        // Mark the event as handled, so TextBox's native DragOver handler is not called.
        e.Handled = true;
    }

    // If the data object in args is a single file, this method will return the filename.
    // Otherwise, it returns null.
    private static string? IsSingleFile(DragEventArgs args)
    {
        // Check for files in the hovering data object.
        if (args.Data.GetDataPresent(DataFormats.FileDrop, true))
        {
            var fileNames = args.Data.GetData(DataFormats.FileDrop, true) as string[];
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

    private void GrabFrameWindow_DragLeave(object sender, DragEventArgs e)
    {
        IsDragOver = false;
    }

    private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void OnRestoreButtonClick(object sender, RoutedEventArgs e)
    {
        if (this.WindowState == WindowState.Maximized)
            this.WindowState = WindowState.Normal;
        else
            this.WindowState = WindowState.Maximized;

        SetRestoreState();
    }

    private void SetRestoreState()
    {
        if (WindowState == WindowState.Maximized)
            RestoreTextlock.Text = "";
        else
            RestoreTextlock.Text = "";
    }

    private void AspectRationMI_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem aspectMI)
            return;

        if (aspectMI.IsChecked is false)
            GrabFrameImage.Stretch = Stretch.Fill;
        else
            GrabFrameImage.Stretch = Stretch.Uniform;
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

        isLanguageBoxLoaded = true;
    }

    private void LanguagesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isLanguageBoxLoaded || sender is not ComboBox langComboBox)
            return;

        Language? pickedLang = langComboBox.SelectedItem as Language;

        if (pickedLang != null)
        {
            Settings.Default.LastUsedLang = pickedLang.LanguageTag;
            Settings.Default.Save();
        }

        ResetGrabFrame();

        reDrawTimer.Stop();
        reDrawTimer.Start();
    }

    private void LanguagesComboBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            Settings.Default.LastUsedLang = String.Empty;
            Settings.Default.Save();
        }
    }

    private void GrabFrameWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        FrameText = "";
        wordBorders.Clear();
        UpdateFrameText();
    }

    private async void OpenImageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create OpenFileDialog 
        Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

        // Set filter for file extension and default file extension
        dlg.Filter = GetImageFilter();

        bool? result = dlg.ShowDialog();

        if (result is false || !File.Exists(dlg.FileName))
            return;

        await TryLoadImageFromPath(dlg.FileName);

        reDrawTimer.Start();
    }

    private async Task TryLoadImageFromPath(string path)
    {
        Uri fileURI = new(path);
        try
        {
            ResetGrabFrame();
            await Task.Delay(300);
            BitmapImage droppedImage = new(fileURI);
            frameContentImageSource = droppedImage;
            FreezeToggleButton.IsChecked = true;
            FreezeGrabFrame();
        }
        catch (Exception)
        {
            UnfreezeGrabFrame();
            MessageBox.Show("Not an image");
        }
    }

    /// <summary>
    /// Get the Filter string for all supported image types.
    /// To be used in the FileDialog class Filter Property.
    /// </summary>
    /// <returns></returns>
    /// From StackOverFlow https://stackoverflow.com/a/69318375/7438031
    /// Author https://stackoverflow.com/users/9610801/paul-nakitare
    /// Accessed on 1/6/2023
    /// Modifed by Joseph Finney
    public static string GetImageFilter()
    {
        string imageExtensions = string.Empty;
        string separator = "";
        ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
        Dictionary<string, string> imageFilters = new Dictionary<string, string>();
        foreach (ImageCodecInfo codec in codecs)
        {
            if (codec.FilenameExtension is not string extension)
                continue;

            imageExtensions = $"{imageExtensions}{separator}{extension.ToLower()}";
            separator = ";";
            imageFilters.Add($"{codec.FormatDescription} files ({extension.ToLower()})", extension.ToLower());
        }
        string result = string.Empty;
        separator = "";
        //foreach (KeyValuePair<string, string> filter in imageFilters)
        //{
        //    result += $"{separator}{filter.Key}|{filter.Value}";
        //    separator = "|";
        //}
        if (!string.IsNullOrEmpty(imageExtensions))
        {
            result += $"{separator}Image files|{imageExtensions}";
        }
        return result;
    }
}
