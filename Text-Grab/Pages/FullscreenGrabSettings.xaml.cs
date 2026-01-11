using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Text_Grab;
using Text_Grab.Properties;
using Text_Grab.Utilities;

namespace Text_Grab.Pages;
/// <summary>
/// Interaction logic for FullscreenGrabSettings.xaml
/// </summary>
public partial class FullscreenGrabSettings : Page
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private bool _loaded = false;

    public FullscreenGrabSettings()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // hydrate controls from settings
        SendToEtwCheckBox.IsChecked = DefaultSettings.FsgSendEtwToggle;
        TryInsertCheckBox.IsChecked = DefaultSettings.TryInsert;

        ShadeOverlayCheckBox.IsChecked = DefaultSettings.FsgShadeOverlay;

        InsertDelaySlider.Value = Math.Clamp(DefaultSettings.InsertDelay, InsertDelaySlider.Minimum, InsertDelaySlider.Maximum);
        InsertDelayValueText.Text = DefaultSettings.InsertDelay.ToString("0.0", CultureInfo.InvariantCulture);

        InsertDelaySlider.IsEnabled = TryInsertCheckBox.IsChecked == true;

        // Determine default mode
        FsgDefaultMode mode = FsgDefaultMode.Default;
        if (!string.IsNullOrWhiteSpace(DefaultSettings.FsgDefaultMode))
            Enum.TryParse(DefaultSettings.FsgDefaultMode, true, out mode);

        if (mode == FsgDefaultMode.Table)
        {
            TableModeRadio.IsChecked = true;
        }
        else if (DefaultSettings.FSGMakeSingleLineToggle)
        {
            SingleLineModeRadio.IsChecked = true;
        }
        else
        {
            DefaultModeRadio.IsChecked = true;
        }

        // Load post-grab action defaults
        LoadPostGrabActionDefault(DefaultSettings.FsgGuidFixDefault, GuidFixOffRadio, GuidFixLastUsedRadio, GuidFixOnRadio);
        LoadPostGrabActionDefault(DefaultSettings.FsgTrimEachLineDefault, TrimEachLineOffRadio, TrimEachLineLastUsedRadio, TrimEachLineOnRadio);
        LoadPostGrabActionDefault(DefaultSettings.FsgRemoveDuplicatesDefault, RemoveDuplicatesOffRadio, RemoveDuplicatesLastUsedRadio, RemoveDuplicatesOnRadio);
        LoadPostGrabActionDefault(DefaultSettings.FsgWebSearchDefault, WebSearchOffRadio, WebSearchLastUsedRadio, WebSearchOnRadio);
        LoadPostGrabActionDefault(DefaultSettings.FsgInsertTextDefault, InsertTextOffRadio, InsertTextLastUsedRadio, InsertTextOnRadio);
        LoadPostGrabActionDefault(DefaultSettings.FsgTranslateDefault, TranslateOffRadio, TranslateLastUsedRadio, TranslateOnRadio);

        _loaded = true;
    }

    private void LoadPostGrabActionDefault(string settingValue, RadioButton offRadio, RadioButton lastUsedRadio, RadioButton onRadio)
    {
        if (!Enum.TryParse<PostGrabActionDefault>(settingValue, true, out PostGrabActionDefault mode))
            mode = PostGrabActionDefault.Off;

        switch (mode)
        {
            case PostGrabActionDefault.Off:
                offRadio.IsChecked = true;
                break;
            case PostGrabActionDefault.LastUsed:
                lastUsedRadio.IsChecked = true;
                break;
            case PostGrabActionDefault.On:
                onRadio.IsChecked = true;
                break;
        }
    }

    private void DefaultModeRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded || DefaultModeRadio.IsChecked != true) return;
        DefaultSettings.FsgDefaultMode = nameof(FsgDefaultMode.Default);
        DefaultSettings.FSGMakeSingleLineToggle = false;
        DefaultSettings.Save();
    }

    private void SingleLineModeRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded || SingleLineModeRadio.IsChecked != true) return;
        // SingleLine uses the legacy flag, FsgDefaultMode stays Default
        DefaultSettings.FSGMakeSingleLineToggle = true;
        DefaultSettings.FsgDefaultMode = nameof(FsgDefaultMode.Default);
        DefaultSettings.Save();
    }

    private void TableModeRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded || TableModeRadio.IsChecked != true) return;
        DefaultSettings.FsgDefaultMode = nameof(FsgDefaultMode.Table);
        DefaultSettings.FSGMakeSingleLineToggle = false;
        DefaultSettings.Save();
    }

    private void SendToEtwCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        DefaultSettings.FsgSendEtwToggle = SendToEtwCheckBox.IsChecked == true;
        DefaultSettings.Save();
    }

    private void TryInsertCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        bool enabled = TryInsertCheckBox.IsChecked == true;
        DefaultSettings.TryInsert = enabled;
        DefaultSettings.Save();
        InsertDelaySlider.IsEnabled = enabled;
    }

    private void ShadeOverlayCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        bool isChecked = sender is CheckBox cb && cb.IsChecked == true;
        DefaultSettings.FsgShadeOverlay = isChecked;
        DefaultSettings.Save();
    }

    private void InsertDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        double newVal = Math.Round(InsertDelaySlider.Value, 1);
        DefaultSettings.InsertDelay = newVal;
        DefaultSettings.Save();
        InsertDelayValueText.Text = newVal.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private void GuidFixRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        SavePostGrabActionDefault(GuidFixOffRadio, GuidFixLastUsedRadio, GuidFixOnRadio, 
            value => DefaultSettings.FsgGuidFixDefault = value);
    }

    private void TrimEachLineRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        SavePostGrabActionDefault(TrimEachLineOffRadio, TrimEachLineLastUsedRadio, TrimEachLineOnRadio,
            value => DefaultSettings.FsgTrimEachLineDefault = value);
    }

    private void RemoveDuplicatesRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        SavePostGrabActionDefault(RemoveDuplicatesOffRadio, RemoveDuplicatesLastUsedRadio, RemoveDuplicatesOnRadio,
            value => DefaultSettings.FsgRemoveDuplicatesDefault = value);
    }

    private void WebSearchRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        SavePostGrabActionDefault(WebSearchOffRadio, WebSearchLastUsedRadio, WebSearchOnRadio,
            value => DefaultSettings.FsgWebSearchDefault = value);
    }

    private void InsertTextRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        SavePostGrabActionDefault(InsertTextOffRadio, InsertTextLastUsedRadio, InsertTextOnRadio,
            value => DefaultSettings.FsgInsertTextDefault = value);
    }

    private void TranslateRadio_Click(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        SavePostGrabActionDefault(TranslateOffRadio, TranslateLastUsedRadio, TranslateOnRadio,
            value => DefaultSettings.FsgTranslateDefault = value);
    }

    private void SavePostGrabActionDefault(RadioButton offRadio, RadioButton lastUsedRadio, RadioButton onRadio, Action<string> saveSetting)
    {
        PostGrabActionDefault mode = PostGrabActionDefault.Off;
        if (lastUsedRadio.IsChecked == true)
            mode = PostGrabActionDefault.LastUsed;
        else if (onRadio.IsChecked == true)
            mode = PostGrabActionDefault.On;

        saveSetting(mode.ToString());
        DefaultSettings.Save();
    }
}
