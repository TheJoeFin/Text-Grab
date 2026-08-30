using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.DXGI;

namespace Text_Grab.Utilities.Hdr;

/// <summary>
/// Describes the HDR state of a single monitor as reported by DXGI and the OS display config.
/// </summary>
/// <param name="Monitor">The HMONITOR handle, used to create a Windows.Graphics.Capture item.</param>
/// <param name="DesktopRect">The monitor's bounds in virtual-desktop (physical pixel) coordinates.</param>
/// <param name="IsHdrActive">True when the monitor is currently outputting an HDR (ST.2084) color space.</param>
/// <param name="SdrWhiteNits">The SDR reference white level in nits (0 if unknown).</param>
public readonly record struct MonitorHdrInfo(
    IntPtr Monitor,
    Rectangle DesktopRect,
    bool IsHdrActive,
    double SdrWhiteNits);

/// <summary>
/// Queries per-monitor HDR status. Detection uses <c>IDXGIOutput6</c> to read the output's current
/// color space (so it reflects whether HDR is actually <em>on</em>, not merely supported), and the
/// Win32 display-config APIs to read the SDR white level needed to tone-map captured frames.
/// </summary>
public static class DisplayHdrInfo
{
    /// <summary>
    /// Finds the monitor containing the given virtual-desktop point and returns its HDR info.
    /// Returns false if no matching output could be enumerated (e.g. remote sessions).
    /// </summary>
    public static bool TryGetForPoint(int x, int y, out MonitorHdrInfo info)
    {
        info = GetAll().FirstOrDefault(monitor => monitor.DesktopRect.Contains(x, y));
        return info.Monitor != IntPtr.Zero;
    }

    internal static IReadOnlyList<MonitorHdrInfo> GetForRegion(Rectangle region)
        => [.. GetAll().Where(monitor => Rectangle.Intersect(region, monitor.DesktopRect) is { Width: > 0, Height: > 0 })];

    private static IReadOnlyList<MonitorHdrInfo> GetAll()
    {
        List<MonitorHdrInfo> monitors = [];

        try
        {
            if (DXGI.CreateDXGIFactory1(out IDXGIFactory1? factory).Failure || factory is null)
                return monitors;

            using (factory)
            {
                for (uint a = 0; factory.EnumAdapters1(a, out IDXGIAdapter1? adapter).Success && adapter is not null; a++)
                {
                    using (adapter)
                    {
                        for (uint o = 0; adapter.EnumOutputs(o, out IDXGIOutput? output).Success && output is not null; o++)
                        {
                            using (output)
                            {
                                using IDXGIOutput6? output6 = output.QueryInterfaceOrNull<IDXGIOutput6>();
                                if (output6 is null)
                                    continue;

                                OutputDescription1 desc = output6.Description1;
                                RawRect coords = desc.DesktopCoordinates;
                                Rectangle rect = new(
                                    coords.Left,
                                    coords.Top,
                                    coords.Right - coords.Left,
                                    coords.Bottom - coords.Top);

                                bool hdrActive = desc.ColorSpace == ColorSpaceType.RgbFullG2084NoneP2020;
                                double sdrWhite = hdrActive ? GetSdrWhiteNits(desc.DeviceName) : 0;

                                monitors.Add(new MonitorHdrInfo(desc.Monitor, rect, hdrActive, sdrWhite));
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Any failure enumerating adapters means we can't confirm HDR; caller falls back to GDI.
            monitors.Clear();
        }

        return monitors;
    }

    /// <summary>
    /// Reads the SDR reference white level (in nits) for the display whose GDI device name is given.
    /// Returns 0 when it cannot be determined; callers should assume a sensible default in that case.
    /// </summary>
    private static double GetSdrWhiteNits(string gdiDeviceName)
    {
        try
        {
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount) != 0)
                return 0;

            DISPLAYCONFIG_PATH_INFO[] paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            DISPLAYCONFIG_MODE_INFO[] modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
                return 0;

            for (int i = 0; i < pathCount; i++)
            {
                DISPLAYCONFIG_PATH_INFO path = paths[i];

                DISPLAYCONFIG_SOURCE_DEVICE_NAME sourceName = new();
                sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                sourceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                sourceName.header.adapterId = path.sourceInfo.adapterId;
                sourceName.header.id = path.sourceInfo.id;

                if (DisplayConfigGetDeviceInfo(ref sourceName) != 0)
                    continue;

                if (!string.Equals(sourceName.viewGdiDeviceName, gdiDeviceName, StringComparison.OrdinalIgnoreCase))
                    continue;

                DISPLAYCONFIG_SDR_WHITE_LEVEL whiteLevel = new();
                whiteLevel.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL;
                whiteLevel.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SDR_WHITE_LEVEL>();
                whiteLevel.header.adapterId = path.targetInfo.adapterId;
                whiteLevel.header.id = path.targetInfo.id;

                if (DisplayConfigGetDeviceInfo(ref whiteLevel) == 0 && whiteLevel.SDRWhiteLevel > 0)
                {
                    // SDRWhiteLevel is in units of 1/1000th of 80 nits.
                    return whiteLevel.SDRWhiteLevel / 1000.0 * HdrToneMapper.SdrReferenceWhiteNits;
                }
            }
        }
        catch
        {
            return 0;
        }

        return 0;
    }

    #region Display config interop

    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const int DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    private const int DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL = 11;

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SDR_WHITE_LEVEL requestPacket);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public int outputTechnology;
        public int rotation;
        public int scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public int scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    // The mode info union is 64 bytes total; we never read its contents, only pass the buffer through.
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public int type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SDR_WHITE_LEVEL
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint SDRWhiteLevel;
    }

    #endregion
}
