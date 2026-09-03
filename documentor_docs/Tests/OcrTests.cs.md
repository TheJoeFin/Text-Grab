# Technical Documentation: `Tests/OcrTests.cs`

## Overview

The `Tests/OcrTests.cs` file contains a unit and integration test suite for the Optical Character Recognition (OCR) features of the **Text-Grab** application. It evaluates OCR pipelines, character output cleanup, table detection and reconstruction, paragraph wrapping detection, CJK (Chinese, Japanese, Korean) reading order, Furigana (ruby text) filtering heuristics, and Tesseract engine integrations.

The test suite uses **xUnit** as its testing framework alongside custom WPF test attributes (`[WpfFact]`) to handle WPF-dependent image and UI thread operations.

---

## Test File Dependencies & Mock Types

### Key External & Project Dependencies
- **xUnit**: Testing attributes (`[Fact]`, `[Theory]`, `[InlineData]`) and assertions (`Assert`).
- **Text-Grab Namespaces**:
  - `Text_Grab.Interfaces`: `IOcrLinesWords`, `IOcrLine`, `IOcrWord`, `ILanguage`.
  - `Text_Grab.Models`: `OcrOutput`, `OcrOutputKind`, `GlobalLang`, `TessLang`, `WindowsAiLang`, `WordBorderInfo`.
  - `Text_Grab.Properties`: Application settings access via `Settings`.
  - `Text_Grab.Utilities`: `AppUtilities`, `OcrUtilities`, `ResultTable`, `FileUtilities`, `ImageMethods`, `LanguageUtilities`, `TesseractHelper`, `TesseractGitHubFileDownloader`.
- **Windows Runtime**: `Windows.Foundation.Rect`, `Windows.Globalization.Language`, `Windows.Media.Ocr.OcrEngine`.

### Internal Test Helper Classes
To allow isolated unit testing without requiring real OCR engine calls, the file defines three mock implementations at the bottom of the class:

- `FakeOcrLinesWords`: Implements `IOcrLinesWords`. Holds an array of `IOcrLine` objects, a string `Text`, and a float `Angle`.
- `FakeOcrLine`: Implements `IOcrLine`. Stores a `Text` string, an array of `IOcrWord` objects, and a `BoundingBox` rectangle (`Windows.Foundation.Rect`).
- `FakeOcrWord`: Implements `IOcrWord`. Stores a `Text` string and a `BoundingBox` rectangle.
- `Word(string text, double x, double y, double width, double height)`: Static factory helper method that instantiates `FakeOcrWord`.

---

## Constants & Test Data

The file contains several pre-defined constants representing file paths and expected text outputs:

| Constant Name | Description / Path | Purpose |
| :--- | :--- | :--- |
| `fontSamplePath` | `.\Images\font_sample.png` | Sample image with various font names. |
| `fontSampleResult` | Multi-line string | Expected Windows OCR result for `font_sample.png`. |
| `fontSampleResultForTesseract` | Multi-line string | Expected Tesseract OCR result for `font_sample.png`. |
| `fontTestPath` | `.\Images\FontTest.png` | Secondary test image for font detection. |
| `fontTestResult` | Multi-line string | Expected OCR output for `FontTest.png`. |
| `tableTestPath` | `.\Images\Table-Test.png` | Simple tab-separated table sample image. |
| `tableTestResult` | Tab-delimited string | Expected parsed output from simple table image. |
| `ComplexTablePath` | `.\Images\Table-Complex.png` | Complex grid/financial table sample. |
| `ComplexWordBorders` | `.\TextFiles\Table-Complex-WordBorders.json` | JSON dataset of pre-parsed `WordBorderInfo` entries for complex table testing. |
| `ComplexTableResult` | Tab-delimited string | Expected output from complex table layout analysis. |
| `JaTestExpectedResult` | Japanese text string | Aspirational/target fully-corrected Japanese text. |
| `jaTestPath` | `.\Images\Ja-Lang-Image.png` | Sample image containing Japanese body text with Furigana (ruby annotations). |
| `JaReadingOrderResult` | Japanese text string | Expected OCR result when reading order is fixed but Furigana removal is disabled. |
| `JaFuriganaRemovedResult` | Japanese text string | Expected OCR result when Furigana removal heuristic is enabled. |

---

## Test Categories & Functional Breakdown

### 1. Character Normalization & Output Cleaning

#### `CleanOutput_CorrectsOnlyLatinCaptureLanguages(string languageTag, string expected)`
- **Type**: `[Theory]`
- **Parameters**: `languageTag` (`en-US`, `ru-RU`), `expected` (`H3llO`, `HЭllΘ`)
- **Description**: Validates `OcrOutput.CleanOutput()` under specific settings (`CorrectToLatin = true`, `CorrectErrors = false`). Ensures character substitution logic targets Latin-based language inputs (`en-US`) while preserving non-Latin characters for non-supported languages (e.g., Russian `ru-RU`). Resets application settings in a `finally` block.

