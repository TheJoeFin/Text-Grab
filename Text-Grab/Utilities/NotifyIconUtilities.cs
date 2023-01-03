using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Text_Grab.Properties;
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
        icon.Icon = new Icon(System.Windows.Application.GetResourceStream(new Uri("/t_ICON2.ico", UriKind.Relative)).Stream);
        icon.Visible = true;

        ContextMenuStrip? contextMenu = new();

        ToolStripMenuItem? settingsItem = new("&Settings");
        settingsItem.Click += (s, e) => { SettingsWindow sw = new(); sw.Show(); };
        ToolStripMenuItem? quickSimpleLookupItem = new("&Quick Simple Lookup");
        quickSimpleLookupItem.Click += (s, e) => { QuickSimpleLookup qsl = new(); qsl.Show(); };
        ToolStripMenuItem? fullscreenGrabItem = new("&Fullscreen Grab");
        fullscreenGrabItem.Click += (s, e) => { WindowUtilities.LaunchFullScreenGrab(); };
        ToolStripMenuItem? grabFrameItem = new("&Grab Frame");
        grabFrameItem.Click += (s, e) => { GrabFrame gf = new(); gf.Show(); };
        ToolStripMenuItem? editTextWindowItem = new("&Edit Text Window");
        editTextWindowItem.Click += (s, e) => { EditTextWindow etw = new(); etw.Show(); };

        ToolStripMenuItem? exitItem = new("&Close");
        exitItem.Click += (s, e) => { System.Windows.Application.Current.Shutdown(); };

        contextMenu.Items.AddRange(
            new ToolStripMenuItem[] {
                fullscreenGrabItem,
                grabFrameItem,
                editTextWindowItem,
                quickSimpleLookupItem,
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
        KeysConverter keysConverter = new();
        Keys? fullscreenKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.FullscreenGrabHotKey);
        Keys? grabFrameKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.GrabFrameHotkey);
        Keys? editWindowKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.EditWindowHotKey);
        Keys? LookupKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.LookupHotKey);

        List<Keys?> keysList = new()
        {
            fullscreenKey,
            grabFrameKey,
            editWindowKey,
            LookupKey
        };

        foreach (Keys? key in keysList)
            if (key is not null)
                app.HotKeyIds.Add(HotKeyManager.RegisterHotKey(key.Value, KeyModifiers.Windows | KeyModifiers.Shift));

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

        KeysConverter keysConverter = new();
        Keys? fullscreenKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.FullscreenGrabHotKey);
        Keys? grabFrameKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.GrabFrameHotkey);
        Keys? editWindowKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.EditWindowHotKey);
        Keys? lookupKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.LookupHotKey);

        if (editWindowKey is not null && e.Key == editWindowKey.Value)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                EditTextWindow etw = new();
                etw.Show();
                etw.Activate();
            }));
        }
        else if (fullscreenKey is not null && e.Key == fullscreenKey.Value)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                WindowUtilities.LaunchFullScreenGrab();
            }));
        }
        else if (grabFrameKey is not null && e.Key == grabFrameKey.Value)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                GrabFrame gf = new();
                gf.Show();
            }));
        }
        else if (lookupKey is not null && e.Key == lookupKey)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                QuickSimpleLookup qsl = new();
                qsl.Show();
            }));
        }
    }
}
