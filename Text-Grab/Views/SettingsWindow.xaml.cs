using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.ApplicationModel;
using Wpf.Ui.Controls.Window;

namespace Text_Grab;

/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class SettingsWindow : FluentWindow
{
    #region Fields

    private Brush BadBrush = new SolidColorBrush(Colors.Red);
    private Brush GoodBrush = new SolidColorBrush(Colors.Transparent);

    #endregion Fields

    #region Constructors

    public SettingsWindow()
    {
        InitializeComponent();
        App.SetTheme();
    }

    #endregion Constructors

    #region Properties

    public double InsertDelaySeconds { get; set; } = 3;

    #endregion Properties

    #region Methods

    private void AboutBTN_Click(object sender, RoutedEventArgs e)
    {
        ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;
        ErrorCorrectBox.IsChecked = Settings.Default.CorrectErrors;
        NeverUseClipboardChkBx.IsChecked = Settings.Default.NeverAutoUseClipboard;
        RunInBackgroundChkBx.IsChecked = Settings.Default.RunInTheBackground;
        TryInsertCheckbox.IsChecked = Settings.Default.TryInsert;
        GlobalHotkeysCheckbox.IsChecked = Settings.Default.GlobalHotkeysEnabled;
        ReadBarcodesBarcode.IsChecked = Settings.Default.TryToReadBarcodes;
        CorrectToLatin.IsChecked = Settings.Default.CorrectToLatin;
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is FirstRunWindow firstRunWindowOpen)
            {
                firstRunWindowOpen.Activate();
                return;
            }
        }

        FirstRunWindow frw = new();
        frw.Show();
    }

    private void CloseBTN_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private bool HotKeysAllDifferent()
    {
        if (EditTextHotKeyTextBox is null
            || FullScreenHotkeyTextBox is null
            || GrabFrameHotkeyTextBox is null
            || LookupHotKeyTextBox is null)
            return false;

        string gfKey = GrabFrameHotkeyTextBox.Text.Trim().ToUpper();
        string fsgKey = FullScreenHotkeyTextBox.Text.Trim().ToUpper();
        string etwKey = EditTextHotKeyTextBox.Text.Trim().ToUpper();
        string qslKey = LookupHotKeyTextBox.Text.Trim().ToUpper();

        bool anyMatchingKeys = false;

        if (!string.IsNullOrEmpty(gfKey))
        {
            if (gfKey == fsgKey
                || gfKey == etwKey
                || gfKey == qslKey)
            {
                GrabFrameHotkeyTextBox.BorderBrush = BadBrush;
                anyMatchingKeys = true;
            }
            else
                GrabFrameHotkeyTextBox.BorderBrush = GoodBrush;
        }

        if (!string.IsNullOrEmpty(fsgKey))
        {
            if (fsgKey == gfKey
                || fsgKey == etwKey
                || fsgKey == qslKey)
            {
                FullScreenHotkeyTextBox.BorderBrush = BadBrush;
                anyMatchingKeys = true;
            }
            else
                FullScreenHotkeyTextBox.BorderBrush = GoodBrush;
        }

        if (!string.IsNullOrEmpty(etwKey))
        {
            if (etwKey == gfKey
                || etwKey == fsgKey
                || etwKey == qslKey)
            {
                EditTextHotKeyTextBox.BorderBrush = BadBrush;
                anyMatchingKeys = true;
            }
            else
                EditTextHotKeyTextBox.BorderBrush = GoodBrush;
        }

        if (!string.IsNullOrEmpty(qslKey))
        {
            if (qslKey == gfKey
                || qslKey == fsgKey
                || qslKey == etwKey)
            {
                LookupHotKeyTextBox.BorderBrush = BadBrush;
                anyMatchingKeys = true;
            }
            else
                LookupHotKeyTextBox.BorderBrush = GoodBrush;
        }

        if (anyMatchingKeys)
            return false;

        return true;
    }

    private void HotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox hotkeytextbox
            || hotkeytextbox.Text is null
            || !IsLoaded)
            return;

        if (string.IsNullOrEmpty(hotkeytextbox.Text))
        {
            hotkeytextbox.BorderBrush = GoodBrush;
            return;
        }

        hotkeytextbox.Text = hotkeytextbox.Text[0].ToString();

        KeyConverter keyConverter = new();
        Key? convertedKey = (Key?)keyConverter.ConvertFrom(hotkeytextbox.Text.ToUpper());
        if (convertedKey is not null && HotKeysAllDifferent())
        {
            hotkeytextbox.BorderBrush = GoodBrush;
            return;
        }

        hotkeytextbox.BorderBrush = BadBrush;
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult areYouSure = MessageBox.Show("Are you sure you want to reset all settings to default?", "Reset Settings to Default", MessageBoxButton.YesNo);

        if (areYouSure != MessageBoxResult.Yes)
            return;

        Settings.Default.Reset();
        this.Close();
    }

    private async void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        if (ShowToastCheckBox.IsChecked is bool showToast)
            Settings.Default.ShowToast = showToast;
        if (SystemThemeRdBtn.IsChecked is true)
            Settings.Default.AppTheme = "System";
        else if (LightThemeRdBtn.IsChecked is true)
            Settings.Default.AppTheme = "Light";
        else if (DarkThemeRdBtn.IsChecked is true)
            Settings.Default.AppTheme = "Dark";

        if (ShowToastCheckBox.IsChecked != null)
            Settings.Default.ShowToast = (bool)ShowToastCheckBox.IsChecked;

        if (FullScreenRDBTN.IsChecked is true)
            Settings.Default.DefaultLaunch = "Fullscreen";
        else if (GrabFrameRDBTN.IsChecked is true)
            Settings.Default.DefaultLaunch = "GrabFrame";
        else if (EditTextRDBTN.IsChecked is true)
            Settings.Default.DefaultLaunch = "EditText";
        else if (QuickLookupRDBTN.IsChecked is true)
            Settings.Default.DefaultLaunch = "QuickLookup";

        if (ErrorCorrectBox.IsChecked is bool errorCorrect)
            Settings.Default.CorrectErrors = errorCorrect;

        if (NeverUseClipboardChkBx.IsChecked is bool neverClipboard)
            Settings.Default.NeverAutoUseClipboard = neverClipboard;

        if (RunInBackgroundChkBx.IsChecked is bool runInBackaground)
        {
            Settings.Default.RunInTheBackground = runInBackaground;
            ImplementAppOptions.ImplementBackgroundOption(Settings.Default.RunInTheBackground);
        }

        if (TryInsertCheckbox.IsChecked is bool tryInsert)
            Settings.Default.TryInsert = tryInsert;

        if (StartupOnLoginCheckBox.IsChecked is bool startupOnLogin)
        {
            Settings.Default.StartupOnLogin = startupOnLogin;
            await ImplementAppOptions.ImplementStartupOption(Settings.Default.StartupOnLogin);
        }

        if (GlobalHotkeysCheckbox.IsChecked is bool globalHotKeys)
            Settings.Default.GlobalHotkeysEnabled = globalHotKeys;

        if (ReadBarcodesBarcode.IsChecked is bool readBarcodes)
            Settings.Default.TryToReadBarcodes = readBarcodes;

        if (UseTesseractCheckBox.IsChecked is bool useTesseract)
            Settings.Default.UseTesseract = useTesseract;

        if (ReadBarcodesBarcode.IsChecked is not null)
            Settings.Default.TryToReadBarcodes = (bool)ReadBarcodesBarcode.IsChecked;

        if (CorrectToLatin.IsChecked is not null)
            Settings.Default.CorrectToLatin = (bool)CorrectToLatin.IsChecked;

        if (HotKeysAllDifferent())
        {
            KeyConverter keyConverter = new();
            Key? fullScreenKey = (Key?)keyConverter.ConvertFrom(FullScreenHotkeyTextBox.Text.ToUpper());
            if (fullScreenKey is not null)
                Settings.Default.FullscreenGrabHotKey = FullScreenHotkeyTextBox.Text.ToUpper();

            Key? grabFrameKey = (Key?)keyConverter.ConvertFrom(GrabFrameHotkeyTextBox.Text.ToUpper());
            if (grabFrameKey is not null)
                Settings.Default.GrabFrameHotkey = GrabFrameHotkeyTextBox.Text.ToUpper();

            Key? editWindowKey = (Key?)keyConverter.ConvertFrom(EditTextHotKeyTextBox.Text.ToUpper());
            if (editWindowKey is not null)
                Settings.Default.EditWindowHotKey = EditTextHotKeyTextBox.Text.ToUpper();

            Key? lookupKey = (Key?)keyConverter.ConvertFrom(LookupHotKeyTextBox.Text.ToUpper());
            if (lookupKey is not null)
                Settings.Default.LookupHotKey = LookupHotKeyTextBox.Text.ToUpper();
        }
        else
        {
            Settings.Default.FullscreenGrabHotKey = "F";
            Settings.Default.GrabFrameHotkey = "G";
            Settings.Default.EditWindowHotKey = "E";
            Settings.Default.LookupHotKey = "Q";
        }

        if (!string.IsNullOrEmpty(SecondsTextBox.Text))
            Settings.Default.InsertDelay = InsertDelaySeconds;

        Settings.Default.Save();

        App app = (App)App.Current;
        if (app.TextGrabIcon != null)
        {
            NotifyIconUtilities.UnregisterHotkeys(app);
            NotifyIconUtilities.RegisterHotKeys(app);
        }
        App.SetTheme();

        Close();
    }

    private void ValidateTextIsNumber(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is TextBox numberInputBox)
        {
            bool wasAbleToConvert = double.TryParse(numberInputBox.Text, out double parsedText);
            if (wasAbleToConvert && parsedText > 0 && parsedText < 10)
            {
                InsertDelaySeconds = parsedText;
                DelayTimeErrorSeconds.Visibility = Visibility.Collapsed;
                numberInputBox.BorderBrush = new SolidColorBrush(Colors.Transparent);
            }
            else
            {
                InsertDelaySeconds = 3;
                DelayTimeErrorSeconds.Visibility = Visibility.Visible;
                numberInputBox.BorderBrush = new SolidColorBrush(Colors.Red);
            }
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        WindowUtilities.ShouldShutDown();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        AppTheme appTheme = Enum.Parse<AppTheme>(Settings.Default.AppTheme, true);
        switch (appTheme)
        {
            case AppTheme.System:
                SystemThemeRdBtn.IsChecked = true;
                break;
            case AppTheme.Dark:
                DarkThemeRdBtn.IsChecked = true;
                break;
            case AppTheme.Light:
                LightThemeRdBtn.IsChecked = true;
                break;
            default:
                SystemThemeRdBtn.IsChecked = true;
                break;
        }

        ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;
        ErrorCorrectBox.IsChecked = Settings.Default.CorrectErrors;
        NeverUseClipboardChkBx.IsChecked = Settings.Default.NeverAutoUseClipboard;
        RunInBackgroundChkBx.IsChecked = Settings.Default.RunInTheBackground;
        TryInsertCheckbox.IsChecked = Settings.Default.TryInsert;
        GlobalHotkeysCheckbox.IsChecked = Settings.Default.GlobalHotkeysEnabled;
        ReadBarcodesBarcode.IsChecked = Settings.Default.TryToReadBarcodes;
        CorrectToLatin.IsChecked = Settings.Default.CorrectToLatin;
        UseTesseractCheckBox.IsChecked = Settings.Default.UseTesseract;

        InsertDelaySeconds = Settings.Default.InsertDelay;
        SecondsTextBox.Text = InsertDelaySeconds.ToString("##.#", System.Globalization.CultureInfo.InvariantCulture);


        if (ImplementAppOptions.IsPackaged())
        {
            StartupTask startupTask = await StartupTask.GetAsync("StartTextGrab");

            switch (startupTask.State)
            {
                case StartupTaskState.Disabled:
                    // Task is disabled but can be enabled.
                    StartupOnLoginCheckBox.IsChecked = false;
                    break;
                case StartupTaskState.DisabledByUser:
                    // Task is disabled and user must enable it manually.
                    StartupOnLoginCheckBox.IsChecked = false;
                    StartupOnLoginCheckBox.IsEnabled = false;

                    StartupTextBlock.Text += "\nDisabled in Task Manager";
                    break;
                case StartupTaskState.Enabled:
                    StartupOnLoginCheckBox.IsChecked = true;
                    break;
            }
        }
        else
        {
            StartupOnLoginCheckBox.IsChecked = Settings.Default.StartupOnLogin;
        }

        DefaultLaunchSetting defaultLaunchSetting = Enum.Parse<DefaultLaunchSetting>(Settings.Default.DefaultLaunch, true);
        switch (defaultLaunchSetting)
        {
            case DefaultLaunchSetting.Fullscreen:
                FullScreenRDBTN.IsChecked = true;
                break;
            case DefaultLaunchSetting.GrabFrame:
                GrabFrameRDBTN.IsChecked = true;
                break;
            case DefaultLaunchSetting.EditText:
                EditTextRDBTN.IsChecked = true;
                break;
            case DefaultLaunchSetting.QuickLookup:
                QuickLookupRDBTN.IsChecked = true;
                break;
            default:
                FullScreenRDBTN.IsChecked = true;
                break;
        }

        FullScreenHotkeyTextBox.Text = Settings.Default.FullscreenGrabHotKey;
        GrabFrameHotkeyTextBox.Text = Settings.Default.GrabFrameHotkey;
        EditTextHotKeyTextBox.Text = Settings.Default.EditWindowHotKey;
        LookupHotKeyTextBox.Text = Settings.Default.LookupHotKey;
    }

    #endregion Methods

    private void MoreInfoHyperlink_Click(object sender, RoutedEventArgs e)
    {
        TessMoreInfoBorder.Visibility = Visibility.Visible;
    }
    private void TessInfoCloseHypBtn_Click(object sender, RoutedEventArgs e)
    {
        TessMoreInfoBorder.Visibility = Visibility.Collapsed;
    }
}

