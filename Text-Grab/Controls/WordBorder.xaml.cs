using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for WordBorder.xaml
/// </summary>
[DebuggerDisplay("{Word} : Size {Width}:{Height} Pos. {Left}:{Top} Table {ResultRowID}:{ResultColumnID}")]
public partial class WordBorder : UserControl, INotifyPropertyChanged
{
    #region Fields

    // Using a DependencyProperty as the backing store for Word.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty WordProperty =
        DependencyProperty.Register(nameof(Word), typeof(string), typeof(WordBorder), new PropertyMetadata(string.Empty, OnWordChanged));

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(WordBorder), new PropertyMetadata(string.Empty, OnDisplayTextChanged));

    public static readonly DependencyProperty KeepSingleLineOutputProperty =
        DependencyProperty.Register(nameof(KeepSingleLineOutput), typeof(bool), typeof(WordBorder), new PropertyMetadata(false, OnLayoutPropertyChanged));

    public static readonly DependencyProperty DisplayLineHeightProperty =
        DependencyProperty.Register(nameof(DisplayLineHeight), typeof(double), typeof(WordBorder), new PropertyMetadata(0d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty TemplateIndexProperty =
        DependencyProperty.Register(nameof(TemplateIndex), typeof(int), typeof(WordBorder),
            new PropertyMetadata(0, OnTemplateIndexChanged));

    private static void OnTemplateIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WordBorder wb)
        {
            wb.PropertyChanged?.Invoke(wb, new PropertyChangedEventArgs(nameof(TemplateBadgeVisibility)));
            wb.PropertyChanged?.Invoke(wb, new PropertyChangedEventArgs(nameof(TemplateBadgeText)));
        }
    }

    public static RoutedCommand MergeWordsCommand = new();
    private int contextMenuBaseSize;
    private SolidColorBrush contrastingForeground = new(Colors.White);
    private readonly DispatcherTimer debounceTimer = new();
    private bool isSyncingTextProperties;
    private double left = 0;
    private SolidColorBrush matchingBackground = new(Colors.Black);
    private double top = 0;

    #endregion Fields

    #region Constructors

    public WordBorder()
    {
        StandardInitialization();
    }

    public WordBorder(WordBorderInfo info)
    {
        StandardInitialization();

        KeepSingleLineOutput = info.KeepSingleLineOutput;
        DisplayLineHeight = info.DisplayLineHeight;
        Word = info.Word;
        DisplayText = string.IsNullOrWhiteSpace(info.DisplayText) ? info.Word : info.DisplayText;
        Left = info.BorderRect.Left;
        Top = info.BorderRect.Top;
        Width = info.BorderRect.Width;
        Height = info.BorderRect.Height;
        LineNumber = info.LineNumber;
        ResultColumnID = info.ResultColumnID;
        ResultRowID = info.ResultRowID;
        IsBarcode = info.IsBarcode;

        if (info.MatchingBackground != "Transparent"
            && new BrushConverter().ConvertFromString(info.MatchingBackground) is SolidColorBrush solidColorBrush)
        {
            MatchingBackground = solidColorBrush;
        }
    }

    private void StandardInitialization()
    {
        InitializeComponent();
        DataContext = this;

        // An empty placeholder keeps ContextMenuOpening firing; the items are
        // built on first open in EnsureContextMenuItems so the many
        // WordBorders rendered per OCR pass don't each allocate a menu tree.
        ContextMenu lazyContextMenu = new();
        WordBorderBorder.ContextMenu = lazyContextMenu;
        EditWordTextBox.ContextMenu = lazyContextMenu;

        Loaded += WordBorder_Loaded;
        SizeChanged += WordBorder_SizeChanged;

        debounceTimer.Interval = new(0, 0, 0, 0, 300);
        debounceTimer.Tick += DebounceTimer_Tick;
    }

    private static void OnDisplayTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WordBorder wb || wb.isSyncingTextProperties)
            return;

        wb.isSyncingTextProperties = true;
        wb.Word = wb.KeepSingleLineOutput
            ? (e.NewValue as string ?? string.Empty).MakeStringSingleLine()
            : e.NewValue as string ?? string.Empty;
        wb.isSyncingTextProperties = false;
        wb.PropertyChanged?.Invoke(wb, new PropertyChangedEventArgs(nameof(DisplayText)));
        wb.ApplyTextLayout();
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WordBorder wb)
            wb.ApplyTextLayout();
    }

    private static void OnWordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WordBorder wb)
            return;

        if (!wb.isSyncingTextProperties)
        {
            wb.isSyncingTextProperties = true;
            wb.DisplayText = e.NewValue as string ?? string.Empty;
            wb.isSyncingTextProperties = false;
        }

        wb.PropertyChanged?.Invoke(wb, new PropertyChangedEventArgs(nameof(Word)));
    }
    #endregion Constructors

    #region Events

    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion Events

    #region Properties

    public double Bottom => Top + Height;
    public bool IsBarcode { get; set; } = false;
    public bool IsEditing => EditWordTextBox.IsFocused;
    public bool IsFromEditWindow { get; set; } = false;
    public bool IsSelected { get; set; } = false;
    public string DisplayText
    {
        get { return (string)GetValue(DisplayTextProperty); }
        set { SetValue(DisplayTextProperty, value); }
    }

    public double DisplayLineHeight
    {
        get { return (double)GetValue(DisplayLineHeightProperty); }
        set { SetValue(DisplayLineHeightProperty, value); }
    }

    public bool KeepSingleLineOutput
    {
        get { return (bool)GetValue(KeepSingleLineOutputProperty); }
        set { SetValue(KeepSingleLineOutputProperty, value); }
    }

    public double Left
    {
        get { return left; }
        set
        {
            left = value;
            Canvas.SetLeft(this, left);
        }
    }

    public int LineNumber { get; set; } = 0;
    public SolidColorBrush MatchingBackground
    {
        get { return matchingBackground; }
        set
        {
            matchingBackground = value;
            MainGrid.Background = matchingBackground;

            byte r = matchingBackground.Color.R;  // extract red
            byte g = matchingBackground.Color.G;  // extract green
            byte b = matchingBackground.Color.B;  // extract blue

            double luma = 0.2126 * r + 0.7152 * g + 0.0722 * b; // per ITU-R BT.709

            if (luma > 180)
            {
                contrastingForeground = new SolidColorBrush(Colors.Black);
                EditWordTextBox.Foreground = contrastingForeground;
            }
        }
    }

    public GrabFrame? OwnerGrabFrame { get; set; }
    public int ResultColumnID { get; set; } = 0;
    public int ResultRowID { get; set; } = 0;
    public double Right => Left + Width;
    public double Top
    {
        get { return top; }
        set
        {
            top = value;
            Canvas.SetTop(this, top);
        }
    }

    public int TemplateIndex
    {
        get => (int)GetValue(TemplateIndexProperty);
        set => SetValue(TemplateIndexProperty, value);
    }

    public Visibility TemplateBadgeVisibility => TemplateIndex > 0 ? Visibility.Visible : Visibility.Collapsed;

    public string TemplateBadgeText => TemplateIndex > 0 ? $"{{{TemplateIndex}}}" : string.Empty;

    public bool WasRegionSelected { get; set; } = false;
    public string Word
    {
        get { return (string)GetValue(WordProperty); }
        set
        {
            SetValue(WordProperty, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Word)));
        }
    }

    #endregion Properties

    #region Methods

    public void Deselect()
    {
        IsSelected = false;
        ApplyTemplateStateBorderBrush();
    }

    private bool _isInOutputPattern = false;

    /// <summary>
    /// Highlights the border orange when this region is referenced in the output template.
    /// Call with false to restore the normal teal border color.
    /// </summary>
    public void SetHighlightedForOutput(bool isHighlighted)
    {
        _isInOutputPattern = isHighlighted;
        if (!IsSelected)
            ApplyTemplateStateBorderBrush();
    }

    private void ApplyTemplateStateBorderBrush()
    {
        SolidColorBrush brush = _isInOutputPattern
            ? new SolidColorBrush(Colors.Orange)
            : new SolidColorBrush(Color.FromRgb(48, 142, 152));
        WordBorderBorder.BorderBrush = brush;
        MoveResizeBorder.BorderBrush = brush;
    }

    public void EnterEdit()
    {
        EditWordTextBox.Visibility = Visibility.Visible;
        MainGrid.Background = matchingBackground;
    }

    public void ExitEdit()
    {
        EditWordTextBox.Visibility = Visibility.Collapsed;
        MainGrid.Background = new SolidColorBrush(matchingBackground.Color)
        {
            Opacity = 0.1
        };
    }

    public void FocusTextbox()
    {
        EditWordTextBox.Focus();
        Keyboard.Focus(EditWordTextBox);
        EditWordTextBox.SelectAll();
    }

    public bool IntersectsWith(Rect rectToCheck)
    {
        Rect wbRect = new(Left, Top, Width, Height);
        return rectToCheck.IntersectsWith(wbRect);
    }

    public void Select()
    {
        IsSelected = true;
        WordBorderBorder.BorderBrush = new SolidColorBrush(Colors.Orange);
    }

    public void SetAsBarcode()
    {
        IsBarcode = true;

        EditWordTextBox.TextWrapping = TextWrapping.Wrap;
        EditWordTextBox.TextAlignment = TextAlignment.Center;

        EditWordTextBox.Width = this.Width - 2;
        EditWordTextBox.Height = this.Height - 2;
        EditWordTextBox.FontSize = 14;

        if (Uri.TryCreate(Word, UriKind.Absolute, out Uri? uri))
            EditWordTextBox.Background = new SolidColorBrush(Colors.Blue);
    }

    private void ApplyTextLayout()
    {
        if (IsBarcode)
            return;

        if (KeepSingleLineOutput && DisplayLineHeight > 0)
        {
            EditWordTextBox.TextWrapping = TextWrapping.Wrap;
            EditWordTextBox.Width = Math.Max(Width - 2, 10);
            EditWordTextBox.Height = Math.Max(Height - 2, 14);
            EditWordTextBox.FontSize = Math.Max(1, DisplayLineHeight * 0.75);
            EditWordTextBox.SetValue(TextBlock.LineHeightProperty, Math.Max(1, DisplayLineHeight));
            EditWordTextBox.SetValue(TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight);
            return;
        }

        EditWordTextBox.TextWrapping = TextWrapping.NoWrap;
        EditWordTextBox.ClearValue(FrameworkElement.WidthProperty);
        EditWordTextBox.ClearValue(FrameworkElement.HeightProperty);
        EditWordTextBox.ClearValue(Control.FontSizeProperty);
        EditWordTextBox.ClearValue(TextBlock.LineHeightProperty);
        EditWordTextBox.ClearValue(TextBlock.LineStackingStrategyProperty);
    }

    private void BreakIntoWordsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (OwnerGrabFrame is null)
            return;

        OwnerGrabFrame.BreakWordBorderIntoWords(this);
    }

    private void CanMergeWordBorderExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (OwnerGrabFrame?.SelectedWordBorders().Count > 1)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void CopyWordMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetDataObject(Word, true); } catch { }
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        debounceTimer.Stop();
        OwnerGrabFrame?.WordChanged();
    }
    private void DeleteWordMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OwnerGrabFrame?.DeleteThisWordBorder(this);
    }

    private MenuItem NewContextMenuItem(string header, RoutedEventHandler clickHandler)
    {
        MenuItem menuItem = new()
        {
            Header = header,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        menuItem.Click += clickHandler;
        return menuItem;
    }

    private void EnsureContextMenuItems(ContextMenu contextMenu)
    {
        if (contextMenu.Items.Count > 0)
            return;

        contextMenu.Items.Add(NewContextMenuItem("Copy Text", CopyWordMenuItem_Click));
        contextMenu.Items.Add(NewContextMenuItem("Try To Make _Numbers", TryToNumberMenuItem_Click));
        contextMenu.Items.Add(NewContextMenuItem("Try To Make _Letters", TryToAlphaMenuItem_Click));
        contextMenu.Items.Add(NewContextMenuItem("Make Text _Single Line", MakeSingleLineMenuItem_Click));
        contextMenu.Items.Add(new Separator());

        MenuItem translateMenuItem = NewContextMenuItem("Translate to System Language", TranslateWordMenuItem_Click);
        translateMenuItem.Name = "TranslateWordMenuItem";
        translateMenuItem.Visibility = Visibility.Collapsed;
        contextMenu.Items.Add(translateMenuItem);
        contextMenu.Items.Add(new Separator()
        {
            Name = "TranslateSeparator",
            Visibility = Visibility.Collapsed
        });

        contextMenu.Items.Add(new MenuItem()
        {
            Header = "_Merge Selected Word Borders",
            HorizontalAlignment = HorizontalAlignment.Left,
            Command = MergeWordsCommand,
            InputGestureText = "Ctrl + M"
        });
        contextMenu.Items.Add(NewContextMenuItem("_Break into words", BreakIntoWordsMenuItem_Click));
        contextMenu.Items.Add(NewContextMenuItem("_Search for similar text", SearchForSimilarMenuItem_Click));
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(NewContextMenuItem("_Delete", DeleteWordMenuItem_Click));

        contextMenuBaseSize = contextMenu.Items.Count;
    }

    private void EditWordTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement senderElement
            || senderElement.ContextMenu is not ContextMenu textBoxContextMenu)
        {
            return;
        }

        EnsureContextMenuItems(textBoxContextMenu);

        while (textBoxContextMenu.Items.Count > contextMenuBaseSize)
        {
            textBoxContextMenu.Items.RemoveAt(contextMenuBaseSize);
        }

        // Show/hide translate menu item based on Windows AI availability
        // Find the translate menu items in the context menu
        MenuItem? translateMenuItem = null;
        Separator? translateSeparator = null;

        foreach (object item in textBoxContextMenu.Items)
        {
            if (item is MenuItem menuItem && menuItem.Name == "TranslateWordMenuItem")
                translateMenuItem = menuItem;
            else if (item is Separator separator && separator.Name == "TranslateSeparator")
                translateSeparator = separator;
        }

        if (WindowsAiUtilities.CanDeviceUseWinAI())
        {
            if (translateMenuItem != null)
            {
                translateMenuItem.Visibility = Visibility.Visible;

                // Get system language for the menu item header
                string systemLanguage = GetSystemLanguageName();
                translateMenuItem.Header = $"Translate to {systemLanguage}";
            }

            translateSeparator?.Visibility = Visibility.Visible;
        }
        else
        {
            translateMenuItem?.Visibility = Visibility.Collapsed;
            translateSeparator?.Visibility = Visibility.Collapsed;
        }

        if (Uri.TryCreate(Word, UriKind.Absolute, out Uri? uri))
        {
            string headerText = $"Try to go to: {Word}";
            int maxLength = 36;
            if (headerText.Length > maxLength)
                headerText = string.Concat(headerText.AsSpan(0, maxLength), "...");

            MenuItem urlMi = new()
            {
                Header = headerText
            };
            urlMi.Click += (sender, e) =>
            {
                Process.Start(new ProcessStartInfo(Word) { UseShellExecute = true });
            };
            textBoxContextMenu.Items.Add(urlMi);
        }
    }

    private void EditWordTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        Select();

        // The user focusing a word's edit box is a strong signal they are about to correct
        // recognized text, so freeze the frame to keep it from resetting while they edit.
        OwnerGrabFrame?.FreezeFrameForWordEditing();
    }

    private void EditWordTextBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Select();
        e.Handled = true;
    }

    private void EditWordTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private void MergeWordBordersExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        OwnerGrabFrame?.MergeSelectedWordBorders();
    }

    private void MergeWordBordersMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (OwnerGrabFrame is null)
            return;

        OwnerGrabFrame.MergeSelectedWordBorders();
    }

    private void MoveResizeBorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Select();
        OwnerGrabFrame?.StartWordBorderMoveResize(this, Side.None);
    }

    private void SearchForSimilarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OwnerGrabFrame?.SearchForSimilar(this);
    }

    private void SizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe)
            return;
        Enum.TryParse(typeof(Side), fe.Tag.ToString(), out object? side);

        if (side is not Side sideEnum)
            return;
        OwnerGrabFrame?.StartWordBorderMoveResize(this, sideEnum);
    }

    private void TryToAlphaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string oldWord = Word;
        if (EditWordTextBox.SelectedText != string.Empty)
            EditWordTextBox.SelectedText = EditWordTextBox.SelectedText.TryFixToLetters();
        else
            Word = Word.TryFixToLetters();

        OwnerGrabFrame?.UndoableWordChange(this, oldWord, true);
    }

    private void TryToNumberMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string oldWord = Word;
        if (EditWordTextBox.SelectedText != string.Empty)
            EditWordTextBox.SelectedText = EditWordTextBox.SelectedText.TryFixToNumbers();
        else
            Word = Word.TryFixToNumbers();

        OwnerGrabFrame?.UndoableWordChange(this, oldWord, true);
    }

    private void MakeSingleLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string oldWord = Word;
        Word = Word.MakeStringSingleLine();

        OwnerGrabFrame?.UndoableWordChange(this, oldWord, true);
    }

    private void WordBorder_MouseEnter(object sender, RoutedEventArgs e)
    {
        if (OwnerGrabFrame?.IsCtrlDown is true)
            MoveResizeBorder.Visibility = Visibility.Visible;
        else
            MoveResizeBorder.Visibility = Visibility.Collapsed;
    }

    private void WordBorder_MouseLeave(object sender, RoutedEventArgs e)
    {
        MoveResizeBorder.Visibility = Visibility.Collapsed;
    }

    private void WordBorderControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EditWordTextBox.Visibility == Visibility.Collapsed)
        {
            EnterEdit();
            return;
        }

        try { Clipboard.SetDataObject(Word, true); } catch { }

        if (AppUtilities.TextGrabSettings.ShowToast
            && !IsFromEditWindow)
            NotificationUtilities.ShowToast(Word);

        if (IsFromEditWindow)
            WindowUtilities.AddTextToOpenWindow(Word);
    }

    private void WordBorderControl_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.RightButton == MouseButtonState.Pressed)
            return;

        e.Handled = true;
        if (IsSelected)
            Deselect();
        else
            Select();
    }
    private void WordBorderControl_Unloaded(object sender, RoutedEventArgs e)
    {
        this.MouseDoubleClick -= WordBorderControl_MouseDoubleClick;
        this.MouseDown -= WordBorderControl_MouseDown;
        this.Unloaded -= WordBorderControl_Unloaded;
        Loaded -= WordBorder_Loaded;
        SizeChanged -= WordBorder_SizeChanged;

        debounceTimer.Stop();
        debounceTimer.Tick -= DebounceTimer_Tick;

        OwnerGrabFrame = null;
    }

    private void WordBorder_Loaded(object sender, RoutedEventArgs e) => ApplyTextLayout();

    private void WordBorder_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyTextLayout();

    private async void TranslateWordMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Word))
            return;

        if (!WindowsAiUtilities.CanDeviceUseWinAI())
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Translation Not Available",
                Content = "Windows AI is not available on this device.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return;
        }

        // Store original text
        string originalWord = Word;

        try
        {
            // Get system language
            string targetLanguage = GetSystemLanguageName();

            // Translate the word
            string translatedText = await WindowsAiUtilities.TranslateText(originalWord, targetLanguage);

            // Update the word with translation
            if (!string.IsNullOrWhiteSpace(translatedText) && translatedText != originalWord)
            {
                // Notify the owner GrabFrame of the change for undo support
                OwnerGrabFrame?.UndoableWordChange(this, originalWord, true);

                Word = translatedText;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Translation failed: {ex.Message}");
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Translation Error",
                Content = $"Translation failed: {ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
    }

    /// <summary>
    /// Gets the system's display language name (e.g., "English", "Spanish", "French")
    /// Falls back to "English" if the system language is not recognized.
    /// </summary>
    private static string GetSystemLanguageName()
    {
        // Use the shared utility method from LanguageUtilities
        return LanguageUtilities.GetSystemLanguageForTranslation();
    }

    #endregion Methods
}
