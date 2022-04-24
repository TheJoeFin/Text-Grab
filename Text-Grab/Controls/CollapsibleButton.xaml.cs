using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for CollapsibleButton.xaml
/// </summary>
public partial class CollapsibleButton : Button, INotifyPropertyChanged
{
    private string _buttonText = "Button Text";

    public bool IsSymbol { get; set; } = false;

    public bool CanChangeStyle { get; set; } = true;

    public string ButtonText
    {
        get { return _buttonText; }
        set
        {
            if (_buttonText != value)
            {
                _buttonText = value;
                OnPropertyChanged();
            }
        }
    }

    private string _symbolText = "";

    public string SymbolText
    {
        get { return _symbolText; }
        set
        {
            if (_symbolText != value)
            {
                _symbolText = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public CollapsibleButton()
    {
        DataContext = this;
        InitializeComponent();
    }

    private void CollapsibleButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsSymbol == true)
        {
            // change to a symbol button
            Style? SymbolButtonStyle = this.FindResource("SymbolButton") as Style;
            if (SymbolButtonStyle != null)
                this.Style = SymbolButtonStyle;
            ButtonTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void ChangeButtonLayout_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (IsSymbol == true)
        {
            // change to a normal button
            Style? tealButtonStyle = this.FindResource("TealColor") as Style;
            if (tealButtonStyle != null)
                this.Style = tealButtonStyle;
            ButtonTextBlock.Visibility = Visibility.Visible; ;

            IsSymbol = false;
        }
        else
        {
            // change to a symbol button
            Style? SymbolButtonStyle = this.FindResource("SymbolButton") as Style;
            if (SymbolButtonStyle != null)
                this.Style = SymbolButtonStyle;
            ButtonTextBlock.Visibility = Visibility.Collapsed;

            IsSymbol = true;
        }
    }
}
