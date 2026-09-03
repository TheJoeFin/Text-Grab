# Technical Documentation: `Tests/BarcodeUtilitiesTests.cs`

## Overview

The `BarcodeUtilitiesTests` class is a unit test suite within the `Tests` namespace. Its primary purpose is to test barcode reading and generation utilities (primarily handled by `BarcodeUtilities`), as well as stream-to-bitmap conversion utilities (handled by `ImageMethods`).

The suite verifies edge cases (such as handling disposed bitmap objects), multi-barcode detection on combined images, single QR code reading from stored image files, and stream independence when instantiating bitmaps from Windows random-access streams.

---

## File Info

* **File Path:** `Tests/BarcodeUtilitiesTests.cs`
* **Namespace:** `Tests`
* **Class Name:** `BarcodeUtilitiesTests`

---

## Dependencies & Imports

### System Namespaces
* `System.Drawing`: Provides bitmap creation, graphics drawing, and color manipulation.
* `System.Drawing.Imaging`: Provides image formats (e.g., `ImageFormat.Png`).
* `System.IO`: Handles stream and memory manipulation (`MemoryStream`).
* `System.Runtime.InteropServices.WindowsRuntime`: Provides helper extensions for converting buffers (`AsBuffer()`).
* `Windows.Storage.Streams`: Handles Windows runtime stream types (`InMemoryRandomAccessStream`).

### External / Project Namespaces
* `Text_Grab`: Root namespace for the application.
* `Text_Grab.Models`: Provides models such as `OcrOutput` and `OcrOutputKind`.
* `Text_Grab.Utilities`: Supplies utility classes `BarcodeUtilities`, `ImageMethods`, and `FileUtilities`.
* `UnitsNet`: Namespace imported in the unit test header.
* `ZXing.QrCode.Internal`: Used for error correction settings (`ErrorCorrectionLevel`).

---

## Test Methods Summary

| Test Method Name | Attribute | Async | Purpose |
| :--- | :--- | :--- | :--- |
| `TryToReadBarcodes_WithDisposedBitmap_ReturnsEmptyList` | `[Fact]` | No | Validates that calling `TryToReadBarcodes` on a disposed `Bitmap` handles the scenario gracefully and returns an empty list. |
| `TryToReadBarcodes_WithTwoQrCodes_ReturnsTwoResults` | `[Fact]` | No | Tests the detection and parsing of multiple QR codes present within a single composite bitmap image. |
| `ReadTestSingleQRCode` | `[WpfFact]` | No | Tests loading a local PNG file containing a QR code and reading its single string result. |
| `GetBitmapFromIRandomAccessStream_ReturnsBitmapIndependentOfSourceStream` | `[Fact]` | Yes | Ensures that `ImageMethods.GetBitmapFromIRandomAccessStream` creates a fully realized, independent `Bitmap` from an `InMemoryRandomAccessStream`. |

---

## Detailed Test Method Specifications

### 1. `TryToReadBarcodes_WithDisposedBitmap_ReturnsEmptyList`

* **Attribute:** `[Fact]`
* **Description:** Verifies the behavior of `BarcodeUtilities.TryToReadBarcodes` when passed an invalid/disposed `Bitmap` object.
* **Execution Flow:**
  1. Instantiates an 8x8 pixel `Bitmap`.
  2. Disposes the bitmap immediately using `disposedBitmap.Dispose()`.
  3. Calls `BarcodeUtilities.TryToReadBarcodes(disposedBitmap)`.
* **Assertions:**
  * Asserts that the returned `List<OcrOutput>` is empty (`Assert.Empty(results)`).

---

### 2. `TryToReadBarcodes_WithTwoQrCodes_ReturnsTwoResults`

