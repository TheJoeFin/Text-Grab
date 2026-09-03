# Technical Documentation: `BarcodeUtilities.cs`

## Overview
The `BarcodeUtilities` class is a static utility within the `Text_Grab.Utilities` namespace. It provides helper functions for decoding barcodes from bitmap images and generating QR codes in both bitmap (`System.Drawing.Bitmap`) and SVG (`SvgImage`) formats using the **ZXing** library.

---

## Class Information
* **Namespace:** `Text_Grab.Utilities`
* **Type:** `public static class`
* **Primary Dependencies:**
  * `ZXing` / `ZXing.Windows.Compatibility` (for barcode reading and rendering)
  * `System.Drawing` (for `Bitmap` manipulation)
  * `Text_Grab.Models` (for `OcrOutput` and `OcrOutputKind`)

---

## Methods Detail

### 1. `TryToReadBarcodes(Bitmap bitmap)`

#### Signature
```csharp
public static List<OcrOutput> TryToReadBarcodes(Bitmap bitmap)
```

#### Purpose
Scans an input `Bitmap` object for readable barcodes or QR codes and converts the detected barcode data into a list of `OcrOutput` objects.

#### Logic Workflow
1. **Dimension Validation:** Calls the private helper `CanReadBitmapDimensions(bitmap)`. If the bitmap is null or invalid, returns an empty list (`[]`).
2. **Reader Configuration:** Instantiates a `ZXing.Windows.Compatibility.BarcodeReader` with:
   - `AutoRotate = true`: Automatically attempts rotation to find readable barcodes.
   - `Options.TryHarder = true`: Configures decoding options to spend more time attempting to decode difficult barcodes.
3. **Decoding Execution:** Executes `barcodeReader.DecodeMultiple(bitmap)`.
4. **Exception Handling:** Catches the following runtime exceptions during decoding and logs debug information to `System.Diagnostics.Debug`:
   - `ArgumentException`
   - `ObjectDisposedException`
   - `ExternalException` (GDI+ errors)
   If an exception occurs, an empty list is returned.
5. **Output Mapping:** If decoding yields results, filters out any null entries or results with `null` text. Converts valid results into `OcrOutput` instances setting:
   - `Kind`: `OcrOutputKind.Barcode`
   - `RawOutput`: Result text string (`r.Text`)
   - `SourceBitmap`: The input `bitmap`
6. **Return:** Returns a `List<OcrOutput>` containing all mapped barcode entries.

---

### 2. `CanReadBitmapDimensions(Bitmap? bitmap)`

#### Signature
```csharp
private static bool CanReadBitmapDimensions(Bitmap? bitmap)
```

#### Purpose
A private helper method to safely verify that a `Bitmap` is non-null and possesses valid positive dimensions before processing.

#### Logic Workflow
1. Checks if `bitmap` is `null`. Returns `false` if true.
2. Checks if `bitmap.Width > 0` and `bitmap.Height > 0`. Returns `true` if both conditions are met.
3. Catches and logs `ArgumentException`, `ObjectDisposedException`, or `ExternalException` to `Debug.WriteLine` if reading bitmap dimensions fails, returning `false`.

---

### 3. `GetQrCodeForText(string text, ErrorCorrectionLevel correctionLevel)`

#### Signature
```csharp
public static Bitmap GetQrCodeForText(string text, ErrorCorrectionLevel correctionLevel)
```

#### Purpose
Generates a QR code as a standard GDI+ `Bitmap` image from a given text string and error correction level.

#### Logic Workflow
1. Configures a `BitmapRenderer` with:
   - Foreground Color: `System.Drawing.Color.Black`
   - Background Color: `System.Drawing.Color.White`
2. Instantiates `BarcodeWriter` with:
   - Format: `ZXing.BarcodeFormat.QR_CODE`
   - Renderer: Configured `BitmapRenderer`
3. Sets `EncodingOptions`:
   - `Width`: `500`
   - `Height`: `500`
   - `Margin`: `5`
   - Hint `EncodeHintType.ERROR_CORRECTION`: Supplied `correctionLevel`
4. Encodes the text using `barcodeWriter.Write(text)` and returns the resulting `Bitmap`.

---

### 4. `GetSvgQrCodeForText(string text, ErrorCorrectionLevel correctionLevel)`

#### Signature
```csharp
public static SvgImage GetSvgQrCodeForText(string text, ErrorCorrectionLevel correctionLevel)
```

#### Purpose
Generates a QR code formatted as a scalable vector graphic (`SvgRenderer.SvgImage`) from input text and error correction settings.

#### Logic Workflow
1. Instantiates `BarcodeWriterSvg` configured with:
   - Format: `ZXing.BarcodeFormat.QR_CODE`
   - Renderer: `new SvgRenderer()`
2. Configures `EncodingOptions`:
   - `Width`: `500`
   - `Height`: `500`
   - `Margin`: `5`
   - Hint `EncodeHintType.ERROR_CORRECTION`: Supplied `correctionLevel`
3. Writes the QR code using `barcodeWriter.Write(text)` and returns the generated `SvgImage`.

---

## Error Handling & Robustness

The class handles standard image-processing exceptions associated with GDI+ and bitmap disposal states. The methods reading bitmaps (`TryToReadBarcodes` and `CanReadBitmapDimensions`) catch:
* **`ArgumentException`**: Invalid parameters or bitmap formats.
* **`ObjectDisposedException`**: Bitmap objects that have already been freed/disposed.
* **`ExternalException`**: Underling GDI+ native interop issues.

In case of any exception, failure information is emitted via `Debug.WriteLine`, preventing unhandled crashes and gracefully returning fallback values (e.g., `false` or empty lists).