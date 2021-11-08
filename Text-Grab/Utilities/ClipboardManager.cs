using System;
using System.Windows;
using System.Windows.Interop;


// From StackOverflow: Colin Dabritz
// Read on 9/20/2021
// https://stackoverflow.com/a/33018459/7438031

public class ClipboardManager
{
    public event EventHandler ClipboardChanged;

    public ClipboardManager(Window windowSource)
    {
        HwndSource source = PresentationSource.FromVisual(windowSource) as HwndSource;
        if (source == null)
        {
            throw new ArgumentException(
                "Window source MUST be initialized first, such as in the Window's OnSourceInitialized handler."
                , nameof(windowSource));
        }

        source.AddHook(WndProc);

        // get window handle for interop
        IntPtr windowHandle = new WindowInteropHelper(windowSource).Handle;

        // register for clipboard events
        NativeMethods.AddClipboardFormatListener(windowHandle);
    }

    private void OnClipboardChanged()
    {
        ClipboardChanged?.Invoke(this, EventArgs.Empty);
    }

    private static readonly IntPtr WndProcSuccess = IntPtr.Zero;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            OnClipboardChanged();
            handled = true;
        }

        return WndProcSuccess;
    }
}