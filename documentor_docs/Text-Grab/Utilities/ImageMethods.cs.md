# Technical Documentation Guide: `ImageMethods.cs`

**File Path:** `Text-Grab/Utilities/ImageMethods.cs`  
**Namespace:** `Text-Grab`

---

## Overview

The `ImageMethods` class is a `public static` utility class within the **Text-Grab** application. It provides helper methods for image conversion, screen capture (including HDR display capture support), window boundary detection, image scaling, padding, and rotation.

It serves as an interop bridge between different .NET image representations:
* **GDI+** (`System.Drawing.Bitmap`, `System.Drawing.Image`)
* **WPF / Media** (`System.Windows.Media.Imaging.BitmapImage`, `BitmapSource`, `ImageSource`, `InteropBitmap`, `CachedBitmap`)
* **WinRT / Universal Windows Platform** (`Windows.Storage.Streams.IRandomAccessStream`)

---

## Key Dependencies & References

* **System.Drawing & System.Drawing.Imaging**: Used for GDI+ bitmap manipulation, pixel locking (`LockBits`), and GDI screen capturing.
* **System.Windows.Media.Imaging**: Used for WPF image sources, bitmap encoders (`BmpBitmapEncoder`, `PngBitmapEncoder`), and image transformations.
* **Text_Grab.Utilities.Hdr.HdrScreenCapture**: Facilitates full-precision HDR screen captures and tone-mapping back to SDR.
* **Text_Grab.Services.HistoryService**: Receives cached bitmap results via `Singleton<HistoryService>.Instance`.
* **Text_Grab.Views.GrabFrame**: Custom window view checked for specific content rectangle extraction.
* **WrappingStream**: Custom stream wrapper used to manage memory streams during image format conversions.

---

## Method Breakdown by Category

### 1. Screen Capture & Window Measurement Methods

#### `CaptureScreenRegion(Rectangle region)`
* **Access**: `private static`
* **Parameters**: `Rectangle region` — Screen area rectangle to capture.
* **Returns**: `Bitmap` — Screen contents contained in the specified region.
* **Description**:
  Captures a virtual desktop area. If `AppUtilities.TextGrabSettings.HdrCaptureCorrection` is enabled, it attempts to capture using `HdrScreenCapture.TryCaptureRegion(region)`. If HDR capture is disabled or returns `null`, it falls back to standard GDI screen copy using `Graphics.CopyFromScreen` with `Format32bppArgb` pixel format.

#### `GetRegionOfScreenAsBitmap(Rectangle region, bool cacheResult = true)`
* **Access**: `public static`
* **Parameters**:
  * `Rectangle region` — The screen area rectangle to capture.
  * `bool cacheResult` (default: `true`) — Whether to cache the captured image in `HistoryService`.
* **Returns**: `Bitmap` — Captured and padded bitmap.
* **Description**:
  1. Captures the specified region using `CaptureScreenRegion(region)`.
  2. Applies padding using `PadImage()`.
  3. If `cacheResult` is `true`, stores the bitmap in `Singleton<HistoryService>.Instance.CacheLastBitmap(bmp)`.
  4. Returns the padded bitmap.

#### `GetWindowsBoundsBitmap(Window passedWindow)`
* **Access**: `public static`
* **Parameters**: `Window passedWindow` — Target WPF window.
* **Returns**: `Bitmap` — A screen capture matching the physical pixel bounds of the window or its content area.
* **Description**:
  Calculates DPI scale and screen coordinates for a given window:
  * Uses `VisualTreeHelper.GetDpi` and `GetAbsolutePosition()`.
  * If `passedWindow` is a `GrabFrame`:
    * Evaluates `grabFrame.GetImageContentRect()`.
    * If empty, falls back to `grabFrame.GetContentAreaScreenRect()`.
    * Returns a 1x1 empty `Bitmap` if `GetContentAreaScreenRect()` returns `Rectangle.Empty`.
  * Captures and returns the calculated window region via `CaptureScreenRegion`.

#### `GetWindowBoundsImage(Window passedWindow)`
* **Access**: `public static`
* **Parameters**: `Window passedWindow` — Target WPF window.
* **Returns**: `ImageSource` — WPF-compatible image source of the window capture.
* **Description**:
  Calls `GetWindowsBoundsBitmap(passedWindow)`, converts the resulting `Bitmap` to `ImageSource` using `BitmapToImageSource()`, disposes of the original GDI bitmap, and returns the WPF image source.

---

### 2. Image Type Conversion Methods

#### `BitmapImageToBitmap(BitmapImage bitmapImage)`
* **Access**: `public static`
* **Parameters**: `BitmapImage bitmapImage` — WPF bitmap image input.
* **Returns**: `Bitmap` — Converted GDI+ Bitmap.
* **Description**:
  Encodes the WPF `BitmapImage` using a `BmpBitmapEncoder` into a `MemoryStream` wrapped by `WrappingStream`, then constructs and returns a new GDI+ `Bitmap`.

#### `BitmapToImageSource(Bitmap bitmap)`
* **Access**: `public static`
* **Parameters**: `Bitmap bitmap` — GDI+ bitmap input.
* **Returns**: `BitmapImage` — Frozen WPF `BitmapImage`.
* **Description**:
  Saves a GDI+ `Bitmap` as a BMP stream into a `MemoryStream`/`WrappingStream`. Reads it into a new WPF `BitmapImage` using `BitmapCacheOption.OnLoad`, explicitly detaches the stream source, freezes the object for thread-safety, and returns it.

