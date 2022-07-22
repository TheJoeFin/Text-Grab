using System;
using System.Windows.Forms;
using Text_Grab.Properties;
using Text_Grab.Views;

namespace Text_Grab.Utilities;

public static class NotifyIconUtilities
{
    public static void SetupNotifyIcon()
    {
        App app = (App)App.Current;
        if (app.TextGrabIcon != null
            || app.NumberOfRunningInstances > 1)
        {
            return;
        }

        NotifyIcon icon = new NotifyIcon();
        icon.Text = "Text Grab";
        icon.Icon = new System.Drawing.Icon(System.Windows.Application.GetResourceStream(new Uri("/t_ICON2.ico", UriKind.Relative)).Stream);
        icon.Visible = true;

        ContextMenuStrip? contextMenu = new();

        ToolStripMenuItem? settingsItem = new("&Settings");
        settingsItem.Click += (s, e) => { SettingsWindow sw = new(); sw.Show(); };
        ToolStripMenuItem? fullscreenGrabItem = new("&Fullscreen Grab");
        fullscreenGrabItem.Click += (s, e) => { WindowUtilities.LaunchFullScreenGrab(true); };
        ToolStripMenuItem? grabFrameItem = new("&Grab Frame");
        grabFrameItem.Click += (s, e) => { GrabFrame gf = new(); gf.Show(); };
        ToolStripMenuItem? editTextWindowItem = new("&Edit Text Window");
        editTextWindowItem.Click += (s, e) => { EditTextWindow etw = new(); etw.Show(); };

        ToolStripMenuItem? exitItem = new("&Close");
        exitItem.Click += (s, e) => { System.Windows.Application.Current.Shutdown(); };

        contextMenu.Items.AddRange(
            new ToolStripMenuItem[] {
                settingsItem,
                fullscreenGrabItem,
                grabFrameItem,
                editTextWindowItem,
                exitItem
            }
        );
        icon.ContextMenuStrip = contextMenu;

        icon.MouseClick += (s, e) =>
        {
            // TODO Add a setting to customize click behavior
            if (e.Button == MouseButtons.Left)
            {
                switch (Settings.Default.DefaultLaunch)
                {
                    case "Fullscreen":
                        WindowUtilities.LaunchFullScreenGrab(true);
                        break;
                    case "GrabFrame":
                        GrabFrame gf = new GrabFrame();
                        gf.Show();
                        break;
                    case "EditText":
                        EditTextWindow manipulateTextWindow = new EditTextWindow();
                        manipulateTextWindow.Show();
                        break;
                    default:
                        EditTextWindow editTextWindow = new EditTextWindow();
                        editTextWindow.Show();
                        break;
                }
            }
        };

        icon.Disposed += trayIcon_Disposed;

        // Double click just triggers the single click
        // icon.DoubleClick += (s, e) =>
        // {
        //     // TODO Add a setting to customize doubleclick behavior
        //     EditTextWindow etw = new(); etw.Show();
        // };
        KeysConverter keysConverter = new();
        Keys? fullscreenKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.FullscreenGrabHotKey);
        Keys? grabFrameKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.GrabFrameHotkey);
        Keys? editWindowKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.EditWindowHotKey);

        if (fullscreenKey is not null)
            app.HotKeyIds.Add(HotKeyManager.RegisterHotKey(fullscreenKey.Value, KeyModifiers.Windows | KeyModifiers.Shift));

        if (grabFrameKey is not null)
            app.HotKeyIds.Add(HotKeyManager.RegisterHotKey(grabFrameKey.Value, KeyModifiers.Windows | KeyModifiers.Shift));

        if (editWindowKey is not null)
            app.HotKeyIds.Add(HotKeyManager.RegisterHotKey(editWindowKey.Value, KeyModifiers.Windows | KeyModifiers.Shift));

        HotKeyManager.HotKeyPressed -= new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
        HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);

        app.TextGrabIcon = icon;
    }

    private static void trayIcon_Disposed(object? sender, EventArgs e)
    {
        App app = (App)App.Current;

        HotKeyManager.HotKeyPressed -= new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);

        foreach (int hotKeyId in app.HotKeyIds)
            HotKeyManager.UnregisterHotKey(hotKeyId);
    }

    static void HotKeyManager_HotKeyPressed(object? sender, HotKeyEventArgs e)
    {
        if (Settings.Default.GlobalHotkeysEnabled == false)
            return;

        KeysConverter keysConverter = new();
        Keys? fullscreenKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.FullscreenGrabHotKey);
        Keys? grabFrameKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.GrabFrameHotkey);
        Keys? editWindowKey = (Keys?)keysConverter.ConvertFrom(Settings.Default.EditWindowHotKey);

        if (fullscreenKey is null || grabFrameKey is null || editWindowKey is null)
            return;

        if (e.Key == editWindowKey.Value)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                EditTextWindow etw = new();
                etw.Show();
                etw.Activate();
            }));
        }

        if (e.Key == fullscreenKey.Value)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                WindowUtilities.LaunchFullScreenGrab(true);
            }));
        }

        if (e.Key == grabFrameKey.Value)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                GrabFrame gf = new();
                gf.Show();
            }));
        }
    }
}
