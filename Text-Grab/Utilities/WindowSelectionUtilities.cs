using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

public static class WindowSelectionUtilities
{
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    public static List<WindowSelectionCandidate> GetCapturableWindows(IReadOnlyCollection<IntPtr>? excludedHandles = null)
    {
        HashSet<IntPtr> excluded = excludedHandles is null ? [] : [.. excludedHandles];
        IntPtr shellWindow = OSInterop.GetShellWindow();
        List<WindowSelectionCandidate> candidates = [];

        _ = OSInterop.EnumWindows((windowHandle, _) =>
        {
            WindowSelectionCandidate? candidate = CreateCandidate(windowHandle, shellWindow, excluded);
            if (candidate is not null)
                candidates.Add(candidate);

            return true;
        }, IntPtr.Zero);

        return candidates;
    }

    public static WindowSelectionCandidate? FindWindowAtPoint(IEnumerable<WindowSelectionCandidate> candidates, Point screenPoint)
    {
        return candidates.FirstOrDefault(candidate => candidate.Contains(screenPoint));
    }

    internal static bool IsValidWindowBounds(Rect bounds)
    {
        return bounds != Rect.Empty && bounds.Width > 20 && bounds.Height > 20;
    }

    private static WindowSelectionCandidate? CreateCandidate(IntPtr windowHandle, IntPtr shellWindow, ISet<IntPtr> excludedHandles)
    {
        if (windowHandle == IntPtr.Zero || windowHandle == shellWindow || excludedHandles.Contains(windowHandle))
            return null;

        if (!OSInterop.IsWindowVisible(windowHandle) || OSInterop.IsIconic(windowHandle))
            return null;

        if (IsCloaked(windowHandle))
            return null;

        int extendedStyle = OSInterop.GetWindowLong(windowHandle, GwlExStyle);
        if ((extendedStyle & WsExToolWindow) != 0 || (extendedStyle & WsExNoActivate) != 0)
            return null;

        Rect bounds = GetWindowBounds(windowHandle);
        if (!IsValidWindowBounds(bounds))
            return null;

        _ = OSInterop.GetWindowThreadProcessId(windowHandle, out uint processId);

        return new WindowSelectionCandidate(
            windowHandle,
            bounds,
            GetWindowTitle(windowHandle),
            (int)processId,
            GetProcessName((int)processId));
    }

    private static Rect GetWindowBounds(IntPtr windowHandle)
    {
        int rectSize = Marshal.SizeOf<OSInterop.RECT>();

        if (OSInterop.DwmGetWindowAttribute(windowHandle, DwmwaExtendedFrameBounds, out OSInterop.RECT frameBounds, rectSize) == 0)
        {
            Rect extendedBounds = new(frameBounds.left, frameBounds.top, frameBounds.width, frameBounds.height);
            if (IsValidWindowBounds(extendedBounds))
                return extendedBounds;
        }

        if (OSInterop.GetWindowRect(windowHandle, out OSInterop.RECT windowRect))
            return new Rect(windowRect.left, windowRect.top, windowRect.width, windowRect.height);

        return Rect.Empty;
    }

    private static string GetWindowTitle(IntPtr windowHandle)
    {
        int titleLength = OSInterop.GetWindowTextLength(windowHandle);
        if (titleLength <= 0)
            return string.Empty;

        StringBuilder titleBuilder = new(titleLength + 1);
        _ = OSInterop.GetWindowText(windowHandle, titleBuilder, titleBuilder.Capacity);
        return titleBuilder.ToString();
    }

    private static bool IsCloaked(IntPtr windowHandle)
    {
        return OSInterop.DwmGetWindowAttribute(windowHandle, DwmwaCloaked, out int cloakedState, sizeof(int)) == 0
            && cloakedState != 0;
    }

    private static string GetProcessName(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
        catch (Win32Exception)
        {
            return string.Empty;
        }
    }
}
