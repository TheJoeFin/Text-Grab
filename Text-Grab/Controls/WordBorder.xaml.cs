using System.ComponentModel;
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
    }

    public void Deselect()
    {
        IsSelected = false;
        WordBorderBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 48, 142, 152));
        EditWordTextBox.Foreground = new SolidColorBrush(Colors.White);
    }

    public void EnterEdit()
    {
        EditWordTextBox.Visibility = Visibility.Visible;
    }

    public void ExitEdit()
    {
        EditWordTextBox.Visibility = Visibility.Collapsed;
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

        Clipboard.SetDataObject(Word, true);

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
