using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Windows.ApplicationModel;
using Wpf.Ui.Controls;

namespace Text_Grab.Pages;

/// <summary>
/// Interaction logic for GeneralSettings.xaml
/// </summary>
public partial class GeneralSettings : Page
{
    #region Fields

    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private const double DisabledOpacity = 0.55;
    private bool settingsSet = false;

    #endregion Fields

    public GeneralSettings()
    {
        InitializeComponent();

        if (!AppUtilities.IsPackaged())
            OpenExeFolderButton.Visibility = Visibility.Visible;

        VersionTextblock.Text = $"Version {AppUtilities.GetAppVersion()}";
    }

    private void OpenExeFolderButton_Click(object sender, RoutedEventArgs args)
    {
        if (Path.GetDirectoryName(FileUtilities.GetExePath()) is not string exePath)
            return;

        Uri source = new(exePath, UriKind.Absolute);
        System.Windows.Navigation.RequestNavigateEventArgs e = new(source, exePath);
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void AboutBTN_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.OpenOrActivateWindow<FirstRunWindow>();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        AppTheme appTheme = Enum.Parse<AppTheme>(DefaultSettings.AppTheme, true);
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

        TrayIconStyle trayIconStyle = Enum.TryParse(DefaultSettings.TrayIconStyle, true, out TrayIconStyle parsedTrayIconStyle)
            ? parsedTrayIconStyle
            : TrayIconStyle.Color;
        switch (trayIconStyle)
        {
            case TrayIconStyle.Monochrome:
                MonochromeTrayIconRdBtn.IsChecked = true;
                break;
            case TrayIconStyle.Color:
            default:
                ColorTrayIconRdBtn.IsChecked = true;
                break;
        }

        bool flyoutOnClick = Enum.TryParse(DefaultSettings.TrayLeftClickAction, true, out TrayLeftClickAction leftClick)
            && leftClick == TrayLeftClickAction.Flyout;
        TrayClickFlyoutRdBtn.IsChecked = flyoutOnClick;
        TrayClickDefaultLaunchRdBtn.IsChecked = !flyoutOnClick;

        TrayFlyoutSources flyoutSources = TrayFlyoutItemProvider.ParseSources(DefaultSettings.TrayFlyoutSources);
        FlyoutEditTextChkBx.IsChecked = flyoutSources.HasFlag(TrayFlyoutSources.EditText);
        FlyoutGrabFramesChkBx.IsChecked = flyoutSources.HasFlag(TrayFlyoutSources.GrabFrames);
        FlyoutFilesChkBx.IsChecked = flyoutSources.HasFlag(TrayFlyoutSources.Files);
        FlyoutItemCountBox.Value = TrayFlyoutItemProvider.ClampItemCount(DefaultSettings.TrayFlyoutItemCount);

        RenderingMode renderingMode = RenderingUtilities.ParseRenderingMode(DefaultSettings.RenderingMode);
        RenderingAutoRadio.IsChecked = renderingMode == RenderingMode.Auto;
        RenderingHardwareRadio.IsChecked = renderingMode == RenderingMode.Hardware;
        RenderingSoftwareRadio.IsChecked = renderingMode == RenderingMode.Software;

        TextGrabMode defaultLaunchSetting = Enum.Parse<TextGrabMode>(DefaultSettings.DefaultLaunch, true);
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

        if (AppUtilities.IsPackaged())
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
                    SetEnabledState(StartupCard, false);

                    StartupNotice.Message = "Windows has blocked Text Grab from starting at sign-in. "
                        + "Turn Text Grab back on in Windows Settings under Apps > Startup (or in Task Manager's Startup apps tab), then reopen this page.";
                    StartupNotice.ActionText = "Open Startup settings";
                    break;
                case StartupTaskState.Enabled:
                    StartupOnLoginCheckBox.IsChecked = true;
                    break;
            }
        }
        else
        {
            StartupOnLoginCheckBox.IsChecked = DefaultSettings.StartupOnLogin;
        }

        List<WebSearchUrlModel> searcherSettings = Singleton<WebSearchUrlCatalog>.Instance.WebSearchers;

        WebSearchersComboBox.Items.Clear();
        foreach (WebSearchUrlModel searcher in searcherSettings)
            WebSearchersComboBox.Items.Add(searcher);

        WebSearchersComboBox.SelectedItem = Singleton<WebSearchUrlCatalog>.Instance.DefaultSearcher;

        ShowToastCheckBox.IsChecked = DefaultSettings.ShowToast;

        RunInBackgroundChkBx.IsChecked = DefaultSettings.RunInTheBackground;
        ReadBarcodesBarcode.IsChecked = DefaultSettings.TryToReadBarcodes;
        HdrCaptureCorrectionToggle.IsChecked = DefaultSettings.HdrCaptureCorrection;
        HistorySwitch.IsChecked = DefaultSettings.UseHistory;
        ErrorCorrectBox.IsChecked = DefaultSettings.CorrectErrors;
        CorrectToLatin.IsChecked = DefaultSettings.CorrectToLatin;
        ParagraphDetectionToggle.IsChecked = DefaultSettings.ParagraphDetection;
        // The setting is stored as "never copy"; the toggle reads as "copy".
        CopyToClipboardToggle.IsChecked = !DefaultSettings.NeverAutoUseClipboard;
        TryInsertCheckbox.IsChecked = DefaultSettings.TryInsert;
        InsertDelayBox.Value = Math.Clamp(DefaultSettings.InsertDelay, InsertDelayBox.Minimum, InsertDelayBox.Maximum);

        // Context menu integration - only available for unpackaged apps
        if (!AppUtilities.IsPackaged())
        {
            AddToContextMenuCheckBox.IsChecked = ContextMenuUtilities.IsRegisteredInContextMenu();
            RegisterOpenWithCheckBox.IsChecked = DefaultSettings.RegisterOpenWith;
        }
        else
        {
            AddToContextMenuCheckBox.IsChecked = false;
            RegisterOpenWithCheckBox.IsChecked = false;
            SetEnabledState(ContextMenuCard, false);
            SetEnabledState(OpenWithCard, false);

            ContextMenuNotice.Message = "Not available in the Microsoft Store version of Text Grab. "
                + "Install the standalone version from GitHub to add Text Grab to the right-click menu.";
            OpenWithNotice.Message = "Not needed in the Microsoft Store version. "
                + "It already appears in \"Open with\" for supported images and PDFs through its app package.";
        }

        UpdateDependentSettings();
        settingsSet = true;
    }

    /// <summary>
    /// Greys out a setting and blocks input, or restores it. Always pair a disabled
    /// state with a SettingsNotice that tells the user why and how to fix it.
    /// </summary>
    private static void SetEnabledState(UIElement element, bool isEnabled)
    {
        element.IsEnabled = isEnabled;
        element.Opacity = isEnabled ? 1 : DisabledOpacity;
    }

    /// <summary>
    /// Re-evaluates settings that depend on other settings, enabling or disabling them
    /// and showing or hiding the notice that explains why.
    /// </summary>
    private void UpdateDependentSettings()
    {
        // Tray icon: only exists while Text Grab runs in the background.
        bool runInBackground = RunInBackgroundChkBx.IsChecked is true;
        SetEnabledState(TrayIconStyleExpander, runInBackground);
        SetEnabledState(TrayLeftClickExpander, runInBackground);
        TrayNeedsBackgroundNotice.Message = runInBackground
            ? string.Empty
            : "These settings don't apply right now because the tray icon only appears while Text Grab runs in the background.";
        TrayNeedsBackgroundNotice.ActionText = runInBackground ? string.Empty : "Run in the background";

        // Tray flyout recent items come from history, which is not recorded while history is off.
        bool historyOn = HistorySwitch.IsChecked is true;
        TrayHistoryNotice.Message = runInBackground && !historyOn
            ? "History is turned off, so the flyout's recent items won't update until you turn it back on."
            : string.Empty;
        TrayHistoryNotice.ActionText = TrayHistoryNotice.Message.Length > 0 ? "Turn on history" : string.Empty;

        // Pasting sends Ctrl+V, so it needs the grabbed text to be on the clipboard.
        bool copyOn = CopyToClipboardToggle.IsChecked is true;
        SetEnabledState(TryInsertExpander, copyOn);
        TryInsertNotice.Message = copyOn
            ? string.Empty
            : "Pasting after a grab needs the text on the clipboard, and copying is turned off.";
        TryInsertNotice.ActionText = copyOn ? string.Empty : "Turn on copying";
    }

    private void SetToggleAndSave(ToggleSwitch toggle)
    {
        // Setting IsChecked raises Checked, which runs the normal save handler.
        toggle.IsChecked = true;
        UpdateDependentSettings();
    }

    private void TrayNeedsBackgroundNotice_ActionClick(object sender, RoutedEventArgs e) => SetToggleAndSave(RunInBackgroundChkBx);

    private void TrayHistoryNotice_ActionClick(object sender, RoutedEventArgs e) => SetToggleAndSave(HistorySwitch);

    private void TryInsertNotice_ActionClick(object sender, RoutedEventArgs e) => SetToggleAndSave(CopyToClipboardToggle);

    private void StartupNotice_ActionClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true });
    }

    private void InsertDelayBox_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
    {
        if (!settingsSet || args.NewValue is not double value)
            return;

        DefaultSettings.InsertDelay = Math.Round(value, 1);
    }

    private void FullScreenRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.DefaultLaunch = TextGrabMode.Fullscreen.ToString();
    }

    private void GrabFrameRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.DefaultLaunch = TextGrabMode.GrabFrame.ToString();
    }

    private void EditTextRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.DefaultLaunch = TextGrabMode.EditText.ToString();
    }

    private void QuickLookupRDBTN_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.DefaultLaunch = TextGrabMode.QuickLookup.ToString();
    }

    private void RunInBackgroundChkBx_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        if (sender is not ToggleSwitch runInBackgroundSwitch)
            return;

        DefaultSettings.RunInTheBackground = runInBackgroundSwitch.IsChecked is true;
        ImplementAppOptions.ImplementBackgroundOption(DefaultSettings.RunInTheBackground);
        DefaultSettings.Save();
        UpdateDependentSettings();
    }

    private void SystemThemeRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.AppTheme = AppTheme.System.ToString();
        App.SetTheme();
    }

    private void LightThemeRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.AppTheme = AppTheme.Light.ToString();
        App.SetTheme();
    }

    private void DarkThemeRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.AppTheme = AppTheme.Dark.ToString();
        App.SetTheme();
    }

    private void ColorTrayIconRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.TrayIconStyle = TrayIconStyle.Color.ToString();
        DefaultSettings.Save();
        NotifyIconUtilities.RefreshTrayIconStyle();
    }

    private void MonochromeTrayIconRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.TrayIconStyle = TrayIconStyle.Monochrome.ToString();
        DefaultSettings.Save();
        NotifyIconUtilities.RefreshTrayIconStyle();
    }

    private void TrayLeftClickRdBtn_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet
            || sender is not RadioButton radioButton
            || !Enum.TryParse(radioButton.Tag?.ToString(), out TrayLeftClickAction action))
            return;

        DefaultSettings.TrayLeftClickAction = action.ToString();
        DefaultSettings.Save();
        NotifyIconUtilities.RefreshTrayIconStyle();
    }

    private void FlyoutSource_Changed(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        TrayFlyoutSources sources = TrayFlyoutSources.None;
        if (FlyoutEditTextChkBx.IsChecked == true)
            sources |= TrayFlyoutSources.EditText;
        if (FlyoutGrabFramesChkBx.IsChecked == true)
            sources |= TrayFlyoutSources.GrabFrames;
        if (FlyoutFilesChkBx.IsChecked == true)
            sources |= TrayFlyoutSources.Files;

        DefaultSettings.TrayFlyoutSources = TrayFlyoutItemProvider.FormatSources(sources);
        DefaultSettings.Save();
    }

    private void FlyoutItemCountBox_ValueChanged(object sender, Wpf.Ui.Controls.NumberBoxValueChangedEventArgs args)
    {
        if (!settingsSet || args.NewValue is not double value)
            return;

        DefaultSettings.TrayFlyoutItemCount = TrayFlyoutItemProvider.ClampItemCount((int)Math.Round(value));
        DefaultSettings.Save();
    }

    private void RenderingModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet
            || sender is not RadioButton radioButton
            || !Enum.TryParse(radioButton.Tag?.ToString(), out RenderingMode mode))
            return;

        // WPF fixes its render mode when the first window is created, so this applies on next launch.
        DefaultSettings.RenderingMode = mode.ToString();
        DefaultSettings.Save();
    }

    private void ReadBarcodesBarcode_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.TryToReadBarcodes = true;
    }

    private void ReadBarcodesBarcode_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.TryToReadBarcodes = false;
    }

    private void HdrCaptureCorrectionToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.HdrCaptureCorrection = true;
    }

    private void HdrCaptureCorrectionToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.HdrCaptureCorrection = false;
    }

    private async void CheckHdrPermissionButton_Click(object sender, RoutedEventArgs e)
    {
        CheckHdrPermissionButton.IsEnabled = false;

        Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus status =
            await Utilities.Hdr.HdrScreenCapture.RequestBorderlessAccessAsync();

        CheckHdrPermissionButton.IsEnabled = true;

        bool allowed = status == Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.Allowed;
        DefaultSettings.HdrBorderlessGranted = allowed;
        DefaultSettings.Save();

        string message = status switch
        {
            Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.Allowed
                => "Borderless capture is allowed. The yellow capture border will no longer appear.",
            Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.DeniedByUser
                => "Borderless capture was denied. You can allow it in Windows Settings under Privacy & security → Graphics capture (or the consent prompt).",
            Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.DeniedBySystem
                => "Borderless capture is blocked on this system, so the capture border can't be removed.",
            _ => "Borderless capture permission is currently unavailable.",
        };

        await new Wpf.Ui.Controls.MessageBox
        {
            Title = "HDR Capture Permission",
            Content = message,
            CloseButtonText = "OK"
        }.ShowDialogAsync();
    }

    private void HistorySwitch_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.UseHistory = true;
        UpdateDependentSettings();
    }

    private void HistorySwitch_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.UseHistory = false;
        UpdateDependentSettings();
    }

    private void ErrorCorrectBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.CorrectErrors = true;
    }

    private void ErrorCorrectBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.CorrectErrors = false;
    }

    private void ParagraphDetectionToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.ParagraphDetection = true;
    }

    private void ParagraphDetectionToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.ParagraphDetection = false;
    }

    private void CorrectToLatin_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.CorrectToLatin = true;
    }

    private void CorrectToLatin_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.CorrectToLatin = false;
    }

    private void CopyToClipboardToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.NeverAutoUseClipboard = false;
        UpdateDependentSettings();
    }

    private void CopyToClipboardToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.NeverAutoUseClipboard = true;
        UpdateDependentSettings();
    }

    private async void StartupOnLoginCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.StartupOnLogin = true;
        await ImplementAppOptions.ImplementStartupOption(true);
    }

    private async void StartupOnLoginCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.StartupOnLogin = false;
        await ImplementAppOptions.ImplementStartupOption(false);
    }

    private void TryInsertCheckbox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.TryInsert = true;
    }

    private void TryInsertCheckbox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.TryInsert = false;
    }

    private void ShowToastCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.ShowToast = true;
    }

    private void ShowToastCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        DefaultSettings.ShowToast = false;
    }

    private void WebSearchersComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!settingsSet
            || sender is not ComboBox comboBox
            || comboBox.SelectedItem is not WebSearchUrlModel newDefault)
            return;

        Singleton<WebSearchUrlCatalog>.Instance.DefaultSearcher = newDefault;
    }

    private async void AddToContextMenuCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        bool success = ContextMenuUtilities.AddToContextMenu(out string? errorMessage);
        if (success)
        {
            DefaultSettings.AddToContextMenu = true;
            DefaultSettings.Save();
        }
        else
        {
            // Revert the checkbox if registration failed
            settingsSet = false;
            AddToContextMenuCheckBox.IsChecked = false;
            settingsSet = true;

            // Show error message to user
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Context Menu Registration Failed",
                Content = errorMessage ?? "Failed to add Text Grab to the context menu.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
    }

    private async void AddToContextMenuCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        bool success = ContextMenuUtilities.RemoveFromContextMenu(out string? errorMessage);

        if (success)
        {
            DefaultSettings.AddToContextMenu = false;
            DefaultSettings.Save();
        }
        else
        {
            // Revert the checkbox since removal failed - the context menu is still registered
            settingsSet = false;
            AddToContextMenuCheckBox.IsChecked = true;
            settingsSet = true;

            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "Context Menu Removal Failed",
                Content = errorMessage ?? "Some context menu entries could not be removed.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
    }

    private void RegisterOpenWithCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        ImplementAppOptions.RegisterAsImageOpenWithApp();
        DefaultSettings.RegisterOpenWith = true;
        DefaultSettings.Save();
    }

    private void RegisterOpenWithCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!settingsSet)
            return;

        ImplementAppOptions.UnregisterAsImageOpenWithApp();
        DefaultSettings.RegisterOpenWith = false;
        DefaultSettings.Save();
    }
}
