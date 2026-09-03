# Documentation Guide: `SoftwareBitmapExtensions.cs`

## Overview

The `SoftwareBitmapExtensions` class in the `Text_Grab.Extensions` namespace provides a set of static extension methods designed to simplify operations involving `Windows.Graphics.Imaging.SoftwareBitmap`. 

Key capabilities implemented in this class include:
* Converting a `SoftwareBitmap` to a XAML-compatible `SoftwareBitmapSource`.
* Asynchronously loading a `SoftwareBitmap` from a file path.
* Generating a 8-bit grayscale rectangular mask bitmap.
* Applying an 8-bit grayscale mask to transparency (alpha channel) on a `SoftwareBitmap`.
* Converting legacy `System.Drawing.Bitmap` objects into `SoftwareBitmap` objects.

---

## Class Signature

```csharp
namespace Text_Grab.Extensions;

public static class SoftwareBitmapExtensions
```

* **Type**: `public static class`
* **Namespace**: `Text_Grab.Extensions`

---

## Extension Methods Summary

| Method Name | Extension Target | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `ToSourceAsync` | `SoftwareBitmap` | `Task<SoftwareBitmapSource>` | Converts a `SoftwareBitmap` to a `SoftwareBitmapSource` suitable for UI display. |
| `FilePathToSoftwareBitmapAsync` | `string` | `Task<SoftwareBitmap>` | Loads an image file from a file path string and decodes it to a `SoftwareBitmap`. |
| `CreateMaskBitmap` | `SoftwareBitmap` | `SoftwareBitmap` | Creates a new 8-bit grayscale mask image with a specified rectangular region set to white and the background set to black. |
| `ApplyMask` | `SoftwareBitmap` | `SoftwareBitmap` | Applies a grayscale mask bitmap to an input bitmap by clearing alpha channel values where the mask is black. |
| `CreateSoftwareBitmap` | `System.Drawing.Bitmap` | `Task<SoftwareBitmap>` | Converts a GDI+ `System.Drawing.Bitmap` to a `SoftwareBitmap`. |

---

## Detailed Method Documentation

### 1. `ToSourceAsync`

Converts a `SoftwareBitmap` into a `SoftwareBitmapSource` for display in XAML UI elements.

```csharp
public static async Task<SoftwareBitmapSource> ToSourceAsync(this SoftwareBitmap softwareBitmap)
```

#### Parameters
* `softwareBitmap` (`this SoftwareBitmap`): The source image to convert.

#### Return Value
* `Task<SoftwareBitmapSource>`: A task returning a `SoftwareBitmapSource` populated with the converted image data.

#### Implementation Details
1. Instantiates a new `SoftwareBitmapSource`.
2. Checks whether the input `softwareBitmap` format is `BitmapPixelFormat.Bgra8` and alpha mode is `BitmapAlphaMode.Premultiplied`.
3. If the input format or alpha mode differs:
   * Converts the input bitmap using `SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)`.
   * Sets the converted bitmap on the `SoftwareBitmapSource` via `SetBitmapAsync()`.
4. If the input format already matches, sets the original `softwareBitmap` directly via `SetBitmapAsync()`.
5. Returns the `SoftwareBitmapSource`.

---

### 2. `FilePathToSoftwareBitmapAsync`

Loads an image file from a string file path and decodes it into a `SoftwareBitmap`.

```csharp
public static async Task<SoftwareBitmap> FilePathToSoftwareBitmapAsync(this string filePath)
```

#### Parameters
* `filePath` (`this string`): The full file path string pointing to the image file.

#### Return Value
* `Task<SoftwareBitmap>`: A task returning the decoded `SoftwareBitmap`.

#### Implementation Details
1. Asynchronously opens a stream to the file using `StorageFileExtensions.CreateStreamAsync(filePath)`.
2. Creates a `BitmapDecoder` from the retrieved `IRandomAccessStream`.
3. Asynchronously decodes and returns the `SoftwareBitmap` via `decoder.GetSoftwareBitmapAsync()`.

---

### 3. `CreateMaskBitmap`

Generates a grayscale mask (`Gray8`) bitmap with identical dimensions to the provided source bitmap. The region covered by the provided rectangle (`Rect`) is filled with white pixel values (`255`), and all other areas are filled with black pixel values (`0`).

```csharp
public static SoftwareBitmap CreateMaskBitmap(this SoftwareBitmap bitmap, Rect rect)
```

#### Parameters
* `bitmap` (`this SoftwareBitmap`): The reference bitmap whose width (`PixelWidth`) and height (`PixelHeight`) determine the mask dimensions.
* `rect` (`Windows.Foundation.Rect`): The target rectangle defining the region to mask as white.

