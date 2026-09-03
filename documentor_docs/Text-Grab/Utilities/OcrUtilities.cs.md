# Technical Documentation: `Text-Grab/Utilities/OcrUtilities.cs`

## Overview

The `OcrUtilities` static class provides a comprehensive set of utility methods for performing Optical Character Recognition (OCR), processing text line positioning, filtering UI/annotation noise (such as Japanese Furigana), formatting extracted text, and running OCR across various input types including screen regions, bitmaps, `BitmapSource` objects, WPF windows, and files (images and PDFs).

It supports multiple recognition backends and abstractions, including Windows Media OCR (`WinRtOcrLinesWords`), Windows AI / Copilot+ OCR (`WindowsAiLang`), Tesseract OCR (`TessLang`), Windows AI Descriptions (`WindowsAiDescriptionLang`), UI Automation (`UiAutomationLang`), and barcode reading capabilities.

---

## Inner Data Structures

### `PositionedOcrLine`
An internal `readonly record struct` representing an OCR line with its order index, text, and bounding box.
* **`LineNumber`** (`int`): Zero-based index of the line.
* **`Text`** (`string`): Text content of the line.
* **`BoundingBox`** (`Windows.Foundation.Rect`): Positional rectangle of the line.

### `GroupedOcrLines`
An internal class that groups lines belonging to the same wrapped paragraph block.
* **`BoundingBox`** (`Windows.Foundation.Rect`): Combined bounding rectangle of all lines in the group.
* **`Lines`** (`IReadOnlyList<PositionedOcrLine>`): Collection of positioned lines in this group.
* **`StartingLineNumber`** (`int`): Index of the first line in the group.
* **`DisplayText`** (`string`): Returns multi-line text separated by newlines.
* **`SingleLineText`** (`string`): Joins non-empty lines in the group into a single space-delimited string.

---

## Functional Overview by Category

### 1. Language and Engine Fallback Utilities

* **`IsUiAutomationLanguage(ILanguage language)`**: Checks if the specified language is an instance of `UiAutomationLang`.
* **`IsWindowsAiDescriptionLanguage(ILanguage language)`**: Checks if the specified language is an instance of `WindowsAiDescriptionLang`.
* **`GetCompatibleOcrLanguage(ILanguage language)`**: Returns a fallback OCR language if the passed language is `UiAutomationLang`; otherwise, returns the original language unchanged.
* **`GetExcludedWindowHandles(Window passedWindow)`**: Retrieves the native window handle (`IntPtr`) for the passed WPF `Window` to exclude it during UI Automation screen queries. Returns `null` if the handle is `IntPtr.Zero`.

---

### 2. Core Image-to-OCR Execution

* **`GetTextFromImageAsync(Bitmap bitmap, ILanguage language)`**:
  Directs bitmap OCR processing depending on the language type:
  * Redirects `TessLang` to `TesseractHelper`.
  * Redirects `WindowsAiLang` to `GetTextFromWinAiAsync`.
  * Redirects `WindowsAiDescriptionLang` to `GetTextFromWinAiDescriptionAsync`.
  * Processes standard languages using `GetOcrResultFromImageAsync`, uniform scaling via `GetIdealScaleFactorForOcrAsync`, and `GetTextFromOcrResult`.
  * If `DefaultSettings.TryToReadBarcodes` is enabled, appends barcode scan outputs from `BarcodeUtilities`.

* **`GetOcrResultFromImageAsync(SoftwareBitmap scaledBitmap, ILanguage language)`**:
  Executes low-level OCR on a `SoftwareBitmap`:
  * Returns Windows AI Description results if `WindowsAiDescriptionLang`.
  * Uses `WindowsAiUtilities.GetOcrResultAsync` for `WindowsAiLang` (falls back to system input language or `en-US` if null).
  * Uses `OcrEngine.TryCreateFromLanguage` to obtain standard `WinRtOcrLinesWords` results via Windows Media OCR.

* **`GetOcrResultFromImageAsync(Bitmap scaledBitmap, ILanguage language)`**:
  Converts a `System.Drawing.Bitmap` into a `SoftwareBitmap` via an intermediate `WrappingStream` and `BitmapDecoder`, then delegates to the `SoftwareBitmap` overload.

* **`GetTextFromWinAiAsync(Bitmap bitmap, WindowsAiLang language)`**:
  Runs OCR using Windows AI utilities. Uses paragraph detection if applicable, or saves the bitmap to a temporary directory to call `WindowsAiUtilities.GetTextWithWinAI`.

