# Developer Documentation: `DisplayHdrInfo.cs`

**File Path:** `Text-Grab/Utilities/Hdr/DisplayHdrInfo.cs`  
**Namespace:** `Text_Grab.Utilities.Hdr`

---

## Overview

The `DisplayHdrInfo.cs` file provides utility methods and structures for querying per-monitor High Dynamic Range (HDR) status, virtual-desktop bounds, and SDR reference white levels across connected displays.

It combines two primary technologies:
1. **DirectX Graphics Infrastructure (DXGI)** via `Vortice.DXGI` to inspect output desktop coordinates and active color space settings (`IDXGIOutput6`).
2. **Win32 Display Configuration APIs** via P/Invoke (`user32.dll`) to match GDI device names to display targets and retrieve the SDR white level (in nits) required for tone-mapping HDR frames.

---

## Data Structures

### `MonitorHdrInfo`

A `readonly record struct` that represents the HDR state and display attributes of a single monitor.

```csharp
public readonly record struct MonitorHdrInfo(
    IntPtr Monitor,
    Rectangle DesktopRect,
    bool IsHdrActive,
    double SdrWhiteNits);
```

#### Properties

| Property | Type | Description |
| :--- | :--- | :--- |
| `Monitor` | `IntPtr` | Native `HMONITOR` handle, used for creating capture items (such as `Windows.Graphics.Capture`). |
| `DesktopRect` | `System.Drawing.Rectangle` | The display's bounding rectangle in virtual-desktop (physical pixel) coordinates. |
| `IsHdrActive` | `bool` | Set to `true` if the monitor is actively outputting an HDR color space (`ColorSpaceType.RgbFullG2084NoneP2020`). |
| `SdrWhiteNits` | `double` | The display's SDR reference white level in nits. Evaluates to `0` if HDR is inactive or the value cannot be retrieved. |

---

## Class: `DisplayHdrInfo`

`DisplayHdrInfo` is a `static` class providing public and internal methods to query monitor HDR information.

### Public & Internal API

#### `TryGetForPoint(int x, int y, out MonitorHdrInfo info)`
Finds the monitor whose virtual-desktop coordinates contain the given `(x, y)` point.

* **Parameters:**
  * `x` (`int`): X-coordinate in physical virtual-desktop space.
  * `y` (`int`): Y-coordinate in physical virtual-desktop space.
  * `info` (`out MonitorHdrInfo`): Output parameter populated with matching monitor details if found.
* **Returns:** `bool` — `true` if a valid monitor was found at the given point (i.e., `Monitor` handle is non-zero); otherwise, `false`.

#### `GetForRegion(Rectangle region)`
Finds all monitors that intersect with a given virtual-desktop rectangular region.

* **Parameters:**
  * `region` (`System.Drawing.Rectangle`): Target virtual-desktop region.
* **Returns:** `IReadOnlyList<MonitorHdrInfo>` — A list of monitors whose desktop rectangles overlap with the specified `region` with an intersection width and height greater than `0`.

---

## Private Methods & Logic

### DXGI Monitor Enumeration (`GetAll`)

```csharp
private static IReadOnlyList<MonitorHdrInfo> GetAll()
```

Enumerates all active display adapters and outputs to collect HDR configuration for every connected monitor.

#### Process Workflow:
1. **Factory Creation:** Calls `DXGI.CreateDXGIFactory1` to get an `IDXGIFactory1` instance.
2. **Adapter Enumeration:** Iterates through adapters using `factory.EnumAdapters1`.
3. **Output Enumeration:** Iterates through outputs using `adapter.EnumOutputs`.
4. **Interface Query:** Queries `IDXGIOutput` for the `IDXGIOutput6` interface using `QueryInterfaceOrNull<IDXGIOutput6>()`. If `IDXGIOutput6` is unavailable, the output is skipped.
5. **Coordinate Calculation:** Reads `OutputDescription1.DesktopCoordinates` (`RawRect`) and converts it to a `System.Drawing.Rectangle`.
6. **HDR Active Check:** Evaluates whether `OutputDescription1.ColorSpace` equals `ColorSpaceType.RgbFullG2084NoneP2020` (ST.2084 / BT.2020 signal).
7. **SDR White Level Retrieval:** If HDR is active, calls `GetSdrWhiteNits(desc.DeviceName)` using the monitor's GDI device name; otherwise sets the value to `0`.
8. **Exception Safety:** If an exception occurs during DXGI enumeration, the returned list is cleared and returned empty, triggering callers to fall back to standard GDI behavior.

