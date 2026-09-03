# Technical Documentation: `PdfDocumentRenderer.cs`

## Overview

The `PdfDocumentRenderer.cs` file is a utility within `Text-Grab` responsible for processing, rendering, and extracting text from PDF documents. It implements a hybrid PDF processing pipeline that combines:
1. **Windows Native PDF Rendering (`Windows.Data.Pdf`)** for high-performance rendering of PDF pages into bitmap images.
2. **PdfPig Library (`UglyToad.PdfPig`)** for extracting native text bounding boxes, word structures, and embedded image regions.
3. **Windows OCR Integration (`Windows.Media.Ocr`)** for fallback text extraction on non-native text (e.g., scanned documents or embedded image regions).
4. **Least Recently Used (LRU) Page Caching** to manage memory overhead during page rendering and inspection.

---

## Classes & Data Models

### 1. `PdfPageContent`
An `internal sealed` container model representing processed content for a single PDF page.

* **Constructor**:
  ```csharp
  public PdfPageContent(
      int pageIndex,
      BitmapSource renderedPage,
      IReadOnlyList<PdfPageTextLine> nativeLines,
      IReadOnlyList<Windows.Foundation.Rect> imageRegions)
  ```
* **Properties**:
  * `PageIndex` (`int`): The zero-based index of the PDF page.
  * `RenderedPage` (`BitmapSource`): The rendered bitmap representation of the page.
  * `NativeLines` (`IReadOnlyList<PdfPageTextLine>`): Text lines extracted directly from the PDF stream using native vector/text descriptors.
  * `ImageRegions` (`IReadOnlyList<Windows.Foundation.Rect>`): Bounding rectangles (in rendered image pixel coordinates) for embedded images found on the page.
  * `HasNativeText` (`bool`): Returns `true` if `NativeLines.Count > 0`.

---

### 2. `PdfPageTextLine`
An `internal sealed` model representing a single line of text located within a page.

* **Constructor**:
  ```csharp
  public PdfPageTextLine(Windows.Foundation.Rect sourceRect, string text, bool isNativeText)
  ```
* **Properties**:
  * `SourceRect` (`Windows.Foundation.Rect`): Coordinate bounding box for the line of text relative to the rendered page bitmap pixels.
  * `Text` (`string`): The textual content of the line.
  * `IsNativeText` (`bool`): Flag indicating whether the text was extracted natively from the PDF structure (`true`) or derived via OCR (`false`).

---

### 3. `PdfDocumentRenderer`
The primary rendering and text extraction manager class. Implements `IDisposable`.

#### **Constants & Fields**
* `DefaultRenderScale` (`double` = `2.0`): Default multiplier for rendering resolution.
* `MaxRenderPixelCount` (`long` = `20_000_000`): Upper safety threshold for rendered bitmap pixel count ($width \times height$).
* `MaxCachedPages` (`int` = `10`): Maximum number of rendered page instances held in memory simultaneously.
* `MaxCachedPageBytes` (`long` = `268,435,456` bytes / `256 MB`): Total byte size threshold allowed for the memory cache.
* `renderDocument` (`WinPdfDocument`): Native Windows API handle for PDF rendering.
* `textDocument` (`PigPdfDocument`): PdfPig document instance for structural and native text analysis.
* Cache management data structures:
  * `pageCache` (`Dictionary<int, PdfPageContent>`): Map of zero-based page indices to cached `PdfPageContent`.
  * `pageCacheSizes` (`Dictionary<int, long>`): Byte size tracker per cached page.
  * `cacheOrder` (`LinkedList<int>`): Doubly-linked list maintaining LRU cache eviction ordering.
  * `cachedPageBytes` (`long`): Current aggregated memory footprint of all cached pages.

---

## Public Methods & API Usage

### `LoadAsync`
```csharp
public static async Task<PdfDocumentRenderer> LoadAsync(string filePath)
```
* **Description**: Factory method to validate and load a PDF file from a given local path.
* **Exceptions**:
  * `InvalidOperationException`: Thrown if `IoUtilities.IsPdfFileExtension` returns `false` for the file extension.
* **Workflow**:
  1. Validates the PDF file extension.
  2. Resolves the absolute path and retrieves a `StorageFile` reference.
  3. Loads `Windows.Data.Pdf.PdfDocument` (`WinPdfDocument`) asynchronously.
  4. Opens `UglyToad.PdfPig.PdfDocument` (`PigPdfDocument`) on the absolute path.
  5. Returns a initialized `PdfDocumentRenderer` instance.

### `ExtractTextAsync`
```csharp
public async Task<string> ExtractTextAsync(ILanguage? language = null, GrabTemplate? grabTemplate = null)
```
* **Description**: Sequentially extracts text across all pages in the PDF document.
* **Parameters**:
  * `language` (`ILanguage?`): Target language for OCR/text operations. Defaults to `LanguageUtilities.GetCurrentInputLanguage()`.
  * `grabTemplate` (`GrabTemplate?`): Optional template instance to apply custom region/extraction rules.
* **Behavior**:
  * If `grabTemplate` is provided, it converts each page to a `Bitmap` and runs `GrabTemplateExecutor.ExecuteTemplateOnBitmapAsync`.
  * If no template is provided, it calls `GetSelectableLinesAsync` per page and joins text lines with newlines.
  * Pages separated by two empty lines (`Environment.NewLine`).

### `GetPageContentAsync`
```csharp
public async Task<PdfPageContent> GetPageContentAsync(int pageIndex)
```
* **Description**: Retrieves or generates `PdfPageContent` for the specified page index, utilizing an LRU cache.
* **Cache Eviction**: If cache limits (`MaxCachedPages` or `MaxCachedPageBytes`) are exceeded after adding a new page, the oldest cached pages are purged using `RemoveOldestCachedPage()`.

