# Technical Documentation: `Tests/HistoryServiceTests.cs`

## Overview

The `Tests/HistoryServiceTests.cs` file contains the unit test suite for the `HistoryService` class in the Text-Grab application. Its primary objective is to verify the correct behavior of history persistence, lazy loading, history cache release, retention limits, error recovery during JSON deserialization, and normalization of history records (such as UI Automation language flags, Word Border JSON sidecars, and Markdown editor modes).

---

## Test Collection & Parallelization Settings

### `HistoryServiceCollectionDefinition`
- **Attribute:** `[CollectionDefinition("History service", DisableParallelization = true)]`
- **Purpose:** Defines a shared xUnit test collection named `"History service"`. Disabling parallelization ensures that tests modifying application settings or writing to actual disk/history storage files run sequentially, avoiding file I/O race conditions.

### `HistoryServiceTests`
- **Attribute:** `[Collection("History service")]`
- **Purpose:** Test class containing all facts and WPF facts targeting `HistoryService`.

---

## Class Configuration & Static Fields

### `HistoryJsonOptions`
- **Type:** `JsonSerializerOptions` (static readonly)
- **Configuration:**
  - `AllowTrailingCommas = true`
  - `WriteIndented = true`
  - Adds `JsonStringEnumConverter` to serialize and deserialize enums as string names.

---

## Test Methods Summary

### Lazy Loading & Cache Clearing Tests

#### `TextHistory_LazyLoadsAgainAfterRelease()`
- **Attribute:** `[WpfFact]`
- **Behavior:** 
  1. Writes an initial `HistoryInfo` entry to `HistoryTextOnly.json`.
  2. Instantiates `HistoryService` and calls `GetLastTextHistory()`, asserting it loads the initial text (`"first text history"`).
  3. Invokes `historyService.ReleaseLoadedHistories()` to clear cached state.
  4. Overwrites `HistoryTextOnly.json` with a updated history item (`"second text history"`).
  5. Calls `GetLastTextHistory()` again to verify that `HistoryService` re-reads the updated file from disk.

#### `ImageHistory_LazyLoadsAgainAfterRelease()`
- **Attribute:** `[WpfFact]`
- **Behavior:**
  1. Saves an initial image history item (`"one.bmp"`) to `HistoryWithImage.json`.
  2. Verifies `GetRecentGrabs()` returns `"one.bmp"`.
  3. Releases loaded history using `ReleaseLoadedHistories()`.
  4. Saves a new image history entry (`"two.bmp"`) to `HistoryWithImage.json`.
  5. Asserts `GetRecentGrabs()` reloads and returns `"two.bmp"`, and `GetLastFullScreenGrabInfo()?.ID` matches `"image-2"`.

---

### Content Classification & Filtering Tests

#### `ImageHistory_SeparatesPdfDocumentsFromRecentGrabs()`
- **Attribute:** `[Fact]`
- **Behavior:**
  1. Prepares a image history item (`OpenContentKind.Image`) and a PDF history item (`OpenContentKind.PdfDocument`).
  2. Injects these items directly into private field `HistoryWithImage` using `SetPrivateField`.
  3. Sets `_imageHistoryLoaded` to `true`.
  4. Asserts `GetRecentGrabs()` returns only the image grab.
  5. Asserts `GetRecentPdfDocuments()` returns only the PDF grab.
  6. Confirms properties such as `SourcePageIndex` (set to `4`), `HasAnyRecentGrabs()`, and static helper `HistoryService.GetMostRecentGrab()` behavior.

#### `GetMostRecentGrab_ReturnsNull_WhenHistoryOnlyContainsPdfs()`
- **Attribute:** `[Fact]`
- **Behavior:**
  1. Passes a list containing only a PDF document (`OpenContentKind.PdfDocument`) into static method `HistoryService.GetMostRecentGrab()`.
  2. Asserts the return value is `null` (PDFs are excluded from image grab returns).

---

### Retention Policy Tests

#### `VisualHistoryRetention_LimitsGrabsAndPdfsIndependently()`
- **Attribute:** `[Fact]`
- **Behavior:**
  1. Creates 12 image history items and 12 PDF history items (24 total).
  2. Passes the collection to `HistoryService.GetExcessVisualHistoryItems()`.
  3. Asserts that 4 total items are identified for removal (the 2 oldest image items `grab-0`, `grab-1` and the 2 oldest PDF items `pdf-0`, `pdf-1`), confirming retention limits apply independently to each content type.

---

### Word Border & Sidecar Metadata Tests

