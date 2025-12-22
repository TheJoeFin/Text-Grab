using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Text_Grab.Utilities;

public static class HdrUtilities
{
    /// <summary>
    /// Checks if HDR is enabled on the monitor at the specified screen coordinates.
    /// </summary>
    /// <param name="x">X coordinate on screen</param>
    /// <param name="y">Y coordinate on screen</param>
    /// <returns>True if HDR is enabled on the monitor, false otherwise</returns>
    public static bool IsHdrEnabledAtPoint(int x, int y)
    {
        try
        {
            NativeMethods.POINT pt = new() { X = x, Y = y };
            IntPtr hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);

            if (hMonitor == IntPtr.Zero)
                return false;

            NativeMethods.MONITORINFOEX monitorInfo = new()
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
            };

            if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
                return false;

            IntPtr hdc = NativeMethods.CreateDC(null, monitorInfo.szDevice, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero)
                return false;

            try
            {
                int colorCaps = NativeMethods.GetDeviceCaps(hdc, NativeMethods.COLORMGMTCAPS);
                return (colorCaps & NativeMethods.CM_HDR_SUPPORT) != 0;
            }
            finally
            {
                NativeMethods.DeleteDC(hdc);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Converts an HDR bitmap to SDR (Standard Dynamic Range) by applying tone mapping.
    /// This fixes the overly bright appearance of screenshots taken on HDR displays.
    /// </summary>
    /// <param name="bitmap">The bitmap to convert</param>
    /// <returns>A new bitmap with SDR color values, or null if the input is null</returns>
    public static Bitmap? ConvertHdrToSdr(Bitmap? bitmap)
    {
        if (bitmap == null)
            return null;

        try
        {
            // Create a new bitmap with the same dimensions
            Bitmap result = new(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);

            // Lock both bitmaps for fast pixel access
            BitmapData sourceData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            BitmapData resultData = result.LockBits(
                new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* sourcePtr = (byte*)sourceData.Scan0;
                    byte* resultPtr = (byte*)resultData.Scan0;

                    int bytes = Math.Abs(sourceData.Stride) * sourceData.Height;

                    // Process each pixel
                    for (int i = 0; i < bytes; i += 4)
                    {
                        // Read BGRA values
                        byte b = sourcePtr[i];
                        byte g = sourcePtr[i + 1];
                        byte r = sourcePtr[i + 2];
                        byte a = sourcePtr[i + 3];

                        // Convert to linear RGB space (0.0 to 1.0)
                        double rLinear = SrgbToLinear(r / 255.0);
                        double gLinear = SrgbToLinear(g / 255.0);
                        double bLinear = SrgbToLinear(b / 255.0);

                        // Apply simple tone mapping (Reinhard operator)
                        // This compresses the HDR range to SDR range
                        rLinear = ToneMap(rLinear);
                        gLinear = ToneMap(gLinear);
                        bLinear = ToneMap(bLinear);

                        // Convert back to sRGB space
                        r = (byte)Math.Clamp((int)(LinearToSrgb(rLinear) * 255.0 + 0.5), 0, 255);
                        g = (byte)Math.Clamp((int)(LinearToSrgb(gLinear) * 255.0 + 0.5), 0, 255);
                        b = (byte)Math.Clamp((int)(LinearToSrgb(bLinear) * 255.0 + 0.5), 0, 255);

                        // Write BGRA values
                        resultPtr[i] = b;
                        resultPtr[i + 1] = g;
                        resultPtr[i + 2] = r;
                        resultPtr[i + 3] = a;
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(sourceData);
                result.UnlockBits(resultData);
            }

            return result;
        }
        catch
        {
            // If conversion fails, return original bitmap
            return bitmap;
        }
    }

    /// <summary>
    /// Converts sRGB color value to linear RGB.
    /// </summary>
    private static double SrgbToLinear(double value)
    {
        if (value <= 0.04045)
            return value / 12.92;
        else
            return Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// Converts linear RGB value to sRGB.
    /// </summary>
    private static double LinearToSrgb(double value)
    {
        if (value <= 0.0031308)
            return 12.92 * value;
        else
            return 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055;
    }

    /// <summary>
    /// Applies tone mapping to compress HDR values to SDR range.
    /// Uses a modified Reinhard operator with exposure adjustment to preserve mid-tones while compressing highlights.
    /// Formula: L_out = (L_in * exposure) / (1 + L_in * exposure)
    /// </summary>
    private static double ToneMap(double value)
    {
        const double exposure = 0.8; // Adjust exposure to darken the image slightly
        value *= exposure;
        
        // Apply tone mapping
        return value / (1.0 + value);
    }
}
