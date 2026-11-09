using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Views;

namespace Text_Grab.Utilities;

/// <summary>
/// Launch actions shared by the tray icon menu, the global hotkeys, and the tray flyout.
/// Every method must be called on the UI thread.
/// </summary>
public static class TrayActions
{
    public static void OpenFullscreenGrab() => WindowUtilities.LaunchFullScreenGrab();

    public static void OpenGrabFrame()
    {
        GrabFrame gf = new();
        gf.Show();
        gf.Activate();
    }

    public static void OpenEditTextWindow()
    {
        EditTextWindow etw = new();
        etw.Show();
        etw.Activate();
    }

    public static void OpenLookup()
    {
        QuickSimpleLookup qsl = new();
        qsl.Show();
    }

    public static void OpenSettings()
    {
        SettingsWindow settingsWindow = new();
        settingsWindow.Show();
    }

    public static void CopyPreviousRegion() => OcrSourceUtilities.GetCopyTextFromPreviousRegion();

    public static void OpenLastGrabFrame() => Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame();

    public static void OpenLastEditTextWindow()
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
        etwHistory.Activate();
    }

    /// <summary>
    /// Opens clipboard text in an Edit Text window, otherwise an image (file or bitmap) in a Grab Frame.
    /// </summary>
    public static void OpenClipboardContent()
    {
        if (Clipboard.ContainsText())
        {
            string text = Clipboard.GetText();
            EditTextWindow etw = new(text, false);
            etw.Show();
            etw.Activate();
            return;
        }

        TryOpenClipboardImageInGrabFrame();
    }

    public static bool ClipboardHasImage()
    {
        try
        {
            if (Clipboard.ContainsFileDropList()
                && Clipboard.GetFileDropList().Cast<string>().Any(IoUtilities.IsImageFile))
            {
                return true;
            }

            return Clipboard.ContainsImage() || ClipboardUtilities.TryGetImageFromClipboard().Item1;
        }
        catch
        {
            return false;
        }
    }

    public static bool ClipboardHasContent()
    {
        try
        {
            return Clipboard.ContainsText() || ClipboardHasImage();
        }
        catch
        {
            return false;
        }
    }

    public static bool TryOpenClipboardImageInGrabFrame()
    {
        // Check for image file paths in clipboard first
        if (Clipboard.ContainsFileDropList())
        {
            StringCollection files = Clipboard.GetFileDropList();
            string? imagePath = files.Cast<string?>().FirstOrDefault(f => f is not null && IoUtilities.IsImageFile(f));

            if (imagePath is not null)
            {
                GrabFrame gf = new(imagePath);
                gf.Show();
                gf.Activate();
                return true;
            }
        }

        // Fall back to image content in clipboard (bitmap, base64, etc.)
        (bool success, ImageSource? clipboardImage) = ClipboardUtilities.TryGetImageFromClipboard();

        if (!success || clipboardImage is null)
            return false;

        BitmapSource? bitmapSource = null;

        if (clipboardImage is InteropBitmap interopBitmap)
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
            return false;

        string tempPath = Path.Combine(AutomationProfile.GetTemporaryDirectory(), $"TextGrab_Clipboard_{Guid.NewGuid()}.png");

        using (FileStream fileStream = new(tempPath, FileMode.Create))
        {
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(fileStream);
        }

        GrabFrame grabFrame = new(tempPath, recordRecentFile: false);
        grabFrame.Show();
        grabFrame.Activate();
        return true;
    }
}
