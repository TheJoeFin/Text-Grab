﻿using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Fasetto.Word;

/// <summary>
/// The dock position of the window
/// </summary>
public enum WindowDockPosition
{
    /// <summary>
    /// Not docked
    /// </summary>
    Undocked,
    /// <summary>
    /// Docked to the left of the screen
    /// </summary>
    Left,
    /// <summary>
    /// Docked to the right of the screen
    /// </summary>
    Right,
}

/// <summary>
/// Fixes the issue with Windows of Style <see cref="WindowStyle.None"/> covering the taskbar
/// </summary>
public class WindowResizer
{
    #region Private Members

    /// <summary>
    /// The window to handle the resizing for
    /// </summary>
    private Window? mWindow;

    /// <summary>
    /// The last calculated available screen size
    /// </summary>
    private Rect mScreenSize = new();

    /// <summary>
    /// How close to the edge the window has to be to be detected as at the edge of the screen
    /// </summary>
    private int mEdgeTolerance = 2;

    /// <summary>
    /// The transform matrix used to convert WPF sizes to screen pixels
    /// </summary>
    private Matrix mTransformToDevice;

    /// <summary>
    /// The last screen the window was on
    /// </summary>
    private IntPtr mLastScreen;

    /// <summary>
    /// The last known dock position
    /// </summary>
    private WindowDockPosition mLastDock = WindowDockPosition.Undocked;

    #endregion

    #region Dll Imports

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromPoint(POINT pt, MonitorOptions dwFlags);

    #endregion

    #region Public Events

    /// <summary>
    /// Called when the window dock position changes
    /// </summary>
    public event Action<WindowDockPosition> WindowDockChanged = (dock) => { };

    #endregion

    #region Constructor

    /// <summary>
    /// Default constructor
    /// </summary>
    /// <param name="window">The window to monitor and correctly maximize</param>
    /// <param name="adjustSize">The callback for the host to adjust the maximum available size if needed</param>
    public WindowResizer(Window window)
    {
        if (window is null)
            return;

        mWindow = window;

        // Create transform visual (for converting WPF size to pixel size)
        GetTransform();

        // Listen out for source initialized to setup
        mWindow.SourceInitialized += Window_SourceInitialized;

        // Monitor for edge docking
        mWindow.SizeChanged += Window_SizeChanged;
    }

    #endregion

    #region Initialize

    /// <summary>
    /// Gets the transform object used to convert WPF sizes to screen pixels
    /// </summary>
    private void GetTransform()
    {
        // Get the visual source
        PresentationSource source = PresentationSource.FromVisual(mWindow);

        // Reset the transform to default
        mTransformToDevice = default(Matrix);

        // If we cannot get the source, ignore
        if (source?.CompositionTarget == null)
            return;

        // Otherwise, get the new transform object
        mTransformToDevice = source.CompositionTarget.TransformToDevice;
    }

    /// <summary>
    /// Initialize and hook into the windows message pump
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window_SourceInitialized(object? sender, System.EventArgs e)
    {
        // Get the handle of this window
        nint handle = (new WindowInteropHelper(mWindow)).Handle;
        HwndSource handleSource = HwndSource.FromHwnd(handle);

        // If not found, end
        if (handleSource == null)
            return;

        // Hook into it's Windows messages
        handleSource.AddHook(WindowProc);
    }

    #endregion

    #region Edge Docking

    /// <summary>
    /// Monitors for size changes and detects if the window has been docked (Aero snap) to an edge
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // We cannot find positioning until the window transform has been established
        if (mTransformToDevice == default(Matrix)
            || mWindow is null)
            return;

        // Get the WPF size
        Size size = e.NewSize;

        // Get window rectangle
        double top = mWindow.Top;
        double left = mWindow.Left;
        double bottom = top + size.Height;
        double right = left + mWindow.Width;

        // Get window position/size in device pixels
        Point windowTopLeft = mTransformToDevice.Transform(new Point(left, top));
        Point windowBottomRight = mTransformToDevice.Transform(new Point(right, bottom));

        // Check for edges docked
        bool edgedTop = windowTopLeft.Y <= (mScreenSize.Top + mEdgeTolerance);
        bool edgedLeft = windowTopLeft.X <= (mScreenSize.Left + mEdgeTolerance);
        bool edgedBottom = windowBottomRight.Y >= (mScreenSize.Bottom - mEdgeTolerance);
        bool edgedRight = windowBottomRight.X >= (mScreenSize.Right - mEdgeTolerance);

        // Get docked position
        WindowDockPosition dock = WindowDockPosition.Undocked;

        // Left docking
        if (edgedTop && edgedBottom && edgedLeft)
            dock = WindowDockPosition.Left;
        else if (edgedTop && edgedBottom && edgedRight)
            dock = WindowDockPosition.Right;
        // None
        else
            dock = WindowDockPosition.Undocked;

