# Documentation Guide: `MagickHelpers.cs`

## Overview

The `MagickHelpers` class located in `Text-Grab.Utilities` provides static helper methods to perform common image manipulations on WPF `ImageSource` objects. It primarily leverages the **Magick.NET** library (`MagickImage`) for adjustments such as brightness, contrast, and grayscale conversion, alongside direct pixel array manipulations for color inversion.

---

## Class Definition

- **Namespace**: `Text_Grab.Utilities`
- **Class**: `public class MagickHelpers`
- **Type**: Static utility helper class (contains static methods)

---

## Dependencies

- **`ImageMagick` & `ImageMagick.Factories`**: Used for advanced image processing operations (`MagickImage`, `MagickImageFactory`, `Percentage`).
- **`System.Drawing`**: Utilized during intermediate bitmap conversions.
- **`System.Windows.Media` & `System.Windows.Media.Imaging`**: Used for handling WPF native image classes (`ImageSource`, `BitmapImage`, `CachedBitmap`, `BitmapSource`).
- **`Text_Grab.Utilities.ImageMethods`**: An internal utility dependency used to convert between WPF image types (`CachedBitmap`, `BitmapImage`) and `System.Drawing.Bitmap`.

---

## Processing Flow Overview

Most methods in this utility follow a standard 4-step image conversion pipeline before applying their specific transformation:

1. **Input Normalization**: Checks if the input `ImageSource` is a `CachedBitmap` or `BitmapImage`. Converts `CachedBitmap` to `BitmapImage` via `ImageMethods.CachedBitmapToBitmapImage()`.
2. **Null Validation**: If the input cannot be resolved to a valid `BitmapImage`, the method returns `null`.
3. **Bitmap Conversion**: Converts `BitmapImage` to `System.Drawing.Bitmap` using `ImageMethods.BitmapImageToBitmap()`.
4. **MagickImage Instantiation**: Wraps the `Bitmap` into a `MagickImage` instance using `MagickImageFactory.Create()`. Returns `null` if instantiation fails.

*Note: The `Invert` method diverges from this pipeline by directly altering byte arrays of the WPF image buffer.*

---

## Methods

### 1. `Brighten`

Increases the brightness of the provided image by 10%.

#### Signature
```csharp
public static ImageSource? Brighten(ImageSource? source)
```

#### Parameters
- `source` (`ImageSource?`): The source WPF image to be brightened. Accepts `CachedBitmap` or `BitmapImage`.

#### Returns
- `ImageSource?`: A modified `BitmapSource` with increased brightness, or `null` if conversion fails or source is `null`.

#### Logic
1. Normalizes and validates the input `source` to a `Bitmap`.
2. Creates a `MagickImage` instance from the `Bitmap`.
3. Applies brightness adjustment: `magickImage.BrightnessContrast(new Percentage(10), new Percentage(0));`
   - Brightness: +10%
   - Contrast: 0% change
4. Returns the converted `BitmapSource` via `magickImage.ToBitmapSource()`.

---

### 2. `Contrast`

Applies a sigmoidal contrast adjustment to the provided image.

#### Signature
```csharp
public static ImageSource? Contrast(ImageSource? source)
```

#### Parameters
- `source` (`ImageSource?`): The source WPF image to adjust contrast. Accepts `CachedBitmap` or `BitmapImage`.

#### Returns
- `ImageSource?`: A modified `BitmapSource` with adjusted contrast, or `null` if conversion fails or source is `null`.

#### Logic
1. Normalizes and validates the input `source` to a `Bitmap`.
2. Creates a `MagickImage` instance from the `Bitmap`.
3. Applies sigmoidal contrast: `magickImage.SigmoidalContrast(10);`
4. Returns the converted `BitmapSource` via `magickImage.ToBitmapSource()`.

---

### 3. `Darken`

Decreases the brightness of the provided image by 10%.

#### Signature
```csharp
public static ImageSource? Darken(ImageSource? source)
```

#### Parameters
- `source` (`ImageSource?`): The source WPF image to be darkened. Accepts `CachedBitmap` or `BitmapImage`.

#### Returns
- `ImageSource?`: A modified `BitmapSource` with reduced brightness, or `null` if conversion fails or source is `null`.

#### Logic
1. Normalizes and validates the input `source` to a `Bitmap`.
2. Creates a `MagickImage` instance from the `Bitmap`.
3. Applies brightness reduction: `magickImage.BrightnessContrast(new Percentage(-10), new Percentage(0));`
   - Brightness: -10%
   - Contrast: 0% change
4. Returns the converted `BitmapSource` via `magickImage.ToBitmapSource()`.

---

### 4. `Grayscale`

Converts the provided image to grayscale.

#### Signature
```csharp
public static ImageSource? Grayscale(ImageSource? source)
```

#### Parameters
- `source` (`ImageSource?`): The source WPF image to convert to grayscale. Accepts `CachedBitmap` or `BitmapImage`.

#### Returns
- `ImageSource?`: A grayscaled `BitmapSource`, or `null` if conversion fails or source is `null`.

#### Logic
1. Normalizes and validates the input `source` to a `Bitmap`.
2. Creates a `MagickImage` instance from the `Bitmap`.
3. Applies grayscale conversion: `magickImage.Grayscale();`
4. Returns the converted `BitmapSource` via `magickImage.ToBitmapSource()`.

---

### 5. `Invert`

Inverts the color channels (Red, Green, Blue) of the source image using direct byte array bitwise manipulation instead of ImageMagick.

#### Signature
```csharp
public static ImageSource? Invert(ImageSource? source)
```

#### Parameters
- `source` (`ImageSource?`): The source WPF image to invert. Accepts `CachedBitmap` or `BitmapImage`.

#### Returns
- `ImageSource?`: A color-inverted `BitmapSource`, or `null` if input conversion fails or source is `null`.

#### Detailed Execution Steps
1. Normalizes `source` (`CachedBitmap` or `BitmapImage`) into a `BitmapImage`. If `null`, returns `null`.
2. Calculates pixel stride:
   $$\text{stride} = \frac{(\text{PixelWidth} \times \text{BitsPerPixel}) + 7}{8}$$
3. Allocates a byte array `data` of size `stride * PixelHeight`.
4. Copies raw pixel data into `data` using `bitmapImage.CopyPixels(data, stride, 0)`.
5. Iterates through the byte buffer in 4-byte increments (`i += 4` assuming 32 bits-per-pixel formats):
   - Inverts standard color channels:
     - `data[i] = (byte)(255 - data[i]);` (Byte 0)
     - `data[i + 1] = (byte)(255 - data[i + 1]);` (Byte 1)
     - `data[i + 2] = (byte)(255 - data[i + 2]);` (Byte 2)
   - Channel 4 (Alpha at `data[i + 3]`) is deliberately left unchanged to maintain original transparency.
6. Reconstructs and returns a new `BitmapSource` via `BitmapSource.Create()` using the original dimensions, DPI, pixel format, and modified byte array.

---

## Key Technical Notes & Considerations

- **Null Safety**: All methods accept a nullable `ImageSource?` and return `null` if input resolution or transformation fails.
- **Image Formats in `Invert`**: The pixel iteration in `Invert()` increments by 4 bytes (`i += 4`). This assumes a 32-bit pixel format (e.g., Bgr32 or Bgra32). Non-32bpp formats could result in index alignment mismatch issues during direct pixel array iteration.
- **ImageMagick Usage**: Methods `Brighten`, `Contrast`, `Darken`, and `Grayscale` instantiate a `MagickImageFactory` object and perform operation calls on `MagickImage`. `Invert` handles image buffers natively within WPF memory constructs.