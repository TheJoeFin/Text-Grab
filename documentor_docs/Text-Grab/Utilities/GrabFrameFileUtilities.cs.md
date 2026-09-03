# Technical Documentation: `GrabFrameFileUtilities.cs`

## Overview

The `GrabFrameFileUtilities` class in the `Text_Grab.Utilities` namespace provides asynchronous reading, writing, and validation functionality for **Text Grab Frame files** (file extension `.tggf`). 

A `.tggf` file is a ZIP archive that bundles together a Grab Frame session's metadata, word borders, and source image. This enables users to save complete Grab Frame sessions to disk and reload them at a later time, seamlessly integrating into the existing `HistoryInfo` framework.

---

## Architecture & Archive Structure

A `.tggf` file is structured as a standard ZIP container containing up to three specific entries:

| Entry Name | Type | Description |
| :--- | :--- | :--- |
| `metadata.json` | JSON Text | Serialized `HistoryInfo` object containing frame settings (language, table state, position, source mode, OCR text). |
| `wordborders.json` | JSON Text | Serialized JSON string representing word border information (`WordBorderInfo`). Optional. |
| `image.png` | PNG Image | The source image bitmap associated with the frame session. Optional. |

---

## Security & Validation Constants

To prevent resource exhaustion, Zip Bomb attacks, and memory overflow during archive processing, `GrabFrameFileUtilities` enforces strict boundary limits on archive structure, file size, compression ratios, and image dimensions:

| Constant | Value | Purpose |
| :--- | :--- | :--- |
| `GrabFrameFileExtension` | `".tggf"` | File extension string for Grab Frame files. |
| `MaxArchiveBytes` | `128 MB` ($128 \times 1024 \times 1024$) | Maximum allowed total size for the input `.tggf` archive stream. |
| `MaxMetadataBytes` | `4 MB` ($4 \times 1024 \times 1024$) | Maximum allowed uncompressed size for `metadata.json`. |
| `MaxWordBordersBytes` | `32 MB` ($32 \times 1024 \times 1024$) | Maximum allowed uncompressed size for `wordborders.json`. |
| `MaxImageBytes` | `64 MB` ($64 \times 1024 \times 1024$) | Maximum allowed uncompressed size for `image.png`. |
| `MaxExpandedBytes` | `96 MB` ($96 \times 1024 \times 1024$) | Cumulative maximum allowed expanded size across all archive entries during extraction. |
| `MaxArchiveEntries` | `16` | Maximum total allowed archive entries in a `.tggf` package. |
| `MaxCompressionRatio` | `1,000` | Maximum allowed ratio of uncompressed size to compressed size ($\text{Length} / \text{CompressedLength}$). |
| `MaxImageDimension` | `16,384` | Maximum allowable width or height in pixels for an embedded image. |
| `MaxImagePixelCount` | `40,000,000` | Maximum total allowable pixels ($\text{width} \times \text{height}$) for an embedded image. |

---

## Public Methods

### `IsGrabFrameFileExtension`

```csharp
public static bool IsGrabFrameFileExtension(string? extension)
```

- **Description:** Checks if a given string matches the Grab Frame file extension (`.tggf`) using a case-insensitive comparison.
- **Parameters:**
  - `extension` (`string?`): The extension string to test.
- **Returns:** `true` if `extension` equals `".tggf"` (case-insensitive); otherwise, `false`.

---

### `IsGrabFrameFile`

```csharp
public static bool IsGrabFrameFile(string? path)
```

- **Description:** Determines whether a given file path has the `.tggf` extension.
- **Parameters:**
  - `path` (`string?`): The full or relative file path to test.
- **Returns:** `true` if the file extension of `path` matches `.tggf`; otherwise, `false`.

---

### `GetGrabFrameFileFilter`

```csharp
public static string GetGrabFrameFileFilter()
```

- **Description:** Generates a formatted file dialog filter string suitable for open/save dialogs.
- **Returns:** `"Text Grab Frame (*.tggf)|*.tggf"`

---

### `SaveGrabFrameFileAsync`

```csharp
public static async Task<bool> SaveGrabFrameFileAsync(HistoryInfo info, string destinationPath)
```

- **Description:** Serializes and packs a `HistoryInfo` object, its associated word border JSON, and its bitmap image into a `.tggf` archive file asynchronously.
- **Parameters:**
  - `info` (`HistoryInfo`): The session data to be saved.
  - `destinationPath` (`string`): Target file path on disk.