### `GetSelectableLinesAsync`
```csharp
public async Task<IReadOnlyList<PdfPageTextLine>> GetSelectableLinesAsync(int pageIndex, ILanguage? language = null)
```
* **Description**: Generates text lines using a hybrid approach combining native text extraction and OCR fallback.
* **Hybrid Logic**:
  1. **No Native Text**: Executes full-page OCR via `GetOcrLinesAsync`.
  2. **Native Text Present & No Embedded Images**: Returns `NativeLines` directly.
  3. **Native Text & Embedded Images Present**: Combines `NativeLines` with OCR performed *specifically* within embedded image regions that do not overlap existing native text lines. Lines are sorted using `SortLines`.

### `RenderPageAsync`
```csharp
public async Task<BitmapSource> RenderPageAsync(int pageIndex)
```
* **Description**: Helper method that fetches page content and returns the `BitmapSource` rendered page image.

### `Dispose`
```csharp
public void Dispose()
```
* **Description**: Clears all cache dictionaries/lists, resets cache memory counters, and disposes the underlying PdfPig `textDocument`.

---

## Core Algorithms & Helper Methods

### 1. PDF Point to Rendered Image Coordinate Mapping
#### `ConvertPdfRectToImageRect`
```csharp
internal static Windows.Foundation.Rect ConvertPdfRectToImageRect(
    PdfRectangle pdfRect,
    double pageWidthPoints,
    double pageHeightPoints,
    double renderedWidth,
    double renderedHeight)
```
* **Purpose**: Converts PDF page bounding boxes (which use a coordinate space measured in points with the origin `(0,0)` at the **bottom-left**) into standard pixel space (origin `(0,0)` at the **top-left**).
* **Formula**:
  * $X_{pixel} = \frac{X_{pdf}}{Width_{points}} \times Width_{rendered}$
  * $Y_{pixel} = \left(1 - \frac{Y_{pdf}}{Height_{points}}\right) \times Height_{rendered}$

---

### 2. Native Word-to-Line Clustering Algorithm
#### `GroupWordsIntoLines`
```csharp
internal static IReadOnlyList<PdfPageTextLine> GroupWordsIntoLines(IEnumerable<(Windows.Foundation.Rect SourceRect, string Text)> words)
```
* **Purpose**: Native PDF text extraction yields individual words. This method groups distinct words into coherent baseline-aligned text lines.
* **Rules for Line Grouping**:
  1. Filters out empty strings or words with non-positive dimensions.
  2. Orders words vertically by `Y` coordinate, then horizontally by `X` coordinate.
  3. Iterates over ordered words and evaluates proximity to the active line group:
     * **Baseline Alignment**: $\lvert CenterY_{word} - CenterY_{group} \rvert \le LineHeight \times 0.6$
     * **Horizontal Spacing**: $HorizontalGap \le LineHeight \times 6$
  4. If both conditions pass, the word is added to the active group. Otherwise, a new line group is initialized.
  5. Formatted lines are constructed by joining words with single spaces and wrapping them in `PdfPageTextLine` instances with `IsNativeText = true`.

---

### 3. Target Render Dimension Calculation
#### `GetRenderDimensions`
```csharp
internal static (uint Width, uint Height) GetRenderDimensions(double pageWidth, double pageHeight, double scaleFactor = DefaultRenderScale)
```
* **Purpose**: Calculates optimal bitmap pixel dimensions while applying system constraints.
* **Constraints Enforced**:
  1. Applies `scaleFactor` (Default: `2.0`).
  2. Clamps maximum width/height to `OcrEngine.MaxImageDimension`.
  3. Clamps total pixel surface area ($Width \times Height$) to `MaxRenderPixelCount` (`20,000,000` pixels).

---

### 4. Image Region OCR Filtering
#### `ShouldIncludeOcrLine`
```csharp
internal static bool ShouldIncludeOcrLine(Windows.Foundation.Rect sourceRect, IReadOnlyList<Windows.Foundation.Rect> imageRegions)
```
* **Purpose**: Determines if an OCR-detected text bounding box falls inside any designated target rects (e.g., embedded image regions).
* **Criterion**: An OCR line is included if the intersection area between `sourceRect` and an `imageRegion` accounts for **at least 25%** ($0.25$) of the `sourceRect` total area.

---

### 5. Line Sorting
#### `SortLines`
```csharp
private static List<PdfPageTextLine> SortLines(IEnumerable<PdfPageTextLine> lines)
```
* **Purpose**: Orders line objects primarily top-to-bottom by bounding box Y coordinate (`Y`), and secondarily left-to-right (`X`).

---

## Memory Management & Cache Strategy

The `PdfDocumentRenderer` implements an **In-Memory LRU (Least Recently Used) Cache** for `PdfPageContent`:

* **Tracking**: `cacheOrder` (`LinkedList<int>`) holds page indices. When a page is read, its node is moved to the end of the list (`AddLast`).
* **Capacity Limit Rules**: Eviction triggers when either:
  1. `pageCache.Count >= MaxCachedPages` (`10` pages)
  2. `cachedPageBytes + newPageSize > MaxCachedPageBytes` (`256 MB`)
* **Eviction Method** (`RemoveOldestCachedPage`): Removes the node at `cacheOrder.First`, deletes corresponding entries from `pageCache` and `pageCacheSizes`, and decrements `cachedPageBytes`.
* **Bitmap Size Estimation** (`EstimateBitmapBytes`):
  $$\text{Bytes} = \frac{\text{PixelWidth} \times \text{PixelHeight} \times \max(\text{BitsPerPixel}, 32)}{8}$$