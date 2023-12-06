using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Text_Grab.Controls;
using Text_Grab.Properties;
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

        NotifyIcon icon = new();
        icon.Text = "Text Grab";
        icon.Icon = new Icon(System.Windows.Application.GetResourceStream(new Uri("/TealSelect.ico", UriKind.Relative)).Stream);
        icon.Visible = true;

        ContextMenuStrip? contextMenu = new();

        ToolStripMenuItem? settingsItem = new("&Settings");
        settingsItem.Click += (s, e) => { SettingsWindow sw = new(); sw.Show(); };
        ToolStripMenuItem? editLastItem = new("&Edit Last Grab");
        editLastItem.Click += (s, e) => { Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame(); };
        ToolStripMenuItem? quickSimpleLookupItem = new("&Quick Simple Lookup");
        quickSimpleLookupItem.Click += (s, e) => { QuickSimpleLookup qsl = new(); qsl.Show(); };
        ToolStripMenuItem? previousGrabRegion = new("&Grab Previous Region");
        previousGrabRegion.Click += async (s, e) => { await OcrUtilities.GetTextFromPreviousFullscreenRegion(); };
        ToolStripMenuItem? fullScreenGrabItem = new("&Fullscreen Grab");
        fullScreenGrabItem.Click += (s, e) => { WindowUtilities.LaunchFullScreenGrab(); };
        ToolStripMenuItem? grabFrameItem = new("&Grab Frame");
        grabFrameItem.Click += (s, e) => { GrabFrame gf = new(); gf.Show(); };
        ToolStripMenuItem? editTextWindowItem = new("&Edit Text Window");
        editTextWindowItem.Click += (s, e) => { EditTextWindow etw = new(); etw.Show(); };

        ToolStripMenuItem? exitItem = new("&Close");
        exitItem.Click += (s, e) => { System.Windows.Application.Current.Shutdown(); };

        contextMenu.Items.AddRange(
            new ToolStripMenuItem[] {
                fullScreenGrabItem,
                previousGrabRegion,
                grabFrameItem,
                editTextWindowItem,
                quickSimpleLookupItem,
                editLastItem,
                settingsItem,
                exitItem
            }
        );
        icon.ContextMenuStrip = contextMenu;

        icon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
                App.DefaultLaunch();
        };

        icon.Disposed += trayIcon_Disposed;

        RegisterHotKeys(app);

        app.TextGrabIcon = icon;
    }

    public static void RegisterHotKeys(App app)
    {
        ShortcutKeySet fsgKeySet = new(Settings.Default.FullscreenGrabHotKey);
        ShortcutKeySet gfKeySet = new(Settings.Default.GrabFrameHotkey);
        ShortcutKeySet qslKeySet = new(Settings.Default.LookupHotKey);
        ShortcutKeySet etwKeySet = new(Settings.Default.EditWindowHotKey);

        if (HotKeyManager.RegisterHotKey(fsgKeySet) is int fsgId)
            app.HotKeyIds.Add(fsgId);
        if (HotKeyManager.RegisterHotKey(gfKeySet) is int gfId)
            app.HotKeyIds.Add(gfId);
        if (HotKeyManager.RegisterHotKey(qslKeySet) is int qslId)
            app.HotKeyIds.Add(qslId);
        if (HotKeyManager.RegisterHotKey(etwKeySet) is int etwId)
            app.HotKeyIds.Add(etwId);

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
        if (!Settings.Default.GlobalHotkeysEnabled)
            return;

        ShortcutKeySet fsgKeySet = new(Settings.Default.FullscreenGrabHotKey);
        ShortcutKeySet gfKeySet = new(Settings.Default.GrabFrameHotkey);
        ShortcutKeySet qslKeySet = new(Settings.Default.LookupHotKey);
        ShortcutKeySet etwKeySet = new(Settings.Default.EditWindowHotKey);

        if (etwKeySet.Equals(e))
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                EditTextWindow etw = new();
                etw.Show();
                etw.Activate();
            }));
        }
        else if (fsgKeySet.Equals(e))
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                WindowUtilities.LaunchFullScreenGrab();
            }));
        }
        else if (gfKeySet.Equals(e))
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                GrabFrame gf = new();
                gf.Show();
            }));
        }
        else if (qslKeySet.Equals(e))
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                QuickSimpleLookup qsl = new();
                qsl.Show();
            }));
        }
    }
}