- **Returns:** `Task<bool>` — `true` if saving succeeded; `false` if serialization or file writing failed.
- **Behavior & Workflow:**
  1. Validates that `destinationPath` is not null or whitespace.
  2. Extracts `WordBorderInfoJson` and `ImageContent` from `info`.
  3. Creates a shallow copy of `info` (`info.ShallowCopy()`) and clears pointer fields (`WordBorderInfoJson = null`, `WordBorderInfoFileName = null`, `ImagePath = "image.png"`) to prevent duplicate data in `metadata.json`.
  4. Serializes the copied metadata to JSON using `MetadataJsonOptions` (indented, enum converters enabled, trailing commas allowed).
  5. Generates temporary (`.tmp`) and backup (`.bak`) file paths using GUIDs.
  6. Creates a new ZIP archive at the temporary path using `CompressionLevel.Optimal`.
  7. Writes `metadata.json`, `wordborders.json` (if content exists), and `image.png` (if bitmap exists) into the archive.
  8. Replaces the file at `destinationPath` atomically using `ReplaceFileAtomically`.
  9. Cleanly deletes temporary files in a `finally` block.

---

### `LoadGrabFrameFileAsync`

```csharp
public static async Task<HistoryInfo?> LoadGrabFrameFileAsync(string sourcePath)
```

- **Description:** Reads and validates a `.tggf` archive file, reconstructing a `HistoryInfo` object populated with image data and word borders.
- **Parameters:**
  - `sourcePath` (`string`): File path of the `.tggf` archive.
- **Returns:** `Task<HistoryInfo?>` — The populated `HistoryInfo` object, or `null` if the file is missing, exceeds size limits, or is invalid.
- **Behavior & Workflow:**
  1. Verifies that `sourcePath` exists and is not empty.
  2. Ensures stream length does not exceed `MaxArchiveBytes` (128 MB).
  3. Opens the ZIP archive and verifies entry count does not exceed `MaxArchiveEntries` (16).
  4. Tracks remaining budget against `MaxExpandedBytes` (96 MB cumulative).
  5. Extracts and deserializes `metadata.json` into a `HistoryInfo` object.
  6. Reads `wordborders.json` (if present) into `info.WordBorderInfoJson` and clears `WordBorderInfoFileName`.
  7. Decodes `image.png` (if present) via `ReadImageEntry`, enforcing image dimensions and pixel limits, placing the resulting `Bitmap` into `info.ImageContent`.
  8. Generates a new `Guid` string for `info.ID` if missing or whitespace.

---

## Internal & Private Helper Methods

### Serialization & I/O Helpers

#### `WriteTextEntry`
```csharp
private static void WriteTextEntry(ZipArchive archive, string entryName, string content)
```
Creates a ZIP archive entry with optimal compression and writes text using UTF-8 encoding (without byte order mark).

#### `WriteImageEntry`
```csharp
private static void WriteImageEntry(ZipArchive archive, string entryName, Bitmap image)
```
Encodes a GDI+ `Bitmap` into a PNG stream via an intermediate `MemoryStream` (required because GDI+ requires seekable streams) and writes it to the ZIP archive.

#### `ReadImageEntry`
```csharp
private static Bitmap ReadImageEntry(ZipArchiveEntry entry, ref long remainingExpandedBytes)
```
Reads raw bytes from the entry, decodes them into an `Image` object, validates dimensions using `AreImageDimensionsAllowed`, and returns a new `Bitmap`.

#### `ReadEntryText`
```csharp
private static string ReadEntryText(ZipArchiveEntry entry, long maxEntryBytes, ref long remainingExpandedBytes)
```
Reads entry bytes and converts them to a UTF-8 string.

#### `ReadEntryBytes`
```csharp
private static byte[] ReadEntryBytes(ZipArchiveEntry entry, long maxEntryBytes, ref long remainingExpandedBytes)
```
Streams entry content into a byte array in 80 KB chunks. Enforces entry-specific maximum sizes, cumulative limits (`remainingExpandedBytes`), and validates that total bytes read matches `entry.Length`.

---

### Validation Helpers

#### `ValidateEntrySize`
```csharp
private static void ValidateEntrySize(ZipArchiveEntry entry, long maxEntryBytes, long remainingExpandedBytes)
```
Throws an `InvalidDataException` if:
- Entry length is negative or exceeds `maxEntryBytes`.
- Entry length exceeds `remainingExpandedBytes`.
- Compression ratio ($\text{Length} / \text{CompressedLength}$) exceeds `MaxCompressionRatio` (1,000).

#### `AreImageDimensionsAllowed`
```csharp
internal static bool AreImageDimensionsAllowed(int width, int height)
```
Checks if image dimensions are positive non-zero, $\le 16,384$ pixels per dimension, and total pixel area ($\text{width} \times \text{height}$) $\le 40,000,000$ pixels.

---

### File System Operations

#### `ReplaceFileAtomically`
```csharp
private static void ReplaceFileAtomically(string tempPath, string destinationPath, string backupPath)
```
Safely overwrites target files. Uses `File.Replace` when the destination file exists to perform an atomic file swap using a backup file. Uses `File.Move` if the target destination file does not exist.

#### `TryDeleteFile`
```csharp
private static void TryDeleteFile(string path)
```
Deletes a specified file while handling and logging `IOException` and `UnauthorizedAccessException` exceptions to prevent crashing during cleanup steps.