# Technical Documentation: `Tests/FilesIoTests.cs`

## Overview

The `FilesIoTests` class contains unit and integration tests for the file I/O operations, file extension classifications, file filter definitions, and drag-and-drop processing functionality within the **Text-Grab** application.

It validates utilities such as `FileUtilities`, `IoUtilities`, and drag-and-drop helper methods in `App`, ensuring file reading, writing, path resolution, and edge cases (such as race conditions during image cleanup) function correctly.

---

## File Details

* **File Path:** `Tests/FilesIoTests.cs`
* **Namespace:** `Tests`
* **Target Scope:** Class `FilesIoTests`

---

## Dependencies & Imports

The file imports the following namespaces:
* `System.Drawing` – Used for `Bitmap` instantiation and manipulation.
* `System.IO` – Used for path combinations and temporary file management (`Path`, `File`).
* `System.Windows` – WPF UI interactions and drag-and-drop types (`DataObject`, `DataFormats`, `DragDropEffects`).
* `Text_Grab` – Contains application-level static helpers (e.g., `App`).
* `Text_Grab.Models` – Contains models such as `HistoryInfo`, `FileStorageKind`, `EtwEditorMode`, and `OpenContentKind`.
* `Text_Grab.Utilities` – Contains I/O helpers like `FileUtilities` and `IoUtilities`.

---

## Constants & Fields

| Member | Type | Value | Description |
| :--- | :--- | :--- | :--- |
| `fontSamplePath` | `private const string` | `@"Images\font_sample.png"` | Relative path to a sample image used for testing image file operations. |

---

## Test Methods

### Image File Operations & Race Conditions

#### 1. `CanSaveImagesWithHistory()`
* **Attribute:** `[WpfFact]`
* **Return Type:** `async Task`
* **Description:** Verifies that a `Bitmap` image loaded from a local path can be successfully saved to disk using `FileUtilities.SaveImageFile` with `FileStorageKind.WithHistory`.
* **Assertion:** Asserts that `SaveImageFile` returns `true`.

#### 2. `SaveImageFile_SucceedsAfterClearTransientImage()`
* **Attribute:** `[WpfFact]`
* **Return Type:** `async Task`
* **Description:** Reproduces a race condition scenario where `HistoryInfo.ClearTransientImage()` nulls out the transient bitmap reference immediately after initiating an asynchronous save operation (`FileUtilities.SaveImageFile`). Mirrors the fire-and-forget pattern used in `HistoryService.SaveToHistory`.
* **Assertion:** Asserts that the save task still completes successfully (`true`) despite `ClearTransientImage()` being executed concurrently before the task finishes.

---

### Text File Operations & Error Handling

#### 3. `CanSaveTextFilesWithExe()`
* **Attribute:** `[WpfFact]`
* **Return Type:** `async Task`
* **Description:** Asserts that text content can be saved to a file relative to the executable directory using `FileUtilities.SaveTextFile` with `FileStorageKind.WithExe`.

#### 4. `CanStoreThenReadTextFilesWithExe(FileStorageKind storageKind)`
* **Attribute:** `[WpfTheory]`
* **Inline Data:** `FileStorageKind.WithExe`, `FileStorageKind.WithHistory`
* **Return Type:** `async Task`
* **Description:** Tests the round-trip operation of saving a text string and subsequently retrieving it using `FileUtilities.GetTextFileAsync`.
* **Assertion:** Asserts that the string read back matches the string originally saved.

#### 5. `ReadNotExistingTextFileEmpty(FileStorageKind storageKind)`
* **Attribute:** `[WpfTheory]`
* **Inline Data:** `FileStorageKind.WithExe`, `FileStorageKind.WithHistory`, `FileStorageKind.Absolute`
* **Return Type:** `async Task`
* **Description:** Verifies behavior when attempting to read a non-existent text file (`"FileNotFound.json"`).
* **Assertion:** Asserts that `FileUtilities.GetTextFileAsync` returns an empty string (`Assert.Empty`).

#### 6. `ReadNotExistingImageFileEmpty(FileStorageKind storageKind)`
* **Attribute:** `[WpfTheory]`
* **Inline Data:** `FileStorageKind.WithExe`, `FileStorageKind.WithHistory`, `FileStorageKind.Absolute`
* **Return Type:** `async Task`
* **Description:** Verifies behavior when attempting to read a non-existent image file (`"FileNotFound.json"`).
* **Assertion:** Asserts that `FileUtilities.GetImageFileAsync` returns `null`.

---

### Path Classification & Editor Mode Utilities

