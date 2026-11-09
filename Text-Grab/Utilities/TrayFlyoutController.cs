using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Services;
using Text_Grab.Views;
using Application = System.Windows.Application;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Text_Grab.Utilities;

/// <summary>
/// Owns the single, reused <see cref="TrayFlyoutWindow"/>: toggling, positioning, and activating items.
/// All members must be called on the UI thread.
/// </summary>
public static partial class TrayFlyoutController
{
    // A tray click while the flyout is open first deactivates (and hides) it, then raises LeftClick.
    // Without a guard that click would immediately re-open the flyout it just closed.
    private const long ReopenGuardMilliseconds = 500;
    private const int EdgeGapDips = 8;

    private static TrayFlyoutWindow? s_window;
    private static long? s_lastHiddenTick;

    public static bool IsOpen => s_window is { IsVisible: true };

    public static void Toggle(bool fromTrayIcon)
    {
        if (IsOpen)
        {
            s_window!.Hide();
            return;
        }

        if (fromTrayIcon && ShouldSuppressReopen(Environment.TickCount64, s_lastHiddenTick))
            return;

        Show(fromTrayIcon);
    }

    /// <summary>
    /// True when a tray click lands right after the flyout hid itself, meaning the click that
    /// deactivated the flyout is the same one now asking to open it.
    /// </summary>
    public static bool ShouldSuppressReopen(long nowTick, long? lastHiddenTick)
        => lastHiddenTick is long hidden && nowTick - hidden < ReopenGuardMilliseconds;

    public static void Show(bool fromTrayIcon = true)
    {
        if (s_window is null)
        {
            s_window = new TrayFlyoutWindow();
            s_window.IsVisibleChanged += (_, _) =>
            {
                if (!s_window.IsVisible)
                    s_lastHiddenTick = Environment.TickCount64;
            };
        }

        s_window.PrepareForShow();
        PositionNearCursor(s_window, fromTrayIcon);
        s_window.Show();
        // WPF may re-apply its own DPI-scaled location while showing; place it again once shown.
        PositionNearCursor(s_window, fromTrayIcon);
        s_window.Activate();
        s_window.FocusSearch();
    }

    public static void Close()
    {
        s_window?.Hide();
    }

    /// <summary>Hides the flyout, then runs the action once the flyout has released activation.</summary>
    public static void CloseThenRun(Action action)
    {
        Close();
        Application.Current.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
    }

    public static void ActivateItem(TrayFlyoutItem item)
    {
        CloseThenRun(() => OpenItem(item));
    }

    private static async void OpenItem(TrayFlyoutItem item)
    {
        try
        {
            HistoryInfo history = item.History;

            switch (item.Kind)
            {
                case TrayFlyoutItemKind.EditText:
                    EditTextWindow etw = new(history);
                    etw.Show();
                    etw.Activate();
                    break;
                case TrayFlyoutItemKind.GrabFrame:
                    new GrabFrame(history).Show();
                    break;
                case TrayFlyoutItemKind.File:
                    await OpenFile(history);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tray flyout failed to open item: {ex}");
        }
    }

    private static async Task OpenFile(HistoryInfo history)
    {
        if (string.IsNullOrWhiteSpace(history.SourcePath) || !File.Exists(history.SourcePath))
            return;

        if (history.IsPdfDocument && !string.IsNullOrWhiteSpace(history.ImagePath))
        {
            new GrabFrame(history, history.SourcePath).Show();
            return;
        }

        EditTextWindow etw = new();
        etw.Show();
        etw.Activate();
        await etw.OpenPath(history.SourcePath);
    }

    /// <summary>Copies the item's text (or file path) to the clipboard.</summary>
    public static void CopyItem(TrayFlyoutItem item)
    {
        string text = item.Kind == TrayFlyoutItemKind.File ? item.History.SourcePath : item.History.TextContent;
        if (string.IsNullOrEmpty(text))
            return;

        try { System.Windows.Clipboard.SetText(text); }
        catch (COMException) { /* clipboard is busy; nothing useful to do */ }
    }

    private static void PositionNearCursor(TrayFlyoutWindow window, bool snapToCorner)
    {
        if (!WindowUtilities.GetMousePosition(out System.Windows.Point mouse))
            return;

        Point cursor = new((int)mouse.X, (int)mouse.Y);
        Screen screen = Screen.FromPoint(cursor);
        Rectangle workArea = screen.WorkingArea;
        double scale = GetMonitorScale(cursor);
        int gap = (int)Math.Round(EdgeGapDips * scale);

        nint handle = new WindowInteropHelper(window).EnsureHandle();

        // Size in physical pixels: prefer the real window rect; fall back to DIPs * monitor scale.
        Size size = new(
            (int)Math.Ceiling(window.Width * scale),
            (int)Math.Ceiling(window.Height * scale));
        if (window.IsVisible && GetWindowRect(handle, out RectNative rect) && rect.Right > rect.Left)
            size = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);

        // Never let the window be taller/wider than the work area.
        int newW = Math.Min(size.Width, Math.Max(1, workArea.Width - (2 * gap)));
        int newH = Math.Min(size.Height, Math.Max(1, workArea.Height - (2 * gap)));
        size = new Size(newW, newH);

        Point topLeft = TrayFlyoutPlacement.Compute(cursor, workArea, size, gap, snapToCorner);

        // Set size too so the pixel size matches what placement assumed on the target monitor.
        SetWindowPos(handle, HwndTopmost, topLeft.X, topLeft.Y, size.Width, size.Height, SwpNoActivate);
    }

    private static double GetMonitorScale(Point pixelPoint)
    {
        try
        {
            nint monitor = MonitorFromPoint(new PointNative { X = pixelPoint.X, Y = pixelPoint.Y }, MonitorDefaultToNearest);
            if (monitor != 0 && GetDpiForMonitor(monitor, MdtEffectiveDpi, out uint dpiX, out _) == 0 && dpiX > 0)
                return dpiX / 96.0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // fall through to the system default
        }

        return 1.0;
    }

    private static readonly nint HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hWnd, out RectNative lpRect);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(PointNative pt, uint dwFlags);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
