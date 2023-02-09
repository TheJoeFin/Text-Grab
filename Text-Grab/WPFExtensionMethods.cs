using System;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;


static class WPFExtensionMethods
{
    public static Point GetAbsolutePosition(this Window w)
    {
        if (w.WindowState != WindowState.Maximized)
            return new Point(w.Left, w.Top);

        Int32Rect r;
        bool multimonSupported = OSInterop.GetSystemMetrics(OSInterop.SM_CMONITORS) != 0;
        if (!multimonSupported)
        {
            OSInterop.RECT rc = new OSInterop.RECT();
            OSInterop.SystemParametersInfo(48, 0, ref rc, 0);
            r = new Int32Rect(rc.left, rc.top, rc.width, rc.height);
        }
        else
        {
            WindowInteropHelper helper = new WindowInteropHelper(w);
            IntPtr hmonitor = OSInterop.MonitorFromWindow(new HandleRef(null, helper.EnsureHandle()), 2);
            OSInterop.MONITORINFOEX info = new OSInterop.MONITORINFOEX();
            OSInterop.GetMonitorInfo(new HandleRef(null, hmonitor), info);
            r = new Int32Rect(info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.width, info.rcMonitor.height);
        }
        return new Point(r.X, r.Y);
    }
}