---

### Win32 SDR White Level Query (`GetSdrWhiteNits`)

```csharp
private static double GetSdrWhiteNits(string gdiDeviceName)
```

Queries the Win32 Display Configuration API to determine the display's configured SDR white level in nits.

#### Process Workflow:
1. **Buffer Size Allocation:** Calls `GetDisplayConfigBufferSizes` with `QDC_ONLY_ACTIVE_PATHS` to allocate path (`DISPLAYCONFIG_PATH_INFO[]`) and mode (`DISPLAYCONFIG_MODE_INFO[]`) arrays.
2. **Config Query:** Populates array data via `QueryDisplayConfig`.
3. **Device Matching:** Iterates through active paths and issues `DisplayConfigGetDeviceInfo` with request type `DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME` to locate the source matching `gdiDeviceName` (case-insensitive comparison against `viewGdiDeviceName`).
4. **SDR White Level Reading:** Issues `DisplayConfigGetDeviceInfo` with request type `DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL` (`DISPLAYCONFIG_SDR_WHITE_LEVEL`) using target info from the matching path.
5. **Unit Conversion Formula:**
   $$ \text{SdrWhiteNits} = \frac{\text{SDRWhiteLevel}}{1000.0} \times \text{HdrToneMapper.SdrReferenceWhiteNits} $$
   *(Note: `SDRWhiteLevel` is provided by Windows in units of thousandths of 80 nits).*
6. **Fallback:** Returns `0` if matching fails, the value is less than or equal to `0`, or an exception occurs.

---

## Native Interop (P/Invoke)

The class defines low-level Win32 Display Configuration structures and imports from `user32.dll`.

### Imported Functions

```csharp
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
```

### Key Constants

* `QDC_ONLY_ACTIVE_PATHS = 0x00000002`: Query flag to restrict display config results to active paths.
* `DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1`: Request ID to fetch the GDI view device name.
* `DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL = 11`: Request ID to fetch the SDR white level.

### Structures

| Structure Name | Layout / Attributes | Purpose |
| :--- | :--- | :--- |
| `LUID` | `LayoutKind.Sequential` | Locally Unique Identifier (`LowPart`, `HighPart`). |
| `DISPLAYCONFIG_RATIONAL` | `LayoutKind.Sequential` | Refresh rate fraction (`Numerator`, `Denominator`). |
| `DISPLAYCONFIG_PATH_SOURCE_INFO` | `LayoutKind.Sequential` | Identifies adapter ID, source ID, and mode info index. |
| `DISPLAYCONFIG_PATH_TARGET_INFO` | `LayoutKind.Sequential` | Describes target display technology, scaling, rotation, and status flags. |
| `DISPLAYCONFIG_PATH_INFO` | `LayoutKind.Sequential` | Combines source and target path information. |
| `DISPLAYCONFIG_MODE_INFO` | `LayoutKind.Sequential, Size = 64` | Fixed 64-byte union buffer pass-through. |
| `DISPLAYCONFIG_DEVICE_INFO_HEADER` | `LayoutKind.Sequential` | Common header for device info requests (`type`, `size`, `adapterId`, `id`). |
| `DISPLAYCONFIG_SOURCE_DEVICE_NAME` | `LayoutKind.Sequential, CharSet = CharSet.Unicode` | Request packet containing 32-character buffer for `viewGdiDeviceName`. |
| `DISPLAYCONFIG_SDR_WHITE_LEVEL` | `LayoutKind.Sequential` | Request packet containing `SDRWhiteLevel` output field. |

---

## Dependencies

* **`Vortice.DXGI`**: Provides managed wrappers for DXGI COM interfaces (`IDXGIFactory1`, `IDXGIAdapter1`, `IDXGIOutput`, `IDXGIOutput6`).
* **`HdrToneMapper`**: External project utility referenced via `HdrToneMapper.SdrReferenceWhiteNits` to calculate absolute nits.