        // If dock has changed
        if (dock != mLastDock)
            // Inform listeners
            WindowDockChanged(dock);

        // Save last dock position
        mLastDock = dock;
    }

    #endregion

    #region Windows Proc

    /// <summary>
    /// Listens out for all windows messages for this window
    /// </summary>
    /// <param name="hwnd"></param>
    /// <param name="msg"></param>
    /// <param name="wParam"></param>
    /// <param name="lParam"></param>
    /// <param name="handled"></param>
    /// <returns></returns>
    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            // Handle the GetMinMaxInfo of the Window
            case 0x0024:/* WM_GETMINMAXINFO */
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
                break;
        }

        return 0;
    }

    #endregion

    /// <summary>
    /// Get the min/max window size for this window
    /// Correctly accounting for the taskbar size and position
    /// </summary>
    /// <param name="hwnd"></param>
    /// <param name="lParam"></param>
    private void WmGetMinMaxInfo(System.IntPtr hwnd, System.IntPtr lParam)
    {
        if (mWindow is null)
            return;

        // Get the point position to determine what screen we are on
        POINT lMousePosition;
        GetCursorPos(out lMousePosition);

        // Get the primary monitor at cursor position 0,0
        nint lPrimaryScreen = MonitorFromPoint(new POINT(0, 0), MonitorOptions.MONITOR_DEFAULTTOPRIMARY);

        // Try and get the primary screen information
        MONITORINFO lPrimaryScreenInfo = new();
        if (!GetMonitorInfo(lPrimaryScreen, lPrimaryScreenInfo))
            return;

        // Now get the current screen
        nint lCurrentScreen = MonitorFromPoint(lMousePosition, MonitorOptions.MONITOR_DEFAULTTONEAREST);

        // If this has changed from the last one, update the transform
        if (lCurrentScreen != mLastScreen || mTransformToDevice == default(Matrix))
            GetTransform();

        // Store last know screen
        mLastScreen = lCurrentScreen;

        // Get min/max structure to fill with information
        MINMAXINFO? lMmiTmp = (MINMAXINFO?)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
        if (lMmiTmp == null)
            return;

        MINMAXINFO lMmi = lMmiTmp.Value;

        lMmi.ptMaxPosition.X = lPrimaryScreenInfo.rcWork.Left;
        lMmi.ptMaxPosition.Y = lPrimaryScreenInfo.rcWork.Top;
        lMmi.ptMaxSize.X = lPrimaryScreenInfo.rcWork.Right - lPrimaryScreenInfo.rcWork.Left;
        lMmi.ptMaxSize.Y = lPrimaryScreenInfo.rcWork.Bottom - lPrimaryScreenInfo.rcWork.Top;

        // Set min size
        Point minSize = mTransformToDevice.Transform(new Point(mWindow.MinWidth, mWindow.MinHeight));

        lMmi.ptMinTrackSize.X = (int)minSize.X;
        lMmi.ptMinTrackSize.Y = (int)minSize.Y;

        // Store new size
        mScreenSize = new Rect(lMmi.ptMaxPosition.X, lMmi.ptMaxPosition.Y, lMmi.ptMaxSize.X, lMmi.ptMaxSize.Y);

        // Now we have the max size, allow the host to tweak as needed
        Marshal.StructureToPtr(lMmi, lParam, true);
    }
}

#region Dll Helper Structures

internal enum MonitorOptions : uint
{
    MONITOR_DEFAULTTONULL = 0x00000000,
    MONITOR_DEFAULTTOPRIMARY = 0x00000001,
    MONITOR_DEFAULTTONEAREST = 0x00000002
}


[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public class MONITORINFO
{
    public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
    public Rectangle rcMonitor = new();
    public Rectangle rcWork = new();
    public int dwFlags = 0;
}


[StructLayout(LayoutKind.Sequential)]
public struct Rectangle
{
    public int Left, Top, Right, Bottom;

    public Rectangle(int left, int top, int right, int bottom)
    {
        this.Left = left;
        this.Top = top;
        this.Right = right;
        this.Bottom = bottom;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct MINMAXINFO
{
    public POINT ptReserved;
    public POINT ptMaxSize;
    public POINT ptMaxPosition;
    public POINT ptMinTrackSize;
    public POINT ptMaxTrackSize;
};

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    /// <summary>
    /// x coordinate of point.
    /// </summary>
    public int X;
    /// <summary>
    /// y coordinate of point.
    /// </summary>
    public int Y;

    /// <summary>
    /// Construct a point of coordinates (x,y).
    /// </summary>
    public POINT(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }
}

#endregion