* **`GetTextFromWinAiDescriptionAsync(Bitmap bitmap, WindowsAiDescriptionLang language)`**:
  Retrieves text generated from image description functionality using Windows AI models.

* **`GetWindowsAiDescriptionOcrResultAsync(SoftwareBitmap softwareBitmap)`**:
  Internal helper that retrieves AI text descriptions for an image and encapsulates them in a `GeneratedOcrLinesWords` instance using the full bitmap bounds.

---

### 3. Screen Region and WPF Window Extraction

* **`GetTextFromAbsoluteRectAsync(Rect rect, ILanguage language, IReadOnlyCollection<IntPtr>? excludedHandles = null, Bitmap? preCapturedBitmap = null)`**:
  Captures screen text from a bounding rectangle. If UI Automation is specified, attempts UI Automation text extraction first. Defaults to capturing the region as a `Bitmap` (if `preCapturedBitmap` is not supplied) and processing via OCR.

* **`GetRegionsTextAsync(Window passedWindow, Rectangle selectedRegion, ILanguage language)`**:
  Calculates absolute screen coordinates for a relative region on `passedWindow` and invokes `GetTextFromAbsoluteRectAsync`.

* **`GetRegionsTextAsTableAsync(Window passedWindow, Rectangle selectedRegion, ILanguage objLang)`**:
  Captures a screen region, scales it, executes OCR, parses word bounding boxes into `WordBorderInfo` instances using `ResultTable`, constructs a tabular layout analysis (`ResultTable.AnalyzeAsTable`), and extracts structured text.

* **`GetOcrResultFromRegionAsync(Rectangle region, ILanguage language)`**:
  Captures screen region, determines ideal scaling, and returns the underlying `IOcrLinesWords` OCR result along with the scale factor used.

---

### 4. Bitmap & Stream OCR Operations

* **`GetTextFromBitmapAsync(Bitmap bitmap, ILanguage language)`**: Runs OCR processing on a `Bitmap` instance.
* **`GetTextFromBitmapSourceAsync(BitmapSource bitmapSource, ILanguage language)`**: Converts a WPF `BitmapSource` to a `Bitmap` and processes it.
* **`GetTextFromBitmapAsTableAsync(Bitmap bitmap, ILanguage language)`**: Analyzes text alignment in a `Bitmap` to format and return the output as tabular text.
* **`GetTextFromBitmapSourceAsTableAsync(BitmapSource bitmapSource, ILanguage language)`**: Converts a `BitmapSource` to `Bitmap` and runs table extraction.
* **`GetOcrResultFromBitmapAsync(Bitmap bmp, ILanguage language)`**: Scales bitmap, executes OCR engine, and returns `IOcrLinesWords` and the scale factor.
* **`GetTextFromRandomAccessStream(IRandomAccessStream randomAccessStream, ILanguage language)`**: Decodes a `Bitmap` from an image stream and runs standard OCR extraction.

---

### 5. Historical / Replay Operations

* **`GetCopyTextFromPreviousRegion()`**: Re-runs OCR on the most recent full-screen grab history region, showing a `PreviousGrabWindow` UI loading overlay, updating history, and copying extracted text.
* **`GetTextFromPreviousFullscreenRegion(TextBox? destinationTextBox = null)`**: Similar to `GetCopyTextFromPreviousRegion()`, but explicitly targets populating a destination WPF `TextBox`.
* **`CanReplayPreviousFullscreenSelection(HistoryInfo history)`**: Checks whether a history item uses a compatible selection style (`Region` or `AdjustAfter`). Displays a dialog if invalid.

---

### 6. File & Directory Operations

* **`OcrAbsoluteFilePathAsync(string absolutePath, ILanguage? language = null)`**:
  Extracts text from a specified file path. Parses PDF files using `PdfDocumentRenderer` or loads images via `LoadBitmapFromFile` before running OCR.

* **`OcrFile(string path, ILanguage? selectedLanguage, OcrDirectoryOptions options)`**:
  Batch file processing helper. Supports templated grabs (`GrabTemplate`), direct file OCR, saving individual output `.txt` files alongside source images, and error reporting.

* **`LoadBitmapFromFile(string absolutePath)`**:
  Internal helper that reads an image file into a frozen `BitmapImage` (applying EXIF rotation) and converts it to a `System.Drawing.Bitmap`.

---

### 7. Interactive / Clicked Word Utilities

