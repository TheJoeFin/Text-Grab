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
        App app = (App)App.Current;
        if (app.TextGrabIcon is not null
            || app.NumberOfRunningInstances > 1)
        {
            return;
        }
        RegisterHotKeys(app);

        app.TextGrabIcon = CreateNotifyIconWindow();
    }

    public static async Task ResetNotifyIcon()
    {
        App app = (App)App.Current;
        app.TextGrabIcon = null;

        UnregisterHotkeys(app);

        NotifyIconWindow? existingIcon = GetExistingNotifyIconWindow();
        existingIcon?.Close();

        RegisterHotKeys(app);

        app.TextGrabIcon = CreateNotifyIconWindow();
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
                    HistoryInfo? historyInfo = Singleton<HistoryService>.Instance.GetEditWindows().LastOrDefault();

                    if (historyInfo is null)
                    {
                        EditTextWindow etw = new();
                        etw.Show();
                        return;
                    }

                    EditTextWindow etwHistory = new(historyInfo);
                    etwHistory.Show();
                }));
                break;
            case ShortcutKeyActions.PreviousGrabFrame:
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame();
                }));
                break;
            case ShortcutKeyActions.OpenClipboardContent:
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        string text = System.Windows.Clipboard.GetText();
                        EditTextWindow etw = new(text, false);
                        etw.Show();
                        etw.Activate();
                        return;
                    }

                    if (System.Windows.Clipboard.ContainsFileDropList())
                    {
                        StringCollection files = System.Windows.Clipboard.GetFileDropList();
                        string? imagePath = files.Cast<string?>().FirstOrDefault(f => f is not null && IoUtilities.IsImageFile(f!));
                        if (imagePath is not null)
                        {
                            GrabFrame gf = new(imagePath);
                            gf.Show();
                            gf.Activate();
                            return;
                        }
                    }

                    (bool success, System.Windows.Media.ImageSource? clipboardImage) = ClipboardUtilities.TryGetImageFromClipboard();
                    if (!success || clipboardImage is null)
                        return;

                    BitmapSource? bitmapSource = null;
                    if (clipboardImage is System.Windows.Interop.InteropBitmap interopBitmap)
                    {
                        System.Drawing.Bitmap bmp = ImageMethods.InteropBitmapToBitmap(interopBitmap);
                        bitmapSource = ImageMethods.BitmapToImageSource(bmp);
                        bmp.Dispose();
                    }
                    else if (clipboardImage is BitmapSource source)
                    {
                        bitmapSource = source;
                    }

                    if (bitmapSource is null)
                        return;

                    string tempPath = Path.Combine(Path.GetTempPath(), $"TextGrab_Clipboard_{Guid.NewGuid()}.png");
                    using (FileStream fileStream = new(tempPath, FileMode.Create))
                    {
                        PngBitmapEncoder encoder = new();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(fileStream);
                    }

                    GrabFrame grabFrame = new(tempPath);
                    grabFrame.Show();
                    grabFrame.Activate();
                }));
                break;
            default:
                break;
        }
    }

    private static NotifyIconWindow CreateNotifyIconWindow()
    {
        NotifyIconWindow? existingIcon = GetExistingNotifyIconWindow();

        if (existingIcon is not null)
            return existingIcon;

        NotifyIconWindow notifyIconWindow = new();
        notifyIconWindow.Show();

        return notifyIconWindow;
    }

    private static NotifyIconWindow? GetExistingNotifyIconWindow()
    {
        return Application.Current.Windows.OfType<NotifyIconWindow>().FirstOrDefault();
    }
}
