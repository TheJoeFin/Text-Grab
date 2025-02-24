using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Text_Grab.Controls;

public partial class RegExIcon : UserControl
{
    public SolidColorBrush IconColor
    {
        get { return (SolidColorBrush)GetValue(IconColorProperty); }
        set { SetValue(IconColorProperty, value); }
    }

    public static readonly DependencyProperty IconColorProperty =
        DependencyProperty.Register("IconColor", typeof(SolidColorBrush), typeof(RegExIcon), new PropertyMetadata(null));

    public RegExIcon()
    {
        DataContext = this;
        InitializeComponent();
    }
}
