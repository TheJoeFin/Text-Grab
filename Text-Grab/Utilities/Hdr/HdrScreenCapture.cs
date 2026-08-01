using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Text_Grab.Utilities.Hdr;

/// <summary>
/// Captures a screen region from an HDR display using Windows.Graphics.Capture at full FP16
/// (scRGB) precision, then tone-maps it down to an SDR <see cref="Bitmap"/>. Capturing before
/// the pixels are quantized to 8-bit is what lets us undo the HDR brightness boost that makes
/// GDI screenshots look washed out (issue #111).
///
/// All methods return null (rather than throwing) when the region is not on an HDR display or
/// when any part of the capture pipeline is unavailable, so callers can fall back to GDI.
/// </summary>
public static class HdrScreenCapture
{
    internal readonly record struct HdrCaptureSegment(
        MonitorHdrInfo Monitor,
        Rectangle CaptureRegion,
        Point Destination);

    private static readonly Guid ID3D11Texture2DGuid = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    // Reasonable default when the OS won't report the SDR white level; matches the typical
    // Windows "SDR content brightness" default so the correction is close even without it.
    private const double FallbackSdrWhiteNits = 200.0;

    // Bounded so a capture that never receives a frame (e.g. WGC not delivering one for fully
    // static content) falls back to GDI quickly instead of stalling the calling thread — which is
    // often the UI thread. A healthy capture delivers its first frame within a few refresh
    // intervals, so this still leaves ample margin.
    private const int FrameTimeoutMilliseconds = 250;

    private static readonly object _captureLock = new();

    // The D3D11 device, its immediate context, and the WinRT device projection are expensive to
    // create, so they are built once and reused across captures. A failed capture disposes them via
    // DisposeSharedDevice so the next call rebuilds a fresh device, which also recovers a lost one.
    private static ID3D11Device? _sharedDevice;
    private static ID3D11DeviceContext? _sharedContext;
    private static IDirect3DDevice? _sharedWinRtDevice;

    /// <summary>
    /// Attempts to capture the given virtual-desktop region as an SDR bitmap using HDR-aware
    /// capture. Returns null if the region is not on an HDR-active display or capture fails.
    /// </summary>
    public static Bitmap? TryCaptureRegion(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return null;

        if (!GraphicsCaptureSession.IsSupported())
            return null;

        IReadOnlyList<MonitorHdrInfo> monitors = DisplayHdrInfo.GetForRegion(region);
        HdrCaptureSegment[] segments = BuildCaptureSegments(region, monitors);
        if (segments.Length == 0)
            return null;

        lock (_captureLock)
        {
            try
            {
                return CaptureCompositeRegion(region, segments);
            }
            catch
            {
                // Any failure in the HDR pipeline (including a lost device): drop the shared device
                // so the next capture rebuilds it, and let the caller fall back to the GDI capture.
                DisposeSharedDevice();
                return null;
            }
        }
    }

    internal static HdrCaptureSegment[] BuildCaptureSegments(
        Rectangle region,
        IEnumerable<MonitorHdrInfo> monitors)
        => monitors
            .Where(monitor => monitor.IsHdrActive)
            .Select(monitor =>
            {
                Rectangle intersection = Rectangle.Intersect(region, monitor.DesktopRect);
                return new HdrCaptureSegment(
                    monitor,
                    intersection,
                    new Point(intersection.Left - region.Left, intersection.Top - region.Top));
            })
            .Where(segment => segment.CaptureRegion.Width > 0 && segment.CaptureRegion.Height > 0)
            .ToArray();

    private static Bitmap? CaptureCompositeRegion(
        Rectangle region,
        IReadOnlyList<HdrCaptureSegment> segments)
    {
        Bitmap composite = new(region.Width, region.Height, PixelFormat.Format32bppArgb);

        try
        {
            using (Graphics graphics = Graphics.FromImage(composite))
            {
                graphics.CopyFromScreen(
                    region.Left,
                    region.Top,
                    0,
                    0,
                    composite.Size,
                    CopyPixelOperation.SourceCopy);
            }

            foreach (HdrCaptureSegment segment in segments)
            {
                using Bitmap? hdrSegment = CaptureHdrRegion(segment.CaptureRegion, segment.Monitor);
                if (hdrSegment is null)
                {
                    composite.Dispose();
                    return null;
                }

                using Graphics graphics = Graphics.FromImage(composite);
                graphics.DrawImageUnscaled(hdrSegment, segment.Destination);
            }

            return composite;
        }
        catch
        {
            composite.Dispose();
            throw;
        }
    }

