using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Pages;
/// <summary>
/// Interaction logic for EditTextWindowSettings.xaml
/// </summary>
public partial class EditTextWindowSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool _loaded = false;

    public EditTextWindowSettings()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // Window behavior
        EditWindowStartFullscreenCheckBox.IsChecked = DefaultSettings.EditWindowStartFullscreen;
        EditWindowIsOnTopCheckBox.IsChecked = DefaultSettings.EditWindowIsOnTop;
        EditWindowIsWordWrapOnCheckBox.IsChecked = DefaultSettings.EditWindowIsWordWrapOn;
        RestoreEtwPositionsCheckBox.IsChecked = DefaultSettings.RestoreEtwPositions;
        EtwRememberFilePlacementCheckBox.IsChecked = DefaultSettings.EtwRememberFilePlacement;
        EtwDefaultWidthBox.Value = DefaultSettings.EtwDefaultWidth;
        EtwDefaultHeightBox.Value = DefaultSettings.EtwDefaultHeight;
        EtwDefaultWidthPercentBox.Value = DefaultSettings.EtwDefaultWidthPercent;
        EtwDefaultHeightPercentBox.Value = DefaultSettings.EtwDefaultHeightPercent;
        bool usePercent = string.Equals(DefaultSettings.EtwDefaultSizeMode, "Percent", StringComparison.OrdinalIgnoreCase);
        EtwSizeModePixelsRadio.IsChecked = !usePercent;
        EtwSizeModePercentRadio.IsChecked = usePercent;
        UpdateSizeModePanels(usePercent);

        string anchor = DefaultSettings.EtwDefaultAnchor ?? "MiddleCenter";
        foreach (RadioButton anchorRadio in EtwAnchorGrid.Children.OfType<RadioButton>())
            anchorRadio.IsChecked = string.Equals(anchorRadio.Tag?.ToString(), anchor, StringComparison.OrdinalIgnoreCase);

        // Toolbar & UI
        EditWindowBottomBarIsHiddenCheckBox.IsChecked = DefaultSettings.EditWindowBottomBarIsHidden;
        EtwShowLangPickerCheckBox.IsChecked = DefaultSettings.EtwShowLangPicker;
        EtwUseMarginsCheckBox.IsChecked = DefaultSettings.EtwUseMargins;

        // Font
        FontFamilyTextBox.Text = DefaultSettings.FontFamilySetting;
        double fontSize = Math.Clamp(DefaultSettings.FontSizeSetting, FontSizeSlider.Minimum, FontSizeSlider.Maximum);
        FontSizeSlider.Value = fontSize;
        FontSizeValueText.Text = fontSize.ToString("0", CultureInfo.InvariantCulture);
        IsFontBoldCheckBox.IsChecked = DefaultSettings.IsFontBold;
        IsFontItalicCheckBox.IsChecked = DefaultSettings.IsFontItalic;
        IsFontUnderlineCheckBox.IsChecked = DefaultSettings.IsFontUnderline;
        IsFontStrikeoutCheckBox.IsChecked = DefaultSettings.IsFontStrikeout;

        // Status bar
        EtwShowWordCountCheckBox.IsChecked = DefaultSettings.EtwShowWordCount;
        EtwShowCharDetailsCheckBox.IsChecked = DefaultSettings.EtwShowCharDetails;
        EtwShowMatchCountCheckBox.IsChecked = DefaultSettings.EtwShowMatchCount;
        EtwShowRegexPatternCheckBox.IsChecked = DefaultSettings.EtwShowRegexPattern;
        EtwShowSimilarMatchesCheckBox.IsChecked = DefaultSettings.EtwShowSimilarMatches;

        EtwNormalizeLineEndingsOnPasteCheckBox.IsChecked = DefaultSettings.EtwNormalizeLineEndingsOnPaste;

        // Spell check
        if (!Enum.TryParse(DefaultSettings.EtwSpellCheckMode, out SpellCheckMode spellCheckMode))
            spellCheckMode = SpellCheckMode.Auto;
        SpellCheckAutoRadio.IsChecked = spellCheckMode == SpellCheckMode.Auto;
        SpellCheckAlwaysOnRadio.IsChecked = spellCheckMode == SpellCheckMode.AlwaysOn;
        SpellCheckOffRadio.IsChecked = spellCheckMode == SpellCheckMode.Off;

        // Calculator
        CalcShowPaneCheckBox.IsChecked = DefaultSettings.CalcShowPane;
        CalcShowErrorsCheckBox.IsChecked = DefaultSettings.CalcShowErrors;
        CalcShowErrorsCheckBox.IsEnabled = DefaultSettings.CalcShowPane;

        _loaded = true;
    }

    private void EditWindowStartFullscreenCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EditWindowStartFullscreen = EditWindowStartFullscreenCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EditWindowIsOnTopCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EditWindowIsOnTop = EditWindowIsOnTopCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EditWindowIsWordWrapOnCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EditWindowIsWordWrapOn = EditWindowIsWordWrapOnCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void RestoreEtwPositionsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.RestoreEtwPositions = RestoreEtwPositionsCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EtwRememberFilePlacementCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EtwRememberFilePlacement = EtwRememberFilePlacementCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EtwDefaultSizeBox_ValueChanged(object sender, Wpf.Ui.Controls.NumberBoxValueChangedEventArgs args)
    {
        if (!_loaded || args.NewValue is not double value)
            return;

        int pixels = (int)Math.Clamp(Math.Round(value), 200, 10000);
        int percent = (int)Math.Clamp(Math.Round(value), 10, 100);

        if (ReferenceEquals(sender, EtwDefaultWidthBox))
            DefaultSettings.EtwDefaultWidth = pixels;
        else if (ReferenceEquals(sender, EtwDefaultHeightBox))
            DefaultSettings.EtwDefaultHeight = pixels;
        else if (ReferenceEquals(sender, EtwDefaultWidthPercentBox))
            DefaultSettings.EtwDefaultWidthPercent = percent;
        else if (ReferenceEquals(sender, EtwDefaultHeightPercentBox))
            DefaultSettings.EtwDefaultHeightPercent = percent;
        else
            return;

        DefaultSettings.Save();
    }

    private void EtwSizeModeRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded || sender is not RadioButton { IsChecked: true } radioButton)
            return;

        bool usePercent = string.Equals(radioButton.Tag?.ToString(), "Percent", StringComparison.Ordinal);
        DefaultSettings.EtwDefaultSizeMode = usePercent ? "Percent" : "Pixels";
        DefaultSettings.Save();
        UpdateSizeModePanels(usePercent);
    }

    private void EtwAnchorRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded || sender is not RadioButton { IsChecked: true } radioButton || radioButton.Tag is not string anchor)
            return;

        DefaultSettings.EtwDefaultAnchor = anchor;
        DefaultSettings.Save();
    }

    private void UpdateSizeModePanels(bool usePercent)
    {
        EtwPixelSizePanel.Visibility = usePercent ? Visibility.Collapsed : Visibility.Visible;
        EtwPercentSizePanel.Visibility = usePercent ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EtwDefaultSizeReset_Click(object sender, RoutedEventArgs e)
    {
        EtwDefaultWidthBox.Value = 800;
        EtwDefaultHeightBox.Value = 500;
        EtwDefaultWidthPercentBox.Value = 60;
        EtwDefaultHeightPercentBox.Value = 70;
    }

    private void EditWindowBottomBarIsHiddenCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EditWindowBottomBarIsHidden = EditWindowBottomBarIsHiddenCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EtwShowLangPickerCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EtwShowLangPicker = EtwShowLangPickerCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EtwUseMarginsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EtwUseMargins = EtwUseMarginsCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void FontFamilyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.FontFamilySetting = FontFamilyTextBox.Text;
        DefaultSettings.Save();
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        double newVal = Math.Round(FontSizeSlider.Value);
        DefaultSettings.FontSizeSetting = newVal;
        DefaultSettings.Save();
        FontSizeValueText.Text = newVal.ToString("0", CultureInfo.InvariantCulture);
    }

    private void IsFontBoldCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.IsFontBold = IsFontBoldCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void IsFontItalicCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.IsFontItalic = IsFontItalicCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void IsFontUnderlineCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.IsFontUnderline = IsFontUnderlineCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void IsFontStrikeoutCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.IsFontStrikeout = IsFontStrikeoutCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EtwShowWordCountCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EtwShowWordCount = EtwShowWordCountCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EtwShowCharDetailsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EtwShowCharDetails = EtwShowCharDetailsCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EtwShowMatchCountCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EtwShowMatchCount = EtwShowMatchCountCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EtwShowRegexPatternCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EtwShowRegexPattern = EtwShowRegexPatternCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EtwShowSimilarMatchesCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EtwShowSimilarMatches = EtwShowSimilarMatchesCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void EtwNormalizeLineEndingsOnPasteCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.EtwNormalizeLineEndingsOnPaste = EtwNormalizeLineEndingsOnPasteCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void SpellCheckModeRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded
            || sender is not RadioButton radioButton
            || radioButton.IsChecked != true
            || !Enum.TryParse(radioButton.Tag?.ToString(), out SpellCheckMode mode))
            return;

        DefaultSettings.EtwSpellCheckMode = mode.ToString();
        DefaultSettings.Save();
    }

    private void CalcShowPaneCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        bool enabled = CalcShowPaneCheckBox.IsChecked == true;
        DefaultSettings.CalcShowPane = enabled;
        DefaultSettings.Save();
        CalcShowErrorsCheckBox.IsEnabled = enabled;
    }

    private void CalcShowErrorsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.CalcShowErrors = CalcShowErrorsCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }
}
