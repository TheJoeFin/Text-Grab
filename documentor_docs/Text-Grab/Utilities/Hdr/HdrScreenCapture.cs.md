# Technical Documentation: `Text_Grab.Utilities.Hdr.HdrScreenCapture`

## Overview

The `HdrScreenCapture` static class provides high-precision screen capture capabilities specifically designed for High Dynamic Range (HDR) displays on Windows. 

Standard GDI screen captures on HDR displays often result in washed-out images due to 8-bit quantization and unadjusted HDR brightness boosts. `HdrScreenCapture` solves this issue by leveraging `Windows.Graphics.Capture` (WGC) and Direct3D 11 to capture frames in full FP16 (`R16G16B16A16Float` / scRGB) precision. It then tone-maps the pixel data down to a standard SDR 32-bit ARGB `Bitmap`.

If a capture region does not reside on an active HDR display, or if any part of the Direct3D/WGC pipeline fails or times out, the class fails gracefully by returning `null`, enabling callers to cleanly fall back to traditional GDI screen capture.

---

## Key Features & Design Principles

1. **FP16 High-Precision Capture**: Captures HDR frame data before quantization to retain color range and peak brightness accuracy.
2. **Hybrid Composite Capture**: Performs a base GDI screen capture for the entire region and selectively overlays HDR-captured segments for areas intersecting active HDR monitors.
3. **Shared Direct3D Device Caching**: Caches `ID3D11Device`, `ID3D11DeviceContext`, and `IDirect3DDevice` across capture calls to reduce initialization overhead.
4. **Resilient Thread Synchronization**: Executes captures under a static lock (`_captureLock`) and employs a strict 250ms frame timeout to prevent blocking UI threads on static content.
5. **Automatic Fallback and Recovery**: Disposes and invalidates cached Direct3D devices on any exception (including device loss) and returns `null`.
6. **Non-blocking Borderless Permission Request**: Handles the OS capture border toggles without stalling capture threads.

---

## Data Structures

### `HdrCaptureSegment`
An internal `readonly record struct` that represents an intersection between the target capture region and a specific HDR-active monitor.

```csharp
internal readonly record struct HdrCaptureSegment(
    MonitorHdrInfo Monitor,
    Rectangle CaptureRegion,
    Point Destination);
```
* **`Monitor`**: Metadata for the target HDR display (`MonitorHdrInfo`).
* **`CaptureRegion`**: The sub-rectangle intersect with the monitor in desktop coordinates.
* **`Destination`**: The offset point relative to the composite output bitmap's origin.

---

## Class Constants & Fields

| Field / Constant | Type | Description |
| :--- | :--- | :--- |
| `ID3D11Texture2DGuid` | `Guid` | Interface GUID for retrieving an `ID3D11Texture2D` (`6f15aaf2-d208-4e89-9ab4-489535d34f9c`). |
| `FallbackSdrWhiteNits` | `double` | Default SDR white level reference (`200.0` nits) used when the OS does not report monitor brightness. |
| `FrameTimeoutMilliseconds` | `int` | Maximum time (`250` ms) allowed to wait for a frame from `Direct3D11CaptureFramePool`. |
| `_captureLock` | `object` | Lock instance ensuring single-threaded access to shared D3D11 immediate context and capture pipelines. |
| `_sharedDevice` | `ID3D11Device?` | Cached Direct3D 11 hardware device. |
| `_sharedContext` | `ID3D11DeviceContext?` | Cached immediate device context associated with `_sharedDevice`. |
| `_sharedWinRtDevice` | `IDirect3DDevice?` | Cached WinRT projected Direct3D device. |
| `_borderlessGranted` | `volatile bool` | Tracks whether borderless capture access has been granted by the OS. |
| `_borderlessRequestStarted` | `int` | Atomic flag (0 or 1) tracking whether a borderless permission request has been initiated. |
| `GraphicsCaptureItemGuid` | `Guid` | Interface GUID for `GraphicsCaptureItem` interop (`79C3F95B-31F7-4EC2-A464-632EF5D30760`). |

