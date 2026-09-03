# Technical Documentation: `Tests/GrabFrameFileTests.cs`

## Overview

The `GrabFrameFileTests` class is a unit test suite within the `Tests` namespace. It validates the functionality, safety, and edge-case behavior of `GrabFrameFileUtilities`, which handles reading, writing, and validating custom Text Grab Frame (`.tggf`) files.

These tests cover round-trip serialization/deserialization, object immutability during save operations, atomic file replacement, error handling for missing or malformed files, image dimension limits, and file extension checks.

---

## Test Suite Summary

* **Target Namespace/Class Under Test:** `Text_Grab.Utilities.GrabFrameFileUtilities`
* **Data Models Tested:** `HistoryInfo`, `WordBorderInfo`, `TextGrabMode`, `LanguageKind`
* **Testing Framework:** xUnit (`[Fact]`, `[Theory]`, `[InlineData]`, `Assert`)

---

## Test Methods

### 1. `SaveAndLoad_RoundTripsMetadataWordBordersAndImage`
* **Type:** `[Fact]`
* **Purpose:** Verifies that a `HistoryInfo` object containing metadata, word border definitions, and an image can be saved to a `.tggf` file and loaded back without data loss or corruption.
* **Execution Flow:**
  1. Generates a temporary path with a `.tggf` extension.
  2. Constructs a `List<WordBorderInfo>` containing sample word bounding boxes.
  3. Prepares a `HistoryInfo` instance populated with ID, text, mode, table flag, language properties, bounding rectangle (`PositionRect`), serialized word borders JSON, and a 64x48 `Bitmap`.
  4. Calls `GrabFrameFileUtilities.SaveGrabFrameFileAsync` to save the data.
  5. Asserts that saving succeeds and the output file exists.
  6. Loads the file back using `GrabFrameFileUtilities.LoadGrabFrameFileAsync`.
  7. Asserts that all properties match the original input (ID, text, mode, table flag, language settings, position rect, image width/height, and deserialized word borders).
* **Cleanup:** Disposes image resources and deletes the temporary `.tggf` file in a `finally` block.

---

### 2. `SaveGrabFrameFileAsync_DoesNotMutateSuppliedInfo`
* **Type:** `[Fact]`
* **Purpose:** Ensures that calling `SaveGrabFrameFileAsync` leaves the original `HistoryInfo` object passed in by the caller unmodified.
* **Execution Flow:**
  1. Creates a `HistoryInfo` instance initialized with specific values for `WordBorderInfoJson`, `WordBorderInfoFileName`, `ImagePath`, and `ImageContent`.
  2. Saves the file using `SaveGrabFrameFileAsync`.
  3. Verifies that the original object's references and property values remain identical post-save (`WordBorderInfoJson`, `WordBorderInfoFileName`, `ImagePath`, `ImageContent`).
* **Cleanup:** Disposes image resources and deletes the temporary file in a `finally` block.

---

### 3. `SaveGrabFrameFileAsync_PreservesExistingFile_WhenAtomicReplaceFails`
* **Type:** `[Fact]`
* **Purpose:** Verifies file safety during save failures. Specifically, if atomic replacement fails (e.g., target file is locked), the existing file content must remain unchanged.
* **Execution Flow:**
  1. Writes initial seed content (`"existing grab frame content"`) to a temporary file path.
  2. Opens and locks the file using a `FileStream` with `FileShare.None`.
  3. Attempts to execute `SaveGrabFrameFileAsync` over the locked file path.
  4. Asserts that the save operation returns `false`.
  5. Unlocks the file and verifies that the file contents match the original seed content.
* **Cleanup:** Deletes the temporary file in a `finally` block.

---

### 4. `SaveGrabFrameFileAsync_AtomicallyReplacesExistingFile`
* **Type:** `[Fact]`
* **Purpose:** Tests that saving to an existing target file successfully and atomically replaces the previous file content.
* **Execution Flow:**
  1. Creates a temporary file with existing text (`"old content"`).
  2. Calls `SaveGrabFrameFileAsync` with a new `HistoryInfo` payload (`"new content"`).
  3. Asserts that saving returns `true`.
  4. Reloads the file via `LoadGrabFrameFileAsync` and asserts that the content reflects the updated text (`"new content"`).
* **Cleanup:** Deletes the temporary file in a `finally` block.

---

### 5. `LoadGrabFrameFileAsync_ReturnsNull_ForMissingFile`
* **Type:** `[Fact]`
* **Purpose:** Confirms that attempting to load a non-existent file path returns `null` instead of throwing an unhandled exception.
* **Execution Flow:**
  1. Generates a path to a file that does not exist on disk.
  2. Calls `LoadGrabFrameFileAsync`.
  3. Asserts that the return value is `null`.

---

### 6. `LoadGrabFrameFileAsync_ReturnsNull_ForOversizedMetadata`
* **Type:** `[Fact]`
* **Purpose:** Ensures the reader validates file size safety limits and rejects `.tggf` archives whose `metadata.json` entry exceeds the maximum allowed byte size (`GrabFrameFileUtilities.MaxMetadataBytes`).
* **Execution Flow:**
  1. Manually constructs a `.tggf` file as a ZIP archive containing a `metadata.json` entry.
  2. Writes a string payload exceeding `GrabFrameFileUtilities.MaxMetadataBytes` by 1 byte.
  3. Calls `LoadGrabFrameFileAsync` on the constructed archive.
  4. Asserts that the loaded result is `null`.
* **Cleanup:** Deletes the temporary file in a `finally` block.

---

### 7. `AreImageDimensionsAllowed_EnforcesDimensionAndPixelLimits`
* **Type:** `[Theory]`
* **Purpose:** Validates that `GrabFrameFileUtilities.AreImageDimensionsAllowed` correctly enforces width, height, and overall pixel area limits.
* **Test Cases (`[InlineData]`):**
  * `(8_000, 5_000, true)`: Valid dimensions within limits.
  * `(8_001, 5_000, false)`: Exceeds dimension/pixel limits.
  * `(16_385, 1, false)`: Dimension exceeds maximum threshold.

---

### 8. `IsGrabFrameFile_MatchesExtension`
* **Type:** `[Theory]`
* **Purpose:** Validates file extension checking logic provided by `GrabFrameFileUtilities.IsGrabFrameFile`.
* **Test Cases (`[InlineData]`):**
  * `"frame.tggf"` $\rightarrow$ `true`
  * `"frame.TGGF"` $\rightarrow$ `true` (Case-insensitive check)
  * `"image.png"` $\rightarrow$ `false`
  * `""` $\rightarrow$ `false`

---

## Dependencies & Imports

The test file relies on the following namespaces and framework features:
* `System.Drawing`: `Bitmap` generation for testing image encoding/decoding.
* `System.IO` & `System.IO.Compression`: `FileStream`, `ZipArchive`, and file manipulations to construct standard or invalid archive files.
* `System.Text.Json`: `JsonSerializer` for handling serialized word border data.
* `System.Windows`: `Rect` structure for spatial dimensions and coordinates.
* `Text_Grab` / `Text_Grab.Models` / `Text_Grab.Utilities`: System modules containing data structures (`HistoryInfo`, `WordBorderInfo`) and the `GrabFrameFileUtilities` implementation being tested.