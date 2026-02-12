using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Wpf.Ui.Tray.Controls;

namespace Text_Grab.Controls;

public partial class NotifyIconWindow : Window
{
    private readonly Settings DefaultSettings = AppUtilities.TextGrabSettings;

    public NotifyIconWindow()
    {
        InitializeComponent();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        App.Current.Shutdown();
    }

    private void NotifyIcon_LeftClick(NotifyIcon sender, RoutedEventArgs e)
    {
        e.Handled = true;
        App.DefaultLaunch();
    }

    private void Window_Activated(object sender, EventArgs e)
    {
        Hide();
        NotifyIcon.Visibility = Visibility.Visible;

        string toolTipText = "Text Grab";
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

    private void EditWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditTextWindow etw = new();
        etw.Show();
        etw.Activate();
    }

    private void GrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        GrabFrame gf = new();
        gf.Show();
        gf.Activate();
    }

    private void FullscreenGrabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab();
    }

    private async void PreviousRegionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await OcrUtilities.GetTextFromPreviousFullscreenRegion();
    }

    private void LookupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        QuickSimpleLookup qsl = new();
        qsl.Show();
    }

    private void LastGrabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Singleton<HistoryService>.Instance.GetLastHistoryAsGrabFrame();
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

    private void LastEditWindow_Click(object sender, RoutedEventArgs e)
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

    private void OpenClipboardImageGrabFrame_Click(object sender, RoutedEventArgs e)
    {
        // Check for image file paths in clipboard first
        if (Clipboard.ContainsFileDropList())
        {
            StringCollection files = Clipboard.GetFileDropList();
            string? imagePath = files.Cast<string>().FirstOrDefault(IsImageFile);

            if (imagePath is not null)
            {
                GrabFrame gf = new(imagePath);
                gf.Show();
                gf.Activate();
                return;
            }
        }

        // Fall back to image content in clipboard (bitmap, base64, etc.)
        (bool success, ImageSource? clipboardImage) = ClipboardUtilities.TryGetImageFromClipboard();

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
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        bool hasClipboardImage = false;

        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                StringCollection files = Clipboard.GetFileDropList();
                hasClipboardImage = files.Cast<string>().Any(IsImageFile);
            }

            if (!hasClipboardImage)
                hasClipboardImage = Clipboard.ContainsImage() || ClipboardUtilities.TryGetImageFromClipboard().Item1;
        }
        catch
        {
            hasClipboardImage = false;
        }

        OpenClipboardImageGrabFrame.IsEnabled = hasClipboardImage;
    }

    private static bool IsImageFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".tif" or ".webp" or ".ico";
    }
}
