using System;
using System.Runtime.InteropServices;

internal static partial class NativeMethods
{
    // See http://msdn.microsoft.com/en-us/library/ms649021%28v=vs.85%29.aspx
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public static IntPtr HWND_MESSAGE = new(-3);

    // See http://msdn.microsoft.com/en-us/library/ms632599%28VS.85%29.aspx#message_only
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AddClipboardFormatListener(IntPtr hwnd);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(IntPtr hObject);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetKeyboardState(byte[] keyState);

    [LibraryImport("shcore.dll")]
    public static partial void GetScaleFactorForMonitor(IntPtr hMon, out uint pScale);
}