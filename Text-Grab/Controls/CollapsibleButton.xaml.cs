using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Models;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for CollapsibleButton.xaml
/// </summary>
public partial class CollapsibleButton : Button, INotifyPropertyChanged
{
    #region Fields

    public string ButtonText
    {
        get { return (string)GetValue(ButtonTextProperty); }
        set { SetValue(ButtonTextProperty, value); }
    }

    // Using a DependencyProperty as the backing store for ButtonText.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ButtonTextProperty =
        DependencyProperty.Register("ButtonText", typeof(string), typeof(CollapsibleButton), new PropertyMetadata("ButtonText"));



    private string _symbolText = "";
    private bool isSymbol = false;

    #endregion Fields

    #region Constructors

    public CollapsibleButton()
    {
        DataContext = this;
        InitializeComponent();
    }

    #endregion Constructors

    #region Events

    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion Events

    #region Properties

    public bool CanChangeStyle { get; set; } = true;

    public ButtonInfo? CustomButton { get; set; }

    public bool IsSymbol
    {
        get { return isSymbol; }
        set
        {
            isSymbol = value;
            ChangeButtonLayout_Click();
        }
    }
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

    #endregion Properties

    #region Methods

    private void ChangeButtonLayout_Click(object? sender = null, System.Windows.RoutedEventArgs? e = null)
    {
        if (sender is not null)
            isSymbol = !isSymbol;

        if (!isSymbol)
        {
            // change to a normal button
            Style? tealButtonStyle = this.FindResource("TealColor") as Style;
            if (tealButtonStyle != null)
                this.Style = tealButtonStyle;
            ButtonTextBlock.Visibility = Visibility.Visible; ;
        }
        else
        {
            // change to a symbol button
            Style? SymbolButtonStyle = this.FindResource("SymbolButton") as Style;
            if (SymbolButtonStyle != null)
                this.Style = SymbolButtonStyle;
            ButtonTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void CollapsibleButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (isSymbol)
        {
            // change to a symbol button
            Style? SymbolButtonStyle = this.FindResource("SymbolButton") as Style;
            if (SymbolButtonStyle != null)
                this.Style = SymbolButtonStyle;
            ButtonTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion Methods
}