---

### 2. Standard Image & QR Code OCR Tests

#### `OcrFontSampleImage()`
- **Type**: `[WpfFact]`
- **Description**: Executes `OcrUtilities.OcrAbsoluteFilePathAsync()` against `font_sample.png` and asserts that the returned text strictly matches `fontSampleResult`.

#### `OcrFontTestImage()`
- **Type**: `[WpfFact]`
- **Description**: Executes `OcrUtilities.OcrAbsoluteFilePathAsync()` against `FontTest.png` and validates text accuracy against `fontTestResult`.

#### `ReadQrCode()`
- **Type**: `[WpfFact]`
- **Description**: Verifies that `OcrUtilities.OcrAbsoluteFilePathAsync()` can process `QrCodeTestImage.png` and return decoded string content (`"This is a test of the QR Code system"`).

---

### 3. Table Parsing and Reconstruction Tests

#### `AnalyzeTable()`
- **Type**: `[WpfFact]`
- **Description**: Loads `Table-Test.png`, extracts OCR word positions using `OcrUtilities.GetOcrResultFromImageAsync()`, converts results to `WordBorderInfo` instances via `ResultTable.ParseOcrResultIntoWordBorderInfos()`, runs `ResultTable.AnalyzeAsTable()`, and validates the constructed tab-delimited text (`tableTestResult`).

#### `AnalyzeTable2()`
- **Type**: `[WpfFact]`
- **Description**: Similar to `AnalyzeTable()`, processes `Table-Test-2.png` and verifies column/row alignment formatting output.

#### `OcrComplexTableTestImage()`
- **Type**: `[WpfFact]`
- **Description**: Deserializes bounding-box data from `Table-Complex-WordBorders.json` into a list of `WordBorderInfo` objects and runs `ResultTable.AnalyzeAsTable()` against a canvas size of 1514x1243. Confirms that financial figures, headers, and column boundaries format matching `ComplexTableResult`.

---

### 4. Paragraph Wrap & Detection Tests

#### `ParagraphWrapDetection()`
- **Type**: `[WpfFact]`
- **Description**: Tests full OCR text extraction on `paragraph-test-image.png` with `ParagraphDetection = true`. Verifies wrapped lines are merged into unified paragraphs separated by proper CRLF line breaks.

#### `IsWrappedParagraph_ReturnsExpected(...)`
- **Type**: `[Theory]`
- **Parameters**: `currentTop`, `currentHeight`, `nextTop`, `nextHeight`, `expected`
- **Description**: Evaluates boundary geometric conditions inside `OcrUtilities.IsWrappedParagraph()` to determine whether two consecutive lines belong to the same wrapped paragraph block based on bounding box positioning, height ratios, and vertical gaps.

#### `BuildTextFromOcrLines_UsesParagraphDetectionForWinAi()`
- **Type**: `[Fact]`
- **Description**: Tests `OcrUtilities.BuildTextFromOcrLines()` using mock `FakeOcrLinesWords` and a `WindowsAiLang` language setting to ensure line wrapping logic combines wrapped lines while preserving paragraph separations.

#### `ShouldUseParagraphDetection_RespectsTableMode(...)`
- **Type**: `[Theory]`
- **Parameters**: `paragraphDetectionEnabled`, `isSpaceJoiningLanguage`, `isTableMode`, `expected`
- **Description**: Verifies `OcrUtilities.ShouldUseParagraphDetection()`. Ensures paragraph detection is suppressed when in table mode or when processing non-space-joining languages.

#### `GroupWrappedParagraphLines_*`
- **`GroupWrappedParagraphLines_CombinesWrappedLinesIntoParagraphBlocks`**: Tests merging sequential wrapped lines into single `GroupedOcrLines` instances.
- **`GroupWrappedParagraphLines_DoesNotMergeEntriesOnTheSameVisualRow`**: Ensures lines residing on the same vertical position (visual row) are kept distinct.
- **`GroupWrappedParagraphLines_RemovesEmbeddedLineBreaksFromIndividualOcrLines`**: Verifies embedded line breaks within single line entries are stripped.

---

### 5. CJK (Japanese/Chinese), Reading Order & Furigana Filtering Tests

