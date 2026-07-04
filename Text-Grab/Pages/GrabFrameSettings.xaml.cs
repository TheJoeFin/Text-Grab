using System;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Pages;
/// <summary>
/// Interaction logic for GrabFrameSettings.xaml
/// </summary>
public partial class GrabFrameSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool _loaded = false;

    public GrabFrameSettings()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        GrabFrameAutoOcrCheckBox.IsChecked = DefaultSettings.GrabFrameAutoOcr;
        GrabFrameUpdateEtwCheckBox.IsChecked = DefaultSettings.GrabFrameUpdateEtw;
        CloseFrameOnGrabCheckBox.IsChecked = DefaultSettings.CloseFrameOnGrab;
        GrabFrameReadBarcodesCheckBox.IsChecked = DefaultSettings.GrabFrameReadBarcodes;
        GrabFrameTranslationCheckBox.IsChecked = DefaultSettings.GrabFrameTranslationEnabled;
        GrabFrameTranslationLanguageText.Text = DefaultSettings.GrabFrameTranslationLanguage;
        GrabFrameTranslationLanguageText.IsEnabled = DefaultSettings.GrabFrameTranslationEnabled;

        ScrollBehavior scrollBehavior = ScrollBehavior.Resize;
        if (!string.IsNullOrWhiteSpace(DefaultSettings.GrabFrameScrollBehavior))
            _ = Enum.TryParse(DefaultSettings.GrabFrameScrollBehavior, out scrollBehavior);

        switch (scrollBehavior)
        {
            case ScrollBehavior.None:
                NoneScrollRadio.IsChecked = true;
                break;
            case ScrollBehavior.Zoom:
                ZoomScrollRadio.IsChecked = true;
                break;
            case ScrollBehavior.ZoomWhenFrozen:
                ZoomWhenFrozenScrollRadio.IsChecked = true;
                break;
            case ScrollBehavior.Resize:
            default:
                ResizeScrollRadio.IsChecked = true;
                break;
        }

        GrabFrameBorderStyle borderStyle = GrabFrameBorderStyle.Theme;
        if (!string.IsNullOrWhiteSpace(DefaultSettings.GrabFrameBorderStyle))
            _ = Enum.TryParse(DefaultSettings.GrabFrameBorderStyle, out borderStyle);

        switch (borderStyle)
        {
            case GrabFrameBorderStyle.HighContrast:
                HighContrastBorderRadio.IsChecked = true;
                break;
            case GrabFrameBorderStyle.Color:
                ColorBorderRadio.IsChecked = true;
                break;
            case GrabFrameBorderStyle.Theme:
            default:
                ThemeBorderRadio.IsChecked = true;
                break;
        }
        BorderColorSwatchPanel.IsEnabled = borderStyle == GrabFrameBorderStyle.Color;

        _loaded = true;
    }

    private void BorderStyleRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        GrabFrameBorderStyle borderStyle = GrabFrameBorderStyle.Theme;
        if (HighContrastBorderRadio.IsChecked == true) borderStyle = GrabFrameBorderStyle.HighContrast;
        else if (ColorBorderRadio.IsChecked == true) borderStyle = GrabFrameBorderStyle.Color;

        DefaultSettings.GrabFrameBorderStyle = borderStyle.ToString();
        DefaultSettings.Save();
        BorderColorSwatchPanel.IsEnabled = borderStyle == GrabFrameBorderStyle.Color;
    }

    private void BorderColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        if (sender is not Button button || button.Tag is not string hex)
            return;

        DefaultSettings.GrabFrameBorderColor = hex;
        DefaultSettings.GrabFrameBorderStyle = GrabFrameBorderStyle.Color.ToString();
        DefaultSettings.Save();

        ColorBorderRadio.IsChecked = true;
        BorderColorSwatchPanel.IsEnabled = true;
    }

    private void GrabFrameAutoOcrCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.GrabFrameAutoOcr = GrabFrameAutoOcrCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void GrabFrameUpdateEtwCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.GrabFrameUpdateEtw = GrabFrameUpdateEtwCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void CloseFrameOnGrabCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.CloseFrameOnGrab = CloseFrameOnGrabCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void GrabFrameReadBarcodesCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.GrabFrameReadBarcodes = GrabFrameReadBarcodesCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void GrabFrameTranslationCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        bool enabled = GrabFrameTranslationCheckBox.IsChecked == true;
        DefaultSettings.GrabFrameTranslationEnabled = enabled;
        DefaultSettings.Save();
        GrabFrameTranslationLanguageText.IsEnabled = enabled;
    }

    private void GrabFrameTranslationLanguageText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.GrabFrameTranslationLanguage = GrabFrameTranslationLanguageText.Text;
        DefaultSettings.Save();
    }

    private void ScrollBehaviorRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        ScrollBehavior behavior = ScrollBehavior.Resize;
        if (NoneScrollRadio.IsChecked == true) behavior = ScrollBehavior.None;
        else if (ZoomScrollRadio.IsChecked == true) behavior = ScrollBehavior.Zoom;
        else if (ZoomWhenFrozenScrollRadio.IsChecked == true) behavior = ScrollBehavior.ZoomWhenFrozen;

        DefaultSettings.GrabFrameScrollBehavior = behavior.ToString();
        DefaultSettings.Save();
    }
}
