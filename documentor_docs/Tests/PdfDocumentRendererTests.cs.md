# Technical Documentation: `Tests/PdfDocumentRendererTests.cs`

## Overview

The `PdfDocumentRendererTests` class contains unit tests written in C# to validate the functionality of static utility methods within the `PdfDocumentRenderer` class (located in `Text_Grab.Utilities`). 

The tests cover coordinate transformations, dimension calculations, word grouping, and OCR line overlap logic used when processing PDF pages.

---

## File Details

- **File Path:** `Tests/PdfDocumentRendererTests.cs`
- **Namespace:** `Tests`
- **Testing Framework:** xUnit (indicated by `[Fact]` attributes and `Assert` assertions)

---

## External Dependencies & Namespaces

- `Text_Grab.Utilities`: Contains the target class being tested (`PdfDocumentRenderer` and related types like `PdfPageTextLine`).
- `UglyToad.PdfPig.Core`: Provides PDF core structures such as `PdfRectangle`.
- `Windows.Media.Ocr`: Provides OCR limits such as `OcrEngine.MaxImageDimension`.
- `Windows.Foundation`: Provides `Rect` structures for layout coordinates.

---

## Test Methods Summary

| Test Method Name | Target Method Tested | Scenario / Input | Expected Result |
| :--- | :--- | :--- | :--- |
| `GetRenderDimensions_DoublesTypicalPdfPageSize` | `PdfDocumentRenderer.GetRenderDimensions` | Normal PDF page dimensions (612, 792) | Dimensions double to (1224, 1584). |
| `GetRenderDimensions_ClampsToOcrEngineLimit` | `PdfDocumentRenderer.GetRenderDimensions` | Large PDF page dimensions (5000, 2500) | Clamped so max dimension $\le$ `OcrEngine.MaxImageDimension`, total pixels $\le$ `MaxRenderPixelCount`, maintaining aspect ratio ($W > H$). |
| `GetRenderDimensions_ClampsTotalPixelCount` | `PdfDocumentRenderer.GetRenderDimensions` | Very large square dimensions (10,000, 10,000) | Clamped so total pixels $\le$ `MaxRenderPixelCount`, maintaining aspect ratio ($W = H$). |
| `GetRenderDimensions_InvalidSize_ReturnsSinglePixel` | `PdfDocumentRenderer.GetRenderDimensions` | Invalid/zero dimensions (0, -1) | Returns a minimal bounding size of (1, 1). |
| `ConvertPdfRectToImageRect_MapsPdfCoordinatesToRenderedBitmapSpace` | `PdfDocumentRenderer.ConvertPdfRectToImageRect` | `PdfRectangle(10, 20, 60, 80)` with PDF size $100 \times 100$ and target bitmap size $200 \times 200$ | Scaled and mapped image rectangle at X=20, Y=40, Width=100, Height=120. |
| `GroupWordsIntoLines_GroupsNearbyWordsIntoSingleLine` | `PdfDocumentRenderer.GroupWordsIntoLines` | List of word bounds and string pairs | Adjacent words on the same line are grouped into a single line; separated words remain on a new line. |
| `ShouldIncludeOcrLine_OnlyReturnsTrueWhenImageOverlapIsMeaningful` | `PdfDocumentRenderer.ShouldIncludeOcrLine` | A source `Rect(0, 0, 10, 10)` compared against large overlap vs. small overlap rectangles | Returns `true` for significant overlap, `false` for small overlap. |

---

## Detailed Test Method Breakdown

### 1. `GetRenderDimensions_DoublesTypicalPdfPageSize`
* **Purpose:** Verifies that standard PDF page dimensions are scaled by a default factor of 2 during rendering dimension calculation.
* **Tested Input:** Width = `612`, Height = `792`.
* **Assertions:**
  * Width equals `1224u`.
  * Height equals `1584u`.

### 2. `GetRenderDimensions_ClampsToOcrEngineLimit`
* **Purpose:** Ensures rendering dimensions do not exceed the maximum allowed single dimension supported by the Windows OCR engine or the maximum allowed total pixel count.
* **Tested Input:** Width = `5000`, Height = `2500`.
* **Assertions:**
  * `Math.Max(width, height)` $\le$ `OcrEngine.MaxImageDimension`.
  * `(ulong)width * height` $\le$ `PdfDocumentRenderer.MaxRenderPixelCount`.
  * `width > height` (preserves aspect ratio orientation).

### 3. `GetRenderDimensions_ClampsTotalPixelCount`
* **Purpose:** Ensures scaling is capped when total pixel count would exceed `PdfDocumentRenderer.MaxRenderPixelCount`.
* **Tested Input:** Width = `10,000`, Height = `10,000`.
* **Assertions:**
  * `(ulong)width * height` $\le$ `PdfDocumentRenderer.MaxRenderPixelCount`.
  * `width == height` (preserves 1:1 square aspect ratio).

### 4. `GetRenderDimensions_InvalidSize_ReturnsSinglePixel`
* **Purpose:** Handles edge cases with invalid or non-positive input dimensions gracefully.
* **Tested Input:** Width = `0`, Height = `-1`.
* **Assertions:**
  * Width equals `1u`.
  * Height equals `1u`.

### 5. `ConvertPdfRectToImageRect_MapsPdfCoordinatesToRenderedBitmapSpace`
* **Purpose:** Verifies coordinate system mapping from PDF rectangle space (`UglyToad.PdfPig.Core.PdfRectangle`) to target image pixel space (`Windows.Foundation.Rect`).
* **Tested Input:**
  * PDF rectangle: `PdfRectangle(10, 20, 60, 80)`
  * Source PDF dimensions: `100 x 100`
  * Rendered image dimensions: `200 x 200`
* **Assertions:**
  * Image Rect `X` = `20`
  * Image Rect `Y` = `40`
  * Image Rect `Width` = `100`
  * Image Rect `Height` = `120`

### 6. `GroupWordsIntoLines_GroupsNearbyWordsIntoSingleLine`
* **Purpose:** Tests the algorithm that aggregates individual positioned text words into formatted text lines (`PdfPageTextLine`).
* **Tested Input:**
  * Tuple 1: `(Rect(10, 10, 20, 12), "Hello")`
  * Tuple 2: `(Rect(35, 11, 25, 12), "world")`
  * Tuple 3: `(Rect(12, 40, 30, 12), "Again")`
* **Assertions:**
  * Evaluates resulting collection containing 2 lines:
    * **First Line:** Text is `"Hello world"`, `IsNativeText` is `true`, `SourceRect` has `X=10`, `Y=10`, `Width=50`, `Height=13`.
    * **Second Line:** Text is `"Again"`.

### 7. `ShouldIncludeOcrLine_OnlyReturnsTrueWhenImageOverlapIsMeaningful`
* **Purpose:** Evaluates whether an OCR-detected line overlaps significantly with existing content bounding boxes to determine if it should be included.
* **Tested Input:**
  * Source rectangle: `Rect(0, 0, 10, 10)`
  * Large overlap collection: `[Rect(5, 5, 10, 10)]`
  * Small overlap collection: `[Rect(8, 8, 10, 10)]`
* **Assertions:**
  * `shouldIncludeFromLargeOverlap` evaluates to `true`.
  * `shouldIgnoreFromSmallOverlap` evaluates to `false`.