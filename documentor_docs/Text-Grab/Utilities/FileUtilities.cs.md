# Technical Documentation: `Text_Grab.Utilities.FileUtilities`

The `FileUtilities` class in the `Text_Grab.Utilities` namespace provides a unified interface for file input/output (I/O) operations, path construction, and file dialog filter string generation. It abstracts differences between packaged (Windows App SDK / MSIX) and unpackaged execution environments, as well as test automation environments (`AutomationProfile`).

---

## 1. Environment Routing Logic

`FileUtilities` dynamically routes read/write operations depending on the environment in which the application is executing. It evaluates conditions in the following precedence order:

1. **Automation Environment (`AutomationProfile.Current is not null`)**: Uses unpackaged file path resolution and redirects directories to test profile locations (`profile.DataDirectory` or `profile.HistoryDirectory`).
2. **Packaged Windows Application (`AppUtilities.IsPackaged()`)**: Employs Windows Storage APIs (`Windows.Storage.StorageFolder`, `Windows.Storage.StorageFile`, `Windows.Storage.Streams`) to interface with sandboxed storage locations (e.g., `ApplicationData.Current.LocalFolder`).
3. **Unpackaged Application (Fallback)**: Uses standard `System.IO` file system access (`File`, `Directory`, `Path`) relative to the application binary or absolute system paths.

---

## 2. Public API Reference

### Image I/O Operations

#### `GetImageFileAsync(string fileName, FileStorageKind storageKind)`
* **Return Type:** `Task<Bitmap?>`
* **Description:** Asynchronously loads an image file into a `System.Drawing.Bitmap` object.
* **Behavior:** Routes execution to `GetImageFilePackaged` or `GetImageFileUnpackaged` depending on the current runtime environment. Returns `null` if the file does not exist or fails to load.

#### `SaveImageFile(Bitmap image, string filename, FileStorageKind storageKind)`
* **Return Type:** `Task<bool>`
* **Description:** Asynchronously saves a `Bitmap` image to disk at the destination dictated by `storageKind`.
* **Behavior:** 
  * In packaged mode, saves the bitmap using `ImageFormat.Bmp` to a `StorageFile`.
  * In unpackaged mode, creates missing directories if needed, overwrites existing files, and calls `Bitmap.Save(filePath)`.
  * Returns `true` if successful, or `false` if an exception occurs.

---

### Text I/O Operations

#### `GetTextFileAsync(string fileName, FileStorageKind storageKind)`
* **Return Type:** `Task<string>`
* **Description:** Asynchronously reads and returns the full text content of a file.
* **Behavior:** 
  * If the file is missing or fails to read, returns `string.Empty`.
  * Delegates to `GetTextFilePackaged` (using `StreamReader`) or `GetTextFileUnpackaged` (using `File.ReadAllTextAsync`).

#### `SaveTextFile(string textContent, string filename, FileStorageKind storageKind)`
* **Return Type:** `Task<bool>`
* **Description:** Asynchronously writes a text string to a target file.
* **Behavior:** 
  * Packaged mode writes content encoded via `Windows.Storage.Streams.DataWriter`.
  * Unpackaged mode creates/overwrites the file and writes UTF-8 text with a Byte Order Mark (BOM) via a custom `UTF8Encoding(true)` bytes buffer.
  * Returns `true` on success, `false` on failure.

---

### File Dialog Filter Generators

These methods build filter strings for `OpenFileDialog` or `SaveFileDialog` UI components.

#### `GetImageFilter()`
* **Return Type:** `string`
* **Description:** Generates a file dialog filter for system-supported image extensions.
* **Output Format:** `Image files|*.bmp;*.jpg;*.png;...`

#### `GetVisualDocumentFilter()`
* **Return Type:** `string`
* **Description:** Generates a filter string combining supported image types and PDF extensions (`IoUtilities.PdfExtensions`).
* **Output Format:** `Image and PDF files|<combined>|PDF files|<pdf_exts>|<image_filter>`

#### `GetOpenDocumentFilter()`
* **Return Type:** `string`
* **Description:** Builds a comprehensive file filter for open document dialogs including supported documents, visual documents, GrabFrame files (`GrabFrameFileUtilities`), spreadsheet formats, markdown files, plain text files, and all files (`*.*`).

---

### Path & Directory Utilities

#### `GetPathToLocalFile(string imageRelativePath)`
* **Return Type:** `string`
* **Description:** Combines the current executable directory path with a specified relative file path.
* **Exceptions:** Throws `NullReferenceException` if the directory of the executable path cannot be resolved.

