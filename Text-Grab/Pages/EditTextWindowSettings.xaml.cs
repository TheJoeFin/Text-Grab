using System;
using System.Globalization;
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
