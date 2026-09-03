# Technical Documentation: `Text-Grab/Utilities/IoUtilities.cs`

## Overview

The `IoUtilities` class is a utility class within the `Text-Grab.Utilities` namespace. It provides static helper methods and data structures for handling File I/O operations, identifying file types based on file extensions, mapping paths to specific editor modes or content kinds, reading text/OCR content from files, and listing directory contents.

---

## Namespace & Imports

**Namespace:** `Text_Grab.Utilities`

### Used Namespaces
* `System`
* `System.Collections.Generic`
* `System.IO`
* `System.Text`
* `System.Threading.Tasks`
* `Text_Grab.Interfaces`
* `Text_Grab.Models`

---

## Class Definition & Fields

```csharp
public class IoUtilities
```

### Static Fields

The class defines public read-only lists containing standard file extensions (in lowercase with leading dots) to categorize files:

| Field Name | Type | Extensions Included |
| :--- | :--- | :--- |
| `ImageExtensions` | `List<string>` | `".png"`, `".bmp"`, `".jpg"`, `".jpeg"`, `".tiff"`, `".gif"`, `".tif"`, `".webp"`, `".ico"` |
| `PdfExtensions` | `List<string>` | `".pdf"` |
| `MarkdownExtensions` | `List<string>` | `".md"`, `".markdown"` |
| `SpreadsheetExtensions` | `List<string>` | `".csv"`, `".tsv"`, `".tab"` |

---

## Method Documentation

### 1. File Extension & Type Inspection Methods

These methods evaluate extensions or paths to determine their document or media classification.

#### `IsImageFileExtension(string extension)`
* **Description:** Determines if a given file extension corresponds to a supported image format.
* **Parameters:** `extension` (`string`) – The file extension string to evaluate.
* **Returns:** `bool` – `true` if `ImageExtensions` contains the lower-case invariant version of `extension`; otherwise `false`. Returns `false` if `extension` is `null` or whitespace.

#### `IsImageFile(string path)`
* **Description:** Checks if a path exists on the file system and has an image file extension.
* **Parameters:** `path` (`string`) – The target file path.
* **Returns:** `bool` – `true` if `path` is not `null`/whitespace, the file exists (`File.Exists`), and `IsImageFileExtension` returns `true`.

#### `IsPdfFileExtension(string extension)`
* **Description:** Determines if a given file extension corresponds to a PDF document.
* **Parameters:** `extension` (`string`) – The file extension string to evaluate.
* **Returns:** `bool` – `true` if `PdfExtensions` contains the lower-case invariant extension; otherwise `false`. Returns `false` if `extension` is `null` or whitespace.

#### `IsPdfFile(string path)`
* **Description:** Checks if a path exists on the file system and has a PDF extension.
* **Parameters:** `path` (`string`) – The target file path.
* **Returns:** `bool` – `true` if `path` is valid, exists, and `IsPdfFileExtension` returns `true`.

#### `IsVisualDocumentFileExtension(string extension)`
* **Description:** Checks whether an extension belongs to either an image file or a PDF file.
* **Parameters:** `extension` (`string`) – The file extension string.
* **Returns:** `bool` – Result of `IsImageFileExtension(extension) || IsPdfFileExtension(extension)`.

#### `IsVisualDocumentFileExtension(string path)`
* **Description:** Checks if a file path exists and is a visual document (Image or PDF).
* **Parameters:** `path` (`string`) – The target file path.
* **Returns:** `bool` – `true` if `path` is valid, exists, and `IsVisualDocumentFileExtension` returns `true`.

#### `IsMarkdownFileExtension(string extension)`
* **Description:** Determines if a given extension corresponds to a Markdown file.
* **Parameters:** `extension` (`string`) – The extension string.
* **Returns:** `bool` – `true` if contained in `MarkdownExtensions`; otherwise `false`. Returns `false` if `null` or whitespace.

#### `IsSpreadsheetFileExtension(string extension)`
* **Description:** Determines if a given extension corresponds to a spreadsheet format (`.csv`, `.tsv`, `.tab`).
* **Parameters:** `extension` (`string`) – The extension string.
* **Returns:** `bool` – `true` if contained in `SpreadsheetExtensions`; otherwise `false`. Returns `false` if `null` or whitespace.

---

### 2. Path Mapping Methods

#### `GetEditorModeForPath(string? path)`
Maps a file path extension to an `EtwEditorMode` enum value.

