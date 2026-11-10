using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Views;

namespace Text_Grab.Utilities;

public static class NotifyIconUtilities
{
    public static void SetupNotifyIcon()
    {
        if (AutomationProfile.Current is { AllowsSystemIntegration: false })
            return;

        AppRuntimeState state = RuntimeState;
        if (state.TextGrabIcon is not null
            || state.NumberOfRunningInstances > 1)
        {
            return;
        }
        RegisterHotKeys();

        state.TextGrabIcon = CreateNotifyIconWindow();
    }

    public static async Task ResetNotifyIcon()
    {
        AppRuntimeState state = RuntimeState;
        state.TextGrabIcon = null;

        UnregisterHotkeys();

        NotifyIconWindow? existingIcon = GetExistingNotifyIconWindow();
        existingIcon?.Close();

        RegisterHotKeys();

        state.TextGrabIcon = CreateNotifyIconWindow();
    }

    public static void RegisterHotKeys()
    {
        if (AutomationProfile.Current is { AllowsSystemIntegration: false })
            return;

        AppRuntimeState state = RuntimeState;
        IEnumerable<ShortcutKeySet> shortcuts = ShortcutKeysUtilities.GetShortcutKeySetsFromSettings();

        foreach (ShortcutKeySet keySet in shortcuts)
        {
            if (!keySet.IsEnabled)
                continue;

            if (HotKeyManager.RegisterHotKey(keySet) is int id)
                state.HotKeyIds.Add(id);
            else
                AutomationDiagnostics.Record("hotkey-registration-failed", new { keySet.Action, keySet.Name, keySet.NonModifierKey, keySet.Modifiers });
        }

        HotKeyManager.HotKeyPressed -= new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
        HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
    }

    public static void UnregisterHotkeys()
    {
        HotKeyManager.HotKeyPressed -= new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);

        foreach (int hotKeyId in RuntimeState.HotKeyIds)
            HotKeyManager.UnregisterHotKey(hotKeyId);
    }

    private static void trayIcon_Disposed(object? sender, EventArgs e)
    {
        UnregisterHotkeys();
    }

    private static AppRuntimeState RuntimeState => AppServices.Provider.GetRequiredService<AppRuntimeState>();

    private static void HotKeyManager_HotKeyPressed(object? sender, HotKeyEventArgs e)
    {
        if (!AppUtilities.TextGrabSettings.GlobalHotkeysEnabled)
            return;

        IEnumerable<ShortcutKeySet> shortcuts = ShortcutKeysUtilities.GetShortcutKeySetsFromSettings();

        ShortcutKeyActions pressedAction = ShortcutKeyActions.None;
        foreach (ShortcutKeySet keySet in shortcuts)
            if (keySet.Equals(e))
                pressedAction = keySet.Action;

        switch (pressedAction)
        {
            case ShortcutKeyActions.None:
                break;
            case ShortcutKeyActions.Settings:
                break;
            case ShortcutKeyActions.Fullscreen:
                RunOnUiThread(TrayActions.OpenFullscreenGrab);
                break;
            case ShortcutKeyActions.GrabFrame:
                RunOnUiThread(TrayActions.OpenGrabFrame);
                break;
            case ShortcutKeyActions.Lookup:
                RunOnUiThread(TrayActions.OpenLookup);
                break;
            case ShortcutKeyActions.EditWindow:
                RunOnUiThread(TrayActions.OpenEditTextWindow);
                break;
            case ShortcutKeyActions.PreviousRegionGrab:
                RunOnUiThread(TrayActions.CopyPreviousRegion);
                break;
            case ShortcutKeyActions.PreviousEditWindow:
                RunOnUiThread(TrayActions.OpenLastEditTextWindow);
                break;
            case ShortcutKeyActions.PreviousGrabFrame:
                RunOnUiThread(TrayActions.OpenLastGrabFrame);
                break;
            case ShortcutKeyActions.OpenClipboardContent:
                RunOnUiThread(TrayActions.OpenClipboardContent);
                break;
            case ShortcutKeyActions.Flyout:
                RunOnUiThread(() => TrayFlyoutController.Toggle(fromTrayIcon: false));
                break;
            default:
                break;
        }
    }

    private static void RunOnUiThread(Action action)
        => System.Windows.Application.Current.Dispatcher.Invoke(action);

    private static NotifyIconWindow CreateNotifyIconWindow()
    {
        NotifyIconWindow? existingIcon = GetExistingNotifyIconWindow();

        if (existingIcon is not null)
            return existingIcon;

        NotifyIconWindow notifyIconWindow = new();
        notifyIconWindow.Show();

        return notifyIconWindow;
    }

    public static void RefreshTrayIconStyle()
    {
        // Windows theme changes are observed via a registry watcher that raises its event on a
        // background thread, but NotifyIcon.Icon is a DependencyProperty owned by the UI thread.
        System.Windows.Threading.Dispatcher dispatcher = Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(RefreshTrayIconStyle);
            return;
        }

        NotifyIconWindow? notifyIconWindow = GetExistingNotifyIconWindow();
        notifyIconWindow?.ApplyTrayIconStyle();
        notifyIconWindow?.ApplyTooltip();
    }

    private static NotifyIconWindow? GetExistingNotifyIconWindow()
    {
        return Application.Current.Windows.OfType<NotifyIconWindow>().FirstOrDefault();
    }
}
