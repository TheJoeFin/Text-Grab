# Technical Documentation: `HdrScreenCaptureTests.cs`

## Overview

The `HdrScreenCaptureTests` class is a unit test suite written in C# using the **xUnit** testing framework. It validates the behavior of `HdrScreenCapture.BuildCaptureSegments`, a utility method responsible for calculating target screen capture regions and mapping them across multiple monitors (including HDR vs. SDR status and bounds intersections).

- **File Location:** `Tests/HdrScreenCaptureTests.cs`
- **Namespace:** `Tests`
- **Tested Class/Method:** `Text_Grab.Utilities.Hdr.HdrScreenCapture.BuildCaptureSegments`

---

## Dependencies & Namespaces

The file relies on the following namespaces:

- `System.Drawing`: Provides foundational geometry structures (`Rectangle`, `Point`).
- `Text_Grab.Utilities.Hdr`: Contains the HDR screen capture utilities and monitor data structures being tested (`HdrScreenCapture`, `MonitorHdrInfo`).

---

## Key Components & Test Cases

The class contains two unit test methods decorated with xUnit's `[Fact]` attribute:

### 1. `BuildCaptureSegments_MapsCrossMonitorRegionsToCompositeCoordinates`

#### Purpose
Verifies that when a target capture region spans across multiple HDR-enabled monitors, `BuildCaptureSegments` correctly splits the region into individual monitor capture segments and assigns the proper composite destination coordinates for stitching them together.

#### Test Setup
- **Capture Region:** `Rectangle(-200, 100, 500, 300)`
- **Monitors Provided:**
  1. **Monitor 1 (Left):** Handle `(IntPtr)1`, Bounds `Rectangle(-1920, 0, 1920, 1080)`, HDR Enabled (`true`), White Level `200`
  2. **Monitor 2 (Right):** Handle `(IntPtr)2`, Bounds `Rectangle(0, 0, 2560, 1440)`, HDR Enabled (`true`), White Level `160`

#### Execution & Assertions
Calls `HdrScreenCapture.BuildCaptureSegments(region, monitors)` and asserts that exactly two segments are generated:

1. **First Segment (Left Monitor Intersection):**
   - **`CaptureRegion`**: `Rectangle(-200, 100, 200, 300)`
   - **`Destination`**: `Point(0, 0)`
2. **Second Segment (Right Monitor Intersection):**
   - **`CaptureRegion`**: `Rectangle(0, 100, 300, 300)`
   - **`Destination`**: `Point(200, 0)`

---

### 2. `BuildCaptureSegments_ExcludesSdrAndNonIntersectingMonitors`

#### Purpose
Ensures that `BuildCaptureSegments` ignores monitors that either:
1. Do not have HDR enabled (SDR monitors).
2. Do not intersect with the requested capture region.

#### Test Setup
- **Capture Region:** `Rectangle(100, 100, 200, 200)`
- **Monitors Provided:**
  1. **Monitor 1 (Intersects, but SDR):** Bounds `Rectangle(0, 0, 500, 500)`, HDR Enabled (`false`), White Level `0`
  2. **Monitor 2 (HDR Enabled, but Non-intersecting):** Bounds `Rectangle(500, 0, 500, 500)`, HDR Enabled (`true`), White Level `200`

#### Execution & Assertions
Calls `HdrScreenCapture.BuildCaptureSegments(region, monitors)` and uses `Assert.Empty(...)` to verify that no capture segments are returned, as neither monitor meets both criteria (HDR active **and** intersecting bounds).

---

## Data Structures Interacted With

Based on the test execution, the method under test interacts with the following types:

| Type / Struct | Observed Properties / Parameters | Description |
| :--- | :--- | :--- |
| `MonitorHdrInfo` | Constructor: `(IntPtr handle, Rectangle bounds, bool isHdrEnabled, int sdrWhiteLevel)` | Represents monitor metadata including handle, display bounds, HDR status, and white level. |
| `HdrScreenCapture.HdrCaptureSegment` | `CaptureRegion` (`Rectangle`), `Destination` (`Point`) | Represents a segmented portion of a screen capture mapped to a destination point. |
| `Rectangle` | `(int x, int y, int width, int height)` | Specifies the spatial boundaries for target regions and monitor bounds. |
| `Point` | `(int x, int y)` | Specifies composite destination offset coordinates. |