    private static Bitmap? CaptureHdrRegion(Rectangle region, MonitorHdrInfo monitor)
    {
        if (!TryGetSharedDevice(out ID3D11Device d3dDevice, out ID3D11DeviceContext context, out IDirect3DDevice winrtDevice))
            return null;

        GraphicsCaptureItem? item = CreateItemForMonitor(monitor.Monitor);
        if (item is null)
            return null;

        Direct3D11CaptureFramePool framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winrtDevice,
            DirectXPixelFormat.R16G16B16A16Float,
            1,
            item.Size);

        GraphicsCaptureSession session = framePool.CreateCaptureSession(item);
        TrySet(() => session.IsCursorCaptureEnabled = false);

        // The yellow/orange capture border is only removable once the app has been granted
        // "borderless" capture access; setting IsBorderRequired without it throws. The access
        // request must never block the capture thread (it may show a consent dialog that needs
        // the UI pump), so we only disable the border when access has already been granted and
        // otherwise kick off a one-time, non-blocking request that self-heals on later captures.
        if (_borderlessGranted)
            TrySet(() => session.IsBorderRequired = false);
        else
            EnsureBorderlessRequestedOnce();

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

            return ToneMapFrameToBitmap(frame, region, monitor, d3dDevice, context);
        }
        finally
        {
            framePool.FrameArrived -= OnFrameArrived;
            frame?.Dispose();
            session.Dispose();
            framePool.Dispose();
        }
    }

    /// <summary>
    /// Returns the process-wide D3D11 device, its immediate context, and the WinRT device
    /// projection, creating them on first use and reusing them on later captures. Every caller runs
    /// under <see cref="_captureLock"/>, so the shared immediate context is only ever touched by one
    /// capture at a time. Returns false if a device could not be created.
    /// </summary>
    private static bool TryGetSharedDevice(out ID3D11Device device, out ID3D11DeviceContext context, out IDirect3DDevice winrtDevice)
    {
        if (_sharedDevice is not null && _sharedContext is not null && _sharedWinRtDevice is not null)
        {
            device = _sharedDevice;
            context = _sharedContext;
            winrtDevice = _sharedWinRtDevice;
            return true;
        }

        DisposeSharedDevice();

        device = null!;
        context = null!;
        winrtDevice = null!;

        FeatureLevel[] featureLevels =
        [
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0,
        ];

        if (D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, featureLevels, out ID3D11Device? created).Failure
            || created is null)
        {
            return false;
        }

        IDirect3DDevice? winrt = CreateWinRtDevice(created);
        if (winrt is null)
        {
            created.Dispose();
            return false;
        }

        _sharedDevice = created;
        _sharedContext = created.ImmediateContext;
        _sharedWinRtDevice = winrt;

        device = _sharedDevice;
        context = _sharedContext;
        winrtDevice = _sharedWinRtDevice;
        return true;
    }

    /// <summary>
    /// Disposes and clears the cached device, context, and WinRT device so the next capture rebuilds
    /// a fresh device. Called after a capture failure, which also recovers from a lost device.
    /// </summary>
    private static void DisposeSharedDevice()
    {
        _sharedWinRtDevice?.Dispose();
        _sharedWinRtDevice = null;
        _sharedContext?.Dispose();
        _sharedContext = null;
        _sharedDevice?.Dispose();
        _sharedDevice = null;
    }

    private static Bitmap? ToneMapFrameToBitmap(
        Direct3D11CaptureFrame frame,
        Rectangle region,
        MonitorHdrInfo monitor,
        ID3D11Device d3dDevice,
        ID3D11DeviceContext context)
    {
        using ID3D11Texture2D sourceTexture = GetTexture(frame.Surface);

        Texture2DDescription desc = sourceTexture.Description;
        int textureWidth = (int)desc.Width;
        int textureHeight = (int)desc.Height;

        desc.Usage = ResourceUsage.Staging;
        desc.BindFlags = BindFlags.None;
        desc.CPUAccessFlags = CpuAccessFlags.Read;
        desc.MiscFlags = ResourceOptionFlags.None;

        using ID3D11Texture2D staging = d3dDevice.CreateTexture2D(desc);
        context.CopyResource(staging, sourceTexture);

        MappedSubresource map = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            double sdrWhiteNits = monitor.SdrWhiteNits > 0 ? monitor.SdrWhiteNits : FallbackSdrWhiteNits;
            double sdrWhiteScale = HdrToneMapper.SdrWhiteScaleFromNits(sdrWhiteNits);

            int offsetX = region.Left - monitor.DesktopRect.Left;
            int offsetY = region.Top - monitor.DesktopRect.Top;

            Bitmap bmp = new(region.Width, region.Height, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, region.Width, region.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* sourceBase = (byte*)map.DataPointer;

                    for (int y = 0; y < region.Height; y++)
                    {
                        byte* destRow = (byte*)data.Scan0 + (long)y * data.Stride;
                        int sourceY = offsetY + y;

                        if (sourceY < 0 || sourceY >= textureHeight)
                            continue;

                        ushort* sourceRow = (ushort*)(sourceBase + (long)sourceY * map.RowPitch);

                        for (int x = 0; x < region.Width; x++)
                        {
                            byte* destPixel = destRow + x * 4;
                            int sourceX = offsetX + x;

                            if (sourceX < 0 || sourceX >= textureWidth)
                            {
                                destPixel[3] = 255;
                                continue;
                            }

                            ushort* sourcePixel = sourceRow + sourceX * 4;
                            double r = (float)BitConverter.UInt16BitsToHalf(sourcePixel[0]);
                            double g = (float)BitConverter.UInt16BitsToHalf(sourcePixel[1]);
                            double b = (float)BitConverter.UInt16BitsToHalf(sourcePixel[2]);

                            // System.Drawing 32bppArgb is stored BGRA in memory.
                            destPixel[0] = HdrToneMapper.ScRgbChannelToSrgbByte(b, sdrWhiteScale);
                            destPixel[1] = HdrToneMapper.ScRgbChannelToSrgbByte(g, sdrWhiteScale);
                            destPixel[2] = HdrToneMapper.ScRgbChannelToSrgbByte(r, sdrWhiteScale);
                            destPixel[3] = 255;
                        }
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

    private static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        IDirect3DDxgiInterfaceAccess access = surface.As<IDirect3DDxgiInterfaceAccess>();
        Guid guid = ID3D11Texture2DGuid;
        IntPtr texturePointer = access.GetInterface(ref guid);
        return new ID3D11Texture2D(texturePointer);
    }

    private static IDirect3DDevice? CreateWinRtDevice(ID3D11Device d3dDevice)
    {
        using IDXGIDevice dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();

        if (CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr graphicsDevice) != 0)
            return null;

        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevice);
        }
        finally
        {
            Marshal.Release(graphicsDevice);
        }
    }

    private static GraphicsCaptureItem? CreateItemForMonitor(IntPtr hmon)
    {
        if (hmon == IntPtr.Zero)
            return null;

        IGraphicsCaptureItemInterop interop = ActivationFactory
            .Get("Windows.Graphics.Capture.GraphicsCaptureItem")
            .AsInterface<IGraphicsCaptureItemInterop>();

        Guid guid = GraphicsCaptureItemGuid;
        IntPtr itemPointer = interop.CreateForMonitor(hmon, ref guid);

        try
        {
            return GraphicsCaptureItem.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    private static void TrySet(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Property not supported on this OS build; ignore.
        }
    }

    private static volatile bool _borderlessGranted;
    private static int _borderlessRequestStarted;

    /// <summary>
    /// True once the OS has granted this process permission to capture without the capture border.
    /// </summary>
    public static bool IsBorderlessGranted => _borderlessGranted;

    /// <summary>
    /// Requests permission to capture without the OS-drawn capture border. May show a one-time
    /// consent dialog, so this must be awaited from the UI thread (e.g. a settings button), never
    /// from the capture path. Returns the resulting access status.
    /// </summary>
    public static async System.Threading.Tasks.Task<Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus> RequestBorderlessAccessAsync()
    {
        // Mark as started so the capture path won't also fire a duplicate request.
        System.Threading.Interlocked.Exchange(ref _borderlessRequestStarted, 1);

        try
        {
            Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus status =
                await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless);

            _borderlessGranted = status == Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.Allowed;
            return status;
        }
        catch
        {
            _borderlessGranted = false;
            return Windows.Security.Authorization.AppCapabilityAccess.AppCapabilityAccessStatus.DeniedBySystem;
        }
    }

    /// <summary>
    /// Fires a single non-blocking borderless-access request on the UI thread. When the user has
    /// already consented in a previous session the request completes silently and later captures
    /// drop the border automatically; otherwise the border stays until the user grants access.
    /// </summary>
    private static void EnsureBorderlessRequestedOnce()
    {
        // Only silently re-activate for users who already granted access in a past session, so a
        // consent prompt never appears unexpectedly during a grab. First-time consent is explicit,
        // via the "Check permissions" button in settings.
        if (!AppUtilities.TextGrabSettings.HdrBorderlessGranted)
            return;

        if (System.Threading.Interlocked.Exchange(ref _borderlessRequestStarted, 1) != 0)
            return;

        System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        _ = dispatcher.InvokeAsync(async () => await RequestBorderlessAccessAsync());
    }

    #region WinRT / D3D interop

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
    private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] ref Guid iid);
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
        IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
    }

    #endregion
}
