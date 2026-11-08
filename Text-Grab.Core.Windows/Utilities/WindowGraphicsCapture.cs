using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using Vortice.Direct3D11;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Text_Grab.Utilities.Hdr;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Text_Grab.Utilities;

/// <summary>
/// Captures the pixels of a single top-level window with Windows.Graphics.Capture, so the result
/// is the window itself rather than whatever happens to be on screen over the window's bounds
/// (issue #688). Returns null on any failure so callers can fall back to a region capture.
/// </summary>
public static class WindowGraphicsCapture
{
    private const int FrameTimeoutMilliseconds = 500;

    public static Bitmap? TryCaptureWindow(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero || !GraphicsCaptureSession.IsSupported())
                return null;

            lock (HdrScreenCapture._captureLock)
            {
                try
                {
                    return CaptureWindow(hwnd);
                }
                catch
                {
                    HdrScreenCapture.DisposeSharedDevice();
                    return null;
                }
            }
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? CaptureWindow(IntPtr hwnd)
    {
        if (!HdrScreenCapture.TryGetSharedDevice(out ID3D11Device d3dDevice, out ID3D11DeviceContext context, out IDirect3DDevice winrtDevice))
            return null;

        GraphicsCaptureItem? item = HdrScreenCapture.CreateItemForWindow(hwnd);
        if (item is null || item.Size.Width <= 0 || item.Size.Height <= 0)
            return null;

        Direct3D11CaptureFramePool framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winrtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            item.Size);

        GraphicsCaptureSession session = framePool.CreateCaptureSession(item);
        HdrScreenCapture.TrySet(() => session.IsCursorCaptureEnabled = false);

        if (HdrScreenCapture._borderlessGranted)
            HdrScreenCapture.TrySet(() => session.IsBorderRequired = false);
        else
            HdrScreenCapture.EnsureBorderlessRequestedOnce();

        using ManualResetEventSlim frameReady = new(false);
        Direct3D11CaptureFrame? frame = null;

        void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            if (frame is not null)
                return;

            frame = sender.TryGetNextFrame();
            if (frame is not null)
                frameReady.Set();
        }

        framePool.FrameArrived += OnFrameArrived;

        try
        {
            session.StartCapture();

            if (!frameReady.Wait(FrameTimeoutMilliseconds) || frame is null)
                return null;

            return FrameToBitmap(frame, d3dDevice, context);
        }
        finally
        {
            framePool.FrameArrived -= OnFrameArrived;
            frame?.Dispose();
            session.Dispose();
            framePool.Dispose();
        }
    }

    private static Bitmap FrameToBitmap(Direct3D11CaptureFrame frame, ID3D11Device d3dDevice, ID3D11DeviceContext context)
    {
        using ID3D11Texture2D sourceTexture = HdrScreenCapture.GetTexture(frame.Surface);

        Texture2DDescription desc = sourceTexture.Description;
        int textureWidth = (int)desc.Width;
        int textureHeight = (int)desc.Height;

        // ContentSize is the real window size; the texture can be larger if the window shrank.
        int width = Math.Clamp(frame.ContentSize.Width, 1, textureWidth);
        int height = Math.Clamp(frame.ContentSize.Height, 1, textureHeight);

        desc.Usage = ResourceUsage.Staging;
        desc.BindFlags = BindFlags.None;
        desc.CPUAccessFlags = CpuAccessFlags.Read;
        desc.MiscFlags = ResourceOptionFlags.None;

        using ID3D11Texture2D staging = d3dDevice.CreateTexture2D(desc);
        context.CopyResource(staging, sourceTexture);

        MappedSubresource map = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            Bitmap bmp = new(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* sourceBase = (byte*)map.DataPointer;
                    for (int y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(
                            sourceBase + (long)y * map.RowPitch,
                            (byte*)data.Scan0 + (long)y * data.Stride,
                            data.Stride,
                            width * 4L);
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }
        finally
        {
            context.Unmap(staging, 0);
        }
    }
}
