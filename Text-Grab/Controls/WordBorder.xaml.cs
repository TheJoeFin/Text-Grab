using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for WordBorder.xaml
/// </summary>
[DebuggerDisplay("{Word} : Size {Width}:{Height} Pos. {Left}:{Top} Table {ResultRowID}:{ResultColumnID}")]
public partial class WordBorder : UserControl, INotifyPropertyChanged
{
    public bool IsSelected { get; set; } = false;

    public bool WasRegionSelected { get; set; } = false;

    public bool IsEditing => EditWordTextBox.IsFocused;

    private SolidColorBrush matchingBackground = new SolidColorBrush(Colors.Black);
    private SolidColorBrush contrastingForeground = new SolidColorBrush(Colors.White);
    private int contextMenuBaseSize;
    private DispatcherTimer debounceTimer = new();

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

    public string Word
    {
        get { return (string)GetValue(WordProperty); }
        set
        {
            SetValue(WordProperty, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Word)));
        }
    }

    // Using a DependencyProperty as the backing store for Word.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty WordProperty =
        DependencyProperty.Register("Word", typeof(string), typeof(WordBorder), new PropertyMetadata(""));


    public int LineNumber { get; set; } = 0;

    public int ResultRowID { get; set; } = 0;

    public int ResultColumnID { get; set; } = 0;

    private double top = 0;
    public double Top
    {
        get { return top; }
        set
        {
            top = value;
            Canvas.SetTop(this, top);
        }
    }

    private double left = 0;
    public double Left
    {
        get { return left; }
        set
        {
            left = value;
            Canvas.SetLeft(this, left);
        }
    }

    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public static RoutedCommand MergeWordsCommand = new();

    public bool IsFromEditWindow { get; set; } = false;

    public GrabFrame? OwnerGrabFrame { get; set; }

    public WordBorder()
    {
        InitializeComponent();
        DataContext = this;
        contextMenuBaseSize = WordBorderBorder.ContextMenu.Items.Count;

        debounceTimer.Interval = new(0, 0, 0, 0, 300);
        debounceTimer.Tick += DebounceTimer_Tick;
    }

    private void DebounceTimer_Tick(object? sender, EventArgs e)
    {
        debounceTimer.Stop();
        OwnerGrabFrame?.WordChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Select()
    {
        IsSelected = true;
        WordBorderBorder.BorderBrush = new SolidColorBrush(Colors.Orange);
    }

    public void Deselect()
    {
        IsSelected = false;
        WordBorderBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 48, 142, 152));
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

    public void SetAsBarcode()
    {
        EditWordTextBox.TextWrapping = TextWrapping.Wrap;
        EditWordTextBox.TextAlignment = TextAlignment.Center;

        EditWordTextBox.Width = this.Width - 2;
        EditWordTextBox.Height = this.Height - 2;
        EditWordTextBox.FontSize = 14;

        if (Uri.TryCreate(Word, UriKind.Absolute, out var uri))
            EditWordTextBox.Background = new SolidColorBrush(Colors.Blue);
    }

    public bool IntersectsWith(Rect rectToChek)
    {
        Rect wbRect = new(Left, Top, Width, Height);
        return rectToChek.IntersectsWith(wbRect);
    }

    public void FocusTextbox()
    {
        EditWordTextBox.Focus();
        Keyboard.Focus(EditWordTextBox);
        EditWordTextBox.SelectAll();
    }

    private void CanMergeWordBorderExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (OwnerGrabFrame?.SelectedWordBorders().Count > 1)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void MergeWordBordersExecuted(object sender, ExecutedRoutedEventArgs? e = null)
    {
        OwnerGrabFrame?.MergeSelectedWordBorders();
    }

    private void WordBorder_MouseEnter(object sender, RoutedEventArgs e)
    {
        if (OwnerGrabFrame?.isCtrlDown is true)
            MoveResizeBorder.Visibility = Visibility.Visible;
        else
            MoveResizeBorder.Visibility = Visibility.Collapsed;
    }

    private void WordBorder_MouseLeave(object sender, RoutedEventArgs e)
    {
        MoveResizeBorder.Visibility = Visibility.Collapsed;
    }

    private void EditWordTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement senderElement)
            return;

        ContextMenu textBoxContextMenu = senderElement.ContextMenu;

        while (textBoxContextMenu.Items.Count > contextMenuBaseSize)
        {
            senderElement.ContextMenu?.Items.RemoveAt(contextMenuBaseSize);
        }

        if (Uri.TryCreate(Word, UriKind.Absolute, out var uri))
        {
            string headerText = $"Try to go to: {Word}";
            if (headerText.Length > 36)
                headerText = headerText.Substring(0, 36) + "...";

            MenuItem urlMi = new();
            urlMi.Header = headerText;
            urlMi.Click += (sender, e) =>
            {
                Process.Start(new ProcessStartInfo(Word) { UseShellExecute = true });
            };
            senderElement.ContextMenu?.Items.Add(urlMi);
        }
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

    private void WordBorderControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EditWordTextBox.Visibility == Visibility.Collapsed)
        {
            EnterEdit();
            return;
        }

        try { Clipboard.SetDataObject(Word, true); } catch { }

        if (Settings.Default.ShowToast
            && !IsFromEditWindow)
            NotificationUtilities.ShowToast(Word);

        if (IsFromEditWindow)
            WindowUtilities.AddTextToOpenWindow(Word);
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

    private void TryToAlphaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string oldWord = Word;
        if (EditWordTextBox.SelectedText != string.Empty)
            EditWordTextBox.SelectedText = EditWordTextBox.SelectedText.TryFixToLetters();
        else
            Word = Word.TryFixToLetters();

        OwnerGrabFrame?.UndoableWordChange(this, oldWord, true);
    }

    private void BreakIntoWordsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (OwnerGrabFrame is null)
            return;

        OwnerGrabFrame.BreakWordBorderIntoWords(this);
    }

    private void MergeWordBordersMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (OwnerGrabFrame is null)
            return;

        OwnerGrabFrame.MergeSelectedWordBorders();
    }

    private void WordBorderControl_Unloaded(object sender, RoutedEventArgs e)
    {
        this.MouseDoubleClick -= WordBorderControl_MouseDoubleClick;
        this.MouseDown -= WordBorderControl_MouseDown;
        this.Unloaded -= WordBorderControl_Unloaded;
    }

    private void MoveResizeBorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        OwnerGrabFrame?.StartWordBorderMoveResize(this, Side.None);
    }

    private void SizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe)
            return;
        Enum.TryParse(typeof(Side), fe.Tag.ToString(), out var side);

        if (side is not Side sideEnum)
            return;
        OwnerGrabFrame?.StartWordBorderMoveResize(this, sideEnum);
    }

    private void DeleteWordMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OwnerGrabFrame?.DeleteThisWordBorder(this);
    }

    private void SearchForSimilarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OwnerGrabFrame?.SearchForSimilar(this);
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

    private void EditWordTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        Select();
    }
}
