# Technical Documentation Guide: `GrabFrame.xaml.cs`

**File Location:** `Text-Grab/Views/GrabFrame.xaml.cs`  
**Namespace:** `Text_Grab.Views`  
**Class Name:** `GrabFrame` (inherits from `System.Windows.Window`)

---

## 1. Overview and Purpose

`GrabFrame` is a central window view in Text-Grab that acts as an interactive, adjustable framing tool for capturing, reading, analyzing, and editing text from various visual content sources. It creates a frame over the screen or displays a visual document (such as static images, PDFs, or fullscreen captures) and overlays interactive bounding boxes ("Word Borders") around recognized text.

Key functional capabilities of `GrabFrame` include:
* **Text Extraction Sources:** OCR (Optical Character Recognition via Windows Media OCR or Tesseract), UI Automation (Direct Text), and native PDF text rendering.
* **Interactive Editing:** Moving, resizing, splitting, merging, deleting, and editing text within recognized word borders.
* **Freeze Frame Mode:** Capturing a static snapshot of the screen underneath or displaying pre-loaded images/PDFs with scale control.
* **Table Analysis:** Grouping text into tabular rows and columns with manual row/column divider placement.
* **Search and Pattern Matching:** Live regex searching, exact string search, and pattern matching (e.g., regex patterns/built-in recognizers).
* **Templates:** Defining, editing, and executing structured extraction templates across specific regions or pattern matches.
* **AI Translation & Text-to-Speech:** Real-time translation using Windows AI and spoken feedback via TTS.
* **Image Processing & Barcode Reader:** Magick-based image manipulations (Invert, Auto Contrast, Brighten, Darken, Grayscale) and ZXing barcode decoding.

---

## 2. Inner Helper Records

* **`GrabFrameSearchMatch`**: A sealed record that tracks matched text, associated `WordBorder` controls, and `PdfTextLineOverlay` elements, along with selection state (`IsSelected`).
* **`GrabFrameSearchUnit`**: A sealed record representing searchable line units, linking text content, word segments, PDF overlays, and coordinate positions.
* **`OcrBorderRenderInfo`**: A private class used to structure bounding boxes, text lines, display heights, and line numbers before generating `WordBorder` elements.

---

## 3. Class Fields & State Management

### Routed Commands
* `DeleteWordsCommand`, `MergeWordsCommand`, `PasteCommand`, `RedoCommand`, `UndoCommand`, `GrabCommand`, `GrabTrimCommand`.

### State & Canvas Controls
* `wordBorders` (`ObservableCollection<WordBorder>`): Active rendered OCR/text border elements.
* `pdfTextLineOverlays` (`List<PdfTextLineOverlay>`): Rendered overlays for PDF pages with selectable native lines.
* `tableEditState` (`GrabFrameTableEditState`): Handles active manual row/column divider placement for tables.
* `UndoRedo`: Manages the operation stack for canvas edits (adding, deleting, moving, resizing word borders, or altering background images).
* `contentChangeDetector` (`ImageChangeDetector`): Compares captured screen regions to automatically re-OCR when underlying screen content changes.

### Timers
* `reDrawTimer`: Debounces redrawing and running OCR after window movements or content updates.
* `reSearchTimer`: Debounces text searches and pattern matches.
* `contentChangeTimer`: Periodically captures screen regions when unfrozen to detect content changes.
* `translationTimer`: Controls debounced translation triggers.
* `frameMessageTimer`: Automatically hides temporary frame notification banners.

---

## 4. Properties

* **`CurrentLanguage` (`ILanguage`)**: Gets or sets the active capture language. Automatically falls back to system OCR languages if none is set.
* **`DestinationTextBox` (`TextBox?`)**: An optional target `TextBox` (e.g., from an `EditTextWindow`) linked to this frame. When set, frame text updates mirror directly to this control.
* **`FrameText` (`string`)**: The accumulated text derived from all currently selected or rendered word borders, tables, or PDF overlays.
* **`IsFreezeMode` (`bool`)**: Indicates whether the frame is frozen showing a static background snapshot or file.
* **`IsWordEditMode` (`bool`)**: Controls whether `WordBorder` elements are in inline text editing mode.
* **`ShouldSaveOnClose` (`bool`)**: Determines whether frame state should be saved to application history when closed.
* **`IsCtrlDown` (`bool`)**: Indicates if the Control key is pressed or if manual rectangle addition mode is forced.