* **`GetClickedWordAsync(Window passedWindow, Point clickedPoint, ILanguage OcrLang)`**:
  Finds the word directly under `clickedPoint` relative to `passedWindow`. Attempts UI Automation first if specified; otherwise captures window bounds and inspects OCR bounding boxes.

* **`GetTextFromClickedWordAsync(Point singlePoint, Bitmap bitmap, ILanguage language)`**:
  Helper that obtains the `IOcrLinesWords` for `bitmap` and checks bounding boxes.

* **`GetTextFromClickedWord(Point singlePoint, IOcrLinesWords ocrResult)`**:
  Iterates over lines and words within `ocrResult` to return the text of the first word whose bounding box contains `singlePoint`.

---

### 8. Text Reconstruction, Paragraph Detection, & Furigana Filtering

* **`GetTextFromOcrLine(this IOcrLine ocrLine, bool isSpaceJoiningOCRLang, StringBuilder text, bool shouldCorrectToLatin = true)`**:
  Extension method processing individual OCR lines:
  * Handles space-joining languages vs. CJK character layouts.
  * Optionally invokes `FilterFurigana` for CJK words if `DefaultSettings.RemoveFurigana` is enabled.
  * Integrates error correction (`TryFixEveryWordLetterNumberErrors`, `TryFixNumberLetterErrors`) and Latin character replacement (`ReplaceGreekOrCyrillicWithLatin`) based on user settings.

* **`FilterFurigana(List<IOcrWord> words)`**:
  Calculates line median word height. Filters out words shorter than 60% of median height that sit directly above larger words (kanji annotation text).

* **`FilterFuriganaLines(IReadOnlyList<IOcrLine> lines)`**:
  Filters out entire furigana reading lines when a significantly taller line overlaps directly below within close vertical proximity.

* **`OrderLinesForReadingFlow(IReadOnlyList<IOcrLine> lines)`**:
  Groups OCR lines into horizontal visual rows based on vertical overlap (>50% height overlap) and orders rows top-to-bottom and lines within rows left-to-right.

* **`BuildTextFromOcrLines(ILanguage language, IOcrLinesWords ocrResult)`**:
  Primary pipeline for converting raw OCR lines into structured string output. Orchestrates paragraph grouping, reading flow re-ordering, furigana line removal, word joining, and Right-to-Left (RTL) string reversing.

* **`ShouldUseParagraphDetection(bool isSpaceJoiningLanguage, bool isTableMode = false)`**:
  Returns `true` if paragraph detection is enabled in settings, the language is space-joining, and table mode is disabled.

* **`GroupWrappedParagraphLines(IReadOnlyList<PositionedOcrLine> lines)`**:
  Groups consecutive lines into single paragraph blocks using `IsWrappedParagraph`.

* **`IsWrappedLine(IOcrLine currentLine, IOcrLine nextLine)`**:
  Evaluates whether two adjacent `IOcrLine` objects belong to the same wrapped paragraph.

* **`IsWrappedParagraph(double currentTop, double currentHeight, double nextTop, double nextHeight)`**:
  Core heuristic determining paragraph wrapping:
  * Ensures height ratio between lines $\le 1.5$.
  * Ensures vertical progression ($\text{nextTop} - \text{currentTop} \ge 0.5 \times \text{minHeight}$).
  * Checks if vertical gap between lines is $< 0.6 \times \text{average line height}$.

* **`GetStringFromOcrOutputs(List<OcrOutput> outputs)`**:
  Concatenates cleaned or raw text outputs from a collection of `OcrOutput` objects.

* **`UnionRectangles(Windows.Foundation.Rect current, Windows.Foundation.Rect next)`**:
  Computes the bounding rectangle encompassing two `Rect` structures.

---

### 9. Scaling & Geometry Calculation

* **`GetIdealScaleFactorForOcrAsync(Bitmap bitmap, ILanguage selectedLanguage)`**:
  Calculates the optimal image scaling factor to improve OCR accuracy based on line height analysis.

* **`GetIdealScaleFactorForOcrResult(IOcrLinesWords ocrResult, int height, int width)`**:
  Computes average line height across recognized words. Targets an ideal line height of **40 pixels**. Ensures scaling does not violate `OcrEngine.MaxImageDimension`.

* **`GetBoundingRect(this OcrLine ocrLine)`**:
  Calculates the composite bounding rectangle (`Rect`) enclosing all words in an `OcrLine`.

---

## Generated Regular Expressions

* **`SpaceJoiningWordRegex()`**:
  Generated via `[GeneratedRegex(@"^\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}")]`. Identifies words matching space-joining language constraints (letters, digits, or string lengths $\ge 2$).