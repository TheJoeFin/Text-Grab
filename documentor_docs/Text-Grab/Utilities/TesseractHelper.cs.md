# Technical Documentation: `TesseractHelper.cs`

## Overview

The `TesseractHelper.cs` file provides utility functionality for integrating Tesseract OCR (Optical Character Recognition) into the **Text-Grab** application. It handles locating the `tesseract.exe` binary on Windows systems, executing OCR tasks on image files and bitmaps, retrieving installed language packages, downloading new trained language data from GitHub, and parsing hOCR output formats into structured data.

---

## File Summary

- **Namespace:** `Text_Grab.Utilities`
- **Dependencies:** `CliWrap`, `CliWrap.Buffered`, `System.Diagnostics`, `System.Drawing`, `System.Net.Http`, `System.Text.RegularExpressions`

### Classes Defined

1. `TesseractHelper` (Static Class): Main engine interaction logic for running Tesseract CLI commands, detecting paths, and parsing installed languages.
2. `TesseractGitHubFileDownloader` (Public Class): Downloads `.traineddata` language files from the official `tesseract-ocr/tessdata` GitHub repository.
3. `TessOcrLine` (Public Class): Data model representing an OCR-recognized line of text with bounding box coordinates.
4. `HocrReader` (Public Static Class): Helper class for parsing raw hOCR HTML strings into collections of `TessOcrLine` objects.

---

## Class Details & Specifications

### 1. `TesseractHelper`

A static utility class that manages `tesseract.exe` executable discovery, image processing, and language listing.

#### Path Resolution Constants

The class defines three fallback locations for locating the executable if not specified in application settings:

- `rawPath`: `%LOCALAPPDATA%\Tesseract-OCR\tesseract.exe`
- `rawProgramsPath`: `%LOCALAPPDATA%\Programs\Tesseract-OCR\tesseract.exe`
- `basicPath`: `C:\Program Files\Tesseract-OCR\tesseract.exe`

#### Methods

##### `CanLocateTesseractExe()`
- **Return Type:** `bool`
- **Description:** Determines if `tesseract.exe` is installed and accessible.
- **Behavior:** Calls `GetTesseractPath()`. Catches and suppresses non-debug exceptions, returning `false` if no executable is found. Re-throws exceptions in `#if DEBUG` builds.

##### `GetTesseractPath()` *(Private)*
- **Return Type:** `string`
- **Description:** Resolves the absolute path to `tesseract.exe`.
- **Search Logic:**
  1. Checks if `DefaultSettings.TesseractPath` is configured and points to an existing file.
  2. Expands and checks `rawPath`.
  3. Expands and checks `rawProgramsPath`.
  4. Checks `basicPath`.
  5. If found at step 2, 3, or 4, updates `DefaultSettings.TesseractPath`, saves the application settings, and returns the path.
  6. Returns `string.Empty` if no executable is found.

##### `GetTextFromImagePathAsync(string imagePath, string tessTag)`
- **Return Type:** `Task<string>`
- **Parameters:**
  - `imagePath` (`string`): Absolute path to the source image file.
  - `tessTag` (`string`): Tesseract language tag (e.g., `"eng"`).
- **Description:** Executes Tesseract asynchronously via `CliWrap` on a specified file path.
- **Behavior:**
  - Runs: `tesseract.exe <imagePath> - -l <tessTag>`
  - Captures stdout using UTF-8 encoding.
  - Returns `"Cannot find tesseract.exe"` if the path cannot be resolved.

##### `GetOcrOutputFromBitmap(Bitmap bmp, TessLang language)`
- **Return Type:** `Task<OcrOutput>`
- **Parameters:**
  - `bmp` (`Bitmap`): Image bitmap to process.
  - `language` (`TessLang`): Target language object.
- **Description:** Saves the provided `Bitmap` as a temporary PNG file, runs `GetTextFromImagePathAsync`, populates an `OcrOutput` object configured for `OcrEngineKind.Tesseract`, cleans the output via `CleanOutput()`, and returns it.

##### `GetTextFromImagePath(string pathToFile, bool outputHocr)`
- **Return Type:** `Task<string>`
- **Parameters:**
  - `pathToFile` (`string`): Path to image file.
  - `outputHocr` (`bool`): Flag specifying whether to request hOCR formatted output.