* **Attribute:** `[Fact]`
* **Description:** Validates that multiple QR codes combined into a single bitmap can all be successfully identified and read.
* **Execution Flow:**
  1. Generates two distinct QR code bitmaps using `BarcodeUtilities.GetQrCodeForText(...)`:
     * Target string 1: `"https://example.com"` (Error Correction Level: `M`)
     * Target string 2: `"https://example.org"` (Error Correction Level: `M`)
  2. Creates a combined bitmap with a width equal to the sum of both QR code widths and a height equal to the maximum of their heights.
  3. Uses a `Graphics` surface to clear the combined image to white and draw `qr1` at `(0, 0)` and `qr2` adjacent to it at `(qr1.Width, 0)`.
  4. Calls `BarcodeUtilities.TryToReadBarcodes(combined)`.
* **Assertions:**
  * Asserts the result count equals 2 (`Assert.Equal(2, results.Count)`).
  * Asserts that all returned objects have `Kind` equal to `OcrOutputKind.Barcode`.
  * Asserts that the results list contains an entry with `RawOutput` equal to `"https://example.com"`.
  * Asserts that the results list contains an entry with `RawOutput` equal to `"https://example.org"`.

---

### 3. `ReadTestSingleQRCode`

* **Attribute:** `[WpfFact]`
* **Description:** Reads a single static QR code image stored on the local file system to confirm standard barcode decoding functionality.
* **Execution Flow:**
  1. Sets expected output string to `"This is a test of the QR Code system"`.
  2. Resolves path to test image via `FileUtilities.GetPathToLocalFile(@".\Images\QrCodeTestImage.png")`.
  3. Instantiates a `Bitmap` from the resolved file path.
  4. Calls `BarcodeUtilities.TryToReadBarcodes(testBmp)`.
* **Assertions:**
  * Asserts that exactly one result is returned (`Assert.Single(result)`).
  * Asserts that `result[0].RawOutput` equals `"This is a test of the QR Code system"`.

---

### 4. `GetBitmapFromIRandomAccessStream_ReturnsBitmapIndependentOfSourceStream`

* **Attribute:** `[Fact]` (Asynchronous)
* **Description:** Tests the image utility method `ImageMethods.GetBitmapFromIRandomAccessStream` to ensure it successfully reconstructs a GDI+ `Bitmap` object from a UWP/WinRT `IRandomAccessStream`.
* **Execution Flow:**
  1. Constructs an 8x8 `Bitmap` (`sourceBitmap`) and sets pixel `(0, 0)` to `Color.Red`.
  2. Saves `sourceBitmap` into a `MemoryStream` using `ImageFormat.Png`.
  3. Writes the bytes of the memory stream into an `InMemoryRandomAccessStream` via `.WriteAsync(...)`.
  4. Invokes `ImageMethods.GetBitmapFromIRandomAccessStream(randomAccessStream)` to reconstruct the bitmap.
* **Assertions:**
  * Asserts that the cloned bitmap width is 8 (`Assert.Equal(8, clonedBitmap.Width)`).
  * Asserts that the cloned bitmap height is 8 (`Assert.Equal(8, clonedBitmap.Height)`).
  * Asserts that the ARGB value of pixel `(0,0)` matches `Color.Red.ToArgb()`.

---

## Referenced Codebase Utilities

The tests depend on the following methods and types from the application codebase:

* **`Text_Grab.Utilities.BarcodeUtilities`**:
  * `TryToReadBarcodes(Bitmap)`: Scans a bitmap and returns a `List<OcrOutput>`.
  * `GetQrCodeForText(string, ErrorCorrectionLevel)`: Generates a QR code bitmap for the provided text and error correction level.
* **`Text_Grab.Utilities.ImageMethods`**:
  * `GetBitmapFromIRandomAccessStream(IRandomAccessStream)`: Converts a random access stream into a GDI+ `Bitmap`.
* **`Text_Grab.Utilities.FileUtilities`**:
  * `GetPathToLocalFile(string)`: Resolves relative paths to local filesystem paths.
* **`Text_Grab.Models.OcrOutput`**:
  * Represents barcode or OCR text output containing properties such as `Kind` (`OcrOutputKind`) and `RawOutput` (`string`).