#### 7. `GetEditorModeForPath_UsesFileExtension(string path, EtwEditorMode expectedMode)`
* **Attribute:** `[Theory]`
* **Description:** Ensures `IoUtilities.GetEditorModeForPath` correctly identifies the target `EtwEditorMode` based on the file extension.
* **Test Case Data:**
  | File Path | Expected Mode |
  | :--- | :--- |
  | `C:\Temp\sheet.csv` | `EtwEditorMode.Spreadsheet` |
  | `C:\Temp\sheet.TSV` | `EtwEditorMode.Spreadsheet` |
  | `C:\Temp\sheet.tab` | `EtwEditorMode.Spreadsheet` |
  | `C:\Temp\notes.md` | `EtwEditorMode.Markdown` |
  | `C:\Temp\notes.markdown` | `EtwEditorMode.Markdown` |
  | `C:\Temp\notes.txt` | `EtwEditorMode.Text` |
  | `C:\Temp\data.json` | `EtwEditorMode.Text` |

#### 8. `GetOpenContentKindForPath_ClassifiesVisualDocumentsAndText(string path, OpenContentKind expectedKind)`
* **Attribute:** `[Theory]`
* **Description:** Validates that `IoUtilities.GetOpenContentKindForPath` correctly classifies file paths into content categories (`OpenContentKind`).
* **Test Case Data:**
  | File Path | Expected `OpenContentKind` |
  | :--- | :--- |
  | `C:\Temp\scan.png` | `OpenContentKind.Image` |
  | `C:\Temp\scan.PDF` | `OpenContentKind.PdfDocument` |
  | `C:\Temp\notes.txt` | `OpenContentKind.TextFile` |

#### 9. `IsVisualDocumentFileExtension_RecognizesImagesAndPdf(string extension, bool expected)`
* **Attribute:** `[Theory]`
* **Description:** Asserts whether `IoUtilities.IsVisualDocumentFileExtension` correctly flags extensions as visual documents (images or PDFs).
* **Test Case Data:**
  | Extension | Expected Result |
  | :--- | :--- |
  | `.png` | `true` |
  | `.PDF` | `true` |
  | `.txt` | `false` |
  | `""` | `false` |

---

### File Dialog Filters

#### 10. `GetVisualDocumentFilter_IncludesPdfSupport()`
* **Attribute:** `[Fact]`
* **Description:** Tests the string output from `FileUtilities.GetVisualDocumentFilter()`.
* **Assertions:**
  * Contains `"Image and PDF files|"`
  * Contains `"PDF files|*.pdf"`
  * Contains `"Image files|"`

#### 11. `GetOpenDocumentFilter_IncludesVisualAndTextOptions()`
* **Attribute:** `[Fact]`
* **Description:** Tests the filter string returned by `FileUtilities.GetOpenDocumentFilter()`.
* **Assertions:**
  * Contains `"Supported documents|"`
  * Contains `"Image and PDF files|"`
  * Contains `"Spreadsheet documents|*.csv;*.tsv;*.tab"`
  * Contains `"Markdown documents|*.md;*.markdown"`
  * Contains `"Text documents (*.txt)|*.txt"`
  * Contains `"All files (*.*)|*.*"`

---

### Drag-and-Drop Processing (`App` Helpers)

#### 12. `GetDroppedFilePaths_ReturnsExistingFilesOnly()`
* **Attribute:** `[WpfFact]`
* **Description:** Evaluates `App.GetDroppedFilePaths` using a WPF `DataObject` configured with `DataFormats.FileDrop`. The payload contains three path strings: two existing temp files and one non-existent generated GUID path.
* **Cleanup:** Deletes created temp files in a `finally` block.
* **Assertion:** Asserts that only the two existing file paths are returned in the resulting list.

#### 13. `GetDroppedFileEffect_ReturnsCopyWhenExistingFilesAreDropped()`
* **Attribute:** `[WpfFact]`
* **Description:** Tests `App.GetDroppedFileEffect` when a valid file drop payload containing an existing temporary file is passed.
* **Cleanup:** Deletes the created temporary file in a `finally` block.
* **Assertion:** Asserts that the method returns `DragDropEffects.Copy`.

#### 14. `GetDroppedFileEffect_ReturnsNoneWhenNoFilesCanBeOpened()`
* **Attribute:** `[WpfFact]`
* **Description:** Tests `App.GetDroppedFileEffect` when the `DataObject` contains plain text data (`DataFormats.Text`) instead of dropped files.
* **Assertion:** Asserts that the method returns `DragDropEffects.None`.

---

## Referenced Application Classes & Methods

This test file acts as test coverage for the following application components:

* **`FileUtilities`**:
  * `GetPathToLocalFile(string)`
  * `SaveImageFile(Bitmap, string, FileStorageKind)`
  * `SaveTextFile(string, string, FileStorageKind)`
  * `GetTextFileAsync(string, FileStorageKind)`
  * `GetImageFileAsync(string, FileStorageKind)`
  * `GetVisualDocumentFilter()`
  * `GetOpenDocumentFilter()`
* **`IoUtilities`**:
  * `GetEditorModeForPath(string)`
  * `GetOpenContentKindForPath(string)`
  * `IsVisualDocumentFileExtension(string)`
* **`App`**:
  * `GetDroppedFilePaths(IDataObject)`
  * `GetDroppedFileEffect(IDataObject)`
* **`HistoryInfo`**:
  * `ClearTransientImage()`