#### Japanese System Pack Integration Tests
- **`OcrJapaneseImage_ReadingOrder_KeepsFuriganaWhenDisabled`**: Runs OCR on `Ja-Lang-Image.png` with `RemoveFurigana = false`. Checks if execution returns `JaReadingOrderResult` (top-to-bottom, left-to-right reading order with Furigana intact). Skips if Japanese OCR language pack is absent.
- **`OcrJapaneseImage_RemovesFuriganaWhenEnabled`**: Runs OCR on `Ja-Lang-Image.png` with `RemoveFurigana = true`. Asserts output equals `JaFuriganaRemovedResult` (ruby annotations stripped). Skips if Japanese language pack is absent.
- **`InspectJapaneseOcrOutput`**: Diagnostic method that executes OCR on `Ja-Lang-Image.png`, outputs detailed bounding box coordinate reports, tests reading flow order, and writes results to a temporary debug report file (`ja-ocr-report.txt`).

#### `FilterFurigana` Direct Logic Tests
Unit tests exercising `OcrUtilities.FilterFurigana(List<IOcrWord>)`:
- **`FilterFurigana_EmptyList_ReturnsEmpty`**: Handles empty input.
- **`FilterFurigana_SingleWord_IsKept`**: Preserves single-word inputs.
- **`FilterFurigana_UniformHeights_KeepsAllInOrder`**: Retains all words when bounding heights are equal/uniform.
- **`FilterFurigana_RemovesSmallWordAboveOverlappingKanji`**: Strips a small ruby word positioned directly above a horizontally-overlapping taller Kanji word.
- **`FilterFurigana_KeepsSmallWordWhenNotHorizontallyOverlapping`**: Retains small words if they do not overlap Kanji horizontally.
- **`FilterFurigana_KeepsSmallWordBelowMainText`**: Retains small words located below main text.
- **`FilterFurigana_KeepsSmallWordWhenWordBelowIsNotLarger`**: Retains small words if the underlying word is not significantly taller.
- **`FilterFurigana_OnlyRemovesShortWords`**: Theory testing character length thresholds (e.g., 1–2 character ruby text removed, 3+ characters retained).
- **`FilterFurigana_RemovesMultipleFuriganaKeepingMainText`**: Tests stripping multiple Furigana annotations across a sequence of words.

#### Line Assembly & Reading Flow Integration Tests
- **`BuildTextFromOcrLines_FiltersFuriganaForJapanese`**: Ensures CJK processing drops Furigana word entries from `FakeOcrLine` structures.
- **`BuildTextFromOcrLines_JapaneseWithoutFurigana_IsUnchanged`**: Confirms standard Japanese body text remains unmodified.
- **`BuildTextFromOcrLines_ChineseText_JoinsWithoutSpaces`**: Verifies Chinese (`zh-Hans`) characters join directly without whitespace.
- **`BuildTextFromOcrLines_FiltersRubyTextForChinese`**: Confirms ruby/bopomofo annotation filtering applies to Chinese text.
- **`BuildTextFromOcrLines_SpaceJoiningLanguage_DoesNotFilterFurigana`**: Validates that space-joining languages (e.g., `en-US`) skip CJK Furigana filtering heuristics completely.
- **`OrderLinesForReadingFlow_SortsRowsTopToBottomAndLeftToRight`**: Tests `OcrUtilities.OrderLinesForReadingFlow` on scrambled OCR line outputs to confirm correct geometric reading flow sorting.
- **`OrderLinesForReadingFlow_KeepsSeparateRowsInVerticalOrder`**: Validates vertical Y-coordinate ordering across multiple rows.
- **`FilterFuriganaLines_*`**: Direct unit tests targeting line-level Furigana filtering (`FilterFuriganaLines`):
  - Filters short lines directly above taller, overlapping main text lines.
  - Retains two stacked body lines with similar heights.
  - Retains short lines positioned to the side without overlapping Kanji.
  - Retains short lines positioned far above main lines (e.g., section headings).

---

### 6. Tesseract OCR Tests

These tests specifically target Tesseract OCR engine bindings in `TesseractHelper` and downloader features in `TesseractGitHubFileDownloader`.

- **`TesseractHocr()`**: *(Explicitly Skipped via `[WpfFact(Skip = ...)]`)* Tests HOCR format handling against `font_sample.hocr`.
- **`TesseractFontSample()`**: Processes `font_sample.png` via `TesseractHelper.GetOcrOutputFromBitmap()`. Bails gracefully if `tesseract.exe` is missing from the host machine, otherwise verifies output against `fontSampleResultForTesseract`.
- **`GetTessLanguages()`**: *(Skipped in CI)* Validates retrieving installed Tesseract language codes (e.g., `"eng"`, `"spa"`).
- **`GetTesseractStrongLanguages()`**: *(Skipped in CI)* Verifies language wrapper parsing into `ILanguage` models.
- **`GetTesseractGitHubLanguage()`**: *(Skipped in CI)* Tests downloading a random `.traineddata` file asynchronously using `TesseractGitHubFileDownloader` to the local temp directory and cleans up afterwards.