---

## Public API Methods

### `TryCaptureRegion(Rectangle region)`

```csharp
public static Bitmap? TryCaptureRegion(Rectangle region)
```

Main entry point for capturing a region across virtual desktop coordinates.

* **Parameters**: `region` — Target virtual-desktop bounding box.
* **Returns**: A tone-mapped `Bitmap` (in `PixelFormat.Format32bppArgb`), or `null` if the region is invalid, WGC is unsupported, no active HDR monitors intersect the region, or an error occurs.
* **Execution Flow**:
  1. Validates dimensions (`Width > 0` and `Height > 0`).
  2. Verifies `GraphicsCaptureSession.IsSupported()`.
  3. Queries HDR monitor information for the given region via `DisplayHdrInfo.GetForRegion(region)`.
  4. Generates monitor capture segments via `BuildCaptureSegments`.
  5. Acquires `_captureLock`.
  6. Calls `CaptureCompositeRegion`. If any exception occurs, calls `DisposeSharedDevice()` and returns `null`.

### `RequestBorderlessAccessAsync()`

```csharp
public static async Task<AppCapabilityAccessStatus> RequestBorderlessAccessAsync()
```

Requests capability authorization from Windows to disable the yellow/orange capture border (`GraphicsCaptureAccessKind.Borderless`).

* **Returns**: `Task<AppCapabilityAccessStatus>` indicating authorization status.
* **Usage**: Must be awaited from the UI thread (e.g., during explicit user settings actions) because it may show a user consent dialog.

### `IsBorderlessGranted`

```csharp
public static bool IsBorderlessGranted { get; }
```

Property indicating whether borderless capture authorization has been verified and granted by the OS.

---

## Internal & Private Implementation Details

### Region Segmentation

#### `BuildCaptureSegments`
```csharp
internal static HdrCaptureSegment[] BuildCaptureSegments(
    Rectangle region,
    IEnumerable<MonitorHdrInfo> monitors)
```
Filters the provided monitors for active HDR displays (`IsHdrActive == true`), intersects the requested capture bounding box with each monitor's desktop rectangle (`DesktopRect`), calculates destination point offsets, and returns non-empty `HdrCaptureSegment` objects.

---

### Capture & Compositing Pipeline

#### `CaptureCompositeRegion`
```csharp
private static Bitmap? CaptureCompositeRegion(
    Rectangle region,
    IReadOnlyList<HdrCaptureSegment> segments)
```
1. Creates a target `Bitmap` sized to `region`.
2. Performs a baseline capture of the entire target region using GDI (`Graphics.CopyFromScreen`).
3. Iterates over each HDR segment, calling `CaptureHdrRegion`.
4. Overlays the tone-mapped HDR segment bitmaps onto the destination base bitmap via `Graphics.DrawImageUnscaled`.
5. Returns the composite `Bitmap`. If capturing any segment fails, disposes of the composite image and returns `null`.

#### `CaptureHdrRegion`
```csharp
private static Bitmap? CaptureHdrRegion(Rectangle region, MonitorHdrInfo monitor)
```
Executes the low-level Windows Graphics Capture session for a single HDR display segment:
1. Obtains cached Direct3D 11 devices via `TryGetSharedDevice`.
2. Creates a `GraphicsCaptureItem` for the monitor using `CreateItemForMonitor`.
3. Constructs a free-threaded `Direct3D11CaptureFramePool` with pixel format `DirectXPixelFormat.R16G16B16A16Float` and frame buffer size 1.
4. Creates a `GraphicsCaptureSession` and disables cursor capture.
5. Configures borderless capture properties based on `_borderlessGranted` state or triggers `EnsureBorderlessRequestedOnce`.
6. Subscribes to `FrameArrived` and calls `session.StartCapture()`.
7. Waits on a `ManualResetEventSlim` for up to `250ms` (`FrameTimeoutMilliseconds`).
8. Passes the received `Direct3D11CaptureFrame` to `ToneMapFrameToBitmap`.
9. Cleanly unhooks events and disposes of WGC resources.

