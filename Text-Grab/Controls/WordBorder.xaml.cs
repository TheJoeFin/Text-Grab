using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for WordBorder.xaml
/// </summary>
public partial class WordBorder : UserControl, INotifyPropertyChanged
{
    public bool IsSelected { get; set; } = false;

    public bool WasRegionSelected { get; set; } = false;

    public bool IsEditing { get; set; } = false;

    private SolidColorBrush matchingBackground = new SolidColorBrush(Colors.Black);
    private SolidColorBrush contrastingForeground = new SolidColorBrush(Colors.White);

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

    public bool IsFromEditWindow { get; set; } = false;

    public WordBorder()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Select()
    {
        IsSelected = true;
        WordBorderBorder.BorderBrush = new SolidColorBrush(Colors.Yellow);
        EditWordTextBox.Foreground = new SolidColorBrush(Colors.Yellow);
        MainGrid.Background = new SolidColorBrush(Colors.Black);
    }

    public void Deselect()
    {
        IsSelected = false;
        WordBorderBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 48, 142, 152));
        EditWordTextBox.Foreground = contrastingForeground;
        MainGrid.Background = matchingBackground;
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

    private void EditWordTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        ContextMenu textBoxContextMenu = EditWordTextBox.ContextMenu;

        int maxBaseSize = 2;
        while (textBoxContextMenu.Items.Count > maxBaseSize)
        {
            EditWordTextBox.ContextMenu?.Items.RemoveAt(maxBaseSize);
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
            EditWordTextBox.ContextMenu?.Items.Add(urlMi);
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

    private async void WordBorderControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EditWordTextBox.Visibility == Visibility.Collapsed)
        {
            EnterEdit();
            return;
        }

        try { Clipboard.SetDataObject(Word, true); } catch { }

        if (Settings.Default.ShowToast
            && IsFromEditWindow == false)
            NotificationUtilities.ShowToast(Word);

        if (IsFromEditWindow == true)
            WindowUtilities.AddTextToOpenWindow(Word);

        if (IsSelected)
        {
            await Task.Delay(100);
            Deselect();
        }
        else
        {
            await Task.Delay(100);
            Select();
        }
    }

    private void TryToNumberMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Word = Word.TryFixToNumbers();
    }

    private void TryToAlphaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Word = Word.TryFixToLetters();
    }

    private void WordBorderControl_Unloaded(object sender, RoutedEventArgs e)
    {
        this.MouseDoubleClick -= WordBorderControl_MouseDoubleClick;
        this.MouseDown -= WordBorderControl_MouseDown;
        this.Unloaded -= WordBorderControl_Unloaded;

        TryToAlphaMenuItem.Click -= TryToAlphaMenuItem_Click;
        TryToNumberMenuItem.Click -= TryToNumberMenuItem_Click;
    }
}
