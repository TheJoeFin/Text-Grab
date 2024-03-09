using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Models;
using Wpf.Ui.Controls;

namespace Text_Grab.Controls;

public partial class CollapsibleButton : System.Windows.Controls.Button, INotifyPropertyChanged
{
    #region Fields

    public string ButtonText
    {
        get => (string)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public static readonly DependencyProperty ButtonTextProperty =
        DependencyProperty.Register("ButtonText", typeof(string), typeof(CollapsibleButton), new PropertyMetadata("ButtonText"));


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
        get => isSymbol;
        set
        {
            isSymbol = value;
            ChangeButtonLayout_Click();
        }
    }

    public SymbolRegular ButtonSymbol
    {
        get => (SymbolRegular)GetValue(ButtonSymbolProperty);
        set => SetValue(ButtonSymbolProperty, value);
    }

    public static readonly DependencyProperty ButtonSymbolProperty =
        DependencyProperty.Register("ButtonSymbol", typeof(SymbolRegular), typeof(CollapsibleButton), new PropertyMetadata(SymbolRegular.Diamond24));

    #endregion Properties

    #region Methods

    private void ChangeButtonLayout_Click(object? sender = null, System.Windows.RoutedEventArgs? e = null)
    {
        if (sender is not null)
            isSymbol = !isSymbol;

        if (!isSymbol)
        {
            // change to a normal button
            if (FindResource("TealColor") is Style tealButtonStyle)
                Style = tealButtonStyle;
            ButtonTextBlock.Visibility = Visibility.Visible; ;
        }
        else
        {
            // change to a symbol button
            if (FindResource("SymbolButton") is Style SymbolButtonStyle)
                Style = SymbolButtonStyle;
            ButtonTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void CollapsibleButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (isSymbol)
        {
            // change to a symbol button
            if (FindResource("SymbolButton") is Style SymbolButtonStyle)
                Style = SymbolButtonStyle;
            ButtonTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion Methods
}