---

## 5. Constructor Overloads

1. **`GrabFrame()`**  
   Standard default constructor. Initializes components, theme, timers, user settings, and starts the redraw timer.
2. **`GrabFrame(HistoryInfo historyInfo)`**  
   Opens a frame populated with historical session data without auto-saving on close.
3. **`GrabFrame(string imagePath)`**  
   Loads an image or PDF file from the given file path into the frame. Validates path existence and converts to absolute paths.
4. **`GrabFrame(HistoryInfo historyInfo, string sourcePath)`**  
   Combines history loading with a specific source path and initial PDF page index.
5. **`GrabFrame(BitmapSource frozenImage, UiAutomationOverlaySnapshot? uiAutomationSnapshot = null)`**  
   Opens directly in freeze mode displaying a bitmap (e.g., cropped from a Fullscreen Grab) and optional UI Automation snapshot overlay.
6. **`GrabFrame(GrabTemplate template)`**  
   Opens the frame in template editing mode, populating regions, patterns, output text template, and reference image.

---

## 6. Primary Functional Subsystems

### A. Document & Image Loading
* **`TryLoadDocumentFromPath(string path)`**: Determines file type (PDF vs Image) via file extension and routes to `TryLoadPdfFromPath` or `TryLoadImageFromPath`.
* **`TryLoadPdfFromPath(string path)`**: Asynchronously loads a PDF via `PdfDocumentRenderer` and navigates to the initial page.
* **`ShowPdfPageAsync(int pageIndex)` / `ChangePdfPageAsync(int delta)`**: Renders page content, sets native selectable line overlays, and updates page navigation controls.
* **`LoadContentFromHistory(HistoryInfo history)`**: Recreates history state, restores `WordBorder` elements, table separators, frame positioning, and background images.

### B. Freezing and Screen Diffing Strategy
* **`FreezeGrabFrame()`**: Captures the screen area beneath the window (or uses the preloaded image), sets `IsFreezeMode = true`, fixes window topmost state, and allows zooming/scaling.
* **`UnfreezeGrabFrame()`**: Returns the frame to live transparent mode, clearing loaded visual document states and enabling background screen sampling.
* **`UnfreezeGrabFrameWithDiff()` & `FinishUnfreezeWithDiffAsync()`**: When unfreezing a live frame, samples the live screen underneath and performs a bitmap difference check (`ImageChangeDetector.ImagesDifferBeyondThreshold`). If the screen content has not changed, existing edited word borders are preserved rather than reset.

### C. Text Extraction & Overlay Generation
* **`DrawRectanglesAroundWords(string searchWord)`**: Master entry point that routes to OCR, PDF, or UI Automation overlay rendering depending on source document and language mode.
* **`DrawOcrRectanglesAsync(string searchWord)`**: Runs OCR on the image source or screen area, groupings word boxes (Word, Line, Paragraph, or Window mode via `wordGroupingMode`), and generates `WordBorder` elements.
* **`DrawPdfRectanglesAsync(string searchWord)`**: Generates native selectable text overlays (`PdfTextLineOverlay`) directly from PDF text metadata.
* **`DrawUiAutomationRectanglesAsync(string searchWord)`**: Extracts native OS UI Automation element bounding boxes (`UiAutomationOverlaySnapshot`) and maps them into interactive `WordBorder` instances.

### D. Canvas Interaction & Word Border Operations
* **Word Editing & Selection**: Users can select individual borders or drag selection boxes (`selectBorder`) to select groups. `CheckSelectBorderIntersections` evaluates bounding box intersections.
* **Custom Border Addition**: Holding `Ctrl` while dragging creates a new manual `WordBorder`, running localized OCR via `OcrUtilities.GetTextFromAbsoluteRectAsync`.
* **Merging & Splitting**: 
  * `MergeSelectedWordBorders()`: Combines selected borders into a single bounding box using `ResultTable` to reconstruct text.
  * `BreakWordBorderIntoWords(WordBorder wordBorder)`: Splits a multi-line or multi-word border into individual single-word borders.
* **Text Corrections**: `TryToAlphaMenuItem_Click` and `TryToNumberMenuItem_Click` apply character replacement heuristics (fixing number/letter confusion).