- **Description:** Alternative method using `System.Diagnostics.Process` to run Tesseract synchronously with a timeout.
- **Behavior:**
  - Constructs process parameters: `"<pathToFile>" - -l eng [hocr]`
  - Launches process with standard output and error redirected without opening a console window.
  - Waits up to 1000 ms for exit.
  - Returns standard output if available; falls back to standard error output if standard output is empty.

##### `TempImagePath()`
- **Return Type:** `string`
- **Description:** Generates a temporary file path for temporary image operations (`tempImage.png`).
- **Behavior:**
  - If `AutomationProfile.Current` is active, uses `AutomationProfile.GetTemporaryDirectory()`.
  - Otherwise, uses the application execution base directory or falls back to `%LOCALAPPDATA%\Text_Grab`.

##### `TesseractLanguagesAsStrings()`
- **Return Type:** `Task<List<string>>`
- **Description:** Retrieves installed Tesseract language tags by invoking `tesseract.exe --list-langs`.
- **Behavior:**
  - Uses `CliWrap` to execute the command.
  - Splits output lines and filters items: filters out strings exceeding 29 characters, empty/whitespace lines, and the internal `osd` (Orientation and Script Detection) data pack.
  - Returns `["eng"]` as a fallback if Tesseract is missing or produces empty output.

##### `TesseractLanguages()`
- **Return Type:** `Task<List<ILanguage>>`
- **Description:** Wraps the string language tags returned by `TesseractLanguagesAsStrings()` into a list of `TessLang` instances implementing `ILanguage`.

---

### 2. `TesseractGitHubFileDownloader`

A downloader class for fetching pre-trained Tesseract data files directly from the official open-source GitHub repository.

#### Fields & Initialization
- `_client`: An instance of `HttpClient` configured with the request header:
  `User-Agent: Text Grab settings language downloader`

#### Methods

##### `DownloadFileAsync(string filenameToDownload, string localDestination)`
- **Return Type:** `Task`
- **Parameters:**
  - `filenameToDownload` (`string`): Target file name (e.g., `spa.traineddata`).
  - `localDestination` (`string`): Local disk target path.
- **Behavior:**
  - Sends a `GET` request to `https://raw.githubusercontent.com/tesseract-ocr/tessdata/main/{filenameToDownload}`.
  - Ensures a success HTTP status code (`EnsureSuccessStatusCode`).
  - Reads response content bytes and writes them asynchronously to `localDestination`.
  - Catches standard exceptions and logs error output to `Console`.

#### Data Field: `tesseractTrainedDataFileNames`
- **Type:** `readonly string[]`
- **Description:** Static array listing 123 supported `.traineddata` file names available in the GitHub repository (e.g., `afr.traineddata`, `chi_sim.traineddata`, `deu.traineddata`, `eng.traineddata`, `jpn.traineddata`, etc.).

---

### 3. `TessOcrLine`

Data structure representing parsed OCR output lines, typically populated from hOCR formats.

#### Properties
| Property | Type | Description |
| :--- | :--- | :--- |
| `Height` | `int` | Bounding box height |
| `Text` | `string` | Text string detected within the bounding box (defaults to `string.Empty`) |
| `Width` | `int` | Bounding box width |
| `X` | `int` | X-coordinate location (left) |
| `Y` | `int` | Y-coordinate location (top) |

---

### 4. `HocrReader`

A static utility class that parses hOCR formatted strings (HTML format output generated by Tesseract) into structured `TessOcrLine` objects.

#### Fields
- `separator`: String array used to split raw hOCR blocks: `["<span class='ocr_line'", "</span>"]`.

#### Methods

##### `ReadLines(string hocrText)`
- **Return Type:** `List<TessOcrLine>`
- **Parameters:**
  - `hocrText` (`string`): Raw hOCR output string.
  - **Behavior:** Splits `hocrText` by span line markers, parses each segment via `ReadLine()`, and returns the collected list of lines.

##### `ReadLine(string hocrLineText)` *(Private)*
- **Return Type:** `TessOcrLine`
- **Parameters:**
  - `hocrLineText` (`string`): Single hOCR line block text.
- **Parsing Logic:**
  1. **Text Extraction:** Matches regex `<span class='ocr_line'[^>]*>(.*?)</span>` to extract group 1 as `line.Text`.
  2. **Bounding Box Extraction:** Matches regex `bbox (\d+) (\d+) (\d+) (\d+)` to parse integer dimensions into:
     - Group 1 $\rightarrow$ `X`
     - Group 2 $\rightarrow$ `Y`
     - Group 3 $\rightarrow$ `Width`
     - Group 4 $\rightarrow$ `Height`