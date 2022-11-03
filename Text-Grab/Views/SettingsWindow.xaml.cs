using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.ApplicationModel;

namespace Text_Grab;

/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class SettingsWindow : Window
{
    public double InsertDelaySeconds { get; set; } = 3;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ShowToastCheckBox.IsChecked = Settings.Default.ShowToast;
        ErrorCorrectBox.IsChecked = Settings.Default.CorrectErrors;
        NeverUseClipboardChkBx.IsChecked = Settings.Default.NeverAutoUseClipboard;
        RunInBackgroundChkBx.IsChecked = Settings.Default.RunInTheBackground;
        TryInsertCheckbox.IsChecked = Settings.Default.TryInsert;
        GlobalHotkeysCheckbox.IsChecked = Settings.Default.GlobalHotkeysEnabled;
        ReadBarcodesBarcode.IsChecked = Settings.Default.TryToReadBarcodes;
        UseTesseractCheckBox.IsChecked = Settings.Default.UserTesseract;

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

        switch (Settings.Default.DefaultLaunch)
        {
            case "Fullscreen":
                FullScreenRDBTN.IsChecked = true;
                break;
            case "GrabFrame":
                GrabFrameRDBTN.IsChecked = true;
                break;
            case "EditText":
                EditTextRDBTN.IsChecked = true;
                break;
            case "QuickLookup":
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

    private void ValidateTextIsNumber(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded == false)
            return;

        if (sender is TextBox numberInputBox)
        {
            bool wasAbleToConvert = double.TryParse(numberInputBox.Text, out double parsedText);
            if (wasAbleToConvert == true && parsedText > 0 && parsedText < 10)
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

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void CloseBTN_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        if (ShowToastCheckBox.IsChecked is bool showToast)
            Settings.Default.ShowToast = showToast;

        if (FullScreenRDBTN.IsChecked == true)
            Settings.Default.DefaultLaunch = "Fullscreen";
        else if (GrabFrameRDBTN.IsChecked == true)
            Settings.Default.DefaultLaunch = "GrabFrame";
        else if (EditTextRDBTN.IsChecked == true)
            Settings.Default.DefaultLaunch = "EditText";
        else if (QuickLookupRDBTN.IsChecked == true)
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
            Settings.Default.UserTesseract = useTesseract;

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

        if (string.IsNullOrEmpty(SecondsTextBox.Text) == false)
            Settings.Default.InsertDelay = InsertDelaySeconds;

        Settings.Default.Save();

        App app = (App)App.Current;
        if (app.TextGrabIcon != null)
        {
            NotifyIconUtilities.UnregisterHotkeys(app);
            NotifyIconUtilities.RegisterHotKeys(app);
        }


        Close();
    }

    private void AboutBTN_Click(object sender, RoutedEventArgs e)
    {
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

    private void Window_Closed(object? sender, EventArgs e)
    {
        WindowUtilities.ShouldShutDown();
    }

    private void HotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox hotkeytextbox)
            return;

        if (hotkeytextbox.Text.Length == 1 && hotkeytextbox.Text is not null)
        {
            KeyConverter keyConverter = new();
            Key? convertedKey = (Key?)keyConverter.ConvertFrom(hotkeytextbox.Text.ToUpper());
            if (convertedKey is not null && HotKeysAllDifferent())
            {
                hotkeytextbox.BorderBrush = new SolidColorBrush(Colors.Transparent);
                return;
            }
        }

        hotkeytextbox.BorderBrush = new SolidColorBrush(Colors.Red);
    }

    private bool HotKeysAllDifferent()
    {
        if (EditTextHotKeyTextBox is null
            || FullScreenHotkeyTextBox is null
            || GrabFrameHotkeyTextBox is null
            || LookupHotKeyTextBox is null)
            return false;

        if (GrabFrameHotkeyTextBox.Text.ToUpper() != FullScreenHotkeyTextBox.Text.ToUpper()
            && GrabFrameHotkeyTextBox.Text.ToUpper() != EditTextHotKeyTextBox.Text.ToUpper()
            && GrabFrameHotkeyTextBox.Text.ToUpper() != LookupHotKeyTextBox.Text.ToUpper()
            && FullScreenHotkeyTextBox.Text.ToUpper() != LookupHotKeyTextBox.Text.ToUpper()
            && FullScreenHotkeyTextBox.Text.ToUpper() != EditTextHotKeyTextBox.Text.ToUpper()
            && EditTextHotKeyTextBox.Text.ToUpper() != LookupHotKeyTextBox.Text.ToUpper())
        {
            FullScreenHotkeyTextBox.BorderBrush = new SolidColorBrush(Colors.Transparent);
            GrabFrameHotkeyTextBox.BorderBrush = new SolidColorBrush(Colors.Transparent);
            EditTextHotKeyTextBox.BorderBrush = new SolidColorBrush(Colors.Transparent);
            LookupHotKeyTextBox.BorderBrush = new SolidColorBrush(Colors.Transparent);
            return true;
        }

        FullScreenHotkeyTextBox.BorderBrush = new SolidColorBrush(Colors.Red);
        GrabFrameHotkeyTextBox.BorderBrush = new SolidColorBrush(Colors.Red);
        EditTextHotKeyTextBox.BorderBrush = new SolidColorBrush(Colors.Red);
        LookupHotKeyTextBox.BorderBrush = new SolidColorBrush(Colors.Red);
        return false;
    }
}