#### Return Value
* `SoftwareBitmap`: A new 8-bit grayscale `SoftwareBitmap` (`BitmapPixelFormat.Gray8`, `BitmapAlphaMode.Ignore`).

#### Implementation Details
1. Allocates a byte array `pixelData` equal to `bitmap.PixelWidth * bitmap.PixelHeight`.
2. Initializes all bytes in `pixelData` to `0` (Black).
3. Iterates over the coordinates defined by `rect`:
   * Y range: `(int)rect.Y` to `(int)(rect.Y + rect.Height)`
   * X range: `(int)rect.X` to `(int)(rect.X + rect.Width)`
4. Calculates the 1D pixel index using `(y * bitmap.PixelWidth) + x` and sets the byte value to `255` (White).
5. Creates a new `SoftwareBitmap` with dimensions matching `bitmap`, using `BitmapPixelFormat.Gray8` and `BitmapAlphaMode.Ignore`.
6. Copies `pixelData` into the mask bitmap buffer using `.CopyFromBuffer(pixelData.AsBuffer())`.
7. Returns the constructed mask bitmap.

---

### 4. `ApplyMask`

Applies a grayscale mask (`Gray8`) to an input bitmap (`Bgra8`). For every pixel where the corresponding byte in the mask is black (`0`), the alpha channel byte of the target image pixel is set to `0` (fully transparent).

```csharp
public static SoftwareBitmap ApplyMask(this SoftwareBitmap inputBitmap, SoftwareBitmap grayMask)
```

#### Parameters
* `inputBitmap` (`this SoftwareBitmap`): The target bitmap to be masked. Must be in `Bgra8` format.
* `grayMask` (`SoftwareBitmap`): The mask image to apply. Must be in `Gray8` format.

#### Return Value
* `SoftwareBitmap`: A new `SoftwareBitmap` in `Bgra8` format containing the masked result.

#### Exceptions
* `System.Exception`: Thrown with the message `"Input bitmap must be Bgra8 and gray mask must be Gray8"` if `inputBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8` or `grayMask.BitmapPixelFormat != BitmapPixelFormat.Gray8`.

#### Implementation Details
1. Validates the formats of `inputBitmap` (`Bgra8`) and `grayMask` (`Gray8`). Throws an exception if either condition fails.
2. Allocates byte buffers:
   * `inputBuffer`: Size equal to `4 * inputBitmap.PixelWidth * inputBitmap.PixelHeight` (4 bytes per pixel: BGRA).
   * `maskBuffer`: Size equal to `grayMask.PixelWidth * grayMask.PixelHeight` (1 byte per pixel).
3. Reads data into buffers using `CopyToBuffer`.
4. Iterates over each pixel coordinate (`x`, `y`) of `inputBitmap`:
   * `inputIndex` = `(y * inputBitmap.PixelWidth + x) * 4`
   * `maskIndex` = `y * grayMask.PixelWidth + x`
5. Checks `maskBuffer[maskIndex]`. If the mask pixel byte equals `0`:
   * Sets `inputBuffer[inputIndex + 3] = 0` (Modifies the Alpha byte of the BGRA tuple to 0/transparent).
6. Instantiates a new `SoftwareBitmap` with `BitmapPixelFormat.Bgra8` matching the original dimensions.
7. Copies the modified `inputBuffer` into the new `SoftwareBitmap` using `CopyFromBuffer`.
8. Returns the segmented bitmap.

---

### 5. `CreateSoftwareBitmap`

Converts a `System.Drawing.Bitmap` (GDI+) into a `Windows.Graphics.Imaging.SoftwareBitmap`.

```csharp
public static async Task<SoftwareBitmap> CreateSoftwareBitmap(this System.Drawing.Bitmap bitmap)
```

#### Parameters
* `bitmap` (`this System.Drawing.Bitmap`): The GDI+ bitmap to convert.

#### Return Value
* `Task<SoftwareBitmap>`: A task returning the decoded `SoftwareBitmap`.

#### Implementation Details
1. Creates an in-memory stream hierarchy: a `MemoryStream` wrapped inside a custom `WrappingStream`.
2. Saves the GDI+ bitmap into the `WrappingStream` using standard BMP encoding (`ImageFormat.Bmp`).
3. Resets stream position (`wrapper.Position = 0`).
4. Wraps the underlying stream into an `IRandomAccessStream` using `.AsRandomAccessStream()`.
5. Instantiates a `BitmapDecoder` from the stream via `BitmapDecoder.CreateAsync(...)`.
6. Retrieves the `SoftwareBitmap` asynchronously using `bmpDecoder.GetSoftwareBitmapAsync()`.
7. Asynchronously flushes the `WrappingStream` wrapper.
8. Returns the decoded `SoftwareBitmap`.