#### `GetPathToHistory()`
* **Return Type:** `Task<string>`
* **Description:** Asynchronously retrieves the fully qualified path to the application's history directory based on the execution profile (Automation, Packaged, or Unpackaged).

#### `TryDeleteHistoryDirectory()`
* **Return Type:** `async void`
* **Description:** Attempts to asynchronously delete the entire history directory and its contents. Exceptions are caught and ignored.

#### `GetExePath()`
* **Return Type:** `string`
* **Description:** Retrieves `Environment.ProcessPath`. Returns an empty string if `Environment.ProcessPath` is null or empty.

---

## 3. Internal Helper Methods

| Method Signature | Return Type | Description |
| :--- | :--- | :--- |
| `GetImageExtensionsFilterPattern()` | `string` | Queries system encoders (`ImageCodecInfo.GetImageEncoders()`) to assemble semicolon-separated image extensions (e.g., `*.jpg;*.png`). |
| `GetExtensionsFilterPattern(IEnumerable<string> extensions)` | `string` | Prepends `*` to each file extension in the input collection and joins them with `;`. |
| `GetVisualDocumentFilterPattern()` | `string` | Combines `GetImageExtensionsFilterPattern()` and PDF extension patterns. |
| `GetImageFilePackaged(string fileName, FileStorageKind storageKind)` | `Task<Bitmap?>` | Retrieves image from packaged application storage (`StorageFolder`). Returns `null` on error. |
| `GetImageFileUnpackaged(string fileName, FileStorageKind storageKind)` | `Task<Bitmap?>` | Loads image from standard file system using `System.IO.Path` and `System.Drawing.Bitmap`. Returns `null` if file missing. |
| `GetTextFilePackaged(string fileName, FileStorageKind storageKind)` | `Task<string>` | Opens `StorageFile` stream and reads string contents. Returns `string.Empty` on error. |
| `GetTextFileUnpackaged(string fileName, FileStorageKind storageKind)` | `Task<string>` | Reads text content using `File.ReadAllTextAsync`. Returns `string.Empty` if file missing. |
| `AddText(FileStream fs, string value)` | `void` | Encodes text with UTF-8 (with BOM emitted) and writes byte array directly to a `FileStream`. |
| `GetFolderPathUnpackaged(string filename, FileStorageKind storageKind)` | `string` | Resolves target directory paths in non-packaged/automation contexts according to `FileStorageKind` (`Absolute`, `WithExe`, `WithHistory`). Fallback default is `"c:\\Text-Grab"`. |
| `GetStorageFolderPackaged(string fileName, FileStorageKind storageKind)` | `Task<StorageFolder>` | Resolves target `StorageFolder` in packaged contexts. Handles `Absolute`, `WithExe` (maps to `LocalFolder`), and `WithHistory` (creates/opens `"history"` folder in `LocalFolder`). Default fallback is `LocalCacheFolder`. |
| `SaveImageFileUnpackaged(Bitmap image, string filename, FileStorageKind storageKind)` | `Task<bool>` | Writes `Bitmap` directly to standard disk path. |
| `SaveImagePackaged(Bitmap image, string filename, FileStorageKind storageKind)` | `Task<bool>` | Creates a `StorageFile` in packaged storage and saves image in `ImageFormat.Bmp`. |
| `SaveTextFilePackaged(string textContent, string filename, FileStorageKind storageKind)` | `Task<bool>` | Encodes and writes text string to a `StorageFile` via `DataWriter` and `IRandomAccessStream`. |
| `SaveTextFileUnpackaged(string textContent, string filename, FileStorageKind storageKind)` | `Task<bool>` | Creates a `FileStream` and invokes `AddText` to write text content to standard storage path. |

---

## 4. Dependencies & Interfacing Enums

* **`FileStorageKind`**: Enumeration passed into I/O methods to control directory target behavior:
  * `FileStorageKind.Absolute`: Treat `fileName` as an absolute path or relative to the parent directory path.
  * `FileStorageKind.WithExe`: Store relative to the root executable directory or application root data store.
  * `FileStorageKind.WithHistory`: Store relative to the designated history storage folder.
* **`AutomationProfile`**: Referenced via `AutomationProfile.Current` to divert I/O operations during automated testing scenarios.
* **`AppUtilities.IsPackaged()`**: Determines if the app is currently running under a packaged context (e.g. MSIX application container).
* **`IoUtilities`**: Provides extension arrays (`IoUtilities.PdfExtensions`, `IoUtilities.SpreadsheetExtensions`, `IoUtilities.MarkdownExtensions`).
* **`GrabFrameFileUtilities`**: Supplies GrabFrame file extension definitions (`GrabFrameFileExtension`) and file filter properties (`GetGrabFrameFileFilter()`).