---

### Direct3D 11 & Tone Mapping Logic

#### `ToneMapFrameToBitmap`
```csharp
private static Bitmap? ToneMapFrameToBitmap(
    Direct3D11CaptureFrame frame,
    Rectangle region,
    MonitorHdrInfo monitor,
    ID3D11Device d3dDevice,
    ID3D11DeviceContext context)
```
Processes raw Direct3D frame textures into standard sRGB bitmap memory:

1. **Texture Extraction & Staging**:
   * Retrieves the native `ID3D11Texture2D` from `frame.Surface` using `GetTexture`.
   * Creates a staging texture (`CpuAccessFlags.Read`, `Usage = ResourceUsage.Staging`) matching the dimensions of the frame texture.
   * Copies GPU texture data to the staging texture via `context.CopyResource`.
2. **Subresource Mapping**:
   * Maps the staging texture for CPU read access using `context.Map(..., MapMode.Read, ...)`.
3. **Brightness Scaling Calculation**:
   * Determines SDR white level (in nits) from `monitor.SdrWhiteNits` (falling back to `FallbackSdrWhiteNits` if `<= 0`).
   * Computes scaling factors using `HdrToneMapper.SdrWhiteScaleFromNits`.
4. **Unsafe Pixel Processing**:
   * Locks the bits of a new 32-bit ARGB `Bitmap`.
   * Reads raw FP16 data (`ushort*` per channel: R, G, B, A) line-by-line using `BitConverter.UInt16BitsToHalf`.
   * Converts floating-point scRGB channel values into sRGB bytes using `HdrToneMapper.ScRgbChannelToSrgbByte`.
   * Writes values into System.Drawing memory in **BGRA** byte order (`destPixel[0] = B`, `destPixel[1] = G`, `destPixel[2] = R`, `destPixel[3] = 255`).
5. **Cleanup**:
   * Unlocks bitmap bits and unmaps staging texture resource.

---

### Direct3D Device Management

#### `TryGetSharedDevice`
```csharp
private static bool TryGetSharedDevice(
    out ID3D11Device device,
    out ID3D11DeviceContext context,
    out IDirect3DDevice winrtDevice)
```
Creates or returns existing Direct3D hardware objects.
* Feature level preference order: `11_1`, `11_0`, `10_1`, `10_0`.
* Configures creation flags: `DeviceCreationFlags.BgraSupport`.
* Converts Direct3D device to WinRT projected device `IDirect3DDevice` using `CreateWinRtDevice`.

#### `DisposeSharedDevice`
```csharp
private static void DisposeSharedDevice()
```
Disposes and clears references to `_sharedWinRtDevice`, `_sharedContext`, and `_sharedDevice`. Called when capture pipelines hit an exception or require a device reset.

---

### Non-blocking Permission Management

#### `EnsureBorderlessRequestedOnce`
```csharp
private static void EnsureBorderlessRequestedOnce()
```
Checks if borderless capture has been enabled in settings (`HdrBorderlessGranted`) and triggers an asynchronous, non-blocking call to `RequestBorderlessAccessAsync()` on the Application WPF Dispatcher thread if it has not already been started in the process lifecycle.

---

## Native Interop & COM Interfaces

The class defines internal COM interfaces and P/Invoke signatures to bridge native Direct3D 11, DXGI, and Windows App SDK/WinRT constructs.

```csharp
[DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

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
```

* **`CreateDirect3D11DeviceFromDXGIDevice`**: Win32 API that constructs a C++/WinRT `IDirect3DDevice` from a DXGI device pointer.
* **`IDirect3DDxgiInterfaceAccess`**: Native COM interface used in `GetTexture` to extract underlying DXGI/D3D pointers from WinRT `IDirect3DSurface` objects.
* **`IGraphicsCaptureItemInterop`**: Interop COM interface used in `CreateItemForMonitor` to manufacture `GraphicsCaptureItem` instances directly from display monitor handles (`HMONITOR` / `IntPtr`).