### E. Table Analysis & Manual Placement
* **`TryToPlaceTable()`**: Analyzes active `WordBorder` coordinates and manual row/column separators (`tableEditState`) using `ResultTable`, generating tabular text output and visual row/column division lines.
* **Placement Subsystem**: `BeginTablePlacement`, `TryCommitTablePlacement`, and `UpdateTablePlacementPreview` enable interactive row/column line insertion with snapping and minimum distance constraints.

### F. Search and Pattern Matching
* **`ReSearchTimer_Tick(object, EventArgs)`**: Executes live text searches using string matching or regular expressions.
* **`RunPatternSearch(PatternItem pattern, string narrowText)`**: Executes pattern-based recognizers (e.g., URLs, Email addresses, Phone numbers) across search units and highlights matching segments.
* **`BuildSearchText(...)` & `GroupWordBordersIntoSearchLines()`**: Assembles line-by-line searchable text units while maintaining coordinate alignment and right-to-left layout order.

### G. Templates & Template Mode
* **`SaveTemplateSave_Click(object, RoutedEventArgs)`**: Saves active canvas regions as a reusable `GrabTemplate`. Stores relative coordinates (`RatioLeft`, `RatioTop`, etc.), pattern references (`TemplatePatternMatch`), recognizers (`TemplateRecognizerMatch`), and reference images.
* **`UpdateTemplateBadges()` / `UpdateTemplateRegionOverlay()`**: Displays numbered region badges over `WordBorder` elements when drafting templates or highlights template region bounds on the canvas.

### H. AI Translation and Text-to-Speech
* **Translation Subsystem**: `PerformTranslationAsync` uses `WindowsAiUtilities` and a concurrency-limiting `SemaphoreSlim` to translate text inside active `WordBorder` controls to a chosen target language.
* **Text-to-Speech (TTS)**: Subscribes to `Singleton<TtsService>.Instance`. Automatically speaks extracted frame text when armed (`_speakOnNextFrameTextUpdate`) and `isSpeakEnabled` is true.

### I. Image Processing & Barcode Detection
* **Image Filters**: Implements image enhancements using `MagickHelpers`:
  * `InvertColorsMI_Click`: Inverts colors.
  * `AutoContrastMI_Click`: Adjusts contrast.
  * `BrightenMI_Click` / `DarkenMI_Click`: Modifies brightness.
  * `GrayscaleMI_Click`: Converts to grayscale.
* **Barcode Reader**: `TryToReadBarcodes(DpiScale dpi)` utilizes ZXing `BarcodeReader` to decode barcodes/QR codes in the frame image and inserts labeled barcode `WordBorder` elements.

### J. Execution & Grab Output
* **`GrabExecuted(...)`**: Copies frame text to clipboard, appends it to a linked `DestinationTextBox`, or executes the active `GrabTemplate` across the frame bounds.
* **`GrabTrimExecuted(...)`**: Performs the grab operation while stripping line breaks (`MakeStringSingleLine`).

---

## 7. Commands & Event Summary

| Event / Command | Trigger / Handler | Functionality |
| :--- | :--- | :--- |
| `Escape` Key | `Escape_Keyed` | Cancels table placement, clears active searches, resets canvas overlays, or closes window. |
| `Ctrl+V` | `PasteExecuted` | Loads image data from clipboard into frame. |
| `Ctrl+S` | `SaveGrabFrameFileMenuItem_Click` | Serializes frame history and borders into a `.tggf` file via `GrabFrameFileUtilities`. |
| `MouseWheel` | `HandlePreviewMouseWheel` | Resizes frame dimensions or controls image zoom depending on `scrollBehavior`. |
| `LocationChanged` / `SizeChanged` | `Window_LocationChanged` / `Window_SizeChanged` | Resets frame overlays and restarts redraw timer for unfrozen frames. |

---

## 8. Lifetime and Resource Cleanup

The method `CleanupGrabFrame()` ensures thorough resource disposal when the window is unloaded or closed:
* Detaches window and timer event listeners (`reDrawTimer`, `reSearchTimer`, `contentChangeTimer`, `translationTimer`, `frameMessageTimer`).
* Cancels pending PDF tasks and disposes `_loadedPdfDocument`.
* Clears active bitmap sources (`GrabFrameImage.Source = null`) and image caches.
* Resets `UndoRedo` operation stacks to break circular references held by `WordBorder` objects (`wb.OwnerGrabFrame`).
* Invokes `ResetAutomationPeerChildrenCache` on `RectanglesCanvas` and `PdfTextCanvas` to clear cached WPF Automation Peers and prevent memory leaks.