#### `CachedBitmapToBitmapImage(System.Windows.Media.Imaging.CachedBitmap cachedBitmap)`
* **Access**: `public static`
* **Parameters**: `CachedBitmap cachedBitmap` — WPF `CachedBitmap` instance.
* **Returns**: `BitmapImage` — Converted WPF `BitmapImage`.
* **Description**:
  Encodes `CachedBitmap` using a `PngBitmapEncoder` to a `MemoryStream`, creates a new `BitmapImage` loaded from that stream with `BitmapCacheOption.OnLoad`, freezes it, and returns it.

#### `InteropBitmapToBitmap(System.Windows.Interop.InteropBitmap source)`
* **Access**: `public static`
* **Parameters**: `InteropBitmap source` — Source WPF interop bitmap.
* **Returns**: `Bitmap` — GDI+ Bitmap with `Format32bppPArgb` pixel format.
* **Description**:
  Allocates a new GDI+ `Bitmap`, uses `LockBits` to get standard pixel memory handles (`Scan0`), calls `InteropBitmap.CopyPixels()` to write pixel data directly into bitmap memory, and unlocks bits before returning.

#### `BitmapSourceToBitmap(BitmapSource source)`
* **Access**: `public static`
* **Parameters**: `BitmapSource source` — Source WPF `BitmapSource`.
* **Returns**: `Bitmap` — GDI+ Bitmap with `Format32bppPArgb` pixel format.
* **Description**:
  Directly copies raw pixel bytes from a WPF `BitmapSource` into a newly allocated GDI+ `Bitmap` via `LockBits` and `CopyPixels`.

#### `ImageSourceToBitmap(ImageSource? source)`
* **Access**: `public static`
* **Parameters**: `ImageSource? source` — Nullable WPF `ImageSource`.
* **Returns**: `Bitmap?` — Converted GDI+ Bitmap or `null`.
* **Description**:
  Uses pattern matching: if `source` is a `BitmapSource`, delegates to `BitmapSourceToBitmap()`; otherwise returns `null`.

#### `GetBitmapFromIRandomAccessStream(IRandomAccessStream stream)`
* **Access**: `public static`
* **Parameters**: `IRandomAccessStream stream` — WinRT stream input.
* **Returns**: `Bitmap` — GDI+ Bitmap copy.
* **Description**:
  Converts the `IRandomAccessStream` to a standard managed stream (`AsStream()`), resets position to 0 if seekable, initializes a `Bitmap` from the stream, and returns a duplicate `Bitmap` object.

#### `GetBitmapImageFromIRandomAccessStream(IRandomAccessStream stream)`
* **Access**: `public static`
* **Parameters**: `IRandomAccessStream stream` — WinRT stream input.
* **Returns**: `BitmapImage` — Frozen WPF `BitmapImage`.
* **Description**:
  Converts `IRandomAccessStream` to a managed `Stream`, sets it as the `StreamSource` of a `BitmapImage` with `BitmapCacheOption.None`, initializes and freezes the image.

---

### 3. Image Manipulation & Transformation Methods

#### `PadImage(Bitmap image, int minW = 64, int minH = 64)`
* **Access**: `public static`
* **Parameters**:
  * `Bitmap image` — Input bitmap to pad.
  * `int minW` (default: `64`) — Minimum target width threshold.
  * `int minH` (default: `64`) — Minimum target height threshold.
* **Returns**: `Bitmap` — Padded bitmap or the original bitmap if dimensions exceed thresholds.
* **Description**:
  * Checks if `image.Height >= minH` and `image.Width >= minW`. If both are true, returns the original image.
  * Calculates new width: `Math.Max(image.Width + 16, minW + 16)`.
  * Calculates new height: `Math.Max(image.Height + 16, minH + 16)`.
  * Creates a destination `Bitmap` matching the input's `PixelFormat`.
  * Clears the destination graphics canvas using the pixel color at `(0,0)` of the input image.
  * Draws the original image unscaled onto the new canvas at offset coordinates `(8, 8)`.

#### `ScaleBitmapUniform(Bitmap passedBitmap, double scale)`
* **Access**: `public static`
* **Parameters**:
  * `Bitmap passedBitmap` — Input bitmap to scale.
  * `double scale` — Uniform scale multiplier factor.
* **Returns**: `Bitmap` — Scaled GDI+ Bitmap.
* **Description**:
  1. Converts input `Bitmap` to a WPF `BitmapImage`.
  2. Applies a `ScaleTransform(scale, scale)` via WPF's `TransformedBitmap`.
  3. Freezes the `TransformedBitmap`.
  4. Converts the result back into a GDI+ `Bitmap` using `BitmapSourceToBitmap()`.

#### `GetRotateFlipType(string path)`
* **Access**: `internal static`
* **Parameters**: `string path` — File system path to the image file.
* **Returns**: `RotateFlipType` — Orientation metadata read from the file.
* **Description**:
  Loads an image from file using `Image.FromFile(path)` and returns its orientation via the `.GetRotateFlipType()` extension method.

#### `RotateImage(BitmapImage droppedImage, RotateFlipType rotateFlipType)`
* **Access**: `internal static`
* **Parameters**:
  * `BitmapImage droppedImage` — Target WPF `BitmapImage` to rotate.
  * `RotateFlipType rotateFlipType` — Target rotation specification.
* **Returns**: `void`
* **Description**:
  Casts `rotateFlipType` to `int` and applies rotation directly to the `BitmapImage.Rotation` property:
  * `1` sets `Rotation.Rotate90`
  * `2` sets `Rotation.Rotate180`
  * `3` sets `Rotation.Rotate270`
  * Default/Other: leaves `droppedImage.Rotation` unchanged.