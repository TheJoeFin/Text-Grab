# Technical Documentation: `Tests/ImageMethodsTests.cs`

## Overview

The `ImageMethodsTests` class is a unit test suite within the `Tests` namespace. Its primary purpose is to verify the behavior of image conversion utility methods (specifically `ImageMethods.ImageSourceToBitmap`) and to validate image comparison logic using ImageMagick (`MagickImage`).

The tests are designed to run within a WPF thread context, indicated by the use of the `[WpfFact]` attribute.

---

## File Details

* **File Path:** `Tests/ImageMethodsTests.cs`
* **Namespace:** `Tests`
* **Class Name:** `ImageMethodsTests`

---

## Dependencies & Imports

* `ImageMagick`: Used for image manipulation, comparison (`MagickImage`, `IMagickErrorInfo`).
* `System.Drawing`: Used for handling `Bitmap` objects.
* `System.Windows`: Provides structural types like `Int32Rect`.
* `System.Windows.Media`: Provides UI/Drawing types like `DrawingImage`.
* `System.Windows.Media.Imaging`: WPF imaging types (`BitmapSource`, `CroppedBitmap`, `PixelFormats`).
* `Text_Grab`: Contains the application code being tested, specifically `ImageMethods`.
* `Text_Grab.Utilities`: Contains helper utilities such as `FileUtilities`.

---

## Class Constants

The class defines two private constants pointing to local image paths used during test execution:

| Constant Name | Value | Description |
| :--- | :--- | :--- |
| `fontTestPath` | `@".\Images\FontTest.png"` | Relative path to the primary test image file (`FontTest.png`). |
| `fontSamplePath` | `@".\Images\font_sample.png"` | Relative path to a secondary comparison image file (`font_sample.png`). |

---

## Test Methods

### 1. `ImageSourceToBitmap_ConvertsBitmapSourceDerivedImages`

* **Attribute:** `[WpfFact]`
* **Purpose:** Verifies that `ImageMethods.ImageSourceToBitmap` successfully converts a WPF `BitmapSource`-derived image (specifically a `CroppedBitmap`) into a standard `System.Drawing.Bitmap`.

#### Test Logic:
1. Constructs a 2x2 byte array representing pixel data (BGRA 32-bit format).
2. Creates a `BitmapSource` using `BitmapSource.Create()` with dimensions 2x2 and 96 DPI.
3. Wraps the source in a `CroppedBitmap` to extract a 1x2 sub-region (starting at `x=1, y=0`).
4. Invokes `ImageMethods.ImageSourceToBitmap(cropped)` to convert the WPF `CroppedBitmap` into a `System.Drawing.Bitmap`.

#### Assertions:
* `Assert.NotNull(bitmap)`: Ensures the conversion did not return `null`.
* `Assert.Equal(1, bitmap!.Width)`: Validates that the resulting bitmap's width matches the cropped width (1 pixel).
* `Assert.Equal(2, bitmap.Height)`: Validates that the resulting bitmap's height matches the cropped height (2 pixels).

---

### 2. `ImageSourceToBitmap_ReturnsNullForNonBitmapImageSources`

* **Attribute:** `[WpfFact]`
* **Purpose:** Ensures that `ImageMethods.ImageSourceToBitmap` handles non-`BitmapSource` `ImageSource` types gracefully by returning `null`.

#### Test Logic:
1. Instantiates a `DrawingImage` (an `ImageSource` derived type that does not derive from `BitmapSource`).
2. Calls `ImageMethods.ImageSourceToBitmap(drawingImage)`.

#### Assertions:
* `Assert.Null(bitmap)`: Verifies that passing a non-`BitmapSource` image source returns `null`.

---

### 3. `BitmapCompare_ReturnsZeroDiff`

* **Attribute:** `[WpfFact]`
* **Purpose:** Tests image comparison logic via ImageMagick to verify that comparing an image against itself yields zero difference error.

#### Test Logic:
1. Resolves the full path to `fontTestPath` using `FileUtilities.GetPathToLocalFile(fontTestPath)`.
2. Loads the image into a `MagickImage` instance (`img1`).
3. Performs a self-comparison using `img1.Compare(img1)`.

#### Assertions:
* `Assert.NotNull(compare)`: Ensures the error comparison object is populated.
* `Assert.Equal(0, compare.NormalizedMeanError)`: Verifies that the normalized mean error is `0` when comparing an image to itself.

---

### 4. `BitmapCompare_ReturnsNonZeroDiff`

* **Attribute:** `[WpfFact]`
* **Purpose:** Tests image comparison logic via ImageMagick to verify that comparing two different images produces a non-zero difference error.

#### Test Logic:
1. Resolves local paths for `fontTestPath` and `fontSamplePath` via `FileUtilities.GetPathToLocalFile()`.
2. Loads both files into distinct `MagickImage` instances (`img1` and `img2`).
3. Compares `img1` against `img2` using `img1.Compare(img2)`.

#### Assertions:
* `Assert.NotEqual(0, compare.NormalizedMeanError)`: Verifies that the normalized mean error is not `0`, indicating that the images are distinct.