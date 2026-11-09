using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Views;
using System.Runtime.InteropServices;
using Wpf.Ui.Tray.Controls;

namespace Text_Grab.Controls;

public partial class NotifyIconWindow : Window
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;
    private HwndSource? windowSource;

    public NotifyIconWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        nint windowHandle = new WindowInteropHelper(this).Handle;
        windowSource = HwndSource.FromHwnd(windowHandle);
        windowSource?.AddHook(NotifyIconWindowMessageHook);
    }

    protected override void OnClosed(EventArgs e)
    {
        windowSource?.RemoveHook(NotifyIconWindowMessageHook);
        windowSource = null;

        base.OnClosed(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        App.Current.Shutdown();
    }

    private void NotifyIcon_LeftClick(NotifyIcon sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (IsFlyoutLeftClick())
            RunAfterTrayIconInteraction(() => TrayFlyoutController.Toggle(fromTrayIcon: true));
        else
            RunAfterTrayIconInteraction(App.DefaultLaunch);
    }

    private bool IsFlyoutLeftClick()
        => Enum.TryParse(DefaultSettings.TrayLeftClickAction, true, out TrayLeftClickAction action)
            && action == TrayLeftClickAction.Flyout;

    private void RunAfterTrayIconInteraction(Action action)
    {
        // Let the shell's tray interaction release foreground ownership first.
        Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Hide();
        HideFromAltTab();
        NotifyIcon.Visibility = Visibility.Visible;

        ApplyTrayIconStyle();
        ApplyTooltip();
    }

    public void ApplyTooltip()
    {
        string toolTipText = "Text Grab";

        if (IsFlyoutLeftClick())
        {
            NotifyIcon.TooltipText = toolTipText + " - Quick Flyout";
            return;
        }

        TextGrabMode defaultLaunchSetting = Enum.Parse<TextGrabMode>(DefaultSettings.DefaultLaunch, true);

        switch (defaultLaunchSetting)
        {
            case TextGrabMode.Fullscreen:
                toolTipText += " - Fullscreen Grab";
                break;
            case TextGrabMode.GrabFrame:
                toolTipText += " - Grab Frame";
                break;
            case TextGrabMode.EditText:
                toolTipText += " - Edit Text";
                break;
            case TextGrabMode.QuickLookup:
                toolTipText += " - Quick Lookup";
                break;
            default:
                break;
        }

        NotifyIcon.TooltipText = toolTipText;
    }

    public void ApplyTrayIconStyle()
    {
        bool isMonochrome = Enum.TryParse(DefaultSettings.TrayIconStyle, true, out TrayIconStyle style)
            && style == TrayIconStyle.Monochrome;

        string iconPath = isMonochrome
            ? (SystemThemeUtility.IsLightTheme() ? "/Images/Select-Black.ico" : "/Images/Select-White.ico")
            : "/Images/TealSelect40.png";

        NotifyIcon.Icon = new BitmapImage(new Uri($"pack://application:,,,{iconPath}"));
    }

    private void EditWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        TrayActions.OpenEditTextWindow();
    }

    private async void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await App.OpenFileWithPickerAsync();
    }

    private void GrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        TrayActions.OpenGrabFrame();
    }

    private void FullscreenGrabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RunAfterTrayIconInteraction(TrayActions.OpenFullscreenGrab);
    }

    private async void PreviousRegionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await OcrSourceUtilities.GetTextFromPreviousFullscreenRegion();
    }

    private void LookupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        TrayActions.OpenLookup();
    }

    private void LastGrabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        TrayActions.OpenLastGrabFrame();
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SettingsWindow sw = new();
        sw.Show();
    }

    private void NotifyIcon_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!NotifyIcon.IsVisible)
            NotifyIcon.Visibility = Visibility.Visible;
    }

    private void HideFromAltTab()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EX_STYLE);
        NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EX_STYLE, (exStyle | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW);
    }

    private IntPtr NotifyIconWindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == NativeMethods.WM_TASKBARCREATED)
            RestoreNotifyIconAfterExplorerRestart();

        return IntPtr.Zero;
    }

    private void RestoreNotifyIconAfterExplorerRestart()
    {
        if (NotifyIcon.IsRegistered)
            NotifyIcon.Unregister();

        NotifyIcon.Register();
    }

    private void LastEditWindow_Click(object sender, RoutedEventArgs e)
    {
        TrayActions.OpenLastEditTextWindow();
    }

    private void OpenClipboardImageGrabFrame_Click(object sender, RoutedEventArgs e)
    {
        TrayActions.TryOpenClipboardImageInGrabFrame();
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        OpenClipboardImageGrabFrame.IsEnabled = TrayActions.ClipboardHasImage();
    }
}
