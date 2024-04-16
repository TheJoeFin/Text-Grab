using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Views;
using Wpf.Ui.Tray;

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

        NotifyIconService icon = new()
        {
            TooltipText = "Text Grab",
            Icon = new BitmapImage(new Uri("/TealSelect.ico", UriKind.Relative))
        };

        ContextMenu? contextMenu = new();

        MenuItem? settingsItem = new();
        settingsItem.Header = "Settings";
        settingsItem.Click += (s, e) => { SettingsWindow sw = new(); sw.Show(); };

        MenuItem? editLastItem = new();
        editLastItem.Header = "Edit Last Grab";
        editLastItem.Click += (s, e) => { Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame(); };

        MenuItem? quickSimpleLookupItem = new();
        quickSimpleLookupItem.Header = "Quick Simple Lookup";
        quickSimpleLookupItem.Click += (s, e) => { QuickSimpleLookup qsl = new(); qsl.Show(); };

        MenuItem? previousGrabRegion = new();
        previousGrabRegion.Header = "Grab Previous Region";
        previousGrabRegion.Click += async (s, e) => { await OcrUtilities.GetTextFromPreviousFullscreenRegion(); };

        MenuItem? fullScreenGrabItem = new();
        fullScreenGrabItem.Header = "Fullscreen Grab";
        fullScreenGrabItem.Click += (s, e) => { WindowUtilities.LaunchFullScreenGrab(); };

        MenuItem? grabFrameItem = new();
        grabFrameItem.Header = "Grab Frame";
        grabFrameItem.Click += (s, e) => { GrabFrame gf = new(); gf.Show(); };

        MenuItem? editTextWindowItem = new();
        editTextWindowItem.Header = "Edit Text Window";
        editTextWindowItem.Click += (s, e) => { EditTextWindow etw = new(); etw.Show(); };

        MenuItem? exitItem = new();
        exitItem.Header = "Close";
        exitItem.Click += (s, e) => { System.Windows.Application.Current.Shutdown(); };

        MenuItem[] menuItems =
        [
            fullScreenGrabItem,
            previousGrabRegion,
            grabFrameItem,
            editTextWindowItem,
            quickSimpleLookupItem,
            editLastItem,
            settingsItem,
            exitItem
        ];

        foreach (MenuItem item in menuItems)
            contextMenu.Items.Add(item);

        icon.ContextMenu = contextMenu;
        

        // icon. += (s, e) =>
        // {
        //     if (e.Button == MouseButtons.Left)
        //         App.DefaultLaunch();
        // };


        RegisterHotKeys(app);

        // icon.SetParentWindow(app);
        // bool success = icon.Register();
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