#### `ImageHistory_KeepsInlineWordBorderJsonWhileMirroringSidecarStorage()`
- **Attribute:** `[WpfFact]`
- **Behavior:**
  1. Constructs an inline JSON string representing a list of `WordBorderInfo` items with custom coordinates, display text, and line height.
  2. Saves a history item containing `WordBorderInfoJson`, `ManualTableColumnSeparators`, and `ManualTableRowSeparators`.
  3. Asserts properties are loaded onto the `HistoryInfo` model and sidecar filename `image-with-borders.wordborders.json` is automatically derived.
  4. Calls `historyService.GetWordBorderInfosAsync()` to verify deserialization into `WordBorderInfo` instances.
  5. Releases loaded histories and checks underlying disk files (`HistoryWithImage.json` and sidecar `.wordborders.json`) via `FileUtilities.GetTextFileAsync()` to ensure both main JSON and sidecar file were persisted with the expected data.

---

### Data Normalization & Migration Tests

#### `ImageHistory_NormalizesPreviewUiAutomationEntriesToRollbackSafeValues()`
- **Attribute:** `[WpfFact]`
- **Behavior:**
  1. Prepares a JSON history item tagged with `LanguageKind.UiAutomation` and `UiAutomationLang.Tag`.
  2. Instantiates `HistoryService` and loads recent grabs.
  3. Verifies normalization logic on load:
     - `UsedUiAutomation` is set to `true`.
     - `LanguageKind` falls back to `LanguageKind.Global`.
     - `LanguageTag` is stripped of `UiAutomationLang.Tag`.
     - `OcrLanguage` is no longer of type `UiAutomationLang`.
  4. Calls `WriteHistory()`, releases history, and verifies saved JSON no longer contains `"LanguageKind": "UiAutomation"` or the UI automation tag string, but retains `"UsedUiAutomation": true`.

#### `TextHistory_PreservesMarkdownEditorModeAndSource()`
- **Attribute:** `[WpfFact]`
- **Behavior:**
  1. Saves a text history entry with `EditorMode = EtwEditorMode.Markdown`.
  2. Calls `GetEditWindows()` and verifies `EditorMode` is maintained as `EtwEditorMode.Markdown`.

#### `TextHistory_LoadsUnknownLanguageKindsUsingGlobalFallback()`
- **Attribute:** `[WpfFact]`
- **Behavior:**
  1. Writes raw JSON string containing an unknown/legacy enum string (`"LanguageKind": "LegacyPreview"`).
  2. Loads history via `HistoryService` and asserts `LanguageKind` gracefully falls back to `LanguageKind.Global`.
  3. Rewrites history to disk and verifies the string `"LegacyPreview"` is removed and replaced with `"LanguageKind": "Global"`.

#### `TextHistory_RecoversValidEntriesWhenOneEntryIsMalformed()`
- **Attribute:** `[WpfFact]`
- **Behavior:**
  1. Writes raw JSON with two entries: one valid entry and one malformed entry (e.g., `"SourceMode"` set to an object instead of string).
  2. Instantiates `HistoryService` and asserts it recovers gracefully by loading the valid entry (`"valid-entry"`) while discarding/skipping the corrupt entry.

---

### Persistence Mechanics Tests

#### `TextHistory_WriteHistory_PersistsSavedEditWindowText()`
- **Attribute:** `[Fact]`
- **Behavior:**
  1. Enables history settings (`AppUtilities.TextGrabSettings.UseHistory = true`).
  2. Clears history via `historyService.DeleteHistory()`.
  3. Injects a pending write state with private field setting (`_textHistoryLoaded = true`, `_hasPendingWrite = true`, and a `HistoryTextOnly` item).
  4. Calls `WriteHistory()`, releases loaded histories, and asserts `GetEditWindows()` successfully retrieves the persisted item from disk.
  5. Restores original setting state in a `finally` block.

---

## Private Helper Methods

### `SetPrivateField<T>(object target, string fieldName, T value)`
- **Type:** `private static void`
- **Purpose:** Uses Reflection (`BindingFlags.Instance | BindingFlags.NonPublic`) to force set non-public instance fields on target objects (such as setting internal state flags or backing lists in `HistoryService`).
- **Exceptions:** Throws `InvalidOperationException` if the specified `fieldName` is not found.

### `SaveHistoryFileAsync(string fileName, List<HistoryInfo> historyItems)`
- **Type:** `private static Task<bool>`
- **Purpose:** Utility method to serialize a list of `HistoryInfo` items using `HistoryJsonOptions` and save it to the history storage directory via `FileUtilities.SaveTextFile(..., FileStorageKind.WithHistory)`.