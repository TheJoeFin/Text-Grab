using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.ApplicationModel;

namespace Text_Grab;

/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
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

        if (!ImplementAppOptions.IsPackaged())
            OpenExeFolderButton.Visibility = Visibility.Visible;
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

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult areYouSure = MessageBox.Show("Are you sure you want to delete all history?", "Reset Settings to Default", MessageBoxButton.YesNo);

        if (areYouSure != MessageBoxResult.Yes)
            return;

        Singleton<HistoryService>.Instance.DeleteHistory();
    }

    private void CloseBTN_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private bool HotKeysAllDifferent()
    {
        bool anyMatchingKeys = false;

        HashSet<ShortcutControl> shortcuts = new();

        foreach (var child in ShortcutsStackPanel.Children)
            if (child is ShortcutControl shortcutControl)
                shortcuts.Add(shortcutControl);

        if (shortcuts.Count == 0)
            return false;

        foreach (ShortcutControl shortcut in shortcuts)
        {
            ShortcutKeySet keySet = shortcut.KeySet;
            bool isThisShortcutGood = true;

            foreach (ShortcutControl shortcut2 in shortcuts)
            {
                if (shortcut == shortcut2)
                    continue;

                if (keySet.Equals(shortcut2.KeySet))
                {
                    shortcut.BorderBrush = BadBrush;
                    shortcut2.BorderBrush = BadBrush;
                    anyMatchingKeys = true;
                    isThisShortcutGood = false;
                }
            }

            if (isThisShortcutGood)
                shortcut.BorderBrush = GoodBrush;
        }

        if (anyMatchingKeys)
            return false;

        return true;
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void MoreInfoHyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (TessMoreInfoBorder.Visibility == Visibility.Visible)
            TessMoreInfoBorder.Visibility = Visibility.Collapsed;
        else
            TessMoreInfoBorder.Visibility = Visibility.Visible;
    }

    private void OpenExeFolderButton_Click(object sender, RoutedEventArgs ev)
    {
        if (Path.GetDirectoryName(System.AppContext.BaseDirectory) is not string exePath)
            return;

        Uri source = new(exePath, UriKind.Absolute);
        System.Windows.Navigation.RequestNavigateEventArgs e = new(source, exePath);
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OpenPathButton_Click(object sender, RoutedEventArgs ev)
    {
        if (TesseractPathTextBox.Text is not string pathTextBox || !File.Exists(TesseractPathTextBox.Text))
            return;

        string? tesseractExePath = Path.GetDirectoryName(pathTextBox);

        if (tesseractExePath is null)
            return;

        Uri source = new(tesseractExePath, UriKind.Absolute);
        System.Windows.Navigation.RequestNavigateEventArgs e = new(source, tesseractExePath);
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult areYouSure = MessageBox.Show("Are you sure you want to reset all settings to default?", "Reset Settings to Default", MessageBoxButton.YesNo);

        if (areYouSure != MessageBoxResult.Yes)
            return;

        Settings.Default.Reset();
        Singleton<HistoryService>.Instance.DeleteHistory();
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

        if (RunInBackgroundChkBx.IsChecked is bool runInBackground)
        {
            Settings.Default.RunInTheBackground = runInBackground;
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

        if (HistorySwitch.IsChecked is not null)
            Settings.Default.UseHistory = (bool)HistorySwitch.IsChecked;

        if (HotKeysAllDifferent())
        {
            List<ShortcutKeySet> shortcutKeys = new();

            foreach (var child in ShortcutsStackPanel.Children)
                if (child is ShortcutControl control)
                    shortcutKeys.Add(control.KeySet);

            ShortcutKeysUtilities.SaveShortcutKeySetSettings(shortcutKeys);
        }

        if (!string.IsNullOrEmpty(SecondsTextBox.Text))
            Settings.Default.InsertDelay = InsertDelaySeconds;

        if (File.Exists(TesseractPathTextBox.Text))
            Settings.Default.TesseractPath = TesseractPathTextBox.Text;

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

    private void ShortcutControl_Recording(object sender, EventArgs e)
    {
        if (App.Current is App app)
            NotifyIconUtilities.UnregisterHotkeys(app);

        foreach (var child in ShortcutsStackPanel.Children)
            if (child is ShortcutControl shortcutControl
                && sender is ShortcutControl senderShortcut
                && shortcutControl != senderShortcut)
                shortcutControl.StopRecording(sender);
    }

    private void ShortcutControl_KeySetChanged(object sender, EventArgs e)
    {
        HotKeysAllDifferent();
    }

    private void TesseractPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox pathTextbox || pathTextbox.Text is not string pathText)
            return;

        if (File.Exists(pathText))
            UseTesseractCheckBox.IsEnabled = true;
        else
            UseTesseractCheckBox.IsEnabled = false;
    }

    private void TessInfoCloseHypBtn_Click(object sender, RoutedEventArgs e)
    {
        TessMoreInfoBorder.Visibility = Visibility.Collapsed;
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
        HistorySwitch.IsChecked = Settings.Default.UseHistory;

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

        TextGrabMode defaultLaunchSetting = Enum.Parse<TextGrabMode>(Settings.Default.DefaultLaunch, true);
        switch (defaultLaunchSetting)
        {
            case TextGrabMode.Fullscreen:
                FullScreenRDBTN.IsChecked = true;
                break;
            case TextGrabMode.GrabFrame:
                GrabFrameRDBTN.IsChecked = true;
                break;
            case TextGrabMode.EditText:
                EditTextRDBTN.IsChecked = true;
                break;
            case TextGrabMode.QuickLookup:
                QuickLookupRDBTN.IsChecked = true;
                break;
            default:
                FullScreenRDBTN.IsChecked = true;
                break;
        }

        IEnumerable<ShortcutKeySet> shortcutKeySets = ShortcutKeysUtilities.GetShortcutKeySetsFromSettings();

        foreach (ShortcutKeySet keySet in shortcutKeySets)
        {
            switch (keySet.Action)
            {
                case ShortcutKeyActions.None:
                    break;
                case ShortcutKeyActions.Settings:
                    break;
                case ShortcutKeyActions.Fullscreen:
                    FsgShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.GrabFrame:
                    GfShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.Lookup:
                    QslShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.EditWindow:
                    EtwShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.PreviousRegionGrab:
                    GlrShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.PreviousEditWindow:
                    LetwShortcutControl.KeySet = keySet;
                    break;
                case ShortcutKeyActions.PreviousGrabFrame:
                    LgfShortcutControl.KeySet = keySet;
                    break;
                default:
                    break;
            }
        }

        if (TesseractHelper.CanLocateTesseractExe())
        {
            UseTesseractCheckBox.IsChecked = Settings.Default.UseTesseract;
            TesseractPathTextBox.Text = Settings.Default.TesseractPath;
        }
        else
        {
            UseTesseractCheckBox.IsChecked = false;
            UseTesseractCheckBox.IsEnabled = false;
            Settings.Default.UseTesseract = false;
            Settings.Default.Save();
            CouldNotFindTessTxtBlk.Visibility = Visibility.Visible;
        }
    }
    private void WinGetCodeCopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(WinGetInstallTextBox.Text);
    }
    #endregion Methods
}