* **Parameters:** `path` (`string?`) – The file path.
* **Returns:** `EtwEditorMode`
  * Returns `EtwEditorMode.Spreadsheet` if the extension matches `IsSpreadsheetFileExtension`.
  * Returns `EtwEditorMode.Markdown` if the extension matches `IsMarkdownFileExtension`.
  * Default return: `EtwEditorMode.Text`.

#### `GetOpenContentKindForPath(string? path)`
Maps a file path extension to an `OpenContentKind` enum value.

* **Parameters:** `path` (`string?`) – The file path.
* **Returns:** `OpenContentKind`
  * Returns `OpenContentKind.PdfDocument` if `IsPdfFileExtension` is `true`.
  * Returns `OpenContentKind.Image` if `IsImageFileExtension` is `true`.
  * Default return: `OpenContentKind.TextFile`.

---

### 3. File Content Extraction Methods

#### `GetContentFromPath(string pathOfFileToOpen, bool isMultipleFiles = false, ILanguage? language = null)`
Asynchronously reads or extracts text content from a specified file path. Depending on whether the file is an image/PDF or text file, it uses OCR or text reading.

* **Parameters:**
  * `pathOfFileToOpen` (`string`) – Path to the file.
  * `isMultipleFiles` (`bool`, default = `false`) – Flag indicating if this operation is part of processing multiple files.
  * `language` (`ILanguage?`, default = `null`) – Optional language configuration passed to the OCR utility.
* **Returns:** `Task<(string TextContent, OpenContentKind SourceKindOfContent)>` – A tuple containing the processed text string and the detected content kind.
* **Execution Logic:**
  1. Determines content kind via `GetOpenContentKindForPath`.
  2. If `isMultipleFiles` is `true`, appends the file path to the internal `StringBuilder`.
  3. **Visual Documents (`OpenContentKind.Image` or `OpenContentKind.PdfDocument`):**
     * Calls `OcrUtilities.OcrAbsoluteFilePathAsync(pathOfFileToOpen, language)` and appends the result.
     * Catch block: Handles exceptions by showing a `Wpf.Ui.Controls.MessageBox` with title `"Error"` and content `"Failed to read {pathOfFileToOpen}"`.
  4. **Text Files:**
     * Sets content kind to `OpenContentKind.TextFile`.
     * Calls `TryToOpenTextFile(pathOfFileToOpen, isMultipleFiles, stringBuilder)`.
  5. If `isMultipleFiles` is `true`, appends two `Environment.NewLine` sequences.
  6. Returns the accumulated string content and the determined content kind.

#### `TryToOpenTextFile(string pathOfFileToOpen, bool isMultipleFiles, StringBuilder stringBuilder)`
Asynchronously reads a plain text file from disk and appends its content to a `StringBuilder`.

* **Parameters:**
  * `pathOfFileToOpen` (`string`) – File path to read.
  * `isMultipleFiles` (`bool`) – Context flag (passed to method signature).
  * `stringBuilder` (`StringBuilder`) – Target string builder to append file contents to.
* **Returns:** `Task`
* **Error Handling:** Catches `System.Exception` and displays an error dialog via `System.Windows.Forms.MessageBox.Show($"Failed to open file. {ex.Message}")`.

---

### 4. Directory Formatting Helper

#### `ListFilesFoldersInDirectory(string chosenFolderPath)`
Enumerates subdirectories and files within a given directory path and returns a formatted list of relative names.

* **Parameters:** `chosenFolderPath` (`string`) – Directory path to iterate.
* **Returns:** `string` – A formatted list containing:
  1. The base folder path followed by two newlines.
  2. Subfolder relative names (derived via `AsSpan` slice stripping base folder prefix), each followed by a newline.
  3. File relative names (derived via `AsSpan` slice stripping base folder prefix), each followed by a newline.

---

## Dependencies & External Calls

* **`OcrUtilities` (`Text_Grab.Utilities`):** Called asynchronously via `OcrUtilities.OcrAbsoluteFilePathAsync` to extract text from images and PDF files.
* **`Wpf.Ui.Controls.MessageBox`:** WPF UI control used for showing error dialogs during OCR processing failures.
* **`System.Windows.Forms.MessageBox`:** Used for displaying error dialogs when plain text reading fails.
* **`EtwEditorMode` & `OpenContentKind` (`Text_Grab.Models`):** Enumerations returned by path mapping routines.
* **`ILanguage` (`Text_Grab.Interfaces`):** Interface for language parameter configuration during OCR operations.