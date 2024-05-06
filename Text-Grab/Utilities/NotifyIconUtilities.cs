using System;
using System.Collections.Generic;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Views;

namespace Text_Grab.Utilities;

public static class NotifyIconUtilities
{
    public static void SetupNotifyIcon()
    {
        App app = (App)App.Current;
        if (app.TextGrabIcon is not null
            || app.NumberOfRunningInstances > 1)
        {
            return;
        }
        RegisterHotKeys(app);

        app.TextGrabIcon = WindowUtilities.OpenOrActivateWindow<NotifyIconWindow>();
    }

    public static void RegisterHotKeys(App app)
    {
        IEnumerable<ShortcutKeySet> shortcuts = ShortcutKeysUtilities.GetShortcutKeySetsFromSettings();

        foreach (ShortcutKeySet keySet in shortcuts)
            if (keySet.IsEnabled && HotKeyManager.RegisterHotKey(keySet) is int id)
                app.HotKeyIds.Add(id);

        HotKeyManager.HotKeyPressed -= new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
        HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
    }

    public static void UnregisterHotkeys(App app)
    {
        HotKeyManager.HotKeyPressed -= new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);

        foreach (int hotKeyId in app.HotKeyIds)
            HotKeyManager.UnregisterHotKey(hotKeyId);
    }

    private static void trayIcon_Disposed(object? sender, EventArgs e)
    {
        App app = (App)App.Current;

        UnregisterHotkeys(app);
    }

    static void HotKeyManager_HotKeyPressed(object? sender, HotKeyEventArgs e)
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
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    WindowUtilities.LaunchFullScreenGrab();
                }));
                break;
            case ShortcutKeyActions.GrabFrame:
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    GrabFrame gf = new();
                    gf.Show();
                }));
                break;
            case ShortcutKeyActions.Lookup:
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    QuickSimpleLookup qsl = new();
                    qsl.Show();
                }));
                break;
            case ShortcutKeyActions.EditWindow:
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    EditTextWindow etw = new();
                    etw.Show();
                    etw.Activate();
                }));
                break;
            case ShortcutKeyActions.PreviousRegionGrab:
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    OcrUtilities.GetCopyTextFromPreviousRegion();
                }));
                break;
            case ShortcutKeyActions.PreviousEditWindow:
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    EditTextWindow etw = new();
                    etw.OpenMostRecentTextHistoryItem();
                    etw.Show();
                    etw.Activate();
                }));
                break;
            case ShortcutKeyActions.PreviousGrabFrame:
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame();
                }));
                break;
            default:
                break;
        }
    }
}
