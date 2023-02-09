using System;
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    // See http://msdn.microsoft.com/en-us/library/ms649021%28v=vs.85%29.aspx
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public static IntPtr HWND_MESSAGE = new IntPtr(-3);

    // See http://msdn.microsoft.com/en-us/library/ms632599%28VS.85%29.aspx#message_only
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);
}